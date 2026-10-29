#!/usr/bin/env python3
"""Prepare/build Little Legion for Android phones and iOS without Quest/XR."""
from __future__ import annotations

import argparse
import importlib.util
import json
from pathlib import Path
import plistlib
import re
import subprocess
import sys
import uuid
from xml.parsers.expat import ExpatError

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("shared_build", ROOT / "scripts/build-webgl.py")
shared = importlib.util.module_from_spec(spec)
spec.loader.exec_module(shared)
DEFAULT_APP_VERSION = "0.1.0"
IOS_LOCAL_NETWORK_DESCRIPTION = "Connect to Little Legion game servers on your local network for multiplayer matches."


def app_version(value: str) -> str:
    if not re.fullmatch(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)", value):
        raise argparse.ArgumentTypeError("--version must use three non-negative integers, for example 0.1.0")
    return value


def build_number(value: str) -> int:
    if not re.fullmatch(r"[0-9]+", value) or not 1 <= int(value) <= 2_147_483_647:
        raise argparse.ArgumentTypeError("--build-number must be a positive 32-bit integer")
    return int(value)


def validate_output(output: Path, stage: Path) -> None:
    if output == ROOT or ROOT.is_relative_to(output) or output == stage or stage.is_relative_to(output):
        raise RuntimeError("Choose a dedicated build output, not a source project or its parent.")
    if output.is_relative_to(ROOT) and not output.is_relative_to(ROOT / "Builds"):
        raise RuntimeError("Build output inside this repository must be under Builds/.")


def validate_build(output: Path, build_id: str, version: str, platform: str,
                   configuration: str = "development", application_version: str = DEFAULT_APP_VERSION,
                   application_build: int = 1) -> None:
    receipt_path = output.with_suffix(".receipt.json") if platform == "android" else output / "build-receipt.json"
    try:
        receipt = json.loads(receipt_path.read_text())
    except (OSError, ValueError) as error:
        raise RuntimeError("Unity did not produce a fresh mobile build receipt. Inspect the log.") from error
    if not isinstance(receipt, dict) or receipt.get("buildId") != build_id or receipt.get("unityVersion") != version or receipt.get("platform") != platform:
        raise RuntimeError("Mobile build receipt does not match this invocation, platform or Editor version.")
    if (receipt.get("configuration") != configuration or receipt.get("version") != application_version
            or type(receipt.get("buildNumber")) is not int or receipt["buildNumber"] != application_build):
        raise RuntimeError("Mobile build receipt does not match the requested configuration, app version or build number.")
    if platform == "android":
        complete = output.is_file() and output.stat().st_size > 0
    else:
        complete = (output / "Unity-iPhone.xcodeproj/project.pbxproj").is_file() and (output / "Data").is_dir() and (output / "Info.plist").is_file()
    if not complete:
        raise RuntimeError("Unity exited without a complete mobile build. Inspect the log.")


def configure_ios_project(output: Path) -> None:
    """Explain direct LAN/UDP access without enabling HTTP or changing signing."""
    path = output / "Info.plist"
    if path.is_symlink():
        raise RuntimeError("Refusing a symlinked iOS Info.plist.")
    try:
        data = plistlib.loads(path.read_bytes())
    except (OSError, ValueError, plistlib.InvalidFileException, ExpatError) as error:
        raise RuntimeError("Unity did not export a valid iOS Info.plist. Inspect the build output.") from error
    if not isinstance(data, dict):
        raise RuntimeError("Unity exported an invalid iOS Info.plist root.")
    data["NSLocalNetworkUsageDescription"] = IOS_LOCAL_NETWORK_DESCRIPTION
    path.write_bytes(plistlib.dumps(data, sort_keys=False))


def unity_command(editor: Path, stage: Path, output: Path, platform: str, build_id: str,
                  editor_version: str, configuration: str, application_version: str,
                  application_build: int, log: Path) -> list[str]:
    return [str(editor), "-batchmode", "-nographics", "-quit", "-projectPath", str(stage),
            "-buildTarget", "Android" if platform == "android" else "iOS",
            "-executeMethod", "Frontier.Mobile.Editor.MobileBuilder.Build",
            "-frontier-mobile-platform", platform, "-frontier-mobile-output", str(output),
            "-frontier-mobile-configuration", configuration, "-frontier-app-version", application_version,
            "-frontier-build-number", str(application_build), "-frontier-build-id", build_id,
            "-frontier-editor-version", editor_version, "-logFile", str(log)]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("platform", choices=("android", "ios"))
    parser.add_argument("--prepare-only", action="store_true", help="Generate the Unity project without building a player.")
    parser.add_argument("--editor", help="Unity Editor executable; otherwise uses UNITY_EDITOR or Unity Hub.")
    parser.add_argument("--output", type=Path)
    parser.add_argument("--timeout", type=int, default=1800)
    parser.add_argument("--release", action="store_true", help="Build without development flags; HTTP backend connections are disabled.")
    parser.add_argument("--version", type=app_version, default=DEFAULT_APP_VERSION, help="App version X.Y.Z (default: 0.1.0).")
    parser.add_argument("--build-number", type=build_number, default=1, help="Positive build number (default: 1); increase it for each store upload.")
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
    configuration = "release" if args.release else "development"
    command = unity_command(editor, stage, output, args.platform, build_id, record["editorVersion"],
                            configuration, args.version, args.build_number, log)
    result = subprocess.run(command, cwd=ROOT, timeout=args.timeout, check=False)
    if result.returncode:
        raise RuntimeError(f"Unity mobile build failed. See {log}.")
    validate_build(output, build_id, record["editorVersion"], args.platform, configuration, args.version, args.build_number)
    if args.platform == "ios":
        configure_ios_project(output)
    manifest = output.with_suffix(".source.json") if args.platform == "android" else output / "source-manifest.json"
    manifest.write_text(json.dumps({**{key: record[key] for key in ("editorVersion", "sourceDigest")},
                                   "platform": args.platform, "configuration": configuration,
                                   "version": args.version, "buildNumber": args.build_number}, indent=2) + "\n")
    print(f"FRONTIER_MOBILE_BUILD passed: {output} ({configuration} {args.version}, build {args.build_number})")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RuntimeError, subprocess.TimeoutExpired) as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
