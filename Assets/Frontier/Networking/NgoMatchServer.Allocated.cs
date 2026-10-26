using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Frontier.Core;
using Unity.Netcode;
using UnityEngine;

namespace Frontier.Networking
{
    public sealed partial class NgoMatchServer
    {
        private sealed class PendingAdmission
        {
            public ulong client;
            public int generation;
            public string matchId, requestId;
            public double deadline;
            public NetworkManager.ConnectionApprovalResponse response;
        }
        public bool AllocatedMode { get; private set; }
        public string AllocationId => allocation?.matchId;
        public bool AllocationCanRelease => allocation != null && (allocationAborted || finished)
            && ConnectedPlayers == 0 && pendingAdmissions.Count == 0;
        public event Action<AllocatedServerEvent> AllocationEvent;
        private AllocatedMatchAssignment allocation;
        private IAllocatedAdmission admission;
        private readonly Dictionary<ulong, PendingAdmission> pendingAdmissions = new Dictionary<ulong, PendingAdmission>();
        private int allocationGeneration;
        private bool allocationAborted, allocationStartRequested;
        private double allocationDeadline, allocationFinishedAt = -1;
        private double admissionTokens = 8, admissionTokenTime;

        public void ConfigureAllocatedMode(IAllocatedAdmission verifier)
        {
            if (manager != null || verifier == null) throw new InvalidOperationException("Configure allocated admission before starting the server.");
            AllocatedMode = true; admission = verifier;
        }

        public bool AssignMatch(AllocatedMatchAssignment value)
        {
            if (!AllocatedMode || value == null || allocation != null || Match != null || seats.Count != 0 || pendingAdmissions.Count != 0) return false;
            if (string.IsNullOrEmpty(value.matchId) || value.matchId.Length > 64 || value.players == null || value.players.Length != 2
                || value.players.Any(p => p == null || string.IsNullOrEmpty(p.playerId) || p.playerId.Length > 128
                    || p.seat < 0 || p.seat > 1 || p.faction != "industrial" && p.faction != "biological")
                || value.players[0].seat == value.players[1].seat || value.players[0].playerId == value.players[1].playerId) return false;
            var mapAsset = string.IsNullOrEmpty(value.mapId) ? null : Resources.Load<TextAsset>("Maps/" + value.mapId);
            double remaining = value.expiresAtUnix - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (mapAsset == null || remaining <= 0) return false;
            var nextMap = JsonUtility.FromJson<MapDefinition>(mapAsset.text);
            _ = new GridMap(nextMap);
            // Copy trusted assignment metadata; a backend adapter cannot mutate an active roster.
            allocation = new AllocatedMatchAssignment { matchId = value.matchId, mapId = value.mapId,
                status = value.status, expiresAtUnix = value.expiresAtUnix,
                players = value.players.Select(p => new AllocatedPlayer { playerId = p.playerId, seat = p.seat, faction = p.faction }).ToArray() };
            map = nextMap; mapId = value.mapId; matchId = value.matchId;
            allocationDeadline = Time.realtimeSinceStartupAsDouble + Math.Min(60, remaining);
            allocationAborted = allocationStartRequested = false; allocationFinishedAt = -1;
            InvalidateApprovals();
            EmitAllocationEvent("ready");
            Status("Allocated match ready · " + matchId);
            return true;
        }

        private void BeginAllocatedApproval(NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response, ConnectionHello hello)
        {
            if (allocation == null || allocationAborted || allocationStartRequested || Time.realtimeSinceStartupAsDouble >= allocationDeadline
                || hello.matchId != allocation.matchId || string.IsNullOrEmpty(hello.joinTicket) || hello.joinTicket.Length > 128)
            { response.Reason = "This connection has no active match assignment."; return; }
            double now = Time.realtimeSinceStartupAsDouble;
            admissionTokens = Math.Min(8, admissionTokens + Math.Max(0, now - admissionTokenTime) * 4); admissionTokenTime = now;
            if (pendingAdmissions.Count >= 8 || admissionTokens < 1 || pendingAdmissions.ContainsKey(request.ClientNetworkId))
            { response.Reason = "Admission is busy. Try again."; return; }
            admissionTokens--;
            var context = new PendingAdmission { client = request.ClientNetworkId, generation = allocationGeneration,
                matchId = allocation.matchId, requestId = Guid.NewGuid().ToString("N"), deadline = now + 5, response = response };
            pendingAdmissions.Add(context.client, context);
            response.Pending = true;
            StartCoroutine(ValidateAdmission(context, hello.joinTicket));
        }

        private IEnumerator ValidateAdmission(PendingAdmission context, string ticket)
        {
            yield return admission.Admit(context.matchId, ticket, context.requestId,
                result => CompleteAdmission(context, result), reason => DenyAdmission(context, "Could not validate this match admission."));
            // A broken adapter may finish without invoking either callback.
            DenyAdmission(context, "Match admission did not complete.");
        }

        private void CompleteAdmission(PendingAdmission context, AllocatedAdmissionResult result)
        {
            if (!AdmissionIsCurrent(context)) { DenyAdmission(context, "Match admission expired."); return; }
            var expected = result == null ? null : allocation.players.FirstOrDefault(p => p.playerId == result.playerId);
            if (expected == null || expected.seat != result.seat || expected.faction != result.faction
                || seats.Values.Any(s => s.player == result.seat || s.playerId == result.playerId))
            { DenyAdmission(context, "The assigned player already joined or admission is invalid."); return; }
            seats.Add(context.client, new Seat { client = context.client, player = expected.seat, faction = expected.faction,
                playerId = expected.playerId, connectedAt = clock.Elapsed.TotalSeconds });
            pendingAdmissions.Remove(context.client);
            context.response.Approved = true; context.response.CreatePlayerObject = false;
            context.response.Pending = false;
        }

        private bool AdmissionIsCurrent(PendingAdmission context) => !closing && manager != null && allocation != null
            && !allocationAborted && !allocationStartRequested && Match == null && allocationGeneration == context.generation
            && allocation.matchId == context.matchId && Time.realtimeSinceStartupAsDouble < context.deadline
            && manager.PendingClients.ContainsKey(context.client)
            && pendingAdmissions.TryGetValue(context.client, out var current) && ReferenceEquals(current, context);

        private void DenyAdmission(PendingAdmission context, string reason)
        {
            if (!pendingAdmissions.TryGetValue(context.client, out var current) || !ReferenceEquals(current, context)) return;
            pendingAdmissions.Remove(context.client);
            context.response.Reason = reason; context.response.Approved = false; context.response.Pending = false;
        }

        private void CancelPendingApproval(ulong client, string reason)
        { if (pendingAdmissions.TryGetValue(client, out var context)) DenyAdmission(context, reason); }

        private void InvalidateApprovals()
        {
            allocationGeneration++;
            foreach (var context in pendingAdmissions.Values.ToArray()) DenyAdmission(context, "Match admission was cancelled.");
        }

        private void RequestAllocatedStart()
        {
            if (allocation == null || allocationAborted || allocationStartRequested) return;
            allocationStartRequested = true;
            InvalidateApprovals();
            // Commit the handoff before sending Welcome. A concurrent backend
            // cancellation cannot leave a locally running but cancelled match.
            EmitAllocationEvent("started");
        }

        public void ConfirmAllocatedStart(string id)
        {
            if (allocation?.matchId != id || allocationAborted || !allocationStartRequested || Match != null
                || seats.Count != 2 || seats.Values.Any(s => !s.connected || !s.ready)) return;
            try { BeginMatch(); }
            catch (Exception error) { Debug.LogException(error); AbortAllocation("The allocated match could not start."); }
        }

        public void AbortAllocation(string reason)
        {
            if (allocation == null || allocationAborted || finished) return;
            allocationAborted = true; allocationFinishedAt = Time.realtimeSinceStartupAsDouble;
            InvalidateApprovals();
            EmitAllocationEvent("aborted", -1, Match?.Tick ?? 0, reason);
            CloseAllocationConnections(reason);
            Status("Allocated match aborted · " + allocation.matchId);
        }

        public bool DiscardAllocation(string id)
        {
            if (!AllocatedMode || allocation != null || Match != null || seats.Count != 0 || pendingAdmissions.Count != 0
                || string.IsNullOrEmpty(id) || id.Length > 64) return false;
            // Cancellation/expiry can happen before this process's first poll.
            // Adopt only the terminal identity so its ACK/release follows the same
            // ordered path, without loading a map, announcing readiness or admitting anyone.
            allocation = new AllocatedMatchAssignment { matchId = id, status = "aborted", players = Array.Empty<AllocatedPlayer>() };
            allocationAborted = true; allocationStartRequested = false;
            allocationFinishedAt = Time.realtimeSinceStartupAsDouble;
            InvalidateApprovals();
            EmitAllocationEvent("aborted", -1, 0, "The allocation ended before the server received it.");
            return true;
        }

        public void CloseAllocationConnections(string reason = "Match finished.")
        {
            InvalidateApprovals();
            if (manager == null) return;
            foreach (var seat in seats.Values.ToArray())
                if (seat.connected) manager.DisconnectClient(seat.client, reason);
        }

        public bool ReleaseAllocation(string id)
        {
            if (allocation?.matchId != id || !AllocationCanRelease) return false;
            allocation = null; allocationAborted = allocationStartRequested = false;
            allocationFinishedAt = -1;
            ResetMatch(); Status("Waiting for a match assignment.");
            return true;
        }

        private void AdvanceAllocation()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            foreach (var context in pendingAdmissions.Values.ToArray())
                if (now >= context.deadline || manager == null || !manager.PendingClients.ContainsKey(context.client))
                    DenyAdmission(context, "Match admission timed out.");
            if (!AllocatedMode || allocation == null) return;
            if (Match == null && !allocationAborted && now >= allocationDeadline)
                AbortAllocation("The opponent did not join in time. Search again.");
            if (allocationFinishedAt >= 0 && now - allocationFinishedAt >= 30)
                CloseAllocationConnections("Match finished. Return to matchmaking to play again.");
        }

        private void EmitAllocationEvent(string name, int winner = -1, int tick = 0, string reason = "")
        {
            if (!AllocatedMode || allocation == null) return;
            if (name == "finished") allocationFinishedAt = Time.realtimeSinceStartupAsDouble;
            MatchProtocol.Emit(AllocationEvent, new AllocatedServerEvent { matchId = allocation.matchId,
                @event = name, winner = winner, tick = tick, reason = reason });
        }
    }
}
