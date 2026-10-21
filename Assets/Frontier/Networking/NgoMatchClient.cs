using System;
using System.Collections.Generic;
using Frontier.Core;
using Unity.Netcode;
using UnityEngine;

namespace Frontier.Networking
{
    public sealed class NgoMatchClient : MonoBehaviour
    {
        public event Action<MatchWelcome> Started;
        public event Action<PlayerView> StateReceived;
        public event Action<CommandAck> CommandAcknowledged;
        public event Action<MatchReport> Finished;
        public event Action<string> StatusChanged;
        public event Action<string> Disconnected;
        public bool IsConnected => manager != null && manager.IsConnectedClient;
        public bool MatchStarted => welcome != null;
        public double RoundTripTimeSeconds => manager == null || !IsConnected ? 0 : manager.NetworkConfig.NetworkTransport.GetCurrentRtt(Unity.Netcode.NetworkManager.ServerClientId) / 1000d;
        private NetworkManager manager;
        private MatchWelcome welcome;
        private PlayerView bufferedView;
        private FrameAssembler snapshots, results;
        private bool closing, ended, disconnectReported;
        private readonly Dictionary<int, byte[]> pending = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, CommandAck> acknowledged = new Dictionary<int, CommandAck>();
        private readonly Queue<int> acknowledgementOrder = new Queue<int>();
        private double connectedAt, lastStateAt;

        public bool Connect(string address, ushort port, string faction)
        {
            if (manager != null || string.IsNullOrWhiteSpace(address) || address.Length > 255 || port == 0) return false;
            if (faction != "industrial" && faction != "biological") return false;
            try
            {
                welcome = null; bufferedView = null; ended = closing = disconnectReported = false; pending.Clear(); acknowledged.Clear(); acknowledgementOrder.Clear();
                snapshots = new FrameAssembler(NetworkWire.MaxSnapshotCompressed, NetworkWire.MaxSnapshotBytes);
                results = new FrameAssembler(NetworkWire.MaxResultCompressed, NetworkWire.MaxResultBytes);
                var hello = new ConnectionHello { protocol = MatchProtocol.Version, rules = Simulation.ReplayRulesVersion, faction = faction, contentHash = MatchProtocol.ContentHash() };
                manager = MatchProtocol.CreateManager("Frontier client network", address.Trim(), port);
                manager.NetworkConfig.ConnectionData = MatchProtocol.Encode(hello, MatchProtocol.MaxHelloBytes);
                manager.OnClientConnectedCallback += Connected;
                manager.OnClientDisconnectCallback += ConnectionLost;
                if (!manager.StartClient()) throw new InvalidOperationException("Could not start multiplayer connection.");
                manager.CustomMessagingManager.RegisterNamedMessageHandler(MatchProtocol.Welcome, ReceiveWelcome);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(MatchProtocol.Snapshot, ReceiveSnapshot);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(MatchProtocol.Ack, ReceiveAcknowledgement);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(MatchProtocol.Result, ReceiveResult);
                manager.CustomMessagingManager.RegisterNamedMessageHandler(MatchProtocol.Status, ReceiveStatus);
                connectedAt = lastStateAt = Time.realtimeSinceStartupAsDouble;
                MatchProtocol.Emit(StatusChanged, "Connecting to " + address.Trim() + ":" + port + "…");
                return true;
            }
            catch (Exception error)
            {
                MatchProtocol.Emit(StatusChanged, error.Message); Disconnect(); return false;
            }
        }

        private void Connected(ulong client)
        {
            if (closing || manager == null || client != manager.LocalClientId) return;
            connectedAt = lastStateAt = Time.realtimeSinceStartupAsDouble;
            MatchProtocol.Send(manager, MatchProtocol.Ready, NetworkManager.ServerClientId, MatchProtocol.Signal);
            MatchProtocol.Emit(StatusChanged, "Connected. Waiting for the other player.");
        }

        public bool Send(GameCommand command)
        {
            if (!IsConnected || welcome == null || ended || command == null || command.sequence <= 0 || pending.Count >= 128) return false;
            try
            {
                if (!pending.TryGetValue(command.sequence, out var bytes))
                {
                    // The dedicated server derives authority from the connection, never this field.
                    command = RecordedCommand.Copy(command);
                    bytes = MatchProtocol.Encode(command, MatchProtocol.MaxCommandBytes); pending.Add(command.sequence, bytes);
                }
                MatchProtocol.Send(manager, MatchProtocol.Command, NetworkManager.ServerClientId, bytes);
                return true;
            }
            catch (Exception error) { MatchProtocol.Emit(StatusChanged, error.Message); return false; }
        }

        private void ReceiveWelcome(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || welcome != null || closing) return;
            try
            {
                var value = MatchProtocol.Decode<MatchWelcome>(MatchProtocol.Read(reader, 1024));
                if (value == null || value.protocol != MatchProtocol.Version || value.player < 0 || value.player > 1
                    || Resources.Load<TextAsset>("Maps/" + value.mapId) == null) throw new InvalidOperationException("Invalid match welcome.");
                welcome = value; lastStateAt = Time.realtimeSinceStartupAsDouble;
                MatchProtocol.Emit(Started, value);
                if (bufferedView != null) { var initial = bufferedView; bufferedView = null; Publish(initial); }
                MatchProtocol.Emit(StatusChanged, "Match started.");
            }
            catch (Exception error) { Fail("Invalid match setup: " + error.Message); }
        }

        private void ReceiveSnapshot(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || closing || ended) return;
            try
            {
                if (!snapshots.Add(MatchProtocol.Read(reader, NetworkWire.HeaderBytes + NetworkWire.ChunkBytes), out _, out var packed, out int rawLength)) return;
                var view = MatchProtocol.Decode<PlayerView>(NetworkWire.Decompress(packed, rawLength, NetworkWire.MaxSnapshotBytes));
                if (welcome == null) bufferedView = view; else Publish(view);
            }
            catch (Exception error) { MatchProtocol.Emit(StatusChanged, "Discarded invalid state: " + error.Message); }
        }

        private void Publish(PlayerView view)
        {
            if (view == null || welcome == null || view.player != welcome.player || view.tick < 0 || view.entities == null || view.visibility == null
                || view.resources == null || view.attacks == null || view.abilityEvents == null || view.deaths == null) throw new InvalidOperationException("Invalid player state.");
            lastStateAt = Time.realtimeSinceStartupAsDouble;
            MatchProtocol.Emit(StateReceived, view);
        }

        private void ReceiveAcknowledgement(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || closing) return;
            try
            {
                var ack = MatchProtocol.Decode<CommandAck>(MatchProtocol.Read(reader, 2048));
                if (ack == null || ack.sequence <= 0) return;
                if (acknowledged.TryGetValue(ack.sequence, out var earlier))
                {
                    if (ack.accepted != earlier.accepted || ack.tick != earlier.tick || ack.message != earlier.message)
                        throw new InvalidOperationException("Conflicting results for a repeated command.");
                }
                else
                {
                    if (!pending.Remove(ack.sequence)) return;
                    acknowledged.Add(ack.sequence, ack); acknowledgementOrder.Enqueue(ack.sequence);
                    while (acknowledgementOrder.Count > 512) acknowledged.Remove(acknowledgementOrder.Dequeue());
                }
                MatchProtocol.Emit(CommandAcknowledged, ack);
            }
            catch (Exception error) { Fail("Invalid command acknowledgement: " + error.Message); }
        }

        private void ReceiveResult(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || closing || ended) return;
            try
            {
                bool complete = results.Add(MatchProtocol.Read(reader, NetworkWire.HeaderBytes + NetworkWire.ChunkBytes), out _, out var packed, out int rawLength, out bool progressed);
                if (progressed) lastStateAt = Time.realtimeSinceStartupAsDouble;
                if (!complete) return;
                var result = MatchProtocol.Decode<MatchResultEnvelope>(NetworkWire.Decompress(packed, rawLength, NetworkWire.MaxResultBytes));
                var report = RestoreFinalReport(result);
                Publish(result.view); ended = true; pending.Clear();
                MatchProtocol.Send(manager, MatchProtocol.ResultAck, NetworkManager.ServerClientId, MatchProtocol.Signal);
                MatchProtocol.Emit(Finished, report);
            }
            catch (Exception error) { Fail("Invalid match result: " + error.Message); }
        }

        private static MatchReport RestoreFinalReport(MatchResultEnvelope result)
        {
            if (result?.view == null || result.view.winner < 0 || result.view.winner > 2
                || !result.reportUnavailable && (result.report == null || result.view.winner != result.report.winner || result.view.tick != result.report.tick))
                throw new InvalidOperationException("Incomplete final result.");
            // JsonUtility can materialize JSON null as an empty DTO. Availability
            // comes from the explicit wire flag, never the CLR reference alone.
            return result.reportUnavailable ? null : result.report.Restore();
        }

        private void ReceiveStatus(ulong sender, FastBufferReader reader)
        {
            if (sender != NetworkManager.ServerClientId || closing) return;
            try
            {
                var status = MatchProtocol.Decode<NetworkStatus>(MatchProtocol.Read(reader, 2048));
                if (status?.message != null) MatchProtocol.Emit(StatusChanged, status.message);
            }
            catch { }
        }

        private void Update()
        {
            if (manager == null || closing || ended) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!IsConnected && now - connectedAt > 20) { Fail("Connection timed out."); return; }
            if (welcome != null && now - lastStateAt > 15) Fail("The server stopped sending match state.");
        }

        private void ConnectionLost(ulong client)
        {
            if (closing || manager == null) return;
            string reason = manager.DisconnectReason;
            Fail(string.IsNullOrWhiteSpace(reason) ? "Connection to the server was lost." : reason);
        }

        private void Fail(string reason)
        {
            if (disconnectReported || closing) return;
            disconnectReported = true;
            Disconnect();
            MatchProtocol.Emit(Disconnected, reason);
        }

        public void Disconnect()
        {
            closing = true; pending.Clear(); bufferedView = null;
            if (manager == null) return;
            var old = manager; manager = null;
            old.OnClientConnectedCallback -= Connected; old.OnClientDisconnectCallback -= ConnectionLost;
            old.Shutdown(); Destroy(old.gameObject);
        }
        private void OnDestroy() => Disconnect();
    }
}
