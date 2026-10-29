#!/usr/bin/env python3
"""Prepare/build Little Legion for Android phones and iOS without Quest/XR."""
from __future__ import annotations

import argparse
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("shared_build", ROOT / "scripts/build-webgl.py")
shared = importlib.util.module_from_spec(spec)
spec.loader.exec_module(shared)


def validate_output(output: Path, stage: Path) -> None:
    if output == ROOT or ROOT.is_relative_to(output) or output == stage or stage.is_relative_to(output):
        raise RuntimeError("Choose a dedicated build output, not a source project or its parent.")
    if output.is_relative_to(ROOT) and not output.is_relative_to(ROOT / "Builds"):
        raise RuntimeError("Build output inside this repository must be under Builds/.")


def validate_build(output: Path, build_id: str, version: str, platform: str) -> None:
    receipt_path = output.with_suffix(".receipt.json") if platform == "android" else output / "build-receipt.json"
    try:
        receipt = json.loads(receipt_path.read_text())
    except (OSError, ValueError) as error:
        raise RuntimeError("Unity did not produce a fresh mobile build receipt. Inspect the log.") from error
    if receipt.get("buildId") != build_id or receipt.get("unityVersion") != version or receipt.get("platform") != platform:
        raise RuntimeError("Mobile build receipt does not match this invocation, platform or Editor version.")
    if platform == "android":
        complete = output.is_file() and output.stat().st_size > 0
    else:
        complete = (output / "Unity-iPhone.xcodeproj/project.pbxproj").is_file() and (output / "Data").is_dir()
    if not complete:
        raise RuntimeError("Unity exited without a complete mobile build. Inspect the log.")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("platform", choices=("android", "ios"))
    parser.add_argument("--prepare-only", action="store_true", help="Generate the Unity project without building a player.")
    parser.add_argument("--editor", help="Unity Editor executable; otherwise uses UNITY_EDITOR or Unity Hub.")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--timeout", type=int, default=1800)
    args = parser.parse_args()
    if args.timeout <= 0:
        parser.error("--timeout must be positive")
    stage = ROOT / "app/.generated" / args.platform
    default = ROOT / ("Builds/Mobile/Android/LittleLegion.apk" if args.platform == "android" else "Builds/Mobile/iOS")
    output = (args.output or default).expanduser().resolve()
    validate_output(output, stage)
    if args.platform == "android" and output.suffix.lower() != ".apk":
        parser.error("Android output must end in .apk")
    record = shared.prepare(stage=stage, mobile=True)
    print(f"Prepared Unity {record['editorVersion']} {args.platform} project: {stage}", flush=True)
    if args.prepare_only:
        return 0
    editor = shared.find_editor(record["editorVersion"], args.editor)
    if editor is None:
        raise RuntimeError(f"Install Unity {record['editorVersion']} with {'Android' if args.platform == 'android' else 'iOS'} Build Support and activate its license, or set UNITY_EDITOR. Project prepared; no player built.")
    logs = ROOT / "TestResults"
    logs.mkdir(exist_ok=True)
    build_id = uuid.uuid4().hex
    log = logs / f"unity-mobile-{args.platform}.log"
    command = [str(editor), "-batchmode", "-nographics", "-quit", "-projectPath", str(stage),
               "-buildTarget", "Android" if args.platform == "android" else "iOS",
               "-executeMethod", "Frontier.Mobile.Editor.MobileBuilder.Build",
               "-frontier-mobile-platform", args.platform, "-frontier-mobile-output", str(output),
               "-frontier-build-id", build_id, "-frontier-editor-version", record["editorVersion"],
               "-logFile", str(log)]
    result = subprocess.run(command, cwd=ROOT, timeout=args.timeout, check=False)
    if result.returncode:
        raise RuntimeError(f"Unity mobile build failed. See {log}.")
    validate_build(output, build_id, record["editorVersion"], args.platform)
    manifest = output.with_suffix(".source.json") if args.platform == "android" else output / "source-manifest.json"
    manifest.write_text(json.dumps({key: record[key] for key in ("editorVersion", "sourceDigest")}, indent=2) + "\n")
    print("FRONTIER_MOBILE_BUILD passed: " + str(output))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RuntimeError, subprocess.TimeoutExpired) as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
