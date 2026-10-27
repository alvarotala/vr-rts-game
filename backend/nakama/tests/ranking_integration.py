#!/usr/bin/env python3
"""Exercise ranked 1v1 against real Nakama, PostgreSQL and native matchmaking.

Stop only the online NGO slot before running: this test's trusted fake agent owns
local-1 temporarily. Accounts and match history are preserved. Clock/rating fixtures
can update only accounts and searches created by this invocation; no data reset.
"""
from concurrent.futures import ThreadPoolExecutor
import json
import math
import re
import subprocess
import time
import urllib.parse
import urllib.request
import uuid

import integration as base


check = base.check
OWN_PLAYERS = set()
OWN_SEARCHES = set()
COMPOSE = ["docker", "compose", "--env-file", str(base.ROOT / ".cache/online/env"),
           "-f", str(base.ROOT / "compose.online.yml")]
RATING_KEYS = ("rating", "games", "wins", "losses", "draws")


def sql(query):
    return subprocess.check_output(COMPOSE + ["exec", "-T", "postgres", "psql", "-U", "nakama",
        "-d", "nakama", "-v", "ON_ERROR_STOP=1", "-At", "-c", query], text=True).strip()


def player():
    value = base.login("ll-ranking-" + uuid.uuid4().hex)
    OWN_PLAYERS.add(value["uid"])
    return value


def profile(value):
    return base.rpc("ll_rank_profile", {}, value)


def result(value, match_id):
    return base.rpc("ll_rank_result", {"matchId": match_id}, value)


def snapshot(value):
    record = profile(value)
    return {key: record[key] for key in RATING_KEYS}


def owned_player(value):
    uid = value["uid"]
    assert uid in OWN_PLAYERS and str(uuid.UUID(uid)) == uid
    return uid


def seed_rating(value, rating):
    """Simulate prior skill only for a new test account, before opening its search."""
    uid = owned_player(value)
    check(profile(value)["games"] == 0, "Skill fixture belongs to a fresh account")
    assert isinstance(rating, int) and 0 <= rating <= 10000
    sql(f"UPDATE ll_ratings SET rating={rating} WHERE player_id='{uid}' AND games=0;")
    check(profile(value)["rating"] == rating, "Test-owned rating fixture applied")


def open_search(value, faction="industrial"):
    opened = base.rpc("ll_search_open", {**base.COMPAT, "faction": faction}, value)
    sid = opened["searchId"]
    assert re.fullmatch(r"[A-Za-z0-9_-]{43}", sid)
    OWN_SEARCHES.add((sid, value["uid"]))
    check(opened["rating"] == profile(value)["rating"], "Search snapshots server-side rating")
    check(opened["searchRadius"] == 100 and opened["queueRevision"] == 0,
          "Every new search begins within 100 Elo")
    return sid


def age_search(value, sid, seconds):
    uid = owned_player(value)
    assert (sid, uid) in OWN_SEARCHES and seconds in (10, 21, 41, 61, 100)
    sql(f"UPDATE ll_searches SET created_at=now()-interval '{seconds} seconds' "
        f"WHERE id='{sid}' AND player_id='{uid}' AND status='searching';")


def queue(ws, sid, spoof=False):
    # Even the impossible query, inflated rating and fake revision must be replaced.
    response = ws.request("matchmaker_add", {
        "min_count": 2, "max_count": 2,
        "query": "+properties.rating:>=999999" if spoof else "*",
        "string_properties": {"searchId": sid,
                              "compat": f"{base.COMPAT['protocol']}:{base.COMPAT['rules']}:{base.COMPAT['contentHash']}",
                              "queueRevision": "999"},
        "numeric_properties": {"rating": 999999, "searchRadius": 999999},
    })
    return response["matchmaker_ticket"]["ticket"]


def remove(ws, ticket):
    ws.request("matchmaker_remove", {"ticket": ticket})


def matched(first, second, sa, sb, wa, wb):
    ma, mb = wa.matched(), wb.matched()
    check(ma.get("match_id") == mb.get("match_id") and bool(ma.get("match_id")),
          "Native matchmaker returns one assigned match")
    check(not ma.get("token"), "Ranked matches never use relayed tokens")
    expected = {first["uid"]: profile(first)["rating"], second["uid"]: profile(second)["rating"]}
    actual = {entry["presence"]["user_id"]: int(entry["numeric_properties"]["rating"])
              for entry in ma["users"]}
    check(actual == expected, "Native numeric properties came from authoritative ratings")
    return first, second, sa, sb, ma["match_id"]


def pair(first, second, spoof=False):
    sa, sb = open_search(first), open_search(second, "biological")
    wa, wb = base.WebSocket(first), base.WebSocket(second)
    queue(wa, sa, spoof)
    queue(wb, sb, spoof)
    paired = matched(first, second, sa, sb, wa, wb)
    wa.sock.close()
    wb.sock.close()
    return paired


def start(agent, paired):
    ca, cb = base.ready(agent, paired)
    a, b = agent.admit(ca), agent.admit(cb)
    agent.event(paired[4], "started")
    check(result(paired[0], paired[4])["status"] == "pending", "Playing match has no provisional rating result")
    return a["seat"], b["seat"]


def finish(agent, paired, winner, tick, reason="ranking validation"):
    return agent.call("event", {"matchId": paired[4], "event": "finished", "winner": winner,
                               "tick": tick, "reason": reason})


def round_away(value):
    return math.floor(value + .5) if value >= 0 else math.ceil(value - .5)


def expected_rating(rating, opponent, outcome):
    return max(0, rating + round_away(32 * (outcome - 1 / (1 + 10 ** ((opponent - rating) / 400)))))


def native_board(value, leaderboard_id):
    query = urllib.parse.urlencode({"limit": 10, "owner_ids": value["uid"]})
    request = urllib.request.Request(base.BASE + "/v2/leaderboard/" + leaderboard_id + "?" + query,
                                    headers={"Authorization": "Bearer " + value["token"]})
    with urllib.request.urlopen(request, timeout=5) as response:
        return json.load(response)


def delete_native_record(value, leaderboard_id):
    request = urllib.request.Request(base.BASE + "/v2/leaderboard/" + leaderboard_id, method="DELETE",
                                    headers={"Authorization": "Bearer " + value["token"]})
    with urllib.request.urlopen(request, timeout=5) as response:
        return json.load(response)


def projected(value, timeout=8):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        current = profile(value)
        board = native_board(value, current["leaderboardId"])
        owner = next((r for r in board.get("owner_records", []) if r["owner_id"] == value["uid"]), None)
        if owner and int(owner["score"]) == current["rating"] and int(owner.get("subscore", 0)) == current["games"]:
            check(int(owner["rank"]) > 0, "Nakama assigns a real leaderboard position")
            metadata = json.loads(owner.get("metadata", "{}"))
            check(all(int(metadata[k]) == current[k] for k in ("games", "wins", "losses", "draws")),
                  "Nakama projection statistics agree with canonical rating")
            return current, owner
        time.sleep(.2)
    raise AssertionError("Nakama leaderboard did not converge to the current canonical rating")


def cancel_pair(agent, paired):
    before = snapshot(paired[0]), snapshot(paired[1])
    base.rpc("ll_search_cancel", {"searchId": paired[2]}, paired[0])
    agent.event(paired[4], "aborted")
    agent.event(paired[4], "released")
    check((snapshot(paired[0]), snapshot(paired[1])) == before, "Cancelled or aborted pair does not alter ratings")
    check(result(paired[0], paired[4])["status"] == "unrated", "Aborted match is explicitly unrated")


def assert_searching(first, second, sa, sb):
    # More than one native matchmaking pass must elapse before this assertion.
    deadline = time.monotonic() + 2.3
    while time.monotonic() < deadline:
        check(base.status(first, sa)["status"] == base.status(second, sb)["status"] == "searching",
              "Out-of-range pair remains in the queue without failing or allocating")
        time.sleep(.4)


def main():
    if subprocess.check_output(COMPOSE + ["ps", "--status", "running", "-q", "server"], text=True).strip():
        raise SystemExit("Stop the online NGO slot before ranking validation; leave LAN running.")
    # Isolate test matchmaking from users and the ordinary backend regression suite.
    base.COMPAT["contentHash"] = "ranking-" + uuid.uuid4().hex
    agent = None
    try:
        a, b, outsider = player(), player(), player()
        fresh = profile(a)
        check(fresh["playerId"] == a["uid"] and fresh["rating"] == 1000 and fresh["games"] == fresh["rank"] == 0,
              "Fresh account starts at 1000 Elo and remains unranked before playing")
        check(all(fresh[k] == 0 for k in ("wins", "losses", "draws")), "Fresh account has no fabricated results")
        check(bool(fresh["displayName"]), "Profile has a displayable player name")
        base.expect_denied(lambda: base.rpc("ll_rank_profile", {}), "Anonymous cannot read ranked profile")
        base.expect_denied(lambda: base.rpc("ll_rank_profile", {}, server=True), "Server key cannot impersonate a ranked player")
        base.expect_denied(lambda: base.rpc("ll_rank_profile", {"rating": 999999}, a), "Profile cannot accept a client rating")
        base.expect_denied(lambda: base.rpc("ll_search_open", {**base.COMPAT, "faction": "industrial", "rating": 999999}, a),
                           "Search cannot accept a client rating")
        board_id = fresh["leaderboardId"]
        base.expect_denied(lambda: base.http("/v2/leaderboard/" + board_id,
            {"score": "999999", "subscore": "0"}, "Bearer " + a["token"]), "Native leaderboard rejects player writes")

        agent = base.Agent()
        first = pair(a, b, spoof=True)
        base.expect_denied(lambda: result(outsider, first[4]), "Other account cannot read a private match rating result")
        seat_a, _ = start(agent, first)
        base.expect_denied(lambda: base.rpc("ll_server_event", {**agent.identity, "matchId": first[4],
            "event": "finished", "winner": seat_a, "tick": 400}, a), "Player cannot submit an authoritative result")
        with ThreadPoolExecutor(max_workers=4) as workers:
            list(workers.map(lambda _: finish(agent, first, seat_a, 400, "surrender"), range(8)))
        check(snapshot(a) == {"rating": 1016, "games": 1, "wins": 1, "losses": 0, "draws": 0},
              "Concurrent finish retries apply one +16 result to the equal-rated winner")
        check(snapshot(b) == {"rating": 984, "games": 1, "wins": 0, "losses": 1, "draws": 0},
              "Concurrent finish retries apply one -16 result to the equal-rated loser")
        ar, br = result(a, first[4]), result(b, first[4])
        check(ar["status"] == br["status"] == "rated" and ar["ratingDelta"] == 16 and br["ratingDelta"] == -16,
              "Both participants receive their own immutable Elo delta")
        check(ar["ratingBefore"] == 1000 and ar["ratingAfter"] == 1016 and ar["opponentRating"] == 1000,
              "Result records pre-match opponent strength")
        check(sql(f"SELECT count(*) FROM ll_rating_results WHERE match_id='{first[4]}';") == "2",
              "Only two rating ledger rows exist after concurrent retries")
        base.expect_denied(lambda: finish(agent, first, 2, 400), "Terminal winner cannot be changed")
        projected(a); projected(b)
        base.expect_denied(lambda: delete_native_record(a, board_id),
                           "Native leaderboard rejects hiding a player's ranked record")
        agent.event(first[4], "released")

        before = snapshot(a), snapshot(b)
        second = pair(a, b)
        _, seat_b = start(agent, second)
        finish(agent, second, seat_b, 700, "opponent disconnected")
        expected_a = expected_rating(before[0]["rating"], before[1]["rating"], 0)
        expected_b = expected_rating(before[1]["rating"], before[0]["rating"], 1)
        check(profile(a)["rating"] == expected_a and profile(b)["rating"] == expected_b,
              "Unequal-rated disconnect result uses pre-match expected score")
        check(profile(a)["games"] == profile(b)["games"] == 2, "Disconnect result counts one additional match")
        check(result(a, first[4]) == ar, "Later games cannot rewrite an earlier match's rating change")
        agent.event(second[4], "released")
        check(profile(base.login(a["device"]))["rating"] == expected_a, "Rating survives a fresh login")
        projected(a); projected(b)
        uid = owned_player(a)
        sql(f"INSERT INTO ll_rating_outbox(player_id,version) VALUES('{uid}',1) "
            "ON CONFLICT(player_id) DO UPDATE SET version=1;")
        projected(a)
        deadline = time.monotonic() + 8
        while time.monotonic() < deadline and sql(f"SELECT count(*) FROM ll_rating_outbox WHERE player_id='{uid}';") != "0":
            time.sleep(.2)
        check(sql(f"SELECT count(*) FROM ll_rating_outbox WHERE player_id='{uid}';") == "0",
              "Stale projection work drains without restoring an old rating")
        check(projected(a)[0]["rating"] == expected_a, "Outbox replay publishes the latest canonical state")

        c, d = player(), player()
        for tick in (1000, 1100):
            drawn = pair(c, d)
            start(agent, drawn)
            finish(agent, drawn, 2, tick)
            agent.event(drawn[4], "released")
            check(result(c, drawn[4])["ratingDelta"] == 0 and result(d, drawn[4])["ratingDelta"] == 0,
                  "Equal-rated draw preserves both ratings")
        check(snapshot(c) == {"rating": 1000, "games": 2, "wins": 0, "losses": 0, "draws": 2},
              "Repeated draws increment games and draw counters")
        projected(c); projected(d)
        top = base.rpc("ll_rank_top", {}, c)["entries"]
        check(0 < len(top) <= 10 and all(e["games"] > 0 and e["rank"] > 0 for e in top),
              "Top list contains at most ten ranked players, excluding unplayed accounts")
        check(all(top[i]["rating"] >= top[i+1]["rating"] for i in range(len(top)-1)), "Top ranking sorts by descending Elo")
        check(all(e["isSelf"] == (e["playerId"] == c["uid"]) for e in top), "Top list identifies the current account correctly")

        cancelled = pair(c, d)
        base.ready(agent, cancelled)
        cancel_pair(agent, cancelled)
        no_show = pair(c, d)
        base.ready(agent, no_show)
        old_c, old_d = snapshot(c), snapshot(d)
        base.age_fixture(COMPOSE, "ll_matches", no_show[4], "expires_at")
        check(base.status(c, no_show[2])["status"] == "failed", "Arrival timeout terminates the match")
        agent.event(no_show[4], "aborted"); agent.event(no_show[4], "released")
        check(snapshot(c) == old_c and snapshot(d) == old_d and result(c, no_show[4])["status"] == "unrated",
              "No-show timeout cannot award ranking points")

        low, high = player(), player()
        seed_rating(high, 1250)
        sl, sh = open_search(low), open_search(high, "biological")
        wl, wh = base.WebSocket(low), base.WebSocket(high)
        tl, th = queue(wl, sl), queue(wh, sh)
        assert_searching(low, high, sl, sh)
        age_search(low, sl, 41)
        state = base.status(low, sl)
        check(state["searchRadius"] == 300 and state["queueRevision"] == 2,
              "Waiting 40 seconds expands to 300 Elo using backend time")
        remove(wl, tl); tl = queue(wl, sl)
        assert_searching(low, high, sl, sh)
        age_search(high, sh, 41)
        remove(wh, th); queue(wh, sh)
        widened = matched(low, high, sl, sh, wl, wh)
        wl.sock.close(); wh.sock.close()
        base.ready(agent, widened)
        cancel_pair(agent, widened)

        far_a, far_b = player(), player()
        seed_rating(far_b, 1401)
        sa, sb = open_search(far_a), open_search(far_b, "biological")
        for seconds, radius, revision in ((10, 100, 0), (21, 200, 1), (41, 300, 2), (61, 400, 3), (100, 400, 3)):
            age_search(far_a, sa, seconds)
            state = base.status(far_a, sa)
            check((state["searchRadius"], state["queueRevision"]) == (radius, revision),
                  "Server-owned range expands every 20 seconds and caps at 400")
        age_search(far_b, sb, 100)
        wa, wb = base.WebSocket(far_a), base.WebSocket(far_b)
        queue(wa, sa, spoof=True); queue(wb, sb, spoof=True)
        assert_searching(far_a, far_b, sa, sb)
        for value, sid, ws in ((far_a, sa, wa), (far_b, sb, wb)):
            base.rpc("ll_search_cancel", {"searchId": sid}, value); ws.sock.close()
        check(not agent.errors, "Trusted agent retained its lease during ranked checks")
        check("assignment" not in agent.poll(), "Ranked validation leaves the slot free")
        print(f"NAKAMA_RANKING_INTEGRATION passed: {base.CHECKS} checks; authoritative Elo, concurrent retry, draws, cancellation, no-show, native projection, spoof rejection, mutual expanding skill ranges")
    finally:
        if agent:
            agent.stop()
        for ws in base.SOCKETS:
            ws.sock.close()


if __name__ == "__main__":
    main()
