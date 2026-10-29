#!/usr/bin/env python3
"""Prepare an XR-free Unity project from shared sources and build the Web player."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / ".cache" / "unity-webgl"
MARKER = ".frontier-generated-webgl.json"
PACKAGES = (
    "com.unity.netcode.gameobjects", "com.unity.inputsystem",
    "com.unity.render-pipelines.universal", "com.unity.ugui",
)
MODULES = (
    "animation", "audio", "imageconversion", "imgui", "jsonserialize", "physics",
    "physics2d", "ui", "uielements", "unitywebrequest", "unitywebrequestaudio",
    "unitywebrequesttexture", "unitywebrequestassetbundle", "assetbundle",
)
EXCLUDED_NETWORK = ("NgoMatchServer", "AllocatedServerAgent", "WebSocketGateway")
EXCLUDED_RUNTIME = ("DesktopSmokeTest", "MultiplayerSmokeClient")


def package_graph(root: Path, mobile: bool = False) -> tuple[dict, dict]:
    locked = json.loads((root / "Packages/packages-lock.json").read_text())["dependencies"]
    names = list(PACKAGES) + ["com.unity.modules." + module for module in MODULES]
    if mobile:
        names.append("com.unity.modules.androidjni")
    dependencies = {name: locked[name]["version"] for name in names}
    graph = {}
    queue = [(name, 0) for name in names]
    while queue:
        name, depth = queue.pop(0)
        if name in graph and graph[name]["depth"] <= depth:
            continue
        if name not in locked:
            raise RuntimeError("Package missing from the main lock: " + name)
        if name.startswith(("com.meta.", "com.unity.xr.")):
            raise RuntimeError("Unexpected XR dependency in browser graph: " + name)
        item = dict(locked[name])
        if item.get("source") not in ("registry", "builtin"):
            raise RuntimeError("Browser packages must use the reviewed public registry or builtin modules.")
        item["depth"] = depth
        graph[name] = item
        queue.extend((child, depth + 1) for child in item.get("dependencies", {}))
    return dependencies, graph


def source_files(root: Path, mobile: bool = False) -> dict[str, bytes]:
    files = {}

    def tree(source: Path, target: str, excluded=()):
        for path in sorted(source.rglob("*")):
            if path.is_symlink():
                raise RuntimeError("Unexpected symlink in browser source: " + str(path))
            relative = path.relative_to(source)
            if excluded and any(part.startswith(excluded) for part in relative.parts):
                continue
            if path.is_file():
                files[target + "/" + relative.as_posix()] = path.read_bytes()
        meta = Path(str(source) + ".meta")
        if meta.exists():
            files[target + ".meta"] = meta.read_bytes()

    for name in ("Core", "Shaders", "Resources"):
        tree(root / "Assets/Frontier" / name, "Assets/Frontier/" + name,
             ("Controllers",) if name == "Resources" else ())
    tree(root / "Assets/Frontier/Runtime", "Assets/Frontier/Runtime", EXCLUDED_RUNTIME)
    tree(root / "Assets/Frontier/Networking", "Assets/Frontier/Networking", EXCLUDED_NETWORK)
    tree(root / "Assets/Nakama", "Assets/Nakama")
    for name in ("MobileURP.asset", "MobileRenderer.asset"):
        for suffix in ("", ".meta"):
            relative = "Assets/Frontier/Settings/" + name + suffix
            files[relative] = (root / relative).read_bytes()
    files["Assets/link.xml"] = (root / "webgl/link.xml").read_bytes()
    if mobile:
        files["Assets/Editor/MobileBuilder.cs"] = (root / "app/MobileBuilder.cs").read_bytes()
    else:
        files["Assets/Editor/WebGLBuilder.cs"] = (root / "webgl/WebGLBuilder.cs").read_bytes()
        tree(root / "webgl/Template", "Assets/WebGLTemplates/LittleLegion")
    # The desktop platform adapter must compile during Editor import as well as
    # in the player. UNITY_WEBGL alone is insufficient for a generated project.
    files["Assets/csc.rsp"] = ("-define:FRONTIER_BROWSER" + (";FRONTIER_MOBILE" if mobile else "") + "\n").encode()
    return files


def prepare(root: Path = ROOT, stage: Path = STAGE, mobile: bool = False) -> dict:
    def safe(path: Path):
        if path.is_symlink() or any(parent.is_symlink() for parent in path.parents):
            raise RuntimeError("Refusing a symlinked generated-project path.")

    safe(stage)
    marker_name = ".frontier-generated-mobile.json" if mobile else MARKER
    marker = stage / marker_name
    for name in (marker_name, "Packages/manifest.json", "Packages/packages-lock.json",
                 "ProjectSettings/ProjectVersion.txt", "ProjectSettings/ProjectSettings.asset", "Temp/UnityLockfile"):
        safe(stage / name)
    if stage.exists() and not marker.exists() and any(stage.iterdir()):
        raise RuntimeError("The browser staging folder is not owned by this generator.")
    if (stage / "Temp/UnityLockfile").exists():
        raise RuntimeError("Close the generated Unity project before refreshing its sources.")
    version_text = (root / "ProjectSettings/ProjectVersion.txt").read_text()
    version = re.search(r"^m_EditorVersion:\s*(\S+)", version_text, re.MULTILINE)
    if not version or not version[1].startswith("6000.3."):
        raise RuntimeError("Review this generator when changing the Unity 6.3 editor version.")
    dependencies, graph = package_graph(root, mobile)
    files = source_files(root, mobile)
    # Stable metadata for templates and folders which have no authored .meta.
    paths = set(files)
    for name in list(paths):
        paths.update(str(parent) for parent in Path(name).parents if str(parent) not in (".", "Assets"))
    for name in sorted(paths):
        if name.endswith(".meta") or name + ".meta" in files:
            continue
        guid = uuid.uuid5(uuid.NAMESPACE_URL, "frontier-webgl/" + name).hex
        text = f"fileFormatVersion: 2\nguid: {guid}\n"
        if name not in files:
            text += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
        files[name + ".meta"] = text.encode()
    previous = json.loads(marker.read_text()).get("assets", {}) if marker.exists() else {}
    # Preserve edits to copied source code. Unity normalizes importer metadata
    # and serializes rendering assets; these disposable inputs are refreshed.
    for name, digest in previous.items():
        target = stage / name
        if not name.startswith("Assets/") or ".." in Path(name).parts or target.is_symlink():
            raise RuntimeError("Invalid generated asset manifest.")
        safe(target)
        if target.exists() and not name.endswith((".meta", ".asset")) and hashlib.sha256(target.read_bytes()).hexdigest() != digest:
            raise RuntimeError("Generated asset was edited; preserve it before refreshing: " + name)
    # Validate every destination before writing any source. Otherwise a late
    # unmanaged-file conflict could leave a partially updated project whose
    # older manifest mistakes our earlier writes for the user's local edits.
    for name, contents in files.items():
        target = stage / name
        safe(target)
        if target.exists() and name not in previous and target.read_bytes() != contents:
            raise RuntimeError("Unmanaged file in generated assets: " + name)
    stage.mkdir(parents=True, exist_ok=True)
    for name, contents in files.items():
        target = stage / name
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(contents)
    for name in previous.keys() - files.keys():
        (stage / name).unlink(missing_ok=True)
    (stage / "Packages").mkdir(exist_ok=True)
    (stage / "ProjectSettings").mkdir(exist_ok=True)
    (stage / "Packages/manifest.json").write_text(json.dumps({"dependencies": dependencies}, indent=2) + "\n")
    (stage / "Packages/packages-lock.json").write_text(json.dumps({"dependencies": graph}, indent=2) + "\n")
    (stage / "ProjectSettings/ProjectVersion.txt").write_text(version_text)
    settings = stage / "ProjectSettings/ProjectSettings.asset"
    if not settings.exists():
        settings_text = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!129 &1
PlayerSettings:
  m_ObjectHideFlags: 0
  serializedVersion: 28
  companyName: Frontier
  productName: Little Legion
  defaultScreenWidth: 1440
  defaultScreenHeight: 1000
  runInBackground: 1
  preloadedAssets: []
  activeInputHandler: 1
  scriptingDefineSymbols:
    Standalone: FRONTIER_BROWSER
    WebGL: FRONTIER_BROWSER
"""
        if mobile:
            settings_text = settings_text.replace("FRONTIER_BROWSER", "FRONTIER_BROWSER;FRONTIER_MOBILE")
            settings_text += "    Android: FRONTIER_BROWSER;FRONTIER_MOBILE\n    iPhone: FRONTIER_BROWSER;FRONTIER_MOBILE\n"
        settings.write_text(settings_text)
    assets = {name: hashlib.sha256(value).hexdigest() for name, value in sorted(files.items())}
    record = {"generator": "app/build.py" if mobile else "scripts/build-webgl.py", "editorVersion": version[1],
              "sourceDigest": hashlib.sha256(json.dumps(assets, sort_keys=True).encode()).hexdigest(),
              "packages": dependencies, "assets": assets}
    marker.write_text(json.dumps(record, indent=2) + "\n")
    return record


def find_editor(version: str, explicit: str | None) -> Path | None:
    configured = explicit or os.environ.get("UNITY_EDITOR")
    if configured:
        candidate = Path(configured).expanduser()
        if not candidate.is_file() or not os.access(candidate, os.X_OK):
            raise RuntimeError("UNITY_EDITOR/--editor must point to an executable Unity Editor.")
        return candidate
    candidates = [Path(f"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity"),
                  Path.home() / f"Unity/Hub/Editor/{version}/Editor/Unity"]
    if os.name == "nt":
        candidates.append(Path(os.environ.get("ProgramFiles", "C:/Program Files")) / f"Unity/Hub/Editor/{version}/Editor/Unity.exe")
    return next((path for path in candidates if path.is_file() and os.access(path, os.X_OK)), None)


def validate_build(output: Path, build_id: str, version: str) -> None:
    try:
        receipt = json.loads((output / "build-receipt.json").read_text())
    except (OSError, ValueError) as error:
        raise RuntimeError("Unity did not produce a fresh build receipt. Inspect the build log.") from error
    if receipt.get("buildId") != build_id or receipt.get("unityVersion") != version:
        raise RuntimeError("Unity build receipt does not match this invocation or Editor version.")
    if not (output / "index.html").is_file() or not all(any((output / "Build").glob(pattern))
            for pattern in ("*.loader.js", "*.framework.js*", "*.data*", "*.wasm*")):
        raise RuntimeError("Unity exited without a complete Web player. Inspect the build log.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prepare-only", action="store_true", help="Generate the project without requiring Unity or downloading packages.")
    parser.add_argument("--editor", help="Unity Editor executable; defaults to UNITY_EDITOR or the standard Hub location.")
    parser.add_argument("--output", type=Path, default=ROOT / "Builds/WebGL")
    parser.add_argument("--timeout", type=int, default=1800)
    args = parser.parse_args()
    if args.timeout <= 0:
        parser.error("--timeout must be positive")
    record = prepare()
    print(f"Prepared Unity {record['editorVersion']} browser project: {STAGE}", flush=True)
    if args.prepare_only:
        return 0
    editor = find_editor(record["editorVersion"], args.editor)
    if editor is None:
        raise RuntimeError(f"Install Unity {record['editorVersion']} with Web Build Support and activate its license, or set UNITY_EDITOR. The generated project is ready; no player was built.")
    output = args.output.expanduser().resolve()
    if output == ROOT or ROOT.is_relative_to(output) or output == STAGE or STAGE.is_relative_to(output):
        raise RuntimeError("Choose a dedicated build output directory, not a project or its parent.")
    logs = ROOT / "TestResults"
    logs.mkdir(exist_ok=True)
    build_id = uuid.uuid4().hex
    command = [str(editor), "-batchmode", "-nographics", "-quit", "-projectPath", str(STAGE),
               "-buildTarget", "WebGL", "-executeMethod", "Frontier.Browser.Editor.WebGLBuilder.Build",
               "-frontier-webgl-output", str(output), "-frontier-build-id", build_id,
               "-frontier-editor-version", record["editorVersion"], "-logFile", str(logs / "unity-webgl-build.log")]
    result = subprocess.run(command, cwd=ROOT, timeout=args.timeout, check=False)
    if result.returncode:
        raise RuntimeError("Unity WebGL build failed. See TestResults/unity-webgl-build.log.")
    validate_build(output, build_id, record["editorVersion"])
    (output / "source-manifest.json").write_text(json.dumps({key: record[key] for key in ("editorVersion", "sourceDigest")}, indent=2) + "\n")
    print("FRONTIER_WEBGL_BUILD passed: " + str(output))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RuntimeError, subprocess.TimeoutExpired) as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
