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
    // One dedicated process owns one match. No Unity transforms, physics, focus or local pause affect its rules.
    public sealed class NgoMatchServer : MonoBehaviour
    {
        private sealed class Seat
        {
            public ulong client;
            public int player;
            public string faction;
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

        public bool StartServer(ushort port = 7777, string mapId = "rift", string listenAddress = "0.0.0.0")
        {
            if (manager != null || port == 0) return false;
            try
            {
                var catalogAsset = Resources.Load<TextAsset>("catalog");
                var mapAsset = Resources.Load<TextAsset>("Maps/" + mapId);
                if (catalogAsset == null || mapAsset == null) throw new ArgumentException("Requested multiplayer map or catalog is missing.");
                catalog = new Catalog(JsonUtility.FromJson<CatalogDefinition>(catalogAsset.text));
                map = JsonUtility.FromJson<MapDefinition>(mapAsset.text); _ = new GridMap(map);
                this.mapId = mapId; matchId = Guid.NewGuid().ToString("N");
                _ = MatchProtocol.ContentHash();
                closing = finished = false; frame = 0; accumulator = 0; AcceptedCommands = RejectedCommands = 0;
                Match = null; seats.Clear(); clock.Restart(); lastTime = 0; nextSnapshot = 0;
                manager = MatchProtocol.CreateManager("Frontier dedicated network", "127.0.0.1", port, listenAddress);
                manager.ConnectionApprovalCallback = Approve;
                manager.OnClientConnectedCallback += Connected;
                manager.OnClientDisconnectCallback += Disconnected;
                if (!manager.StartServer()) throw new InvalidOperationException("Could not listen on the multiplayer port.");
                manager.CustomMessagingManager.RegisterNamedMessageHandler(MatchProtocol.Ready, Ready);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(MatchProtocol.Command, Command);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(MatchProtocol.ResultAck, ResultAcknowledged);
                Application.runInBackground = true;
                Status("Listening on " + listenAddress + ":" + port + " · " + mapId);
                return true;
            }
            catch (Exception error)
            {
                Status(error.Message); StopServer(); return false;
            }
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.Approved = false; response.CreatePlayerObject = false; response.Pending = false;
            try
            {
                if (Match != null || seats.Count >= 2) { response.Reason = "The match already has two players."; return; }
                if (request.Payload == null || request.Payload.Length == 0 || request.Payload.Length > MatchProtocol.MaxHelloBytes)
                { response.Reason = "Invalid connection request."; return; }
                var hello = MatchProtocol.Decode<ConnectionHello>(request.Payload);
                if (hello == null || hello.protocol != MatchProtocol.Version || hello.rules != Simulation.ReplayRulesVersion || hello.contentHash != MatchProtocol.ContentHash())
                { response.Reason = "Incompatible game version, rules, maps or catalog."; return; }
                if (hello.faction != "industrial" && hello.faction != "biological") { response.Reason = "Unknown faction."; return; }
                int player = seats.Values.Any(s => s.player == 0) ? 1 : 0;
                seats.Add(request.ClientNetworkId, new Seat { client = request.ClientNetworkId, player = player, faction = hello.faction, connectedAt = clock.Elapsed.TotalSeconds });
                response.Approved = true;
            }
            catch { response.Reason = "Invalid connection request."; }
        }

        private void Connected(ulong client)
        {
            if (!seats.TryGetValue(client, out var seat)) { manager.DisconnectClient(client, "Unapproved player."); return; }
            seat.connected = true; seat.connectedAt = clock.Elapsed.TotalSeconds;
            SendStatus(client, "Connected. Waiting for both players to be ready.");
            Status("Player " + (seat.player + 1) + " connected (" + ConnectedPlayers + "/2).");
        }

        private void Ready(ulong client, FastBufferReader reader)
        {
            if (!seats.TryGetValue(client, out var seat) || !seat.connected || Match != null) return;
            try
            {
                if (MatchProtocol.Read(reader, 1)[0] != 1) throw new ArgumentException("Invalid readiness signal.");
            }
            catch
            {
                manager.DisconnectClient(client, "Malformed readiness signal.");
                return;
            }
            try
            {
                seat.ready = true;
                if (seats.Count != 2 || seats.Values.Any(s => !s.ready || !s.connected)) return;
                var ordered = seats.Values.OrderBy(s => s.player).ToArray();
                Match = new Simulation(map, catalog, ordered[0].faction, ordered[1].faction);
                Match.Paused = true;
                startServerTime = manager.ServerTime.Time + MatchProtocol.StartCountdownSeconds;
                lastTime = clock.Elapsed.TotalSeconds; accumulator = 0; nextSnapshot = lastTime;
                foreach (var player in ordered)
                {
                    var welcome = new MatchWelcome { protocol = MatchProtocol.Version, matchId = matchId, mapId = mapId,
                        player = player.player, faction0 = ordered[0].faction, faction1 = ordered[1].faction, startServerTime = startServerTime };
                    MatchProtocol.Send(manager, MatchProtocol.Welcome, player.client, MatchProtocol.Encode(welcome, 1024));
                }
                Status("Match started · " + matchId);
                PublishSnapshots();
            }
            catch (Exception error) { Status("Could not start match: " + error.Message); StopServer(); }
        }

        private void Command(ulong client, FastBufferReader reader)
        {
            if (!seats.TryGetValue(client, out var seat) || !seat.ready || Match == null) return;
            double arrival = clock.Elapsed.TotalSeconds;
            seat.requestTokens = Math.Min(120, seat.requestTokens + Math.Max(0, arrival - seat.requestTime) * 60); seat.requestTime = arrival;
            if (seat.requestTokens < 1) { manager.DisconnectClient(client, "Command traffic limit exceeded."); return; }
            seat.requestTokens -= 1;
            GameCommand command;
            try { command = MatchProtocol.Decode<GameCommand>(MatchProtocol.Read(reader, MatchProtocol.MaxCommandBytes)); }
            catch { manager.DisconnectClient(client, "Malformed command."); return; }
            if (command == null || command.sequence <= 0) { manager.DisconnectClient(client, "Commands require a positive sequence."); return; }
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
            if (seat.connected) MatchProtocol.Send(manager, MatchProtocol.Ack, seat.client, MatchProtocol.Encode(acknowledgement, 2048));
        }

        private void Update()
        {
            if (!IsRunning || closing) return;
            double now = clock.Elapsed.TotalSeconds;
            foreach (var seat in seats.Values.ToArray())
                if (!seat.ready && now - seat.connectedAt > 15)
                {
                    manager.DisconnectClient(seat.client, "Timed out waiting for player readiness.");
                    if (Match == null) seats.Remove(seat.client);
                }
            if (Match == null) { lastTime = now; return; }
            if (AwaitingStart(manager.ServerTime.Time))
            {
                accumulator = 0; lastTime = now;
                if (now >= nextSnapshot) { PublishSnapshots(); nextSnapshot = now + .1; }
                return;
            }
            if (Match.Winner == -1)
            {
                accumulator += Math.Min(.25, Math.Max(0, now - lastTime)) * Math.Max(1, Math.Min(20, SimulationSpeed));
                lastTime = now;
                int steps = 0;
                while (accumulator >= 1d / Simulation.TickRate && steps++ < 128 && Match.Winner == -1)
                { Match.Step(); accumulator -= 1d / Simulation.TickRate; }
                if (now >= nextSnapshot) { PublishSnapshots(); nextSnapshot = now + .1; }
            }
            if (Match.Winner != -1) Finish();
            foreach (var seat in seats.Values)
            {
                if (!seat.connected || seat.result == null || seat.resultAcknowledged) continue;
                int total = NetworkWire.ChunkCount(seat.result.Length);
                for (int sent = 0; sent < 8 && seat.resultNextChunk < total; sent++, seat.resultNextChunk++)
                    MatchProtocol.Send(manager, MatchProtocol.Result, seat.client,
                        NetworkWire.Chunk(1, seat.result, seat.resultRawLength, seat.resultNextChunk));
            }
        }

        private void PublishSnapshots()
        {
            if (Match == null) return;
            long id = ++frame;
            foreach (var seat in seats.Values)
            {
                if (!seat.connected) continue;
                byte[] raw = MatchProtocol.Encode(Match.View(seat.player), NetworkWire.MaxSnapshotBytes);
                byte[] packed = NetworkWire.Compress(raw, NetworkWire.MaxSnapshotBytes, NetworkWire.MaxSnapshotCompressed);
                int count = NetworkWire.ChunkCount(packed.Length);
                for (int index = 0; index < count; index++)
                    // FrameAssembler sequences whole snapshots; the transport must preserve reordered chunks.
                    MatchProtocol.Send(manager, MatchProtocol.Snapshot, seat.client, NetworkWire.Chunk(id, packed, raw.Length, index), NetworkDelivery.Unreliable);
            }
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
            Status("Match finished · winner=" + Match.Winner + " tick=" + Match.Tick + " accepted=" + AcceptedCommands + " rejected=" + RejectedCommands);
        }

        private void ResultAcknowledged(ulong client, FastBufferReader reader)
        {
            if (!finished || !seats.TryGetValue(client, out var seat)) return;
            try { if (MatchProtocol.Read(reader, 1)[0] == 1) { seat.resultAcknowledged = true; seat.result = null; } } catch { }
        }

        private void Disconnected(ulong client)
        {
            if (closing || !seats.TryGetValue(client, out var seat)) return;
            seat.connected = false;
            if (Match == null) { seats.Remove(client); Status("Waiting for two players."); return; }
            if (Match.Winner == -1)
            {
                // Reconnection is deliberately not enabled in this first direct-connect protocol.
                Match.Paused = false;
                Match.Submit(new GameCommand { player = seat.player, kind = CommandKind.Surrender });
                Finish();
            }
            Status("Player " + (seat.player + 1) + " disconnected.");
        }

        private void SendStatus(ulong client, string message) => MatchProtocol.Send(manager, MatchProtocol.Status, client, MatchProtocol.Encode(new NetworkStatus { message = message }, 2048));
        private void Status(string message)
        {
            UnityEngine.Debug.Log("FRONTIER_SERVER " + message);
            MatchProtocol.Emit(StatusChanged, message);
        }

        public void StopServer()
        {
            closing = true; clock.Stop();
            if (manager == null) return;
            var old = manager; manager = null;
            old.OnClientConnectedCallback -= Connected; old.OnClientDisconnectCallback -= Disconnected;
            old.Shutdown(); Destroy(old.gameObject); seats.Clear();
        }
        private void OnDestroy() => StopServer();
    }
}
