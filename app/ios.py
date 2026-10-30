#!/usr/bin/env python3
"""Export, sign and deliver the iOS app using Unity and the selected Xcode team."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
from pathlib import Path, PurePosixPath
import plistlib
import re
import shlex
import subprocess
import sys
import uuid
import zipfile
from xml.parsers.expat import ExpatError

ROOT = Path(__file__).resolve().parents[1]
BUNDLE_ID = "app.sadmonkey.littlelegion.mobile"


def positive_number(value: str) -> int:
    try:
        result = int(value)
    except ValueError as error:
        raise argparse.ArgumentTypeError("Use a positive integer build number.") from error
    if not 0 < result <= 2147483647:
        raise argparse.ArgumentTypeError("Build number must be between 1 and 2147483647.")
    return result


def parse_args(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    for name in ("device", "testflight"):
        command = commands.add_parser(name)
        command.add_argument("--team-id", required=True, help="Explicit 10-character Apple developer team ID.")
        command.add_argument("--version", default="0.1.0")
        command.add_argument("--build-number", type=positive_number, required=True)
        command.add_argument("--editor", help="Unity Editor executable forwarded to app/build.py.")
        command.add_argument("--timeout", type=positive_number, default=3600, help="Maximum seconds per build command.")
        command.add_argument("--plan", action="store_true", help="Print a plan; do not run tools or create files.")
        if name == "device":
            command.add_argument("--device", required=True, help="Physical iPhone/iPad UDID from Xcode Devices or xctrace; not a CoreDevice UUID.")
        else:
            command.add_argument("--upload", action="store_true", help="Upload to App Store Connect using Xcode's signed-in account.")
    args = parser.parse_args(argv)
    if not re.fullmatch(r"[A-Z0-9]{10}", args.team_id):
        parser.error("--team-id must be an explicit 10-character uppercase Apple team ID.")
    if not re.fullmatch(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)", args.version):
        parser.error("--version must have the form X.Y.Z.")
    if args.command == "device" and (not args.device.strip() or args.device.startswith("-")):
        parser.error("--device must be the physical UDID of the intended connected device.")
    return args


def new_run_directory(args) -> Path:
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    return ROOT / "Builds/Mobile/iOS-delivery" / f"{stamp}-{args.command}-{args.build_number}-{uuid.uuid4().hex[:12]}"


def unity_command(args, folder: Path) -> list[str]:
    command = [sys.executable, str(ROOT / "app/build.py"), "ios", "--release", "--version", args.version,
               "--build-number", str(args.build_number), "--output", str(folder / "Unity"), "--timeout", str(args.timeout)]
    if args.editor:
        command += ["--editor", args.editor]
    return command


def xcode_command(args, folder: Path) -> list[str]:
    command = ["xcodebuild", "-project", str(folder / "Unity/Unity-iPhone.xcodeproj"), "-scheme", "Unity-iPhone",
               "-configuration", "Release", "-derivedDataPath", str(folder / "DerivedData"),
               "-destination", f"id={args.device}" if args.command == "device" else "generic/platform=iOS",
               "-allowProvisioningUpdates", f"DEVELOPMENT_TEAM={args.team_id}", "CODE_SIGN_STYLE=Automatic"]
    if args.command == "device":
        return command + ["-allowProvisioningDeviceRegistration", "build"]
    return command + ["-archivePath", str(folder / "LittleLegion.xcarchive"), "archive"]


def export_options(args) -> dict:
    return {"method": "app-store-connect", "destination": "upload" if args.upload else "export",
            "teamID": args.team_id, "signingStyle": "automatic", "manageAppVersionAndBuildNumber": False,
            "uploadSymbols": True}


def export_command(folder: Path) -> list[str]:
    return ["xcodebuild", "-exportArchive", "-archivePath", str(folder / "LittleLegion.xcarchive"),
            "-exportPath", str(folder / "Export"), "-exportOptionsPlist", str(folder / "ExportOptions.plist"),
            "-allowProvisioningUpdates"]


def install_command(args, app: Path) -> list[str]:
    return ["xcrun", "devicectl", "device", "install", "app", "--device", args.device, str(app)]


def launch_command(args) -> list[str]:
    return ["xcrun", "devicectl", "device", "process", "launch", "--device", args.device, BUNDLE_ID, "--timeout", "120"]


def run_command(label: str, command: list[str], timeout: int, capture=False):
    print(label + ": " + shlex.join(command), flush=True)
    return subprocess.run(command, cwd=ROOT, timeout=timeout, check=True, text=True, capture_output=capture)


def load_plist(path: Path) -> dict:
    try:
        with path.open("rb") as stream:
            data = plistlib.load(stream)
    except (OSError, ValueError, plistlib.InvalidFileException, ExpatError) as error:
        raise RuntimeError(f"Missing or invalid artifact metadata: {path}") from error
    if not isinstance(data, dict):
        raise RuntimeError(f"Artifact metadata is not a dictionary: {path}")
    return data


def validate_metadata(info: dict, args, source) -> None:
    expected = {"CFBundleIdentifier": BUNDLE_ID, "CFBundleShortVersionString": args.version,
                "CFBundleVersion": str(args.build_number)}
    for key, value in expected.items():
        if info.get(key) != value:
            raise RuntimeError(f"Artifact {source} has an unexpected {key}; expected {value}.")


def validate_app(app: Path, args) -> None:
    if not app.is_dir() or app.suffix != ".app":
        raise RuntimeError(f"Missing built iOS application: {app}")
    info = load_plist(app / "Info.plist")
    validate_metadata(info, args, app)
    executable = info.get("CFBundleExecutable", "")
    if not isinstance(executable, str) or not executable or Path(executable).name != executable:
        raise RuntimeError(f"Invalid app executable metadata: {app}")
    binary = app / executable
    if not binary.is_file() or binary.stat().st_size == 0:
        raise RuntimeError(f"Missing or empty iOS executable: {binary}")
    run_command("Verificar la firma", ["codesign", "--verify", "--deep", "--strict", "--verbose=2", str(app)], args.timeout)
    signature = run_command("Verificar equipo e identificador de firma", ["codesign", "-d", "--verbose=4", str(app)], args.timeout, capture=True)
    details = signature.stdout + "\n" + signature.stderr
    if not re.search(r"^TeamIdentifier=" + re.escape(args.team_id) + r"$", details, re.MULTILINE):
        raise RuntimeError("The application signature does not belong to the requested team.")
    if not re.search(r"^Identifier=" + re.escape(BUNDLE_ID) + r"$", details, re.MULTILINE):
        raise RuntimeError("The signed application identifier does not match the mobile app.")


def device_app(folder: Path) -> Path:
    apps = list((folder / "DerivedData/Build/Products/Release-iphoneos").glob("*.app"))
    if len(apps) != 1:
        raise RuntimeError("Expected exactly one built Release iPhone application; installation stopped.")
    return apps[0]


def validate_archive(folder: Path, args) -> Path:
    archive = folder / "LittleLegion.xcarchive"
    properties = load_plist(archive / "Info.plist").get("ApplicationProperties")
    if not isinstance(properties, dict):
        raise RuntimeError("The archive has no application metadata.")
    validate_metadata(properties, args, archive)
    relative = properties.get("ApplicationPath", "")
    if not isinstance(relative, str) or not relative:
        raise RuntimeError("The archive has no application path.")
    parts = PurePosixPath(relative)
    app = archive / "Products" / relative
    if parts.is_absolute() or ".." in parts.parts or not app.resolve().is_relative_to((archive / "Products").resolve()):
        raise RuntimeError("The archive application path leaves its Products directory.")
    validate_app(app, args)
    return app


def validate_ipa(folder: Path, args) -> Path:
    packages = list((folder / "Export").glob("*.ipa"))
    if len(packages) != 1:
        raise RuntimeError("Xcode did not export exactly one IPA; no delivery reported.")
    ipa = packages[0]
    try:
        with zipfile.ZipFile(ipa) as package:
            plists = [name for name in package.namelist() if re.fullmatch(r"Payload/[^/]+\.app/Info\.plist", name)]
            if len(plists) != 1:
                raise RuntimeError("The IPA does not contain exactly one app metadata file.")
            info = plistlib.loads(package.read(plists[0]))
            if not isinstance(info, dict):
                raise RuntimeError("The IPA app metadata is invalid.")
            validate_metadata(info, args, ipa)
            executable = info.get("CFBundleExecutable", "")
            if not isinstance(executable, str) or not executable or Path(executable).name != executable:
                raise RuntimeError("The IPA executable metadata is invalid.")
            binary = plists[0].removesuffix("Info.plist") + executable
            if package.getinfo(binary).file_size == 0:
                raise RuntimeError("The IPA contains an empty executable.")
    except (OSError, ValueError, KeyError, zipfile.BadZipFile, plistlib.InvalidFileException, ExpatError) as error:
        raise RuntimeError("Xcode produced an invalid or incomplete IPA.") from error
    return ipa


def print_plan(args, folder: Path) -> None:
    print("PLAN ÚNICAMENTE: no se ejecutan herramientas, no se compila, no se instala ni se sube nada.")
    print("Carpeta nueva para esta ejecución: " + str(folder))
    print("1. Exportar Unity: " + shlex.join(unity_command(args, folder)))
    print("2. Compilar y firmar: " + shlex.join(xcode_command(args, folder)))
    print(f"3. Verificar app/archivo: {BUNDLE_ID}, versión {args.version}, build {args.build_number}, firma del equipo {args.team_id}.")
    if args.command == "device":
        placeholder = folder / "DerivedData/Build/Products/Release-iphoneos/<app-verificada>.app"
        print("4. Instalar: " + shlex.join(install_command(args, placeholder)))
        print("5. Abrir: " + shlex.join(launch_command(args)))
    else:
        print("4. ExportOptions.plist: " + repr(export_options(args)))
        print("5. " + ("Subir" if args.upload else "Exportar IPA local") + ": " + shlex.join(export_command(folder)))
    print("El plan no comprueba disponibilidad de Unity, licencia, espacio, firma ni cuenta de Xcode.")


def main(argv=None) -> int:
    args = parse_args(argv)
    folder = new_run_directory(args)
    if args.plan:
        print_plan(args, folder)
        return 0
    if sys.platform != "darwin":
        raise RuntimeError("iOS signing and delivery require macOS with Xcode.")
    folder.mkdir(parents=True, exist_ok=False)
    run_command("Exportar el proyecto iOS con Unity", unity_command(args, folder), args.timeout)
    if not (folder / "Unity/Unity-iPhone.xcodeproj/project.pbxproj").is_file() or not (folder / "Unity/Data").is_dir():
        raise RuntimeError("Unity export is incomplete; Xcode delivery stopped.")
    run_command("Compilar y firmar con Xcode", xcode_command(args, folder), args.timeout)
    if args.command == "device":
        app = device_app(folder)
        validate_app(app, args)
        run_command("Instalar en el dispositivo indicado", install_command(args, app), args.timeout)
        run_command("Abrir Little Legion en el dispositivo", launch_command(args), args.timeout)
        print("Instalación y apertura completadas en el dispositivo indicado. Artefactos: " + str(folder))
        return 0
    validate_archive(folder, args)
    with (folder / "ExportOptions.plist").open("wb") as stream:
        plistlib.dump(export_options(args), stream)
    run_command("Subir a App Store Connect" if args.upload else "Exportar IPA local", export_command(folder), args.timeout)
    if args.upload:
        print("Xcode terminó la subida. Falta verificar el procesamiento y la disponibilidad en TestFlight.")
    else:
        print("IPA local verificado; no se subió a App Store Connect: " + str(validate_ipa(folder, args)))
    print("Artefactos de esta ejecución: " + str(folder))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RuntimeError, OSError, subprocess.SubprocessError) as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
