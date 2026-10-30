#!/usr/bin/env python3
"""Real Nakama/PostgreSQL contract test using native WebSocket matchmaking.

Uses only Python's standard library. Run against the local backend with the NGO
slot stopped: the fake trusted agent temporarily occupies local-1. No DB reset.
"""
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import socket
import struct
import subprocess
import threading
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[3]
ENV = dict(line.split("=", 1) for line in (ROOT / ".cache/online/env").read_text().splitlines() if "=" in line)
BASE = "http://127.0.0.1:" + ENV.get("LL_API_PORT", "7350")
COMPAT = {"protocol": 3, "rules": 4, "contentHash": "integration-v1"}
SOCKETS = []
CHECKS = 0


def check(value, message):
    global CHECKS
    assert value, message
    CHECKS += 1


def http(path, data, auth=None):
    headers = {"Content-Type": "application/json"}
    if auth:
        headers["Authorization"] = auth
    request = urllib.request.Request(BASE + path, data=json.dumps(data).encode(), headers=headers)
    with urllib.request.urlopen(request, timeout=5) as response:
        return json.load(response)


def rpc(name, data, player=None, server=False):
    query = "?unwrap"
    if server:
        query += "&http_key=" + urllib.parse.quote(ENV["LL_RUNTIME_HTTP_KEY"])
    return http("/v2/rpc/" + name + query, data, "Bearer " + player["token"] if player else None)


def expect_denied(fn, description):
    try:
        fn()
    except urllib.error.HTTPError as exc:
        check(exc.code in (400, 401, 403, 404, 409, 412), description + ": unexpected HTTP " + str(exc.code))
        return
    raise AssertionError(description + ": request unexpectedly succeeded")


def login(device=None):
    device = device or "ll-e2e-" + uuid.uuid4().hex
    basic = "Basic " + base64.b64encode(b"littlelegion:").decode()
    result = http("/v2/account/authenticate/device?create=true", {"id": device}, basic)
    result["device"] = device
    result["uid"] = json.loads(base64.urlsafe_b64decode(result["token"].split(".")[1] + "=="))["uid"]
    return result


class WebSocket:
    def __init__(self, player):
        self.sock = socket.create_connection(("127.0.0.1", int(ENV.get("LL_API_PORT", "7350"))), timeout=5)
        key = base64.b64encode(os.urandom(16)).decode()
        path = "/ws?format=json&status=false&token=" + urllib.parse.quote(player["token"])
        request = f"GET {path} HTTP/1.1\r\nHost: localhost\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: {key}\r\nSec-WebSocket-Version: 13\r\n\r\n"
        self.sock.sendall(request.encode())
        headers = b""
        while b"\r\n\r\n" not in headers:
            headers += self.sock.recv(1)
        check(headers.startswith(b"HTTP/1.1 101"), "WebSocket authenticated")
        expected = base64.b64encode(hashlib.sha1((key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11").encode()).digest())
        check(expected in headers, "WebSocket upgrade validated")
        self.cid = 0
        self.events = []
        SOCKETS.append(self)

    def send_frame(self, opcode, payload):
        mask = os.urandom(4)
        length = len(payload)
        prefix = bytes((0x80 | opcode, 0x80 | length)) if length < 126 else bytes((0x80 | opcode, 0x80 | 126)) + struct.pack("!H", length)
        self.sock.sendall(prefix + mask + bytes(value ^ mask[i % 4] for i, value in enumerate(payload)))

    def exact(self, count):
        result = b""
        while len(result) < count:
            chunk = self.sock.recv(count - len(result))
            if not chunk:
                raise AssertionError("WebSocket unexpectedly closed")
            result += chunk
        return result

    def receive(self):
        while True:
            first, second = self.exact(2)
            length = second & 127
            if length == 126:
                length = struct.unpack("!H", self.exact(2))[0]
            elif length == 127:
                length = struct.unpack("!Q", self.exact(8))[0]
            payload = self.exact(length)
            opcode = first & 15
            if opcode == 9:
                self.send_frame(10, payload)
                continue
            if opcode == 8:
                raise AssertionError("WebSocket server closed connection")
            check(opcode == 1, "JSON WebSocket message")
            return json.loads(payload)

    def request(self, name, data, fail=False):
        self.cid += 1
        cid = str(self.cid)
        self.send_frame(1, json.dumps({"cid": cid, name: data}).encode())
        while True:
            result = self.receive()
            if result.get("cid") == cid:
                check(("error" in result) == fail, f"Native {name} expected {'rejection' if fail else 'success'}: {result}")
                return result
            self.events.append(result)

    def matched(self):
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline:
            for i, event in enumerate(self.events):
                if "matchmaker_matched" in event:
                    return self.events.pop(i)["matchmaker_matched"]
            self.events.append(self.receive())
        raise AssertionError("Native matchmaker did not pair players")


def open_search(player, faction="industrial"):
    return rpc("ll_search_open", {**COMPAT, "faction": faction}, player)["searchId"]


def queue(ws, search_id, compatibility=None, fail=False):
    props = {"searchId": search_id, "compat": compatibility or "3:4:integration-v1"}
    return ws.request("matchmaker_add", {"min_count": 2, "max_count": 2, "query": "*", "string_properties": props}, fail)


def status(player, search_id):
    return rpc("ll_search_status", {"searchId": search_id}, player)


def pair(first=None, second=None):
    first, second = first or login(), second or login()
    a, b = open_search(first), open_search(second, "biological")
    sa, sb = WebSocket(first), WebSocket(second)
    queue(sa, a)
    queue(sb, b)
    ma, mb = sa.matched(), sb.matched()
    check(ma.get("match_id") == mb.get("match_id") and bool(ma.get("match_id")), "Native hook returned same allocation to both users")
    check(not ma.get("token"), "No relayed Nakama match fallback")
    return first, second, a, b, ma["match_id"]


class Agent:
    def __init__(self, ws_port=None):
        self.identity = {"serverId": "local-1", "bootId": "test-" + uuid.uuid4().hex}
        # Builds before the browser transport omit wsPort; the backend stores 0 for them.
        websocket = {} if ws_port is None else {"wsPort": ws_port}
        result = rpc("ll_server_register", {**self.identity, **COMPAT, "host": "127.0.0.1", "port": 7778, **websocket}, server=True)
        self.identity["leaseId"] = result["leaseId"]
        self.running = True
        self.errors = []
        self.thread = threading.Thread(target=self.heartbeat, daemon=True)
        self.thread.start()

    def heartbeat(self):
        while self.running:
            try:
                self.poll()
            except Exception as exc:
                self.errors.append(type(exc).__name__)
            time.sleep(2)

    def call(self, name, data):
        return rpc("ll_server_" + name, {**self.identity, **data}, server=True)

    def poll(self):
        return self.call("poll", {})

    def event(self, match, event, winner=0, tick=0):
        return self.call("event", {"matchId": match, "event": event, "winner": winner, "tick": tick, "reason": "integration test"})

    def admit(self, conn, request_id=None):
        return self.call("admit", {"matchId": conn["matchId"], "joinTicket": conn["joinTicket"], "requestId": request_id or uuid.uuid4().hex})

    def stop(self):
        self.running = False
        self.thread.join(timeout=3)


def ready(agent, paired):
    a, b, sa, sb, match = paired
    assignment = agent.poll()["assignment"]
    check(assignment["matchId"] == match and assignment["status"] == "preparing", "Server received correct preparing assignment")
    check({p["playerId"] for p in assignment["players"]} == {a["uid"], b["uid"]}, "Assignment identities came from authenticated accounts")
    check({p["seat"] for p in assignment["players"]} == {0, 1}, "Two distinct assigned seats")
    agent.event(match, "ready")
    first, second = status(a, sa), status(b, sb)
    check(first["status"] == second["status"] == "ready", "Both searches ready")
    ca, cb = first["connection"], second["connection"]
    check(ca["joinTicket"] != cb["joinTicket"] and ca["joinTicket"] != a["token"], "Scoped admission credentials differ by player and account JWT")
    check(ca["faction"] == "industrial" and cb["faction"] == "biological", "Saved factions preserved")
    return ca, cb


def age_fixture(compose, table, identifier, column):
    # Only test-owned rows and fixed identifiers are eligible for clock injection.
    assert table in ("ll_matches", "ll_servers") and column in ("expires_at", "heartbeat_at")
    assert re.fullmatch(r"[A-Za-z0-9_-]+", identifier)
    sql = f"UPDATE {table} SET {column}=now()-interval '30 seconds' WHERE id='{identifier}';"
    subprocess.run(compose + ["exec", "-T", "postgres", "psql", "-U", "nakama", "-d", "nakama", "-v", "ON_ERROR_STOP=1"], input=sql, text=True, check=True, stdout=subprocess.DEVNULL)


def main():
    compose = ["docker", "compose", "--env-file", str(ROOT / ".cache/online/env"), "-f", str(ROOT / "compose.online.yml")]
    live = subprocess.check_output(compose + ["ps", "--status", "running", "-q", "server"], text=True).strip()
    if live:
        raise SystemExit("Stop the online NGO slot before the fake-agent test; leave the LAN stack running.")
    agent = None
    try:
        a, b = login(), login()
        check(login(a["device"])["uid"] == a["uid"], "Anonymous identity persists across logins")
        basic = "Basic " + base64.b64encode(b"littlelegion:").decode()
        refreshed = http("/v2/account/session/refresh", {"token": a["refresh_token"]}, basic)
        check(bool(refreshed["token"]), "Refresh token restores session")
        expect_denied(lambda: rpc("ll_server_register", {"serverId": "local-1"}, a), "Player cannot register a server")
        expect_denied(lambda: rpc("ll_search_open", {**COMPAT, "faction": "industrial"}, server=True), "Server key cannot impersonate player")
        expect_denied(lambda: rpc("ll_server_poll", {}), "Anonymous cannot access server API")

        sid = open_search(a)
        expect_denied(lambda: status(b, sid), "Other player cannot read search")
        expect_denied(lambda: rpc("ll_search_cancel", {"searchId": sid}, b), "Other player cannot cancel search")
        expect_denied(lambda: open_search(a), "Only one active search per player")
        wa, wb = WebSocket(a), WebSocket(b)
        queue(wb, sid, fail=True)
        queue(wa, sid, compatibility="spoof", fail=True)
        queue(wa, sid)
        duplicate = WebSocket(a)
        queue(duplicate, sid, fail=True)
        rpc("ll_search_cancel", {"searchId": sid}, a)
        check(status(a, sid)["status"] == "cancelled", "Waiting search cancels")
        wa.sock.close()

        agent = Agent()
        first = pair(a, b)
        ca, cb = ready(agent, first)
        expect_denied(lambda: agent.event(first[4], "started"), "Cannot start before two admissions")
        expect_denied(lambda: agent.admit({**ca, "joinTicket": "a" * 43}), "Forged ticket rejected")
        expect_denied(lambda: rpc("ll_server_admit", {**agent.identity, **ca, "requestId": "spoof"}, a), "Player cannot call admission API")
        claim_id = uuid.uuid4().hex
        admitted = agent.admit(ca, claim_id)
        check(admitted == agent.admit(ca, claim_id), "Admission retry is idempotent")
        check(admitted["playerId"] == a["uid"], "Admission derives verified identity")
        expect_denied(lambda: agent.admit(ca), "Claimed ticket cannot be replayed")
        check(agent.admit(cb)["playerId"] == b["uid"], "Second player admitted")
        agent.event(first[4], "started")
        agent.event(first[4], "started")
        rpc("ll_search_cancel", {"searchId": first[2]}, a)
        check(agent.poll()["assignment"]["status"] == "playing", "Search cancellation cannot abort started game")

        second = pair()
        check(status(second[0], second[2])["status"] == "waitingServer", "Extra pair waits while pool is full")
        agent.event(first[4], "finished", winner=1, tick=400)
        agent.event(first[4], "finished", winner=1, tick=400)
        expect_denied(lambda: agent.event(first[4], "finished", winner=2, tick=400), "Persisted result cannot change")
        check(status(a, first[2])["status"] == "finished", "Finished search closes")
        check(status(second[0], second[2])["status"] == "waitingServer", "Result viewing retains server slot")
        agent.event(first[4], "released")
        agent.event(first[4], "released")
        ca2, _ = ready(agent, second)
        rpc("ll_search_cancel", {"searchId": second[2]}, second[0])
        check(status(second[0], second[2])["status"] == "cancelled", "Cancel after allocation invalidates handoff")
        check(status(second[1], second[3])["status"] == "failed", "Opponent sees allocation cancelled")
        expect_denied(lambda: agent.admit(ca2), "Cancelled ticket rejected")
        agent.event(second[4], "aborted")
        agent.event(second[4], "released")

        third = pair(a, b)
        ca3, cb3 = ready(agent, third)
        agent.admit(ca3)
        agent.admit(cb3)
        agent.event(third[4], "started")
        agent.event(third[4], "finished", winner=2, tick=800)
        agent.event(third[4], "released")
        check("assignment" not in agent.poll(), "Second completed session releases pool")

        fourth = pair()
        ca4, _ = ready(agent, fourth)
        old = dict(agent.identity)
        agent.stop()
        agent = Agent()
        expect_denied(lambda: rpc("ll_server_poll", old, server=True), "Restart invalidates old process lease")
        expect_denied(lambda: agent.admit(ca4), "Restart invalidates previous assignment ticket")
        check(status(fourth[0], fourth[2])["status"] == "failed", "Restart closes interrupted search")
        check("assignment" not in agent.poll(), "Replacement process starts clean")
        check(not agent.errors, "Heartbeat maintained healthy lease")

        fifth = pair()
        ca5, _ = ready(agent, fifth)
        age_fixture(compose, "ll_matches", fifth[4], "expires_at")
        check(status(fifth[0], fifth[2])["status"] == "failed", "Arrival deadline expires without a player")
        check(agent.poll()["assignment"]["status"] == "aborted", "Server observes no-show abort")
        expect_denied(lambda: agent.admit(ca5), "Expired ticket rejected")
        agent.event(fifth[4], "aborted")
        agent.event(fifth[4], "released")

        sixth = pair()
        ready(agent, sixth)
        old = dict(agent.identity)
        agent.stop()
        age_fixture(compose, "ll_servers", "local-1", "heartbeat_at")
        check(status(sixth[0], sixth[2])["status"] == "failed", "Stale lease aborts its assigned match")
        expect_denied(lambda: rpc("ll_server_poll", old, server=True), "Expired lease cannot heartbeat back to life")
        agent = Agent()
        check("assignment" not in agent.poll(), "Expired slot can register a fresh lease")

        # Cross-play: a Quest search (UDP) and a desktop search (websocket) form one pair,
        # which only a server with a websocket listener may receive.
        agent.stop()
        agent = Agent(ws_port=0)
        quest, desktop = login(), login()
        sq = open_search(quest)
        sd = rpc("ll_search_open", {**COMPAT, "faction": "biological", "websocket": True}, desktop)["searchId"]
        wq, wd = WebSocket(quest), WebSocket(desktop)
        queue(wq, sq)
        queue(wd, sd)
        mq, md = wq.matched(), wd.matched()
        check(mq.get("match_id") == md.get("match_id") and bool(mq.get("match_id")), "Quest and desktop searches are matched together")
        check("assignment" not in agent.poll(), "A server without a websocket listener never receives a desktop pair")
        check(status(quest, sq)["status"] == status(desktop, sd)["status"] == "waitingServer", "The mixed pair waits for a websocket-capable slot")
        expect_denied(lambda: rpc("ll_server_register", {"serverId": "local-1", "bootId": agent.identity["bootId"], **COMPAT, "host": "127.0.0.1", "port": 7778, "wsPort": 9999}, server=True),
                      "An active process cannot change its websocket port")
        expect_denied(lambda: Agent(ws_port=70000), "Websocket port must be a valid TCP port")
        agent.stop()
        agent = Agent(ws_port=7878)
        assignment = agent.poll()["assignment"]
        check(assignment["matchId"] == mq["match_id"], "The websocket-capable slot receives the mixed pair")
        agent.event(assignment["matchId"], "ready")
        cq, cd = status(quest, sq)["connection"], status(desktop, sd)["connection"]
        check(cq["port"] == cd["port"] == 7778 and cq["wsPort"] == cd["wsPort"] == 7878, "Status advertises the UDP game port and the websocket port")
        check(agent.admit(cq)["playerId"] == quest["uid"] and agent.admit(cd)["playerId"] == desktop["uid"], "Both transports are admitted with the same scoped tickets")
        agent.event(assignment["matchId"], "started")
        agent.event(assignment["matchId"], "finished", winner=1, tick=640)
        check(status(desktop, sd)["status"] == "finished", "The mixed match result closes both searches")
        agent.event(assignment["matchId"], "released")
        regular = pair()
        check(agent.poll()["assignment"]["matchId"] == regular[4], "A websocket-capable slot still serves Quest-only pairs")
        print(f"NAKAMA_INTEGRATION passed: {CHECKS} checks; native queue, trusted admission, backlog, cancellation, results, two sessions, restart, expiry, spoof rejection, cross-play allocation")
    finally:
        if agent:
            agent.stop()
        for ws in SOCKETS:
            ws.sock.close()


if __name__ == "__main__":
    main()
