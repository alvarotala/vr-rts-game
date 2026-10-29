#!/usr/bin/env python3
"""Verify native project isolation, source preservation and fresh build receipts."""
import importlib.util
import argparse
import json
from pathlib import Path
import plistlib
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("mobile_build", Path(__file__).resolve().parents[1] / "app/build.py")
mobile = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mobile)


class MobileProjectChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.stage = Path(self.temp.name).resolve() / "android"

    def test_native_project_shares_sources_and_udp_without_xr(self):
        root_settings = (mobile.ROOT / "ProjectSettings/ProjectSettings.asset").read_bytes()
        first = mobile.shared.prepare(stage=self.stage, mobile=True)
        second = mobile.shared.prepare(stage=self.stage, mobile=True)
        self.assertEqual(first, second)
        self.assertEqual(root_settings, (mobile.ROOT / "ProjectSettings/ProjectSettings.asset").read_bytes())
        generated = json.loads((self.stage / "Packages/packages-lock.json").read_text())["dependencies"]
        original = json.loads((mobile.ROOT / "Packages/packages-lock.json").read_text())["dependencies"]
        self.assertIn("com.unity.modules.androidjni", generated)
        self.assertIn("com.unity.netcode.gameobjects", generated)
        self.assertFalse(any(name.startswith(("com.meta.", "com.unity.xr.")) for name in generated))
        for name, item in generated.items():
            self.assertEqual(item["version"], original[name]["version"], name)
        for source in ("Runtime/GameSession.cs", "Networking/NgoMatchClient.cs", "Runtime/QuestBridge.Browser.cs"):
            self.assertEqual((self.stage / "Assets/Frontier" / source).read_bytes(), (mobile.ROOT / "Assets/Frontier" / source).read_bytes())
        self.assertFalse((self.stage / "Assets/Frontier/Networking/NgoMatchServer.cs").exists())
        self.assertFalse((self.stage / "Assets/Frontier/Resources/Controllers").exists())
        self.assertFalse((self.stage / "Assets/WebGLTemplates").exists())
        self.assertFalse((self.stage / "Assets/Editor/WebGLBuilder.cs").exists())
        self.assertEqual((self.stage / "Assets/Editor/MobileBuilder.cs").read_bytes(), (mobile.ROOT / "app/MobileBuilder.cs").read_bytes())
        self.assertIn("FRONTIER_MOBILE", (self.stage / "Assets/csc.rsp").read_text())
        settings = (self.stage / "ProjectSettings/ProjectSettings.asset").read_text()
        for target in ("Standalone", "Android", "iPhone"):
            self.assertIn(target + ": FRONTIER_BROWSER;FRONTIER_MOBILE", settings)

    def test_refuses_webgl_stage_and_unmanaged_content(self):
        mobile.shared.prepare(stage=self.stage)
        with self.assertRaisesRegex(RuntimeError, "not owned"):
            mobile.shared.prepare(stage=self.stage, mobile=True)
        unknown = self.stage.parent / "user-project"
        unknown.mkdir()
        (unknown / "keep.txt").write_text("keep")
        with self.assertRaisesRegex(RuntimeError, "not owned"):
            mobile.shared.prepare(stage=unknown, mobile=True)
        self.assertEqual((unknown / "keep.txt").read_text(), "keep")

    def test_preserves_local_edits_and_rejects_open_project(self):
        mobile.shared.prepare(stage=self.stage, mobile=True)
        source = self.stage / "Assets/Editor/MobileBuilder.cs"
        source.write_text("// user changes\n")
        with self.assertRaisesRegex(RuntimeError, "was edited"):
            mobile.shared.prepare(stage=self.stage, mobile=True)
        self.assertEqual(source.read_text(), "// user changes\n")
        (self.stage / "Temp").mkdir()
        (self.stage / "Temp/UnityLockfile").touch()
        with self.assertRaisesRegex(RuntimeError, "Close the generated Unity"):
            mobile.shared.prepare(stage=self.stage, mobile=True)

    def test_native_marker_cannot_follow_symlink(self):
        mobile.shared.prepare(stage=self.stage, mobile=True)
        external = self.stage.parent / "keep.txt"
        external.write_text("keep")
        marker = self.stage / ".frontier-generated-mobile.json"
        marker.unlink()
        marker.symlink_to(external)
        with self.assertRaisesRegex(RuntimeError, "symlink"):
            mobile.shared.prepare(stage=self.stage, mobile=True)
        self.assertEqual(external.read_text(), "keep")

    def test_output_cannot_overwrite_source_directories(self):
        for output in (mobile.ROOT, mobile.ROOT.parent, mobile.ROOT / "Assets", self.stage, self.stage.parent):
            with self.assertRaises(RuntimeError):
                mobile.validate_output(output, self.stage)
        mobile.validate_output(mobile.ROOT / "Builds/Mobile/Android/LittleLegion.apk", self.stage)

    def test_build_requires_fresh_receipt_and_platform_artifacts(self):
        for platform in ("android", "ios"):
            output = self.stage.parent / ("game.apk" if platform == "android" else "ios")
            if platform == "android":
                output.write_bytes(b"apk fixture")
                receipt = output.with_suffix(".receipt.json")
            else:
                (output / "Unity-iPhone.xcodeproj").mkdir(parents=True)
                (output / "Unity-iPhone.xcodeproj/project.pbxproj").touch()
                (output / "Data").mkdir()
                (output / "Info.plist").write_bytes(plistlib.dumps({"CFBundleIdentifier": "app.sadmonkey.littlelegion.mobile"}))
                receipt = output / "build-receipt.json"
            data = {"buildId": "new", "unityVersion": "6000.3.26f1", "platform": platform,
                    "configuration": "development", "version": "0.1.0", "buildNumber": 1}
            receipt.write_text(json.dumps(data))
            mobile.validate_build(output, "new", data["unityVersion"], platform)
            with self.assertRaisesRegex(RuntimeError, "does not match"):
                mobile.validate_build(output, "old", data["unityVersion"], platform)
            data["platform"] = "wrong"
            receipt.write_text(json.dumps(data))
            with self.assertRaisesRegex(RuntimeError, "does not match"):
                mobile.validate_build(output, "new", data["unityVersion"], platform)
            data["platform"] = platform
            receipt.write_text(json.dumps(data))
            if platform == "android":
                output.write_bytes(b"")
            else:
                (output / "Unity-iPhone.xcodeproj/project.pbxproj").unlink()
            with self.assertRaisesRegex(RuntimeError, "complete mobile"):
                mobile.validate_build(output, "new", data["unityVersion"], platform)

    def test_release_receipt_cannot_be_confused_with_development_or_other_version(self):
        output = self.stage.parent / "game.apk"
        output.write_bytes(b"apk fixture")
        receipt = output.with_suffix(".receipt.json")
        data = {"buildId": "new", "unityVersion": "6000.3.26f1", "platform": "android",
                "configuration": "release", "version": "0.2.0", "buildNumber": 7}
        receipt.write_text(json.dumps(data))
        mobile.validate_build(output, "new", data["unityVersion"], "android", "release", "0.2.0", 7)
        for configuration, version, number in (("development", "0.2.0", 7), ("release", "0.1.0", 7), ("release", "0.2.0", 8)):
            with self.assertRaisesRegex(RuntimeError, "configuration, app version or build number"):
                mobile.validate_build(output, "new", data["unityVersion"], "android", configuration, version, number)
        data["buildNumber"] = True
        receipt.write_text(json.dumps(data))
        with self.assertRaisesRegex(RuntimeError, "configuration, app version or build number"):
            mobile.validate_build(output, "new", data["unityVersion"], "android", "release", "0.2.0", 1)

    def test_cli_identity_rejects_invalid_build_numbers_and_versions(self):
        self.assertEqual(mobile.app_version("0.1.0"), "0.1.0")
        self.assertEqual(mobile.app_version("12.34.56"), "12.34.56")
        self.assertEqual(mobile.build_number("42"), 42)
        for value in ("0", "-1", "1.5", "1.2.3", "2147483648", "abc", ""):
            with self.assertRaises(argparse.ArgumentTypeError):
                mobile.build_number(value)
        for value in ("1", "1.2", "1.2.3.4", "01.2.3", "1.2.3-beta", "-1.2.3", "1.2.3\n", ""):
            with self.assertRaises(argparse.ArgumentTypeError):
                mobile.app_version(value)

    def test_release_and_development_options_reach_unity_without_shell_parsing(self):
        for platform, configuration in (("ios", "release"), ("android", "development")):
            command = mobile.unity_command(Path("/Editor with spaces/Unity"), self.stage, self.stage.parent / "output with spaces", platform,
                                           "build-fixture", "6000.3.26f1", configuration, "0.2.0", 7, self.stage.parent / "build.log")
            values = {key: command[index + 1] for index, key in enumerate(command[:-1]) if key.startswith("-frontier-")}
            self.assertEqual(command[0], "/Editor with spaces/Unity")
            self.assertEqual(values["-frontier-mobile-platform"], platform)
            self.assertEqual(values["-frontier-mobile-configuration"], configuration)
            self.assertEqual(values["-frontier-app-version"], "0.2.0")
            self.assertEqual(values["-frontier-build-number"], "7")
            self.assertEqual(values["-frontier-mobile-output"], str(self.stage.parent / "output with spaces"))

    def test_ios_network_description_preserves_signing_and_security_settings(self):
        output = self.stage.parent / "ios"
        output.mkdir()
        original = {"CFBundleIdentifier": "app.sadmonkey.littlelegion.mobile", "CFBundleVersion": "7",
                    "CFBundleShortVersionString": "0.2.0", "NSAppTransportSecurity": {"NSAllowsArbitraryLoads": False},
                    "UIRequiresFullScreen": True, "UISupportedInterfaceOrientations": ["UIInterfaceOrientationLandscapeLeft"]}
        plist = output / "Info.plist"
        plist.write_bytes(plistlib.dumps(original))
        mobile.configure_ios_project(output)
        first = plist.read_bytes()
        actual = plistlib.loads(first)
        self.assertEqual(actual.pop("NSLocalNetworkUsageDescription"), mobile.IOS_LOCAL_NETWORK_DESCRIPTION)
        self.assertEqual(actual, original)
        mobile.configure_ios_project(output)
        self.assertEqual(plist.read_bytes(), first)

    def test_ios_network_patch_rejects_missing_invalid_and_symlinked_plists(self):
        output = self.stage.parent / "ios"
        output.mkdir()
        plist = output / "Info.plist"
        with self.assertRaisesRegex(RuntimeError, "valid iOS Info.plist"):
            mobile.configure_ios_project(output)
        for invalid in (b"not a plist", b'<?xml version="1.0"?><plist><dict>', plistlib.dumps(["wrong-root"])):
            plist.write_bytes(invalid)
            with self.assertRaises(RuntimeError):
                mobile.configure_ios_project(output)
            self.assertEqual(plist.read_bytes(), invalid)
        external = self.stage.parent / "untouched.plist"
        contents = plistlib.dumps({"keep": True})
        external.write_bytes(contents)
        plist.unlink()
        plist.symlink_to(external)
        with self.assertRaisesRegex(RuntimeError, "symlinked"):
            mobile.configure_ios_project(output)
        self.assertEqual(external.read_bytes(), contents)


if __name__ == "__main__":
    unittest.main()
