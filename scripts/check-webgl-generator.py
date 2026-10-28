#!/usr/bin/env python3
"""Checks the generated project's isolation and preservation rules without Unity."""
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("webgl_build", Path(__file__).with_name("build-webgl.py"))
build = importlib.util.module_from_spec(spec)
spec.loader.exec_module(build)


class BrowserProjectChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.stage = Path(self.temp.name).resolve() / "browser"

    def test_shared_sources_and_locked_packages_without_xr_or_server(self):
        first = build.prepare(stage=self.stage)
        second = build.prepare(stage=self.stage)
        self.assertEqual(first, second)
        original = json.loads((build.ROOT / "Packages/packages-lock.json").read_text())["dependencies"]
        generated = json.loads((self.stage / "Packages/packages-lock.json").read_text())["dependencies"]
        self.assertNotIn("com.meta.xr.sdk.core", generated)
        self.assertFalse(any(name.startswith("com.unity.xr.") for name in generated))
        for name, item in generated.items():
            self.assertEqual(item["version"], original[name]["version"], name)
        self.assertTrue((self.stage / "Assets/Frontier/Runtime/GameSession.cs").is_file())
        self.assertTrue((self.stage / "Assets/Nakama/Runtime/Plugins/UnityWebGLSocketBridge.jslib").is_file())
        self.assertFalse((self.stage / "Assets/Frontier/Networking/WebSocketGateway.cs").exists())
        self.assertFalse((self.stage / "Assets/Frontier/Networking/NgoMatchServer.cs").exists())
        self.assertFalse((self.stage / "Assets/Frontier/Runtime/MultiplayerSmokeClient.cs").exists())
        self.assertFalse((self.stage / "Assets/Frontier/Settings/XRSettings.asset").exists())
        settings = (self.stage / "ProjectSettings/ProjectSettings.asset").read_text()
        self.assertIn("preloadedAssets: []", settings)
        self.assertIn("Standalone: FRONTIER_BROWSER", settings)
        self.assertIn("WebGL: FRONTIER_BROWSER", settings)

    def test_refuses_a_folder_it_does_not_own(self):
        self.stage.mkdir()
        existing = self.stage / "keep.txt"
        existing.write_text("user content")
        with self.assertRaisesRegex(RuntimeError, "not owned"):
            build.prepare(stage=self.stage)
        self.assertEqual(existing.read_text(), "user content")

    def test_preserves_edits_to_generated_sources(self):
        build.prepare(stage=self.stage)
        source = self.stage / "Assets/Frontier/Runtime/GameSession.cs"
        source.write_text("// local edit\n")
        with self.assertRaisesRegex(RuntimeError, "was edited"):
            build.prepare(stage=self.stage)
        self.assertEqual(source.read_text(), "// local edit\n")

    def test_refreshes_metadata_normalized_by_unity_import(self):
        build.prepare(stage=self.stage)
        meta = self.stage / "Assets/Editor/WebGLBuilder.cs.meta"
        meta.write_text(meta.read_text() + "MonoImporter:\n  externalObjects: {}\n")
        asset = self.stage / "Assets/Frontier/Settings/MobileURP.asset"
        asset.write_text(asset.read_text() + "# Editor serialization\n")
        build.prepare(stage=self.stage)
        self.assertEqual(asset.read_bytes(), (build.ROOT / "Assets/Frontier/Settings/MobileURP.asset").read_bytes())

    def test_rejects_links_in_config_and_marker_before_writing(self):
        build.prepare(stage=self.stage)
        external = self.stage.parent / "untouched.txt"
        external.write_text("keep")
        for name in ("Packages/manifest.json", "ProjectSettings/ProjectVersion.txt", build.MARKER):
            target = self.stage / name
            original = target.read_bytes()
            target.unlink()
            target.symlink_to(external)
            with self.assertRaisesRegex(RuntimeError, "symlink"):
                build.prepare(stage=self.stage)
            self.assertEqual(external.read_text(), "keep")
            target.unlink()
            target.write_bytes(original)

    def test_stale_or_incomplete_build_is_never_certified(self):
        self.stage.mkdir()
        (self.stage / "index.html").touch()
        (self.stage / "Build").mkdir()
        for name in ("game.loader.js", "game.framework.js.unityweb", "game.data.unityweb", "game.wasm.unityweb"):
            (self.stage / "Build" / name).touch()
        receipt = self.stage / "build-receipt.json"
        receipt.write_text(json.dumps({"buildId": "old", "unityVersion": "6000.3.26f1"}))
        with self.assertRaisesRegex(RuntimeError, "does not match"):
            build.validate_build(self.stage, "new", "6000.3.26f1")
        receipt.write_text(json.dumps({"buildId": "new", "unityVersion": "6000.3.26f1"}))
        build.validate_build(self.stage, "new", "6000.3.26f1")
        (self.stage / "Build/game.data.unityweb").unlink()
        with self.assertRaisesRegex(RuntimeError, "complete Web player"):
            build.validate_build(self.stage, "new", "6000.3.26f1")

    def test_refuses_symlinked_stage_and_running_editor(self):
        destination = self.stage.parent / "other"
        destination.mkdir()
        self.stage.symlink_to(destination, target_is_directory=True)
        with self.assertRaisesRegex(RuntimeError, "symlink"):
            build.prepare(stage=self.stage)
        self.stage.unlink()
        build.prepare(stage=self.stage)
        (self.stage / "Temp").mkdir()
        (self.stage / "Temp/UnityLockfile").touch()
        with self.assertRaisesRegex(RuntimeError, "Close the generated Unity"):
            build.prepare(stage=self.stage)


if __name__ == "__main__":
    unittest.main()
