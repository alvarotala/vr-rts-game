using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Frontier.Core;
using Unity.Netcode;
using UnityEngine;

namespace Frontier.Networking
{
    // One dedicated process owns one match at a time. No Unity transforms, physics, focus or local pause affect its rules.
    // Seats are transport-neutral: NGO (Quest and Unity desktop) and websocket (browser) players share one match.
    public sealed partial class NgoMatchServer : MonoBehaviour
    {
        private sealed class Seat
        {
            public ulong client;
            public int player;
            public string faction, playerId;
            public bool connected, ready, resultAcknowledged;
            public double connectedAt, tokenTime;
            public double tokens = 80;
            public double requestTokens = 120, requestTime;
            public readonly CommandReceipts receipts = new CommandReceipts();
            public byte[] result;
            public int resultRawLength, resultNextChunk;
        }

        public event Action<string> StatusChanged;
        public Simulation Match { get; private set; }
        public bool IsRunning => manager != null && manager.IsListening;
        public int ConnectedPlayers => seats.Values.Count(s => s.connected);
        // Validation harness only. A normal server always uses the default logical 20 Hz rate.
        public float SimulationSpeed = 1;
        // Opt-in diagnostics; one bounded summary every five active-match seconds.
        public bool EnableNetworkMetrics;
        public int AcceptedCommands { get; private set; }
        public int RejectedCommands { get; private set; }
        private readonly Dictionary<ulong, Seat> seats = new Dictionary<ulong, Seat>();
        private readonly Stopwatch clock = new Stopwatch();
        private NetworkManager manager;
        private Catalog catalog;
        private MapDefinition map;
        private string mapId, matchId;
        private double lastTime, accumulator, nextSnapshot, startServerTime;
        private long frame;
        private bool closing, finished;
        private ServerMetrics metrics;

        public bool StartServer(ushort port = 7777, string mapId = "rift", string listenAddress = "0.0.0.0", ushort webSocketPort = 0)
        {
            if (manager != null || port == 0) return false;
            try
            {
                var catalogAsset = Resources.Load<TextAsset>("catalog");
                var mapAsset = Resources.Load<TextAsset>("Maps/" + mapId);
                if (catalogAsset == null || mapAsset == null) throw new ArgumentException("Requested multiplayer map or catalog is missing.");
                catalog = new Catalog(JsonUtility.FromJson<CatalogDefinition>(catalogAsset.text));
                map = JsonUtility.FromJson<MapDefinition>(mapAsset.text); _ = new GridMap(map);
                this.mapId = mapId;
                _ = MatchProtocol.ContentHash();
                closing = false; ResetMatch();
                manager = MatchProtocol.CreateManager("Frontier dedicated network", "127.0.0.1", port, listenAddress);
                manager.ConnectionApprovalCallback = Approve;
                manager.OnClientConnectedCallback += Connected;
                manager.OnClientDisconnectCallback += Disconnected;
                if (!manager.StartServer()) throw new InvalidOperationException("Could not listen on the multiplayer port.");
                manager.CustomMessagingManager.RegisterNamedMessageHandler(MatchProtocol.Ready, (client, reader) => ReceiveNamed(client, reader, Ready));
                manager.CustomMessagingManager.RegisterNamedMessageHandler(MatchProtocol.Command, (client, reader) => ReceiveNamed(client, reader, Command));
                manager.CustomMessagingManager.RegisterNamedMessageHandler(MatchProtocol.ResultAck, (client, reader) => ReceiveNamed(client, reader, ResultAcknowledged));
                if (webSocketPort != 0) StartWebSockets(webSocketPort, listenAddress);
                Application.runInBackground = true;
                Status("Listening on " + listenAddress + ":" + port + (webSocketPort != 0 ? " · websocket tcp " + WebSocketPort : "") + " · " + mapId);
                return true;
            }
            catch (Exception error)
            {
                Status(error.Message); StopServer(); return false;
            }
        }

        // NGO delivers named messages through readers; browser frames arrive as bytes.
        // Both transports validate the same payload bytes in the shared handlers. An
        // unreadable payload arrives as null, so each handler keeps its original verdict.
        private void ReceiveNamed(ulong client, FastBufferReader reader, Action<ulong, byte[]> handler)
        {
            byte[] payload;
            try { payload = MatchProtocol.Read(reader, MatchProtocol.MaxCommandBytes); }
            catch { payload = null; }
            handler(client, payload);
        }

        private string ValidateHello(byte[] payload, out ConnectionHello hello)
        {
            hello = null;
            if (Match != null || seats.Count >= 2) return "The match already has two players.";
            if (payload == null || payload.Length == 0 || payload.Length > MatchProtocol.MaxHelloBytes) return "Invalid connection request.";
            try { hello = MatchProtocol.Decode<ConnectionHello>(payload); }
            catch { return "Invalid connection request."; }
            if (hello == null || hello.protocol != MatchProtocol.Version || hello.rules != Simulation.ReplayRulesVersion || hello.contentHash != MatchProtocol.ContentHash())
                return "Incompatible game version, rules, maps or catalog.";
            if (hello.faction != "industrial" && hello.faction != "biological") return "Unknown faction.";
            return null;
        }

        private void AddDirectSeat(ulong client, string faction)
        {
            int player = seats.Values.Any(s => s.player == 0) ? 1 : 0;
            seats.Add(client, new Seat { client = client, player = player, faction = faction, connectedAt = clock.Elapsed.TotalSeconds });
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = false; response.CreatePlayerObject = false; response.Pending = false;
            try
            {
                string reason = ValidateHello(request.Payload, out var hello);
                if (reason != null) { response.Reason = reason; return; }
                if (AllocatedMode)
                {
                    // Pending first: the backend verdict can complete synchronously inside the call.
                    response.Pending = true;
                    if (!BeginAllocatedAdmission(request.ClientNetworkId, hello, response, null, out reason)) { response.Pending = false; response.Reason = reason; }
                    return;
                }
                AddDirectSeat(request.ClientNetworkId, hello.faction);
                response.Approved = true;
            }
            catch { response.Reason = "Invalid connection request."; }
        }

        private void Connected(ulong client)
        {
            if (!seats.TryGetValue(client, out var seat)) { Kick(client, "Unapproved player."); return; }
            if (AllocatedMode && (allocation == null || allocationAborted))
            {
                // NGO may process an approval one frame after the backend cancels
                // the assignment. Do not attach that late transport to the lobby.
                seats.Remove(client); Kick(client, "The match assignment was cancelled."); return;
            }
            seat.connected = true; seat.connectedAt = clock.Elapsed.TotalSeconds;
            // NGO clients learn this from their connection callback; browsers from this signal.
            if (IsWebSocketClient(client)) gateway?.Send(client, WebSocketMatchCodec.Envelope(WebSocketMatchCodec.Admitted, MatchProtocol.Signal));
            SendStatus(client, "Connected. Waiting for both players to be ready.");
            Status("Player " + (seat.player + 1) + " connected (" + ConnectedPlayers + "/2).");
        }

        private void Ready(ulong client, byte[] payload)
        {
            if (!seats.TryGetValue(client, out var seat) || !seat.connected || Match != null) return;
            if (payload == null || payload.Length != 1 || payload[0] != 1)
            {
                Kick(client, "Malformed readiness signal.");
                return;
            }
            try
            {
                seat.ready = true;
                if (seats.Count != 2 || seats.Values.Any(s => !s.ready || !s.connected)) return;
                if (AllocatedMode) { RequestAllocatedStart(); return; }
                BeginMatch();
            }
            catch (Exception error) { Status("Could not start match: " + error.Message); StopServer(); }
        }

        private void BeginMatch()
        {
                var ordered = seats.Values.OrderBy(s => s.player).ToArray();
                Match = new Simulation(map, catalog, ordered[0].faction, ordered[1].faction);
                Match.Paused = true;
                startServerTime = manager.ServerTime.Time + MatchProtocol.StartCountdownSeconds;
                lastTime = clock.Elapsed.TotalSeconds; accumulator = 0; nextSnapshot = lastTime;
                foreach (var player in ordered)
                {
                    var welcome = new MatchWelcome { protocol = MatchProtocol.Version, matchId = matchId, mapId = mapId,
                        player = player.player, faction0 = ordered[0].faction, faction1 = ordered[1].faction, startServerTime = startServerTime,
                        countdownSeconds = MatchProtocol.StartCountdownSeconds };
                    SendTo(player.client, MatchProtocol.Welcome, MatchProtocol.Encode(welcome, 1024));
                }
                Status("Match started · " + matchId);
                PublishSnapshots();
                nextSnapshot = NextSnapshotTime(nextSnapshot, clock.Elapsed.TotalSeconds);
        }

        private void Command(ulong client, byte[] payload)
        {
            if (!seats.TryGetValue(client, out var seat) || !seat.ready || Match == null) return;
            double arrival = clock.Elapsed.TotalSeconds;
            seat.requestTokens = Math.Min(120, seat.requestTokens + Math.Max(0, arrival - seat.requestTime) * 60); seat.requestTime = arrival;
            if (seat.requestTokens < 1) { Kick(client, "Command traffic limit exceeded."); return; }
            seat.requestTokens -= 1;
            GameCommand command;
            try
            {
                if (payload == null || payload.Length == 0 || payload.Length > MatchProtocol.MaxCommandBytes) throw new InvalidOperationException("Invalid network message length.");
                command = MatchProtocol.Decode<GameCommand>(payload);
            }
            catch { Kick(client, "Malformed command."); return; }
            if (command == null || command.sequence <= 0) { Kick(client, "Commands require a positive sequence."); return; }
            if (seat.receipts.TryGet(command.sequence, out var previous)) { SendAck(seat, previous); return; }
            if (seat.receipts.WasProcessed(command.sequence))
            { SendAck(seat, new CommandAck { sequence = command.sequence, tick = Match.Tick, message = "This command sequence has already been processed." }); return; }
            double now = clock.Elapsed.TotalSeconds;
            seat.tokens = Math.Min(80, seat.tokens + Math.Max(0, now - seat.tokenTime) * 40); seat.tokenTime = now;
            string invalid = CommandBoundary.Validate(command);
            if (seat.tokens < 1) invalid = "Too many commands. Please slow down.";
            else seat.tokens -= 1;
            CommandResult result;
            if (AwaitingStart(manager.ServerTime.Time)) result = new CommandResult(false, "The match is starting.");
            else if (invalid != null) result = new CommandResult(false, invalid);
            else
            {
                CommandBoundary.AssignAuthority(command, seat.player);
                try { result = Match.Submit(command); }
                catch (Exception error)
                {
                    UnityEngine.Debug.LogException(error); result = new CommandResult(false, "The server could not process this command.");
                }
            }
            var ack = new CommandAck { sequence = command.sequence, tick = Match.Tick, accepted = result.accepted, message = result.message ?? "" };
            if (result.accepted) AcceptedCommands++; else RejectedCommands++;
            seat.receipts.Record(ack);
            SendAck(seat, ack);
            if (Match.Winner != -1) Finish();
        }

        private void SendAck(Seat seat, CommandAck acknowledgement)
        {
            if (seat.connected) SendTo(seat.client, MatchProtocol.Ack, MatchProtocol.Encode(acknowledgement, 2048));
        }

        private void Update()
        {
            if (!IsRunning || closing) return;
            PollWebSockets();
            if (closing) return;
            AdvanceAllocation();
            if (allocationAborted) return;
            double now = clock.Elapsed.TotalSeconds;
            foreach (var seat in seats.Values.ToArray())
                if (!seat.ready && now - seat.connectedAt > 15)
                {
                    Kick(seat.client, "Timed out waiting for player readiness.");
                    if (Match == null) seats.Remove(seat.client);
                }
            if (Match == null) { lastTime = now; return; }
            if (AwaitingStart(manager.ServerTime.Time))
            {
                accumulator = 0; lastTime = now;
                if (now >= nextSnapshot) { PublishSnapshots(); nextSnapshot = NextSnapshotTime(nextSnapshot, clock.Elapsed.TotalSeconds); }
                return;
            }
            if (Match.Winner == -1)
            {
                metrics?.BeginFrame(now, Match.Tick);
                accumulator += Math.Min(.25, Math.Max(0, now - lastTime)) * Math.Max(1, Math.Min(20, SimulationSpeed));
                lastTime = now;
                int steps = 0;
                while (accumulator >= 1d / Simulation.TickRate && steps++ < 128 && Match.Winner == -1)
                {
                    long started = metrics == null ? 0 : Stopwatch.GetTimestamp();
                    Match.Step();
                    if (metrics != null) metrics.StepMilliseconds.Add(ElapsedMilliseconds(started));
                    accumulator -= 1d / Simulation.TickRate;
                }
                if (now >= nextSnapshot) { PublishSnapshots(); nextSnapshot = NextSnapshotTime(nextSnapshot, clock.Elapsed.TotalSeconds); }
                metrics?.ReportIfDue(clock.Elapsed.TotalSeconds, Match.Tick, matchId);
            }
            if (Match.Winner != -1) Finish();
            foreach (var seat in seats.Values)
            {
                if (!seat.connected || seat.result == null || seat.resultAcknowledged) continue;
                int total = NetworkWire.ChunkCount(seat.result.Length);
                for (int sent = 0; sent < 8 && seat.resultNextChunk < total; sent++, seat.resultNextChunk++)
                    SendTo(seat.client, MatchProtocol.Result, NetworkWire.Chunk(1, seat.result, seat.resultRawLength, seat.resultNextChunk));
            }
        }

        private static double NextSnapshotTime(double deadline, double now)
        {
            const double interval = .1;
            // Keep the original cadence, skipping periods missed during a stall or publication.
            double periods = Math.Floor(Math.Max(0, now - deadline) / interval) + 1;
            double next = deadline + periods * interval;
            // Treat a floating-point remainder at the current boundary as already elapsed.
            return next > now + 1e-9 ? next : next + interval;
        }

        private void PublishSnapshots()
        {
            if (Match == null) return;
            // Initial/countdown frames are excluded from active-match rates.
            ServerMetrics timing = metrics != null && metrics.Started ? metrics : null;
            long started = timing == null ? 0 : Stopwatch.GetTimestamp();
            timing?.BeginSnapshot(clock.Elapsed.TotalSeconds);
            long id = ++frame;
            foreach (var seat in seats.Values)
            {
                if (!seat.connected) continue;
                byte[] raw = MatchProtocol.Encode(Match.View(seat.player), NetworkWire.MaxSnapshotBytes);
                byte[] packed = NetworkWire.Compress(raw, NetworkWire.MaxSnapshotBytes, NetworkWire.MaxSnapshotCompressed);
                int count = NetworkWire.ChunkCount(packed.Length);
                timing?.RecordSeat(seat.player, raw.Length, packed.Length, count);
                for (int index = 0; index < count; index++)
                    // FrameAssembler sequences whole snapshots; the transport must preserve reordered chunks.
                    SendTo(seat.client, MatchProtocol.Snapshot, NetworkWire.Chunk(id, packed, raw.Length, index), NetworkDelivery.Unreliable);
            }
            if (timing != null) timing.SnapshotMilliseconds.Add(ElapsedMilliseconds(started));
        }

        private bool AwaitingStart(double serverTime)
        {
            bool waiting = Match.Winner == -1 && serverTime < startServerTime;
            Match.Paused = waiting;
            return waiting;
        }

        private void Finish()
        {
            if (finished || Match == null || Match.Winner == -1) return;
            byte[] report = null;
            try { report = MatchProtocol.Encode(MatchReportData.From(Match.Report), NetworkWire.MaxResultBytes); }
            catch (Exception error) when (error is InvalidOperationException || error is InvalidDataException)
            { UnityEngine.Debug.LogWarning("Final report unavailable: " + error.Message); }
            foreach (var seat in seats.Values)
            {
                if (!seat.connected) continue;
                byte[] view = MatchProtocol.Encode(Match.View(seat.player), NetworkWire.MaxSnapshotBytes);
                seat.result = NetworkWire.PackResult(view, report, out seat.resultRawLength, out bool unavailable);
                seat.resultNextChunk = 0;
                if (unavailable && report != null) UnityEngine.Debug.LogWarning("Final report exceeds wire limits; delivering the match outcome without statistics.");
            }
            finished = true;
            EmitAllocationEvent("finished", Match.Winner, Match.Tick);
            Status("Match finished · winner=" + Match.Winner + " tick=" + Match.Tick + " accepted=" + AcceptedCommands + " rejected=" + RejectedCommands);
        }

        private void ResultAcknowledged(ulong client, byte[] payload)
        {
            if (!finished || !seats.TryGetValue(client, out var seat)) return;
            if (payload != null && payload.Length == 1 && payload[0] == 1) { seat.resultAcknowledged = true; seat.result = null; }
        }

        private void Disconnected(ulong client)
        {
            string reason = manager == null ? "" : manager.DisconnectReason ?? "";
            HandleDisconnect(client, manager == null ? "Unavailable" : manager.DisconnectEvent.ToString(), reason);
        }

        private void HandleDisconnect(ulong client, string transport, string reason)
        {
            CancelPendingApproval(client, "Connection closed during admission.");
            if (closing || !seats.TryGetValue(client, out var seat)) return;
            seat.connected = false;
            reason = (reason ?? "").Replace('\r', ' ').Replace('\n', ' ');
            if (reason.Length > 512) reason = reason.Substring(0, 512);
            Status("Player " + (seat.player + 1) + " disconnected · transport=" + transport
                + " · reason=" + reason + " · match=" + matchId + " · tick=" + (Match?.Tick ?? 0) + " · remaining=" + ConnectedPlayers);
            if (Match == null)
            {
                seats.Remove(client);
                if (AllocatedMode) AbortAllocation("A player left before the match started.");
                else Status("Waiting for two players.");
                return;
            }
            if (allocationAborted) return;
            if (Match.Winner == -1)
            {
                // Reconnection is deliberately not enabled in this first direct-connect protocol.
                Match.Paused = false;
                Match.Submit(new GameCommand { player = seat.player, kind = CommandKind.Surrender });
                Finish();
            }
            // Preserve the terminal state and reliable result for any remaining
            // participant. Reuse the listening server only after everyone leaves.
            if (!AllocatedMode && Match.Winner != -1 && ConnectedPlayers == 0)
            {
                ResetMatch();
                Status("Waiting for two players.");
            }
        }

        private void ResetMatch()
        {
            InvalidateApprovals();
            Match = null; seats.Clear(); finished = false;
            matchId = Guid.NewGuid().ToString("N");
            frame = 0; accumulator = lastTime = nextSnapshot = startServerTime = 0;
            AcceptedCommands = RejectedCommands = 0;
            metrics = EnableNetworkMetrics ? new ServerMetrics() : null;
            clock.Restart();
        }

        // One outbound path for both transports. Browser frames carry the same
        // payload bytes as the NGO named messages, prefixed by their kind.
        private void SendTo(ulong client, string name, byte[] bytes, NetworkDelivery delivery = NetworkDelivery.ReliableFragmentedSequenced)
        {
            if (IsWebSocketClient(client)) { gateway?.Send(client, WebSocketMatchCodec.Envelope(WebSocketKind(name), bytes)); return; }
            if (manager != null) MatchProtocol.Send(manager, name, client, bytes, delivery);
        }

        private void Kick(ulong client, string reason)
        {
            if (IsWebSocketClient(client)) { gateway?.Close(client, reason); return; }
            manager?.DisconnectClient(client, reason);
        }

        private static double ElapsedMilliseconds(long started) => (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;

        // Ring buffers retain at most 256 observations per metric. Mean/max cover
        // the whole five-second window; p95 uses the most recent bounded samples.
        private sealed class Samples
        {
            private readonly double[] values = new double[256], sorted = new double[256];
            private int retained, next;
            public int Count { get; private set; }
            public double Total { get; private set; }
            public double Maximum { get; private set; }
            public double Mean => Count == 0 ? 0 : Total / Count;
            public void Add(double value)
            {
                values[next] = value; next = (next + 1) % values.Length;
                retained = Math.Min(retained + 1, values.Length);
                Count++; Total += value; Maximum = Math.Max(Maximum, value);
            }
            public double P95()
            {
                if (retained == 0) return 0;
                Array.Copy(values, sorted, retained); Array.Sort(sorted, 0, retained);
                return sorted[(int)((retained - 1) * .95)];
            }
            public void Clear() { retained = next = Count = 0; Total = Maximum = 0; }
        }

        private sealed class ServerMetrics
        {
            public bool Started { get; private set; }
            public readonly Samples StepMilliseconds = new Samples(), SnapshotMilliseconds = new Samples();
            private readonly Samples updateGaps = new Samples(), snapshotGaps = new Samples();
            private readonly long[] rawBytes = new long[2], packedBytes = new long[2], chunks = new long[2];
            private readonly int[] seatSnapshots = new int[2];
            private double windowStart, lastUpdate, lastSnapshot;
            private int windowTick, snapshots;

            public void BeginFrame(double now, int tick)
            {
                if (!Started) { Started = true; windowStart = lastUpdate = now; windowTick = tick; return; }
                updateGaps.Add(Math.Max(0, now - lastUpdate) * 1000); lastUpdate = now;
            }
            public void BeginSnapshot(double now)
            {
                if (lastSnapshot > 0) snapshotGaps.Add(Math.Max(0, now - lastSnapshot) * 1000);
                lastSnapshot = now; snapshots++;
            }
            public void RecordSeat(int player, int raw, int packed, int count)
            { seatSnapshots[player]++; rawBytes[player] += raw; packedBytes[player] += packed; chunks[player] += count; }
            public void ReportIfDue(double now, int tick, string matchId)
            {
                double seconds = now - windowStart;
                if (seconds < 5) return;
                string message = FormattableString.Invariant($"FRONTIER_SERVER_METRICS match={matchId} tick={tick} seconds={seconds:F2} tickHz={(tick - windowTick) / seconds:F2} snapshotHz={snapshots / seconds:F2} ")
                    + FormattableString.Invariant($"updateGapP95Ms={updateGaps.P95():F2} updateGapMaxMs={updateGaps.Maximum:F2} snapshotGapP95Ms={snapshotGaps.P95():F2} snapshotGapMaxMs={snapshotGaps.Maximum:F2} ")
                    + FormattableString.Invariant($"stepAvgMs={StepMilliseconds.Mean:F3} stepP95Ms={StepMilliseconds.P95():F3} stepMaxMs={StepMilliseconds.Maximum:F3} publishAvgMs={SnapshotMilliseconds.Mean:F3} publishP95Ms={SnapshotMilliseconds.P95():F3} publishMaxMs={SnapshotMilliseconds.Maximum:F3} ")
                    + FormattableString.Invariant($"p0Frames={seatSnapshots[0]} p0RawBytesAvg={Average(rawBytes, 0):F0} p0PackedBytesAvg={Average(packedBytes, 0):F0} p0ChunksAvg={Average(chunks, 0):F2} ")
                    + FormattableString.Invariant($"p1Frames={seatSnapshots[1]} p1RawBytesAvg={Average(rawBytes, 1):F0} p1PackedBytesAvg={Average(packedBytes, 1):F0} p1ChunksAvg={Average(chunks, 1):F2}");
                UnityEngine.Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", message);
                windowStart = now; windowTick = tick; snapshots = 0;
                updateGaps.Clear(); snapshotGaps.Clear(); StepMilliseconds.Clear(); SnapshotMilliseconds.Clear();
                Array.Clear(rawBytes, 0, 2); Array.Clear(packedBytes, 0, 2); Array.Clear(chunks, 0, 2); Array.Clear(seatSnapshots, 0, 2);
            }
            private double Average(long[] totals, int player) => seatSnapshots[player] == 0 ? 0 : totals[player] / (double)seatSnapshots[player];
        }

        private void SendStatus(ulong client, string message) => SendTo(client, MatchProtocol.Status, MatchProtocol.Encode(new NetworkStatus { message = message }, 2048));
        private void Status(string message)
        {
            UnityEngine.Debug.Log("FRONTIER_SERVER " + message);
            MatchProtocol.Emit(StatusChanged, message);
        }

        public void StopServer()
        {
            closing = true; clock.Stop();
            InvalidateApprovals();
            StopWebSockets();
            if (manager == null) return;
            var old = manager; manager = null;
            old.OnClientConnectedCallback -= Connected; old.OnClientDisconnectCallback -= Disconnected;
            old.Shutdown(); Destroy(old.gameObject); seats.Clear();
        }
        private void OnDestroy() => StopServer();
    }
}
