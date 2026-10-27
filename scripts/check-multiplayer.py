#!/usr/bin/env python3
"""Run one dedicated process and two real NGO clients; verify terminal agreement."""
import argparse
import json
import heapq
import os
from pathlib import Path
import random
import re
import selectors
import socket
import subprocess
import sys
import threading
import time


ROOT = Path(__file__).resolve().parents[1]
PASS = re.compile(r"FRONTIER_SMOKE_PASSED player=(\d+) winner=(\d+) tick=(\d+) digest=([0-9a-f]{64})")


class DatagramProxy:
    """Bounded UDP delay/jitter/loss in both directions, without host network changes."""

    def __init__(self, address, port, latency_ms, jitter_ms, loss_percent):
        self.remote = socket.getaddrinfo(address, port, socket.AF_INET, socket.SOCK_DGRAM)[0][4]
        self.latency = latency_ms / 1000
        self.jitter = jitter_ms / 1000
        self.loss = loss_percent / 100
        self.random = random.Random(5777)
        self.front = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self.front.bind(("127.0.0.1", 0))
        self.front.setblocking(False)
        self.port = self.front.getsockname()[1]
        self.stop_event = threading.Event()
        self.error = None
        self.received = self.forwarded = self.dropped = self.overflow = 0
        self.thread = threading.Thread(target=self.run, name="frontier-udp-proxy", daemon=True)
        self.thread.start()

    def run(self):
        selector = selectors.DefaultSelector()
        selector.register(self.front, selectors.EVENT_READ, None)
        backends = {}
        queue = []
        queued_bytes = 0
        sequence = 0
        try:
            while not self.stop_event.is_set():
                now = time.monotonic()
                while queue and queue[0][0] <= now:
                    _, _, sock, target, payload = heapq.heappop(queue)
                    queued_bytes -= len(payload)
                    try:
                        if target is None:
                            sock.send(payload)
                        else:
                            sock.sendto(payload, target)
                        self.forwarded += 1
                    except (BlockingIOError, ConnectionRefusedError):
                        self.dropped += 1
                timeout = min(.02, max(0, queue[0][0] - time.monotonic())) if queue else .02
                for key, _ in selector.select(timeout):
                    try:
                        payload, source = key.fileobj.recvfrom(65535)
                    except (BlockingIOError, ConnectionRefusedError):
                        continue
                    self.received += 1
                    if self.random.random() < self.loss:
                        self.dropped += 1
                        continue
                    if key.data is None:
                        if source not in backends:
                            if len(backends) >= 8:
                                self.overflow += 1
                                continue
                            backend = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
                            backend.connect(self.remote)
                            backend.setblocking(False)
                            backends[source] = backend
                            selector.register(backend, selectors.EVENT_READ, source)
                        destination, target = backends[source], None
                    else:
                        destination, target = self.front, key.data
                    if len(queue) >= 4096 or queued_bytes + len(payload) > 8 * 1024 * 1024:
                        self.overflow += 1
                        continue
                    sequence += 1
                    delay = max(0, self.latency + self.random.uniform(-self.jitter, self.jitter))
                    heapq.heappush(queue, (time.monotonic() + delay, sequence, destination, target, payload))
                    queued_bytes += len(payload)
        except Exception as error:
            self.error = str(error)
        finally:
            selector.close()
            self.front.close()
            for backend in backends.values():
                backend.close()

    def close(self):
        self.stop_event.set()
        self.thread.join(timeout=2)

    def stats(self):
        return {"latency_ms_one_way": self.latency * 1000, "jitter_ms": self.jitter * 1000,
                "loss_percent": self.loss * 100, "seed": 5777, "received_datagrams": self.received,
                "forwarded_datagrams": self.forwarded, "dropped_datagrams": self.dropped,
                "overflow_datagrams": self.overflow}


def executable(path):
    path = path.resolve()
    if path.suffix == ".app":
        binaries = [p for p in (path / "Contents/MacOS").iterdir() if p.is_file() and os.access(p, os.X_OK)]
        if len(binaries) != 1:
            raise ValueError(f"Expected one player executable inside {path}")
        return binaries[0]
    if not path.is_file() or not os.access(path, os.X_OK):
        raise ValueError(f"Player executable is missing or not executable: {path}")
    return path


def available_port():
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as sock:
        sock.bind(("127.0.0.1", 0))
        return sock.getsockname()[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    server_source = parser.add_mutually_exclusive_group(required=True)
    server_source.add_argument("--server", type=Path, help="Linux server binary or macOS server .app")
    server_source.add_argument("--external-server", help="Already-running dedicated server address (for example a Docker server on 127.0.0.1)")
    server_source.add_argument("--backend", help="Self-hosted Nakama URL; authenticate two isolated players and use real public matchmaking")
    parser.add_argument("--client", type=Path, default=ROOT / "Builds/Desktop/LittleLegion.app")
    parser.add_argument("--port", type=int, default=0, help="UDP port; zero chooses a currently free port")
    parser.add_argument("--timeout", type=float, default=540)
    parser.add_argument("--join-delay", type=float, default=0, help="Delay the second public client (0..120 seconds) to exercise widening while the first waits")
    parser.add_argument("--output", type=Path, default=ROOT / "TestResults/ngo-multiplayer")
    parser.add_argument("--latency-ms", type=float, default=0, help="One-way UDP delay applied in each direction")
    parser.add_argument("--jitter-ms", type=float, default=0, help="Uniform +/- delay variation per datagram")
    parser.add_argument("--loss-percent", type=float, default=0, help="Random datagram loss in each direction")
    args = parser.parse_args()
    processes = []
    streams = []
    proxy = None
    try:
        server = executable(args.server) if args.server else None
        client = executable(args.client)
        if not 0 <= args.port <= 65535 or args.timeout <= 0:
            raise ValueError("Invalid port or timeout.")
        if not 0 <= args.latency_ms <= 5000 or not 0 <= args.jitter_ms <= 5000 or not 0 <= args.loss_percent < 100:
            raise ValueError("Delay/jitter must be 0..5000 ms and packet loss 0..<100%.")
        if args.backend and (args.latency_ms or args.jitter_ms or args.loss_percent):
            raise ValueError("The local UDP impairment proxy currently supports direct connections only.")
        if not 0 <= args.join_delay <= 120 or args.join_delay and not args.backend:
            raise ValueError("Join delay requires public matchmaking and must be 0..120 seconds.")
        port = args.port or (7777 if args.external_server else available_port())
        address = args.external_server or "127.0.0.1"
        output = args.output.resolve()
        output.mkdir(parents=True, exist_ok=True)
        (output / "result.json").unlink(missing_ok=True)
        # Avoid mixing old pass markers into a fresh run.
        names = ("server", "client-a", "client-b") if server else ("client-a", "client-b")
        logs = {name: output / f"{name}.log" for name in names}
        for path in logs.values():
            path.write_text("")

        def launch(name, binary, extra, player_port=None):
            stream = (output / f"{name}.stdout.log").open("w")
            streams.append(stream)
            process = subprocess.Popen(
                [str(binary), "-batchmode", "-nographics", "-logFile", str(logs[name]),
                 "-frontier-port", str(port if player_port is None else player_port), "-frontier-smoke", *extra],
                cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT,
            )
            processes.append(process)
            return process

        server_process = launch("server", server, ["-frontier-server"]) if server else None
        if server_process:
            ready_deadline = time.monotonic() + min(30, args.timeout)
            while time.monotonic() < ready_deadline:
                if server_process.poll() is not None:
                    raise RuntimeError(f"Server exited during startup: {server_process.returncode}")
                if "FRONTIER_SERVER Listening on" in logs["server"].read_text(errors="replace"):
                    break
                time.sleep(.1)
            else:
                raise RuntimeError("Server did not report a listening UDP socket within the startup deadline.")
        player_port = port
        if args.latency_ms or args.jitter_ms or args.loss_percent:
            proxy = DatagramProxy(address, port, args.latency_ms, args.jitter_ms, args.loss_percent)
            address, player_port = "127.0.0.1", proxy.port
        connection_args = ["-frontier-public-smoke", "-frontier-backend", args.backend] if args.backend else ["-frontier-client", address]
        clients = [launch("client-a", client, connection_args + ["-frontier-faction", "industrial"], player_port)]
        if args.join_delay:
            time.sleep(args.join_delay)
        clients.append(launch("client-b", client, connection_args + ["-frontier-faction", "biological"], player_port))
        deadline = time.monotonic() + args.timeout
        while time.monotonic() < deadline:
            if proxy is not None and proxy.error is not None:
                raise RuntimeError("UDP impairment proxy failed: " + proxy.error)
            texts = {name: path.read_text(errors="replace") for name, path in logs.items()}
            for name, value in texts.items():
                if "FRONTIER_SMOKE_FAILED" in value:
                    raise RuntimeError(f"Smoke assertion failed in {name}; see {logs[name]}")
            if all(process.poll() is not None for process in clients):
                break
            if server_process is not None and server_process.poll() is not None:
                raise RuntimeError(f"Server exited before both client results: {server_process.returncode}")
            if any(process.poll() not in (None, 0) for process in clients):
                raise RuntimeError("A client exited unsuccessfully.")
            time.sleep(.25)
        else:
            raise RuntimeError("Timeout waiting for both network clients.")

        matches = []
        for name, process in zip(("client-a", "client-b"), clients):
            text = logs[name].read_text(errors="replace")
            result = PASS.search(text)
            if process.returncode != 0 or result is None:
                raise RuntimeError(f"Client lacks successful runtime evidence: {logs[name]}")
            matches.append(result.groups())
        if {match[0] for match in matches} != {"0", "1"}:
            raise RuntimeError("Clients were not assigned distinct player seats.")
        if matches[0][1:] != matches[1][1:]:
            raise RuntimeError("Clients disagree about winner, final tick or complete report digest.")
        result = {"passed": True, "port": port, "winner": int(matches[0][1]),
                  "tick": int(matches[0][2]), "report_sha256": matches[0][3],
                  "server": str(server) if server else "nakama:" + args.backend if args.backend else "external:" + args.external_server, "client": str(client),
                  "logs": {k: str(v) for k, v in logs.items()}}
        if args.backend:
            allocations = [re.search(r"FRONTIER_PUBLIC_ALLOCATED match=(\S+) host=(\S+) port=(\d+)", logs[name].read_text())
                           for name in ("client-a", "client-b")]
            if any(value is None for value in allocations) or allocations[0].groups() != allocations[1].groups():
                raise RuntimeError("Public clients were not assigned the same authenticated match and endpoint.")
            result["match_id"], result["host"], assigned_port = allocations[0].groups()
            result["port"] = int(assigned_port)
            ranks = [re.search(r"FRONTIER_RANK_PASSED player=(\d+) before=(\d+) after=(\d+) delta=(-?\d+) rank=(\d+)", logs[name].read_text())
                     for name in ("client-a", "client-b")]
            if any(value is None for value in ranks):
                raise RuntimeError("Public clients did not verify their persisted rating and leaderboard position.")
            result["ratings"] = [dict(zip(("player", "before", "after", "delta", "rank"), map(int, value.groups()))) for value in ranks]
            if args.join_delay:
                result["join_delay_seconds"] = args.join_delay
                if args.join_delay >= 30 and "FRONTIER_PUBLIC_RANGE revision=1 radius=200" not in logs["client-a"].read_text():
                    raise RuntimeError("The waiting public client did not renew its native ticket at the wider range.")
        if proxy is not None:
            result["network_impairment"] = proxy.stats()
            if proxy.overflow:
                raise RuntimeError("UDP impairment proxy exceeded its bounded queue; results are not valid for the requested profile.")
        (output / "result.json").write_text(json.dumps(result, indent=2) + "\n")
        print("FRONTIER_MULTIPLAYER_PASSED " + json.dumps(result))
        return 0
    except (OSError, ValueError, RuntimeError) as error:
        print(f"FRONTIER_MULTIPLAYER_FAILED {error}", file=sys.stderr)
        return 1
    finally:
        for process in reversed(processes):
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=5)
        for stream in streams:
            stream.close()
        if proxy is not None:
            proxy.close()


if __name__ == "__main__":
    sys.exit(main())
