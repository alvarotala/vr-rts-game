#!/usr/bin/env python3
"""Prepare private local Nakama configuration and run the isolated Compose stack."""
import argparse
import json
import os
from pathlib import Path
import secrets
import socket
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
LOCAL = ROOT / ".cache" / "online"


def detect_host():
    for interface in ("en0", "en1"):
        try:
            value = subprocess.check_output(["ipconfig", "getifaddr", interface], text=True, stderr=subprocess.DEVNULL).strip()
            if value:
                return value
        except (OSError, subprocess.CalledProcessError):
            pass
    try:
        value = socket.gethostbyname(socket.gethostname())
        if not value.startswith("127."):
            return value
    except OSError:
        pass
    return "127.0.0.1"


def initialize(host=None):
    LOCAL.mkdir(parents=True, exist_ok=True, mode=0o700)
    os.chmod(LOCAL, 0o700)
    env_path = LOCAL / "env"
    if env_path.exists():
        env = dict(line.split("=", 1) for line in env_path.read_text().splitlines() if "=" in line and not line.startswith("#"))
    else:
        env = {key: secrets.token_hex(32) for key in ("LL_DB_PASSWORD", "LL_RUNTIME_HTTP_KEY", "LL_SESSION_KEY", "LL_REFRESH_KEY", "LL_TICKET_KEY", "LL_CONSOLE_PASSWORD", "LL_CONSOLE_SIGNING_KEY")}
        env["LL_PUBLIC_HOST"] = host or detect_host()
    if host:
        if any(c.isspace() for c in host) or any(c in host for c in "/\\=$\n") or host in ("0.0.0.0", "::"):
            raise SystemExit("Use a reachable LAN IP or hostname, without scheme or port")
        env["LL_PUBLIC_HOST"] = host
    env_path.write_text("".join(f"{key}={value}\n" for key, value in env.items()))
    os.chmod(env_path, 0o600)
    # JSON is YAML-compatible, avoiding a setup dependency on PyYAML.
    config = {
        "name": "little-legion",
        "database": {"address": [f"nakama:{env['LL_DB_PASSWORD']}@postgres:5432/nakama?sslmode=disable"]},
        "logger": {"level": "INFO"},
        "socket": {"server_key": "littlelegion", "port": 7350},
        "session": {"encryption_key": env["LL_SESSION_KEY"], "refresh_encryption_key": env["LL_REFRESH_KEY"], "token_expiry_sec": 900, "refresh_token_expiry_sec": 2592000},
        "runtime": {"http_key": env["LL_RUNTIME_HTTP_KEY"], "env": [f"LL_TICKET_KEY={env['LL_TICKET_KEY']}", "LL_SERVER_IDS=local-1"]},
        "console": {"port": 7351, "username": "admin", "password": env["LL_CONSOLE_PASSWORD"], "signing_key": env["LL_CONSOLE_SIGNING_KEY"]},
        # Keep both players' ranges mandatory, including after a long wait.
        "matchmaker": {"max_tickets": 1, "interval_sec": 1, "max_intervals": 2,
                      "rev_precision": True, "rev_threshold": 0},
    }
    path = LOCAL / "config.yml"
    path.write_text(json.dumps(config, indent=2) + "\n")
    os.chmod(path, 0o600)
    print(f"Local configuration ready. API http://{env['LL_PUBLIC_HOST']}:7350; NGO {env['LL_PUBLIC_HOST']}:7778/udp")
    return env


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("init", "up", "backend", "down", "status", "logs", "test"))
    parser.add_argument("--host", help="Address reachable by the Quest; preserved on subsequent runs")
    args = parser.parse_args()
    if args.action == "init":
        initialize(args.host)
        return
    if args.host or not (LOCAL / "env").exists():
        initialize(args.host)
    compose = ["docker", "compose", "--env-file", str(LOCAL / "env"), "-f", str(ROOT / "compose.online.yml")]
    commands = {
        "up": ["--profile", "game", "up", "--build", "-d", "--wait", "--wait-timeout", "90"],
        "backend": ["up", "--build", "-d", "--wait", "--wait-timeout", "90", "nakama"],
        "down": ["--profile", "game", "down"],
        "status": ["--profile", "game", "ps", "-a"],
        "logs": ["logs", "--tail", "100", "nakama", "server"],
    }
    if args.action == "test":
        # Test uses a fake trusted agent. Stop the game slot first, then restore it.
        raise SystemExit(subprocess.call([sys.executable, str(ROOT / "backend/nakama/tests/integration.py")], cwd=ROOT))
    raise SystemExit(subprocess.call(compose + commands[args.action], cwd=ROOT))


if __name__ == "__main__":
    main()
