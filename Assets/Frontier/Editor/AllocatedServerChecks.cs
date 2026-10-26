using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Frontier.Core;
using Frontier.Networking;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace Frontier.Editor
{
    public static class AllocatedServerChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private sealed class ControlledAdmission : IAllocatedAdmission
        {
            public IEnumerator Admit(string matchId, string ticket, string requestId,
                Action<AllocatedAdmissionResult> success, Action<string> failure) { yield break; }
        }

        [MenuItem("Frontier/Multiplayer/Check allocated server admission")]
        public static void Run()
        {
            int checks = 0;
            var root = new GameObject("Allocated admission checks");
            var managerRoot = new GameObject("Allocated admission network fixture");
            var server = root.AddComponent<NgoMatchServer>();
            var manager = managerRoot.AddComponent<NetworkManager>();
            var events = new List<AllocatedServerEvent>();
            server.AllocationEvent += events.Add;
            try
            {
                server.ConfigureAllocatedMode(new ControlledAdmission());
                Set("catalog", new Catalog(JsonUtility.FromJson<CatalogDefinition>(Resources.Load<TextAsset>("catalog").text)));
                Call("ResetMatch"); Set("manager", manager);
                var seats = (IDictionary)Get("seats");
                var pending = (IDictionary)Get("pendingAdmissions");
                var pendingType = typeof(NgoMatchServer).GetNestedType("PendingAdmission", BindingFlags.NonPublic);
                var seatType = typeof(NgoMatchServer).GetNestedType("Seat", BindingFlags.NonPublic);

                var invalid = Assignment("invalid"); invalid.players[1].seat = 0;
                Check(!server.AssignMatch(invalid), "A duplicate assigned seat is refused.");
                invalid = Assignment("invalid"); invalid.players[1].playerId = invalid.players[0].playerId;
                Check(!server.AssignMatch(invalid), "A duplicate assigned account is refused.");
                invalid = Assignment("invalid"); invalid.expiresAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1;
                Check(!server.AssignMatch(invalid), "An expired assignment is refused.");
                invalid = Assignment("invalid"); invalid.mapId = "missing-map";
                Check(!server.AssignMatch(invalid), "An unavailable assigned map is refused.");
                var assignment = Assignment("match-one");
                Check(server.AssignMatch(assignment) && server.AllocationId == "match-one" && events.Count == 1 && events[0].@event == "ready",
                    "A valid allocation keeps the backend identity and reports readiness.");
                assignment.players[0].faction = "biological";
                Check(((AllocatedMatchAssignment)Get("allocation")).players[0].faction == "industrial",
                    "The active roster is copied from the allocation response.");
                Check(!server.AssignMatch(Assignment("replacement")), "An active allocation cannot be replaced in place.");

                object absent = Pending(1); manager.PendingClients.Remove(1);
                Complete(absent, Result("alpha", 0, "industrial"));
                Check(seats.Count == 0 && !Response(absent).Approved && !Response(absent).Pending,
                    "An HTTP success after the transport disappears cannot create a ghost seat.");
                object expired = Pending(2); pendingType.GetField("deadline").SetValue(expired, Time.realtimeSinceStartupAsDouble - 1);
                Complete(expired, Result("alpha", 0, "industrial"));
                Check(seats.Count == 0 && !Response(expired).Approved, "Admission exceeding its five-second guard fails closed.");
                object wrong = Pending(3); Complete(wrong, Result("outsider", 0, "industrial"));
                Check(seats.Count == 0 && !Response(wrong).Approved, "An unassigned verified account is still refused.");
                wrong = Pending(4); Complete(wrong, Result("alpha", 1, "industrial"));
                Check(seats.Count == 0 && !Response(wrong).Approved, "A wrong verified seat is refused.");
                wrong = Pending(5); Complete(wrong, Result("alpha", 0, "biological"));
                Check(seats.Count == 0 && !Response(wrong).Approved, "Faction comes from the assignment, not a forged admission result.");
                object first = Pending(6); Complete(first, Result("alpha", 0, "industrial"));
                Check(seats.Count == 1 && Response(first).Approved && !Response(first).Pending
                    && (int)seatType.GetField("player").GetValue(seats[6UL]) == 0
                    && (string)seatType.GetField("playerId").GetValue(seats[6UL]) == "alpha",
                    "A verified assigned account receives exactly its reserved seat.");
                Complete(first, Result("alpha", 0, "industrial"));
                Check(seats.Count == 1, "A duplicate HTTP callback cannot add another seat.");
                object duplicate = Pending(7); Complete(duplicate, Result("alpha", 0, "industrial"));
                Check(seats.Count == 1 && !Response(duplicate).Approved, "Two connections cannot occupy the same account/seat.");
                object disconnected = Pending(8); Call("Disconnected", 8UL);
                Complete(disconnected, Result("beta", 1, "biological"));
                Check(seats.Count == 1 && !Response(disconnected).Approved && !pending.Contains(8UL),
                    "A pre-seat disconnect invalidates its pending approval before the no-seat return.");
                object second = Pending(9); Complete(second, Result("beta", 1, "biological"));
                Check(seats.Count == 2 && Response(second).Approved, "The distinct second account receives the other seat.");
                Call("RequestAllocatedStart");
                Check(server.Match == null && events[events.Count - 1].@event == "started",
                    "Both players' handoff is committed at the backend before the simulation starts.");
                server.ConfirmAllocatedStart("wrong-match");
                Check(server.Match == null, "A stale start acknowledgement cannot start a different allocation.");
                server.AbortAllocation("cancelled during start confirmation");
                server.ConfirmAllocatedStart("match-one");
                Check(server.Match == null && events[events.Count - 1].@event == "aborted",
                    "Cancellation before the start ACK prevents the match from starting.");
                Check(!server.ReleaseAllocation("wrong-match") && server.ReleaseAllocation("match-one") && seats.Count == 0,
                    "Release requires the exact terminal allocation and clears its seats.");

                Check(server.AssignMatch(Assignment("match-two")), "The same listening process can take another allocated match.");
                object stale = Pending(10); Call("InvalidateApprovals");
                Complete(stale, Result("alpha", 0, "industrial"));
                Check(!Response(stale).Approved && seats.Count == 0, "A reset generation invalidates a late successful admission.");
                Set("allocationDeadline", Time.realtimeSinceStartupAsDouble - 1); Call("AdvanceAllocation");
                Check(events[events.Count - 1].@event == "aborted" && server.AllocationCanRelease,
                    "A no-show deadline aborts rather than holding the server indefinitely.");
                Check(server.ReleaseAllocation("match-two"), "A no-show allocation can be released after its terminal ACK.");

                Check(server.AssignMatch(Assignment("match-three")), "A later allocation starts with a fresh admission state.");
                first = Pending(11); Complete(first, Result("alpha", 0, "industrial"));
                second = Pending(12); Complete(second, Result("beta", 1, "biological"));
                Call("Disconnected", 11UL);
                Check(events[events.Count - 1].@event == "aborted" && server.Match == null,
                    "A pre-start departure aborts the pair, rather than reopening its seat to strangers.");
                Check(server.ReleaseAllocation("match-three"), "An aborted pair releases cleanly.");

                Check(server.AssignMatch(Assignment("match-four")), "A completed-match fixture receives its own identity.");
                var map = JsonUtility.FromJson<MapDefinition>(Resources.Load<TextAsset>("Maps/training").text);
                var match = new Simulation(map, (Catalog)Get("catalog"));
                match.Submit(new GameCommand { player = 1, kind = CommandKind.Surrender });
                typeof(NgoMatchServer).GetProperty("Match").SetValue(server, match);
                int beforeFinish = events.Count; Call("Finish"); Call("Finish");
                Check(events.Count == beforeFinish + 1 && events[events.Count - 1].@event == "finished"
                    && events[events.Count - 1].winner == 0 && events[events.Count - 1].tick == match.Tick,
                    "A terminal result is emitted once from the authoritative simulation.");
                Check(server.AllocationCanRelease && server.ReleaseAllocation("match-four"), "A reported terminal match can release its allocation.");

                var agent = root.AddComponent<AllocatedServerAgent>();
                typeof(AllocatedServerAgent).GetField("server", Private).SetValue(agent, server);
                int beforeDiscard = events.Count;
                typeof(AllocatedServerAgent).GetMethod("HandleAssignment", Private).Invoke(agent, new object[] {
                    new AllocatedMatchAssignment { matchId = "cancelled-before-first-poll", status = "aborted", expiresAtUnix = 0 }
                });
                Check(events.Count == beforeDiscard + 1 && events[events.Count - 1].@event == "aborted"
                    && server.Match == null && server.AllocationCanRelease,
                    "Cancellation before the first poll acknowledges the terminal allocation without requiring a live roster/map/deadline.");
                Check(server.ReleaseAllocation("cancelled-before-first-poll") && server.AllocationId == null,
                    "A never-started terminal allocation releases the slot for the next pair.");

                Check(server.AssignMatch(Assignment("release-ack-lost")), "The release retry fixture has its own allocation.");
                server.AbortAllocation("terminal fixture");
                var terminal = events[events.Count - 1];
                AgentCall("Enqueue", terminal);
                AgentCall("AcknowledgeEvent", terminal);
                var release = new AllocatedServerEvent { matchId = "release-ack-lost", @event = "released" };
                AgentCall("Enqueue", release);
                var queuedEvents = (List<AllocatedServerEvent>)typeof(AllocatedServerAgent).GetField("events", Private).GetValue(agent);
                int beforeReleaseRetry = events.Count;
                AgentCall("HandleAssignment", (object)null);
                Check(server.AllocationId == "release-ack-lost" && queuedEvents.Contains(release)
                    && events.Count == beforeReleaseRetry && !(bool)typeof(AllocatedServerAgent).GetField("stopped", Private).GetValue(agent),
                    "A committed release with a lost ACK and no next pair retains the pending idempotent retry.");
                var nextAssignment = Assignment("pair-after-release");
                AgentCall("HandleAssignment", nextAssignment);
                Check(server.AllocationId == "release-ack-lost" && queuedEvents.Contains(release)
                    && !(bool)typeof(AllocatedServerAgent).GetField("stopped", Private).GetValue(agent),
                    "A next-pair poll arriving before the release ACK does not stop the dedicated server or replace the old allocation.");
                AgentCall("AcknowledgeEvent", release);
                Check(server.AllocationId == null && queuedEvents.Count == 0
                    && !(bool)typeof(AllocatedServerAgent).GetField("terminalAcknowledged", Private).GetValue(agent),
                    "The retried release ACK clears the terminal local state exactly once.");
                AgentCall("HandleAssignment", nextAssignment);
                Check(server.AllocationId == "pair-after-release" && events[events.Count - 1].@event == "ready",
                    "The next poll adopts the waiting pair after the release ACK, preserving the reusable server slot.");

                Debug.Log($"FRONTIER_ALLOCATED_SERVER passed: {checks} checks; controlled admission responses and lifecycle, no HTTP/transport implied");

                object Get(string name) => typeof(NgoMatchServer).GetField(name, Private).GetValue(server);
                void Set(string name, object value) => typeof(NgoMatchServer).GetField(name, Private).SetValue(server, value);
                object Call(string name, params object[] args) => typeof(NgoMatchServer).GetMethod(name, Private).Invoke(server, args);
                object AgentCall(string name, params object[] args) => typeof(AllocatedServerAgent).GetMethod(name, Private).Invoke(agent, args);
                void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
                object Pending(ulong client)
                {
                    object context = Activator.CreateInstance(pendingType, true);
                    pendingType.GetField("client").SetValue(context, client);
                    pendingType.GetField("generation").SetValue(context, (int)Get("allocationGeneration"));
                    pendingType.GetField("matchId").SetValue(context, server.AllocationId);
                    pendingType.GetField("requestId").SetValue(context, Guid.NewGuid().ToString("N"));
                    pendingType.GetField("deadline").SetValue(context, Time.realtimeSinceStartupAsDouble + 5);
                    pendingType.GetField("response").SetValue(context, new NetworkManager.ConnectionApprovalResponse { Pending = true });
                    pending.Add(client, context); manager.PendingClients[client] = null; return context;
                }
                NetworkManager.ConnectionApprovalResponse Response(object context) => (NetworkManager.ConnectionApprovalResponse)pendingType.GetField("response").GetValue(context);
                void Complete(object context, AllocatedAdmissionResult result) => Call("CompleteAdmission", context, result);
            }
            finally
            {
                // This fixture never opens sockets. Avoid deferred Destroy on the
                // injected, non-listening manager during an Editor batch check.
                typeof(NgoMatchServer).GetField("manager", Private).SetValue(server, null);
                UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(managerRoot);
            }
        }

        private static AllocatedMatchAssignment Assignment(string id) => new AllocatedMatchAssignment {
            matchId = id, mapId = "training", status = "preparing", expiresAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 60,
            players = new[] { new AllocatedPlayer { playerId = "alpha", seat = 0, faction = "industrial" },
                new AllocatedPlayer { playerId = "beta", seat = 1, faction = "biological" } }
        };
        private static AllocatedAdmissionResult Result(string id, int seat, string faction) => new AllocatedAdmissionResult { playerId = id, seat = seat, faction = faction };
    }
}
