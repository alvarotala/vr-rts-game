using System;
using System.Collections;
using System.Collections.Generic;
using Frontier.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Frontier.Networking
{
    // Private control plane only. Player clients never receive the runtime HTTP key.
    public sealed class AllocatedServerAgent : MonoBehaviour, IAllocatedAdmission
    {
        [Serializable] private class LeaseRequest { public string serverId, bootId, leaseId; }
        [Serializable] private sealed class Registration
        { public string serverId, bootId, host, contentHash; public int port, wsPort, protocol, rules; }
        [Serializable] private sealed class LeaseResult { public string leaseId; }
        [Serializable] private sealed class PollResult { public AllocatedMatchAssignment assignment; }
        [Serializable] private sealed class AdmissionRequest : LeaseRequest { public string matchId, joinTicket, requestId; }
        [Serializable] private sealed class EventRequest : LeaseRequest { public string matchId, @event, reason; public int winner, tick; }
        [Serializable] private sealed class OkResult { public bool ok; }
        private NgoMatchServer server;
        private string backendUrl, key, serverId, bootId, advertisedHost, leaseId;
        private ushort port;
        private int advertisedWebSocketPort;
        private bool stopped, terminalAcknowledged;
        private double lastPollSuccess, lastWarning;
        private readonly List<AllocatedServerEvent> events = new List<AllocatedServerEvent>();

        public static AllocatedServerAgent Launch(NgoMatchServer server, ushort port)
        {
            string url = Environment.GetEnvironmentVariable("FRONTIER_BACKEND_URL");
            string key = Environment.GetEnvironmentVariable("FRONTIER_SERVER_KEY");
            string id = Environment.GetEnvironmentVariable("FRONTIER_SERVER_ID");
            string host = Environment.GetEnvironmentVariable("FRONTIER_ADVERTISE_HOST");
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http")
                || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
                || string.IsNullOrWhiteSpace(key) || key.Length < 16 || string.IsNullOrWhiteSpace(id) || id.Length > 64
                || string.IsNullOrWhiteSpace(host) || host.Length > 255 || port == 0)
                throw new InvalidOperationException("Allocated mode requires FRONTIER_BACKEND_URL, FRONTIER_SERVER_KEY (16+ characters), FRONTIER_SERVER_ID and FRONTIER_ADVERTISE_HOST.");
            var value = server.gameObject.AddComponent<AllocatedServerAgent>();
            value.server = server; value.backendUrl = url.TrimEnd('/'); value.key = key;
            value.serverId = id; value.advertisedHost = host; value.port = port;
            // Browsers reach the websocket listener directly or through a TLS proxy port (FRONTIER_ADVERTISE_WS_PORT).
            string wsPort = Environment.GetEnvironmentVariable("FRONTIER_ADVERTISE_WS_PORT");
            value.advertisedWebSocketPort = -1;
            if (!string.IsNullOrWhiteSpace(wsPort))
            {
                if (!ushort.TryParse(wsPort, out ushort advertised)) throw new InvalidOperationException("FRONTIER_ADVERTISE_WS_PORT must be 0..65535.");
                value.advertisedWebSocketPort = advertised;
            }
            value.bootId = Guid.NewGuid().ToString("N");
            server.ConfigureAllocatedMode(value);
            server.AllocationEvent += value.Enqueue;
            return value;
        }

        private IEnumerator Start()
        {
            if (server == null) yield break;
            while (!stopped && !server.IsRunning) yield return null;
            if (advertisedWebSocketPort < 0) advertisedWebSocketPort = server.WebSocketPort;
            while (!stopped && server.IsRunning && string.IsNullOrEmpty(leaseId))
            {
                yield return Rpc<Registration, LeaseResult>("ll_server_register", new Registration {
                    serverId = serverId, bootId = bootId, host = advertisedHost, port = port, wsPort = advertisedWebSocketPort,
                    protocol = MatchProtocol.Version, rules = Simulation.ReplayRulesVersion, contentHash = MatchProtocol.ContentHash()
                }, response => { if (!string.IsNullOrEmpty(response.leaseId)) leaseId = response.leaseId; }, Warn);
                if (string.IsNullOrEmpty(leaseId)) yield return new WaitForSecondsRealtime(2);
            }
            if (stopped || string.IsNullOrEmpty(leaseId)) yield break;
            lastPollSuccess = Time.realtimeSinceStartupAsDouble;
            Debug.Log("FRONTIER_ALLOCATION Registered dedicated server.");
            while (!stopped && server.IsRunning)
            {
                yield return Rpc<LeaseRequest, PollResult>("ll_server_poll", Lease(), response =>
                {
                    lastPollSuccess = Time.realtimeSinceStartupAsDouble;
                    HandleAssignment(response.assignment);
                }, Warn);
                if (stopped) yield break;
                if (Time.realtimeSinceStartupAsDouble - lastPollSuccess > 15)
                {
                    // The backend expires leases at 20 seconds. Fail closed before
                    // another process could receive the same physical slot.
                    Debug.LogError("FRONTIER_ALLOCATION Lost the server lease; stopping allocated server.");
                    server.AbortAllocation("The matchmaking service became unavailable.");
                    server.StopServer(); Application.Quit(1); yield break;
                }
                if (events.Count > 0)
                {
                    var pending = events[0];
                    yield return Rpc<EventRequest, OkResult>("ll_server_event", Event(pending), response =>
                    {
                        if (response.ok) AcknowledgeEvent(pending);
                    }, Warn);
                }
                if (terminalAcknowledged && server.AllocationCanRelease)
                    Enqueue(new AllocatedServerEvent { matchId = server.AllocationId, @event = "released" });
                yield return new WaitForSecondsRealtime(2);
            }
        }

        private void HandleAssignment(AllocatedMatchAssignment assignment)
        {
            // Release can commit at the backend before its HTTP response arrives.
            // Polling may then return no assignment or the next pair. Keep retrying
            // the idempotent release until its ACK clears the old local allocation.
            if (terminalAcknowledged && server.AllocationCanRelease
                && events.Exists(e => e.matchId == server.AllocationId && e.@event == "released")) return;
            // JsonUtility materializes an omitted/null object as an empty DTO on
            // some Unity versions; identity determines assignment availability.
            if (assignment == null || string.IsNullOrEmpty(assignment.matchId))
            {
                if (server.AllocationId != null) server.AbortAllocation("The match assignment was withdrawn.");
                return;
            }
            if (server.AllocationId == null)
            {
                if (assignment.status == "aborted" || assignment.status == "finished")
                {
                    if (!server.DiscardAllocation(assignment.matchId))
                    { Warn("Could not acknowledge a terminal match assignment."); stopped = true; server.StopServer(); Application.Quit(1); }
                    return;
                }
                if (assignment.status != "preparing" && assignment.status != "ready")
                { Warn("Received an allocation in an invalid initial state."); return; }
                if (!server.AssignMatch(assignment))
                {
                    // A malformed/expired assignment must not occupy the slot forever.
                    Warn("Rejected invalid or expired match assignment.");
                    stopped = true; server.StopServer(); Application.Quit(1);
                }
                return;
            }
            if (assignment.matchId != server.AllocationId)
            {
                Warn("The server lease changed its active match unexpectedly.");
                stopped = true; server.StopServer(); Application.Quit(1); return;
            }
            if (assignment.status == "aborted")
            {
                // A canceled assignment invalidates queued ready/started events.
                events.RemoveAll(e => e.@event == "ready" || e.@event == "started");
                server.AbortAllocation("The match was cancelled. Search again.");
            }
        }

        private void AcknowledgeEvent(AllocatedServerEvent pending)
        {
            // The item may have been invalidated by cancellation while
            // this HTTP request was in flight.
            events.Remove(pending);
            if (pending.matchId != server.AllocationId) return;
            if (pending.@event == "started") server.ConfirmAllocatedStart(pending.matchId);
            if (pending.@event == "finished" || pending.@event == "aborted") terminalAcknowledged = true;
            if (pending.@event == "released" && server.ReleaseAllocation(pending.matchId))
            { terminalAcknowledged = false; events.Clear(); }
        }

        public IEnumerator Admit(string matchId, string joinTicket, string requestId,
            Action<AllocatedAdmissionResult> success, Action<string> failure)
        {
            if (stopped || string.IsNullOrEmpty(leaseId)) { failure("Server is not registered."); yield break; }
            // A stable requestId makes a response retry idempotent at the backend.
            // Four-second HTTP timeout leaves room inside NGO's five-second guard.
            yield return Rpc<AdmissionRequest, AllocatedAdmissionResult>("ll_server_admit", new AdmissionRequest {
                serverId = serverId, bootId = bootId, leaseId = leaseId, matchId = matchId,
                joinTicket = joinTicket, requestId = requestId
            }, success, failure);
        }

        private void Enqueue(AllocatedServerEvent value)
        {
            if (value == null || events.Exists(e => e.matchId == value.matchId && e.@event == value.@event)) return;
            if (value.@event == "aborted") events.RemoveAll(e => e.matchId == value.matchId && (e.@event == "ready" || e.@event == "started"));
            events.Add(value);
        }
        private LeaseRequest Lease() => new LeaseRequest { serverId = serverId, bootId = bootId, leaseId = leaseId };
        private EventRequest Event(AllocatedServerEvent value) => new EventRequest { serverId = serverId, bootId = bootId,
            leaseId = leaseId, matchId = value.matchId, @event = value.@event, winner = value.winner, tick = value.tick, reason = value.reason };

        private IEnumerator Rpc<TRequest, TResponse>(string name, TRequest payload, Action<TResponse> success, Action<string> failure)
            where TResponse : class
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
            // The Nakama runtime key is never included in logs or player-facing errors.
            string endpoint = backendUrl + "/v2/rpc/" + name + "?http_key=" + Uri.EscapeDataString(key) + "&unwrap";
            using (var request = new UnityWebRequest(endpoint, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(bytes); request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json"); request.timeout = 4;
                yield return request.SendWebRequest();
                if (stopped) yield break;
                if (request.result != UnityWebRequest.Result.Success || request.downloadHandler.data.Length > 65536)
                { failure("Backend RPC failed (HTTP " + request.responseCode + ")."); yield break; }
                TResponse result = null;
                try { result = JsonUtility.FromJson<TResponse>(request.downloadHandler.text); }
                catch (ArgumentException) { }
                if (result == null) failure("Backend returned an invalid response.");
                else success(result);
            }
        }

        private void Warn(string message)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - lastWarning < 10) return;
            lastWarning = now; Debug.LogWarning("FRONTIER_ALLOCATION " + message);
        }
        private void Update()
        {
            if (stopped || server == null || !server.IsRunning || string.IsNullOrEmpty(leaseId)
                || Time.realtimeSinceStartupAsDouble - lastPollSuccess <= 15) return;
            stopped = true;
            Debug.LogError("FRONTIER_ALLOCATION Lost the server lease; stopping allocated server.");
            server.AbortAllocation("The matchmaking service became unavailable.");
            server.StopServer(); Application.Quit(1);
        }
        private void OnDestroy()
        {
            stopped = true;
            if (server != null) server.AllocationEvent -= Enqueue;
        }
    }
}
