using System;
using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using Frontier.Harness.Shim;
using Frontier.Networking;
using UnityEngine;

namespace Frontier.Harness
{
    // End-to-end checks of the dedicated server code with both transports. NGO
    // clients use the real NgoMatchClient over the in-process transport; browser
    // clients use real websockets against the real gateway.
    public static class Scenarios
    {
        private static int checks;
        private const string Host = "127.0.0.1";

        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new Exception("FAIL: " + message);
            Console.WriteLine("PASS: " + message);
        }

        public static int Run(bool quick)
        {
            MixedTransportMatch(quick);
            ProtocolViolations();
            AllocatedAdmission();
            Console.WriteLine("FRONTIER_HARNESS passed: " + checks + " checks");
            return 0;
        }

        private static NgoMatchServer StartServer(ushort port, ushort webSocketPort, string map, float speed, IAllocatedAdmission admission = null)
        {
            var server = new GameObject("Dedicated match server").AddComponent<NgoMatchServer>();
            server.SimulationSpeed = speed;
            if (admission != null) server.ConfigureAllocatedMode(admission);
            Check(server.StartServer(port, map, Host, webSocketPort), "Server listens on NGO port " + port + " and websocket port " + webSocketPort + ".");
            Check(server.WebSocketPort == webSocketPort, "The websocket listener reports its port.");
            return server;
        }

        private static void Stop(NgoMatchServer server)
        {
            server.StopServer();
            ShimRuntime.DestroyAll();
        }

        private static bool Done(StrategyBot bot) => bot.Passed || bot.Failed;

        private static void MixedTransportMatch(bool quick)
        {
            Console.WriteLine("== Quest-path (NGO) and browser-path (websocket) players share one match ==");
            var server = StartServer(17777, 17877, "rift", 20);
            var quest = new StrategyBot("Quest-path bot", new NgoLink(Host, 17777, "industrial"), AiDifficulty.Hard);
            var browser = new StrategyBot("Browser-path bot", new WebSocketLink("ws://" + Host + ":17877/", "biological"), AiDifficulty.Normal);
            quest.ConcedeAtSeconds = browser.ConcedeAtSeconds = quick ? 420 : 900;
            ShimRuntime.RunUntil(() => Done(quest) && Done(browser), quick ? 120 : 240);
            Check(!quest.Failed, "Quest-path bot completed without protocol errors" + (quest.Failed ? ": " + quest.FailReason : "."));
            Check(!browser.Failed, "Browser-path bot completed without protocol errors" + (browser.Failed ? ": " + browser.FailReason : "."));
            Check(quest.Passed && browser.Passed, "Both players reached a verified terminal result.");
            Console.WriteLine("  " + quest.Summary);
            Console.WriteLine("  " + browser.Summary);
            Check(quest.Player != browser.Player && quest.Player >= 0 && browser.Player >= 0, "The two transports received distinct seats.");
            Check(quest.Winner == browser.Winner && quest.FinalTick == browser.FinalTick, "Both transports agree on winner and final tick.");
            Check(quest.Digest != null && quest.Digest == browser.Digest, "Both transports received the identical full match report.");
            Check(server.AcceptedCommands > 20 && server.RejectedCommands >= 0, "The server executed orders from both transports (" + server.AcceptedCommands + " accepted).");
            quest.Link.Leave(); browser.Link.Leave();
            Check(ShimRuntime.RunUntil(() => server.ConnectedPlayers == 0 && server.Match == null, 10), "Direct server resets after both players leave.");

            Console.WriteLine("== Two browsers; one tab disappears mid-match ==");
            var left = new StrategyBot("Browser A", new WebSocketLink("ws://" + Host + ":17877/", "industrial"), AiDifficulty.Normal)
            { ConcedeAtSeconds = -1, AbortAtSeconds = 90, RequireFullEconomy = false };
            var stays = new StrategyBot("Browser B", new WebSocketLink("ws://" + Host + ":17877/", "industrial"), AiDifficulty.Easy)
            { ConcedeAtSeconds = -1, RequireFullEconomy = false };
            ShimRuntime.RunUntil(() => Done(stays), 60);
            Check(stays.Passed, "The remaining browser receives a verified result" + (stays.Failed ? ": " + stays.FailReason : "."));
            Check(stays.Winner == stays.Player && left.Player == 1 - stays.Player, "An abrupt websocket loss concedes the match to the remaining player.");
            Check(stays.FinalTick >= Simulation.TickRate * 90, "The concession happened after the first player's departure.");
            stays.Link.Leave();
            Check(ShimRuntime.RunUntil(() => server.ConnectedPlayers == 0 && server.Match == null, 10), "The same server process is ready for another pair.");
            Stop(server);
        }

        private static string Rejection(string url, byte[] firstFrame, Action<WebSocketMatchClient> after = null, double timeout = 8, bool autoReady = true)
        {
            var client = WebSocketMatchClient.Create("Protocol probe");
            client.AutoReady = autoReady;
            string reason = null;
            client.Disconnected += message => reason = message;
            client.ConnectRaw(url, firstFrame);
            after?.Invoke(client);
            ShimRuntime.RunUntil(() => reason != null, timeout);
            client.Disconnect(); UnityEngine.Object.Destroy(client.gameObject);
            ShimRuntime.Tick();
            return reason;
        }

        private static byte[] Hello(string faction = "industrial", string hash = null, string matchId = null, string ticket = null, int protocol = MatchProtocol.Version)
            => WebSocketMatchCodec.Envelope(WebSocketMatchCodec.Hello, MatchProtocol.Encode(new ConnectionHello { protocol = protocol, rules = Simulation.ReplayRulesVersion,
                faction = faction, contentHash = hash ?? MatchProtocol.ContentHash(), matchId = matchId, joinTicket = ticket }, MatchProtocol.MaxHelloBytes));

        private static void ProtocolViolations()
        {
            Console.WriteLine("== Browser protocol boundaries ==");
            var server = StartServer(17779, 17879, "training", 1);
            string url = "ws://" + Host + ":17879/";
            Check(Rejection(url, Hello(hash: "0BADC0DE")) == "Incompatible game version, rules, maps or catalog.", "A different catalog/map hash is refused with the same reason as NGO.");
            Check(Rejection(url, Hello(protocol: MatchProtocol.Version + 1)) == "Incompatible game version, rules, maps or catalog.", "A different protocol version is refused.");
            Check(Rejection(url, Hello(faction: "pirates")) == "Unknown faction.", "An unknown faction is refused.");
            Check(Rejection(url, WebSocketMatchCodec.Envelope(WebSocketMatchCodec.Ready, MatchProtocol.Signal)) == "Expected a connection request.", "Readiness before the connection request is refused.");
            Check(Rejection(url, WebSocketMatchCodec.Envelope(0x7F, new byte[] { 1 })) == "Malformed network message.", "Unknown message kinds are refused.");
            Check(Rejection(url, new byte[] { WebSocketMatchCodec.Hello }) == "Malformed network message.", "A kind byte without payload is refused.");
            Check(Rejection(url, Hello(), client => client.SendRaw(Hello())) == "Duplicate connection request.", "A second connection request on one socket is refused.");
            Check(Rejection(url, null, timeout: NgoMatchServer.WebSocketHelloTimeoutSeconds + 4) == "Timed out waiting for the connection request.", "A silent socket is closed after the hello timeout.");
            Check(ShimRuntime.RunUntil(() => server.ConnectedPlayers == 0, 3), "Refused sockets never keep a seat.");
            Check(Rejection(url, Hello(), timeout: 20, autoReady: false) == "Timed out waiting for player readiness.", "An admitted browser that never declares readiness loses its seat.");

            var first = WebSocketMatchClient.Create("Seat 1"); first.Connect(url, "industrial");
            var second = WebSocketMatchClient.Create("Seat 2"); second.Connect(url, "biological");
            Check(ShimRuntime.RunUntil(() => server.Match != null, 10), "Two ready browsers start a direct match.");
            Check(Rejection(url, Hello()) == "The match already has two players.", "A third browser is refused while the match runs.");
            string firstReason = null, secondReason = null;
            first.Disconnected += r => firstReason = r; second.Disconnected += r => secondReason = r;
            first.Abort(); second.Abort();
            Check(ShimRuntime.RunUntil(() => server.ConnectedPlayers == 0 && server.Match == null, 10), "The server recovers after both browsers vanish.");
            Stop(server);
        }

        private static void AllocatedAdmission()
        {
            Console.WriteLine("== Public matchmaking assignment admits a browser and a Quest-path player ==");
            const string matchId = "harnessAllocatedMatch0001";
            var verifier = new FakeAdmission { MatchId = matchId };
            verifier.Tickets["browser-ticket-000000000001"] = new AllocatedAdmissionResult { playerId = "player-browser", seat = 0, faction = "industrial" };
            verifier.Tickets["quest-ticket-000000000002"] = new AllocatedAdmissionResult { playerId = "player-quest", seat = 1, faction = "biological" };
            verifier.SynchronousFailures.Add("sync-refused-ticket-0003");
            var server = StartServer(17778, 17878, "rift", 20, verifier);
            var events = new List<AllocatedServerEvent>();
            string confirm = null;
            server.AllocationEvent += e => { events.Add(e); if (e.@event == "started") confirm = e.matchId; };
            Check(server.AssignMatch(new AllocatedMatchAssignment { matchId = matchId, mapId = "rift", status = "ready",
                expiresAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60,
                players = new[] { new AllocatedPlayer { playerId = "player-browser", seat = 0, faction = "industrial" },
                    new AllocatedPlayer { playerId = "player-quest", seat = 1, faction = "biological" } } }), "The server accepts a backend assignment.");
            Check(events.Count == 1 && events[0].@event == "ready", "Assignment readiness is reported to the backend.");
            string url = "ws://" + Host + ":17878/";
            Check(Rejection(url, Hello()) == "This connection has no active match assignment.", "A browser without a ticket cannot enter an allocated server.");
            Check(Rejection(url, Hello(matchId: "anotherMatch000000000000", ticket: "browser-ticket-000000000001")) == "This connection has no active match assignment.", "A ticket for a different match is refused.");
            Check(Rejection(url, Hello(matchId: matchId, ticket: "forged-ticket-0000000000")) == "Could not validate this match admission.", "A forged browser ticket is refused by the admission check.");

            string syncReason = null;
            var refused = new GameObject("NGO probe").AddComponent<NgoMatchClient>();
            refused.Disconnected += r => syncReason = r;
            refused.Connect(Host, 17778, "biological", matchId, "sync-refused-ticket-0003");
            ShimRuntime.RunUntil(() => syncReason != null, 8);
            Check(syncReason == "Could not validate this match admission.", "A synchronous backend refusal completes the NGO approval immediately (no approval timeout).");
            UnityEngine.Object.Destroy(refused.gameObject); ShimRuntime.Tick();

            var browser = new StrategyBot("Browser player", new WebSocketLink(url, "industrial", matchId, "browser-ticket-000000000001"), AiDifficulty.Normal)
            { ConcedeAtSeconds = 120, ConcedeSeat = -1, RequireFullEconomy = false };
            var quest = new StrategyBot("Quest player", new NgoLink(Host, 17778, "biological", matchId, "quest-ticket-000000000002"), AiDifficulty.Normal)
            { ConcedeAtSeconds = -1, RequireFullEconomy = false };
            ShimRuntime.RunUntil(() =>
            {
                if (confirm != null) { server.ConfirmAllocatedStart(confirm); confirm = null; }
                return Done(browser) && Done(quest);
            }, 90);
            Check(browser.Passed && quest.Passed, "Both admitted players complete the allocated match" + (browser.Failed ? ": " + browser.FailReason : quest.Failed ? ": " + quest.FailReason : "."));
            Check(browser.Player == 0 && quest.Player == 1, "Seats come from the backend assignment, not arrival order.");
            Check(browser.Winner == 1 && quest.Winner == 1 && browser.Digest == quest.Digest, "The concession result is identical on both transports.");
            var names = events.Select(e => e.@event).ToArray();
            Check(names.SequenceEqual(new[] { "ready", "started", "finished" }), "The backend receives ready, started and finished in order (" + string.Join(",", names) + ").");
            var finished = events.Last();
            Check(finished.winner == 1 && finished.tick == quest.FinalTick, "The reported result matches what the players saw.");
            browser.Link.Leave(); quest.Link.Leave();
            Check(ShimRuntime.RunUntil(() => server.AllocationCanRelease, 10), "The slot can be released once both players leave.");
            Check(server.ReleaseAllocation(matchId) && server.AllocationId == null, "Releasing returns the server to the pool.");
            Stop(server);
        }
    }
}
