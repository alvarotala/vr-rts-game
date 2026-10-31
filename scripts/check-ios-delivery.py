#!/usr/bin/env python3
"""Check iOS command planning and delivery gates without Unity, Xcode or accounts."""
from contextlib import redirect_stdout, redirect_stderr
import importlib.util
import io
from pathlib import Path
import plistlib
import subprocess
import sys
import tempfile
import unittest
from unittest import mock
import zipfile

spec = importlib.util.spec_from_file_location("ios_delivery", Path(__file__).resolve().parents[1] / "app/ios.py")
ios = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ios)

TEAM = "ABCDEFGHIJ"
DEVICE = "explicit-device-id"


class IosDeliveryChecks(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.calls = []
        self.settings = {}
        self.info = {"CFBundleIdentifier": ios.BUNDLE_ID, "CFBundleShortVersionString": "0.1.0",
                     "CFBundleVersion": "7", "CFBundleExecutable": "Little Legion"}
        root_patch = mock.patch.object(ios, "ROOT", self.root)
        root_patch.start()
        self.addCleanup(root_patch.stop)

    def args(self, command="device", *extra):
        result = [command, "--team-id", TEAM, "--build-number", "7"]
        if command == "device":
            result += ["--device", DEVICE]
        return result + list(extra)

    def app_fixture(self, path):
        path.mkdir(parents=True)
        data = self.info | self.settings.get("metadata", {})
        (path / "Info.plist").write_bytes(plistlib.dumps(data))
        if not self.settings.get("missing_binary"):
            (path / self.info["CFBundleExecutable"]).write_bytes(b"test executable")
        return data

    def fake_run(self, command, **kwargs):
        self.calls.append(command)
        self.assertTrue(kwargs["check"])
        if command[0] == sys.executable:
            self.folder = Path(command[command.index("--output") + 1]).parent
            if not self.settings.get("missing_unity"):
                project = self.folder / "Unity/Unity-iPhone.xcodeproj"
                project.mkdir(parents=True)
                (project / "project.pbxproj").write_text("test project")
                (self.folder / "Unity/Data").mkdir()
        elif command[0] == "xcodebuild":
            if "-exportArchive" in command:
                self.options = plistlib.loads((self.folder / "ExportOptions.plist").read_bytes())
                if self.options["destination"] == "export" and not self.settings.get("missing_ipa"):
                    export = self.folder / "Export"
                    export.mkdir()
                    with zipfile.ZipFile(export / "Little Legion.ipa", "w") as ipa:
                        metadata = self.info | self.settings.get("ipa_metadata", {})
                        ipa.writestr("Payload/Little Legion.app/Info.plist", plistlib.dumps(metadata))
                        ipa.writestr("Payload/Little Legion.app/Little Legion", b"test executable")
            elif "archive" in command:
                if not self.settings.get("missing_archive"):
                    archive = self.folder / "LittleLegion.xcarchive"
                    app = archive / "Products/Applications/Little Legion.app"
                    data = self.app_fixture(app)
                    data["ApplicationPath"] = self.settings.get("archive_path", "Applications/Little Legion.app")
                    (archive / "Info.plist").write_bytes(plistlib.dumps({"ApplicationProperties": data}))
            elif "build" in command and not self.settings.get("missing_device"):
                self.app_fixture(self.folder / "DerivedData/Build/Products/Release-iphoneos/Little Legion.app")
        elif command[0] == "codesign":
            if self.settings.get("bad_signature"):
                raise subprocess.CalledProcessError(1, command)
            if "-d" in command:
                team = self.settings.get("signed_team", TEAM)
                identifier = self.settings.get("signed_identifier", ios.BUNDLE_ID)
                return subprocess.CompletedProcess(command, 0, "", f"TeamIdentifier={team}\nIdentifier={identifier}\n")
        return subprocess.CompletedProcess(command, 0, "", "")

    def deliver(self, command="device", *extra):
        with mock.patch.object(ios.sys, "platform", "darwin"), mock.patch.object(ios.subprocess, "run", side_effect=self.fake_run), redirect_stdout(io.StringIO()):
            return ios.main(self.args(command, *extra))

    def assert_no_install_or_export(self):
        self.assertFalse(any("devicectl" in command or "-exportArchive" in command for command in self.calls))

    def test_plan_does_not_run_tools_or_create_files(self):
        for command, extra in (("device", []), ("testflight", []), ("testflight", ["--upload"])):
            with self.subTest(command=command, extra=extra), mock.patch.object(ios.subprocess, "run") as run, redirect_stdout(io.StringIO()) as output:
                self.assertEqual(0, ios.main(self.args(command, "--plan", *extra)))
                run.assert_not_called()
                self.assertIn("PLAN ÚNICAMENTE", output.getvalue())
                self.assertFalse((self.root / "Builds").exists())

    def test_requires_explicit_team_device_and_positive_build(self):
        invalid = [["device", "--team-id", TEAM, "--build-number", "7"],
                   ["testflight", "--build-number", "7"], ["testflight", "--team-id", TEAM],
                   self.args("testflight", "--build-number", "0"), self.args("testflight", "--build-number", "-1"),
                   self.args("testflight", "--build-number", "2147483648"), self.args("testflight", "--version", "release"),
                   self.args("testflight", "--team-id", "implicit"), self.args("device", "--upload")]
        for args in invalid:
            with self.subTest(args=args), redirect_stderr(io.StringIO()), self.assertRaises(SystemExit):
                ios.parse_args(args)

    def test_device_build_validates_then_installs_and_launches_explicit_device(self):
        self.assertEqual(0, self.deliver("device", "--editor", "/path with spaces/Unity"))
        unity, xcode, verify, signature, install, launch = self.calls
        self.assertIn("--release", unity)
        self.assertEqual("/path with spaces/Unity", unity[unity.index("--editor") + 1])
        self.assertEqual("0.1.0", unity[unity.index("--version") + 1])
        self.assertEqual("7", unity[unity.index("--build-number") + 1])
        self.assertIn(f"DEVELOPMENT_TEAM={TEAM}", xcode)
        self.assertIn("CODE_SIGN_STYLE=Automatic", xcode)
        self.assertIn("-allowProvisioningUpdates", xcode)
        self.assertEqual(f"id={DEVICE}", xcode[xcode.index("-destination") + 1])
        self.assertEqual(str(self.folder / "DerivedData"), xcode[xcode.index("-derivedDataPath") + 1])
        self.assertEqual(["codesign", "--verify", "--deep", "--strict", "--verbose=2"], verify[:-1])
        self.assertEqual(["codesign", "-d", "--verbose=4"], signature[:-1])
        self.assertEqual(["xcrun", "devicectl", "device", "install", "app", "--device", DEVICE], install[:-1])
        self.assertEqual(ios.launch_command(ios.parse_args(self.args())), launch)
        self.assertTrue(self.folder.is_relative_to(self.root / "Builds/Mobile"))

    def test_each_delivery_uses_a_new_folder_without_overwriting_previous_output(self):
        self.deliver()
        previous = self.folder
        self.deliver()
        self.assertNotEqual(previous, self.folder)
        self.assertTrue((previous / "DerivedData/Build/Products/Release-iphoneos/Little Legion.app/Info.plist").exists())

    def test_missing_unity_export_stops_before_xcode(self):
        self.settings["missing_unity"] = True
        with self.assertRaisesRegex(RuntimeError, "Unity export is incomplete"):
            self.deliver()
        self.assertEqual(1, len(self.calls))

    def test_missing_app_or_binary_stops_installation(self):
        for problem in ("missing_device", "missing_binary"):
            with self.subTest(problem=problem):
                self.settings = {problem: True}
                self.calls = []
                with self.assertRaises(RuntimeError):
                    self.deliver()
                self.assert_no_install_or_export()

    def test_wrong_app_metadata_stops_before_codesign_and_installation(self):
        for key, wrong in (("CFBundleIdentifier", "other.app"), ("CFBundleShortVersionString", "0.2.0"), ("CFBundleVersion", "6")):
            with self.subTest(key=key):
                self.settings = {"metadata": {key: wrong}}
                self.calls = []
                with self.assertRaisesRegex(RuntimeError, key):
                    self.deliver()
                self.assertFalse(any(command[0] == "codesign" for command in self.calls))
                self.assert_no_install_or_export()

    def test_wrong_signing_team_or_identifier_stops_installation(self):
        for key, value in (("signed_team", "OTHERTEAM1"), ("signed_identifier", "other.app")):
            with self.subTest(key=key):
                self.settings = {key: value}
                self.calls = []
                with self.assertRaises(RuntimeError):
                    self.deliver()
                self.assert_no_install_or_export()

    def test_codesign_failure_stops_installation(self):
        self.settings["bad_signature"] = True
        with self.assertRaises(subprocess.CalledProcessError):
            self.deliver()
        self.assert_no_install_or_export()

    def test_testflight_defaults_to_verified_local_ipa_without_upload(self):
        self.assertEqual(0, self.deliver("testflight"))
        xcode = self.calls[1]
        self.assertIn("archive", xcode)
        self.assertEqual("generic/platform=iOS", xcode[xcode.index("-destination") + 1])
        self.assertEqual("app-store-connect", self.options["method"])
        self.assertEqual("export", self.options["destination"])
        self.assertEqual(TEAM, self.options["teamID"])
        self.assertIs(False, self.options["manageAppVersionAndBuildNumber"])
        self.assertTrue((self.folder / "Export/Little Legion.ipa").is_file())
        self.assertFalse(any("devicectl" in command for command in self.calls))

    def test_explicit_upload_uses_upload_destination_and_does_not_require_local_ipa(self):
        self.assertEqual(0, self.deliver("testflight", "--upload"))
        self.assertEqual("upload", self.options["destination"])
        self.assertFalse((self.folder / "Export").exists())
        self.assertEqual(1, sum("-exportArchive" in command for command in self.calls))

    def test_missing_or_invalid_archive_never_exports_or_uploads(self):
        for problem in ({"missing_archive": True}, {"metadata": {"CFBundleVersion": "6"}}, {"archive_path": "../../other.app"}):
            with self.subTest(problem=problem):
                self.settings = problem
                self.calls = []
                with self.assertRaises(RuntimeError):
                    self.deliver("testflight", "--upload")
                self.assert_no_install_or_export()

    def test_missing_ipa_or_wrong_export_version_is_not_success(self):
        for problem in ({"missing_ipa": True}, {"ipa_metadata": {"CFBundleVersion": "8"}}):
            with self.subTest(problem=problem):
                self.settings = problem
                with self.assertRaises(RuntimeError):
                    self.deliver("testflight")

    def test_malformed_xml_plist_has_a_delivery_error(self):
        path = self.root / "Info.plist"
        path.write_bytes(b'<?xml version="1.0"?><plist><dict><key>broken</key>')
        with self.assertRaisesRegex(RuntimeError, "invalid artifact metadata"):
            ios.load_plist(path)

    def test_malformed_xml_inside_ipa_has_a_delivery_error(self):
        export = self.root / "Export"
        export.mkdir()
        with zipfile.ZipFile(export / "broken.ipa", "w") as package:
            package.writestr("Payload/Little Legion.app/Info.plist", b'<?xml version="1.0"?><plist><dict><key>broken</key>')
        with self.assertRaisesRegex(RuntimeError, "invalid or incomplete IPA"):
            ios.validate_ipa(self.root, ios.parse_args(self.args("testflight")))


if __name__ == "__main__":
    unittest.main()
