#!/usr/bin/env python3
"""Generate a minimal Unity server project from current sources, then build Linux.

The generated .cache/dedicated-server project is disposable; edit Core/Networking
and server/ templates in the main checkout. No client, Meta XR or URP assets enter it.
"""
from __future__ import annotations

import argparse
import base64
from concurrent.futures import ThreadPoolExecutor
import hashlib
import hmac
import json
from pathlib import Path
import platform
import re
import shlex
import shutil
import subprocess
import sys
import uuid
from urllib.parse import urlparse


ROOT = Path(__file__).resolve().parents[1]
STAGE = ROOT / ".cache" / "dedicated-server"
MARKER = STAGE / ".frontier-generated-server.json"
PACKAGE_CACHE = ROOT / ".cache" / "server-packages"
NETWORK_PACKAGES = (
    "com.unity.netcode.gameobjects", "com.unity.transport", "com.unity.collections",
    "com.unity.burst", "com.unity.mathematics", "com.unity.nuget.mono-cecil",
)
MODULES = (
    "animation", "audio", "imgui", "jsonserialize", "physics", "physics2d", "unitywebrequest",
)
LINUX_BUILD_PACKAGES = {
    "com.unity.toolchain.macos-arm64-linux": "1.1.0",
    "com.unity.sdk.linux-x86_64": "1.1.0",
    "com.unity.sysroot.base": "1.1.0",
}


def public_download(url: str, output: Path | None = None) -> bytes:
    """Fetch official public artifacts without inheriting curl auth/configuration."""
    parsed = urlparse(url)
    if parsed.scheme != "https" or parsed.hostname not in (
            "packages.unity.com", "download.packages.unity.com", "cdn.packages.unity.com"):
        raise RuntimeError("Unexpected Unity package download host.")
    command = ["curl", "--disable", "--fail", "--silent", "--show-error", "--location",
               "--proto", "=https", "--proto-redir", "=https", "--connect-timeout", "20",
               "--retry", "3", "--max-time", "1800" if output else "120",
               "--max-filesize", "1073741824" if output else "10485760"]
    if output:
        command.extend(["--output", str(output)])
    command.append(url)
    result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=False)
    if result.returncode:
        # No request headers, credentials, user curl config, or redirected URLs are
        # printed; curl's exit code is enough to diagnose transport/HTTP failures.
        raise RuntimeError(f"Public Unity package request failed (curl {result.returncode}) for {parsed.hostname}.")
    return result.stdout


def package_hashes(dist: dict) -> list[tuple[str, bytes]]:
    expected = []
    for value in str(dist.get("integrity", "")).split():
        algorithm, separator, digest = value.partition("-")
        if separator and algorithm in ("sha512", "sha384", "sha256", "sha1"):
            decoded = base64.b64decode(digest, validate=True)
            if len(decoded) != hashlib.new(algorithm).digest_size:
                raise RuntimeError("Invalid Unity package integrity metadata.")
            expected.append((algorithm, decoded))
    shasum = dist.get("shasum", "")
    if shasum:
        if not isinstance(shasum, str) or not re.fullmatch(r"[a-fA-F0-9]{40}", shasum):
            raise RuntimeError("Invalid Unity package shasum metadata.")
        expected.append(("sha1", bytes.fromhex(shasum)))
    if not expected:
        raise RuntimeError("Unity package registry did not provide a verifiable artifact hash.")
    return expected


def verified_archive(path: Path, expected: list[tuple[str, bytes]]) -> bool:
    if not path.is_file() or path.is_symlink():
        return False
    actual = {algorithm: hashlib.new(algorithm) for algorithm, _ in expected}
    with path.open("rb") as source:
        while chunk := source.read(1024 * 1024):
            for digest in actual.values():
                digest.update(chunk)
    return all(hmac.compare_digest(actual[algorithm].digest(), digest) for algorithm, digest in expected)


def acquire_build_package(item: tuple[str, str]) -> tuple[str, str]:
    name, version = item
    registry = json.loads(public_download("https://packages.unity.com/" + name))
    package = registry.get("versions", {}).get(version)
    if not isinstance(package, dict) or package.get("name") != name or package.get("version") != version:
        raise RuntimeError(f"Unexpected official package metadata for {name}@{version}.")
    dist = package.get("dist", {})
    expected = package_hashes(dist)
    archive = PACKAGE_CACHE / f"{name}-{version}.tgz"
    if verified_archive(archive, expected):
        print(f"Verified cached compiler package: {name}@{version}", flush=True)
    else:
        partial = archive.with_suffix(archive.suffix + ".partial")
        if partial.is_symlink():
            raise RuntimeError("Refusing a symlinked package download destination.")
        print(f"Downloading public compiler package: {name}@{version}", flush=True)
        try:
            public_download(dist["tarball"], partial)
            if not verified_archive(partial, expected):
                raise RuntimeError(f"Unity package checksum mismatch: {name}@{version}.")
            partial.replace(archive)
        finally:
            partial.unlink(missing_ok=True)
        print(f"Verified compiler package: {name}@{version}", flush=True)
    return name, "file:" + archive.as_posix()


def acquire_build_packages() -> dict[str, str]:
    if PACKAGE_CACHE.is_symlink():
        raise RuntimeError("Refusing a symlinked compiler package cache.")
    PACKAGE_CACHE.mkdir(parents=True, exist_ok=True)
    with ThreadPoolExecutor(max_workers=3) as executor:
        return dict(executor.map(acquire_build_package, LINUX_BUILD_PACKAGES.items()))


def metadata(path: Path, folder: bool = False) -> None:
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "frontier-dedicated/" + path.relative_to(STAGE).as_posix()).hex
    contents = f"fileFormatVersion: 2\nguid: {guid}\n"
    if folder:
        contents += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    meta.write_text(contents)


def prepare() -> tuple[str, dict]:
    # Unity 6.3's toolchain packages are host-specific. This generator currently
    # targets the validated authoring host; do not silently select an incompatible
    # binary toolchain on another machine.
    if platform.system() != "Darwin" or platform.machine().lower() != "arm64":
        raise RuntimeError("The dedicated IL2CPP generator currently supports macOS ARM64 hosts only.")
    if STAGE.is_symlink():
        raise RuntimeError("Refusing to replace a symlinked generated project.")
    if STAGE.exists() and not MARKER.exists() and any(STAGE.iterdir()):
        raise RuntimeError(f"{STAGE} exists without the generated-project marker; preserve it and choose a clean staging location.")
    if (STAGE / "Temp" / "UnityLockfile").exists():
        raise RuntimeError("Close the generated project's Unity Editor before refreshing its sources.")
    version_text = (ROOT / "ProjectSettings/ProjectVersion.txt").read_text()
    match = re.search(r"^m_EditorVersion:\s*(\S+)", version_text, re.MULTILINE)
    if not match or not match[1].startswith("6000.3."):
        raise RuntimeError("This dedicated build requires the main project's Unity 6.3 editor version.")
    version = match[1]
    locked = json.loads((ROOT / "Packages/packages-lock.json").read_text())["dependencies"]
    original = json.loads((ROOT / "Packages/manifest.json").read_text())["dependencies"]
    if original.get("com.unity.netcode.gameobjects") != "2.13.3":
        raise RuntimeError("Expected the reviewed NGO 2.13.3 dependency; review this generator when changing netcode.")
    dependencies = {name: locked[name]["version"] for name in NETWORK_PACKAGES}
    for module in MODULES:
        name = "com.unity.modules." + module
        dependencies[name] = original.get(name, "1.0.0")
    shared_packages = tuple(dependencies)
    # These editor-only cross-compilation dependencies belong to the disposable
    # server project, never to the Quest/client project's package manifest.
    # Local tarballs avoid coupling publicly downloadable compiler packages to an
    # Editor/CLI account session. Every reuse is checked against the registry hash.
    dependencies.update(acquire_build_packages())

    STAGE.mkdir(parents=True, exist_ok=True)
    # Keep import/build caches, but regenerate every authored asset so deleted source
    # files cannot survive silently in a later server build.
    if (STAGE / "Assets").exists():
        shutil.rmtree(STAGE / "Assets")
    for directory in ("Assets/Frontier", "Assets/Editor", "Packages", "ProjectSettings"):
        (STAGE / directory).mkdir(parents=True, exist_ok=True)
    for name in ("Core", "Networking"):
        source = ROOT / "Assets/Frontier" / name
        target = STAGE / "Assets/Frontier" / name
        shutil.copytree(source, target)
        shutil.copy2(Path(str(source) + ".meta"), Path(str(target) + ".meta"))
    resources = STAGE / "Assets/Frontier/Resources"
    resources.mkdir()
    for name in ("catalog.json", "catalog.json.meta"):
        shutil.copy2(ROOT / "Assets/Frontier/Resources" / name, resources / name)
    shutil.copytree(ROOT / "Assets/Frontier/Resources/Maps", resources / "Maps")
    shutil.copy2(ROOT / "Assets/Frontier/Resources/Maps.meta", resources / "Maps.meta")
    shutil.copy2(ROOT / "server/ServerBootstrap.cs", STAGE / "Assets/ServerBootstrap.cs")
    shutil.copy2(ROOT / "server/ServerBuilder.cs", STAGE / "Assets/Editor/ServerBuilder.cs")
    for directory in (STAGE / "Assets/Frontier", resources, STAGE / "Assets/Editor"):
        metadata(directory, folder=True)
    metadata(STAGE / "Assets/ServerBootstrap.cs")
    metadata(STAGE / "Assets/Editor/ServerBuilder.cs")
    (STAGE / "ProjectSettings/ProjectVersion.txt").write_text(version_text)
    (STAGE / "Packages/manifest.json").write_text(json.dumps({"dependencies": dependencies}, indent=2) + "\n")

    # Pin the complete reachable package graph to the main project's already-resolved
    # versions. In particular, the current client resolves UTP 2.7.4 despite NGO's
    # 2.6.0 minimum. A reduced manifest must not silently downgrade the server wire stack.
    graph: dict = {}
    # Keep the shared gameplay/network graph identical to the client. The staged
    # IL2CPP toolchain is absent from the main lock: UPM resolves those verified
    # local archives when importing the generated project.
    queue = [(name, 0) for name in shared_packages]
    while queue:
        name, depth = queue.pop(0)
        if name in graph and graph[name]["depth"] <= depth:
            continue
        if name not in locked:
            raise RuntimeError("Package missing from the main lock file: " + name)
        item = dict(locked[name])
        item["depth"] = depth
        graph[name] = item
        queue.extend((child, depth + 1) for child in item.get("dependencies", {}))
    (STAGE / "Packages/packages-lock.json").write_text(json.dumps({"dependencies": graph}, indent=2) + "\n")

    copied = {}
    for path in sorted((STAGE / "Assets").rglob("*")):
        if path.is_file():
            copied[path.relative_to(STAGE).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    fingerprint = hashlib.sha256(json.dumps(copied, sort_keys=True).encode()).hexdigest()
    record = {"generator": "scripts/build-server.py", "editorVersion": version, "sourceDigest": fingerprint,
              "packages": dependencies, "assets": copied}
    MARKER.write_text(json.dumps(record, indent=2) + "\n")
    return version, record


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--prepare-only", action="store_true", help="Verify/download compiler archives and refresh the minimal project without launching Unity.")
    parser.add_argument("--output", type=Path, default=ROOT / "Builds/Server/FrontierServer")
    parser.add_argument("--editor-version", help="Must match the main project's ProjectVersion.txt; defaults to that version.")
    parser.add_argument("--timeout", type=int, default=1200, help="Unity CLI build deadline in seconds.")
    args = parser.parse_args()
    version, record = prepare()
    if args.editor_version and args.editor_version != version:
        raise RuntimeError("The server must use the same Unity version as the game: " + version)
    if args.timeout < 1:
        raise RuntimeError("--timeout must be positive.")
    output = args.output.expanduser().resolve()
    logs = ROOT / "TestResults"
    logs.mkdir(exist_ok=True)
    editor_log = logs / "dedicated-server-build.log"
    cli_log = logs / "dedicated-server-build.cli.json"
    stderr_log = logs / "dedicated-server-build.stderr.log"
    command = ["unity", "build", str(STAGE), "--editor-version", version, "--target", "StandaloneLinux64",
               "--execute-method", "Frontier.Dedicated.Editor.ServerBuilder.BuildLinux", "--output-path", str(output),
               "--log-file", str(editor_log), "--no-tail", "--allow-dirty-build", "--timeout", str(args.timeout),
               "--format", "json", "--args", shlex.join(["-standaloneBuildSubtarget", "Server",
                                                           "-frontier-server-output", str(output)])]
    summary = {"stage": str(STAGE), "editorVersion": version, "scriptingBackend": "IL2CPP", "sourceDigest": record["sourceDigest"],
               "output": str(output), "log": str(editor_log), "command": shlex.join(command)}
    print(json.dumps(summary, indent=2), flush=True)
    if args.prepare_only:
        return 0
    if shutil.which("unity") is None:
        raise RuntimeError("Unity CLI is not on PATH. The staged project is ready; install/configure the CLI before building.")
    with cli_log.open("w") as stdout, stderr_log.open("w") as stderr:
        result = subprocess.run(command, cwd=ROOT, stdout=stdout, stderr=stderr, check=False)
    try:
        envelope = json.loads(cli_log.read_text())
        for notification in envelope.get("notifications", []):
            print("Unity CLI advisory: " + str(notification.get("message", notification.get("code", ""))), file=sys.stderr)
    except (ValueError, AttributeError):
        pass
    if result.returncode:
        print(f"Dedicated build failed ({result.returncode}). Logs: {editor_log}; {cli_log}; {stderr_log}", file=sys.stderr)
        return result.returncode
    if not output.is_file():
        raise RuntimeError("Unity exited successfully without creating the server executable; inspect " + str(editor_log))
    print("FRONTIER_DEDICATED_BUILD passed: " + str(output))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, KeyError, ValueError, RuntimeError) as error:
        print("Dedicated server build: " + str(error), file=sys.stderr)
        raise SystemExit(1)
