using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Frontier.Core;
using Frontier.Networking;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class ServerLifecycleChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Frontier/Multiplayer/Check dedicated server reuse")]
        public static void Run()
        {
            // Exercise the real approval and disconnect paths without opening a socket.
            // A player-process check is still required to validate transport callbacks.
            var fixture = new GameObject("Dedicated server lifecycle checks");
            int checks = 0;
            try
            {
                var server = fixture.AddComponent<NgoMatchServer>();
                var catalog = new Catalog(JsonUtility.FromJson<CatalogDefinition>(Resources.Load<TextAsset>("catalog").text));
                var map = JsonUtility.FromJson<MapDefinition>(Resources.Load<TextAsset>("Maps/training").text);
                Set("catalog", catalog); Set("map", map); Set("mapId", "training");
                server.SimulationSpeed = 20;
                Call("ResetMatch");
                var seats = (IDictionary)Get("seats");
                var seatType = typeof(NgoMatchServer).GetNestedType("Seat", BindingFlags.NonPublic);
                object first = AddSeat(7, 0), second = AddSeat(8, 1);
                ((CommandReceipts)seatType.GetField("receipts").GetValue(first)).Record(new CommandAck { sequence = 17, accepted = true });
                var match = new Simulation(map, catalog) { Paused = true };
                Property("Match", match);
                string firstId = (string)Get("matchId");
                Set("frame", 19L); Set("accumulator", .3d); Set("lastTime", 12d); Set("nextSnapshot", 13d); Set("startServerTime", 100d);
                Property("AcceptedCommands", 11); Property("RejectedCommands", 4);

                Call("Disconnected", 7UL);
                Check(match.Winner == 1 && !match.Paused && server.ConnectedPlayers == 1,
                    "A disconnect during countdown concedes and leaves the survivor connected.");
                byte[] survivorResult = (byte[])seatType.GetField("result").GetValue(second);
                Check(server.Match == match && (bool)Get("finished") && survivorResult != null && survivorResult.Length > 0,
                    "The survivor retains the actual reliable final result until leaving.");
                Check(!Approve(9).Approved && server.Match == match && seats.Count == 2,
                    "A new client cannot replace a seat while the survivor reads the result.");

                Call("Disconnected", 8UL);
                Check(server.Match == null && seats.Count == 0 && server.ConnectedPlayers == 0 && !(bool)Get("finished"),
                    "The last departing participant releases the completed world and all previous seats.");
                string nextId = (string)Get("matchId");
                Check(nextId != firstId && nextId.Length == 32 && (long)Get("frame") == 0,
                    "The next match has a fresh identity and snapshot sequence.");
                Check(server.AcceptedCommands == 0 && server.RejectedCommands == 0
                    && (double)Get("accumulator") == 0 && (double)Get("lastTime") == 0
                    && (double)Get("nextSnapshot") == 0 && (double)Get("startServerTime") == 0
                    && ((Stopwatch)Get("clock")).IsRunning,
                    "Command totals, countdown and simulation clocks reset together.");
                Check(ReferenceEquals(Get("catalog"), catalog) && ReferenceEquals(Get("map"), map) && server.SimulationSpeed == 20,
                    "The configured map, catalog and simulation mode survive reuse.");
                Call("Disconnected", 7UL);
                Check((string)Get("matchId") == nextId && seats.Count == 0,
                    "A delayed duplicate disconnect cannot reset the next session.");

                Check(Approve(25).Approved && Approve(26).Approved && !Approve(27).Approved,
                    "The same server accepts two new clients and still rejects a third.");
                first = seats[25UL]; second = seats[26UL];
                Check((int)seatType.GetField("player").GetValue(first) == 0 && (int)seatType.GetField("player").GetValue(second) == 1
                    && !(bool)seatType.GetField("ready").GetValue(first) && seatType.GetField("result").GetValue(first) == null
                    && ((CommandReceipts)seatType.GetField("receipts").GetValue(first)).HighestSequence == 0,
                    "New clients receive distinct seats with fresh readiness, result and command receipt state.");
                seatType.GetField("connected").SetValue(first, true); seatType.GetField("connected").SetValue(second, true);
                var secondMatch = new Simulation(map, catalog);
                secondMatch.Submit(new GameCommand { player = 1, kind = CommandKind.Surrender });
                Property("Match", secondMatch); Set("finished", true);
                var reportBeingRead = new byte[] { 4, 5, 6 };
                seatType.GetField("result").SetValue(second, reportBeingRead);
                Call("Disconnected", 25UL);
                Check(server.Match == secondMatch && ReferenceEquals(seatType.GetField("result").GetValue(second), reportBeingRead),
                    "A normally completed second match keeps its report while one client remains.");
                Call("Disconnected", 26UL);
                Check(server.Match == null && seats.Count == 0 && (string)Get("matchId") != nextId,
                    "A second completed session also returns to an empty lobby.");
                Check(Approve(33).Approved, "A third session can accept a new first player.");
                seatType.GetField("connected").SetValue(seats[33UL], true);
                Call("Disconnected", 33UL);
                Check(seats.Count == 0 && server.Match == null && Approve(34).Approved,
                    "A departure before the match starts still frees the lobby seat.");

                double deadline = 0;
                int snapshots = 0;
                for (int frameIndex = 0; frameIndex < 600; frameIndex++)
                {
                    double now = frameIndex / 60d;
                    if (now < deadline) continue;
                    snapshots++;
                    deadline = NextSnapshotTime(deadline, now + .019);
                }
                Check(snapshots == 100 && Math.Abs(deadline - 10) < 1e-9,
                    "Frame quantization and 19 ms publication preserve the ten-Hz snapshot calendar.");
                double afterStall = NextSnapshotTime(.2, 4.251);
                Check(Math.Abs(afterStall - 4.3) < 1e-9 && afterStall > 4.251 && afterStall <= 4.351,
                    "A long stall skips missed snapshots and schedules only the next future period.");
                Check(Math.Abs(NextSnapshotTime(.1, .3) - .4) < 1e-9,
                    "An exact fractional deadline advances a full period without an immediate catch-up snapshot.");

                UnityEngine.Debug.Log($"FRONTIER_SERVER_LIFECYCLE passed: {checks} checks; real server lifecycle methods, no transport implied");

                double NextSnapshotTime(double previous, double now) => (double)typeof(NgoMatchServer)
                    .GetMethod("NextSnapshotTime", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { previous, now });
                object Get(string name) => typeof(NgoMatchServer).GetField(name, Private).GetValue(server);
                void Set(string name, object value) => typeof(NgoMatchServer).GetField(name, Private).SetValue(server, value);
                void Property(string name, object value) => typeof(NgoMatchServer).GetProperty(name).SetValue(server, value);
                object Call(string name, params object[] args) => typeof(NgoMatchServer).GetMethod(name, Private).Invoke(server, args);
                void Check(bool condition, string message)
                {
                    if (!condition) throw new Exception(message);
                    checks++; UnityEngine.Debug.Log("SERVER_LIFECYCLE_PASS: " + message);
                }
                object AddSeat(ulong id, int player)
                {
                    object seat = Activator.CreateInstance(seatType, true);
                    seatType.GetField("client").SetValue(seat, id); seatType.GetField("player").SetValue(seat, player);
                    seatType.GetField("faction").SetValue(seat, "industrial");
                    seatType.GetField("connected").SetValue(seat, true); seatType.GetField("ready").SetValue(seat, true);
                    seats.Add(id, seat); return seat;
                }
                NetworkManager.ConnectionApprovalResponse Approve(ulong id)
                {
                    Type protocol = typeof(NgoMatchServer).Assembly.GetType("Frontier.Networking.MatchProtocol");
                    string hash = (string)protocol.GetMethod("ContentHash", BindingFlags.Static | BindingFlags.Public).Invoke(null, null);
                    int version = (int)protocol.GetField("Version", BindingFlags.Static | BindingFlags.Public).GetRawConstantValue();
                    string hello = "{\"protocol\":" + version + ",\"rules\":" + Simulation.ReplayRulesVersion
                        + ",\"contentHash\":\"" + hash + "\",\"faction\":\"industrial\"}";
                    var request = new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = id, Payload = Encoding.UTF8.GetBytes(hello) };
                    var response = new NetworkManager.ConnectionApprovalResponse();
                    Call("Approve", request, response); return response;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
        }
    }
}
