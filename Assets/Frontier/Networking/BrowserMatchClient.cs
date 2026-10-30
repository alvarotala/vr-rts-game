#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Net;
using Frontier.Core;
using UnityEngine;

namespace Frontier.Networking
{
    // Same game protocol and authority as NgoMatchClient. The browser speaks the
    // dedicated server's custom binary gateway, not Unity Transport WebSockets.
    public sealed class BrowserMatchClient : MonoBehaviour
    {
        public event Action<MatchWelcome> Started;
        public event Action<PlayerView> StateReceived;
        public event Action<CommandAck> CommandAcknowledged;
        public event Action<MatchReport> Finished;
        public event Action<string> StatusChanged;
        public event Action<string> Disconnected;
        public bool IsConnected => socket != null && socket.IsOpen && admitted && !closing;
        public bool MatchStarted => welcome != null;
        public double CountdownRemaining => !IsConnected || welcome == null || ended ? 0 : Math.Max(0, startsAt - Time.realtimeSinceStartupAsDouble);
        private BrowserMatchSocket socket;
        private MatchWelcome welcome;
        private string expectedMatchId;
        private PlayerView bufferedView;
        private FrameAssembler snapshots, results;
        private bool admitted, closing, ended, disconnectReported;
        private byte[] hello;
        private readonly Dictionary<int, byte[]> pending = new Dictionary<int, byte[]>();
        private readonly Dictionary<int, CommandAck> acknowledged = new Dictionary<int, CommandAck>();
        private readonly Queue<int> acknowledgementOrder = new Queue<int>();
        private double connectedAt, lastStateAt, startsAt;

        public bool Connect(string address, ushort port, string faction, string matchId = null, string joinTicket = null, bool secure = false)
        {
            if (socket != null || string.IsNullOrWhiteSpace(address) || address.Length > 255 || port == 0) return false;
            if (faction != "industrial" && faction != "biological") return false;
            try
            {
                string url = Endpoint(address, port, secure, Application.absoluteURL);
                welcome = null; bufferedView = null; admitted = ended = closing = disconnectReported = false;
                pending.Clear(); acknowledged.Clear(); acknowledgementOrder.Clear(); expectedMatchId = matchId;
                snapshots = new FrameAssembler(NetworkWire.MaxSnapshotCompressed, NetworkWire.MaxSnapshotBytes);
                results = new FrameAssembler(NetworkWire.MaxResultCompressed, NetworkWire.MaxResultBytes);
                hello = MatchProtocol.Encode(new ConnectionHello { protocol = MatchProtocol.Version, rules = Simulation.ReplayRulesVersion,
                    faction = faction, contentHash = MatchProtocol.ContentHash(), matchId = matchId, joinTicket = joinTicket }, MatchProtocol.MaxHelloBytes);
                socket = new GameObject("Browser match socket").AddComponent<BrowserMatchSocket>();
                socket.Opened += Connected; socket.Received += Dispatch;
                socket.Closed += ConnectionLost; socket.Failed += ConnectionLost;
                connectedAt = lastStateAt = Time.realtimeSinceStartupAsDouble;
                if (!socket.Connect(url)) { Disconnect(); return false; }
                MatchProtocol.Emit(StatusChanged, "Connecting to the match server…");
                return true;
            }
            catch (Exception) { Disconnect(); return false; }
        }

        internal static string Endpoint(string address, ushort port, bool secure, string pageUrl)
        {
            string host = address.Trim().Trim('[', ']');
            if (port == 0 || Uri.CheckHostName(host) == UriHostNameType.Unknown || host == "0.0.0.0" || host == "::")
                throw new ArgumentException("Invalid server address.");
            bool local = host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
            if (IPAddress.TryParse(host, out var ip))
            {
                var bytes = ip.GetAddressBytes();
                local = IPAddress.IsLoopback(ip) || bytes.Length == 4 && (bytes[0] == 10
                    || bytes[0] == 192 && bytes[1] == 168 || bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31);
            }
            bool tls = secure || !local || Uri.TryCreate(pageUrl, UriKind.Absolute, out var page) && page.Scheme == "https";
            return (tls ? "wss://" : "ws://") + (host.Contains(":") ? "[" + host + "]" : host) + ":" + port + "/";
        }

        private void Connected()
        {
            if (closing || socket == null) return;
            if (!SendFrame(WebSocketMatchCodec.Hello, hello)) Fail("Could not send the connection request.");
            hello = null;
        }

        private bool SendFrame(byte kind, byte[] payload)
            => socket != null && socket.Send(WebSocketMatchCodec.Envelope(kind, payload));

        public bool Send(GameCommand command)
        {
            if (!IsConnected || welcome == null || CountdownRemaining > 0 || ended || command == null || command.sequence <= 0 || pending.Count >= 128) return false;
            try
            {
                if (!pending.TryGetValue(command.sequence, out var bytes))
                {
                    bytes = MatchProtocol.Encode(RecordedCommand.Copy(command), MatchProtocol.MaxCommandBytes);
                    if (!SendFrame(WebSocketMatchCodec.Command, bytes)) return false;
                    pending.Add(command.sequence, bytes);
                    return true;
                }
                return SendFrame(WebSocketMatchCodec.Command, bytes);
            }
            catch (Exception) { return false; }
        }

        private void Dispatch(byte[] frame)
        {
            if (closing) return;
            if (!WebSocketMatchCodec.TryOpen(frame, out byte kind, out byte[] payload)) { Fail("Invalid server message."); return; }
            try
            {
                if (!admitted && kind != WebSocketMatchCodec.Admitted) throw new InvalidOperationException("Missing admission.");
                switch (kind)
                {
                    case WebSocketMatchCodec.Admitted:
                        if (admitted || payload.Length != 1 || payload[0] != 1) throw new InvalidOperationException("Invalid admission.");
                        admitted = true; connectedAt = lastStateAt = Time.realtimeSinceStartupAsDouble;
                        if (!SendFrame(WebSocketMatchCodec.Ready, MatchProtocol.Signal)) throw new InvalidOperationException("Ready failed.");
                        MatchProtocol.Emit(StatusChanged, "Connected. Waiting for the other player.");
                        break;
                    case WebSocketMatchCodec.Welcome: ReceiveWelcome(Limit(payload, 1024)); break;
                    case WebSocketMatchCodec.Snapshot: ReceiveSnapshot(Limit(payload, NetworkWire.HeaderBytes + NetworkWire.ChunkBytes)); break;
                    case WebSocketMatchCodec.Ack: ReceiveAcknowledgement(Limit(payload, 2048)); break;
                    case WebSocketMatchCodec.Result: ReceiveResult(Limit(payload, NetworkWire.HeaderBytes + NetworkWire.ChunkBytes)); break;
                    case WebSocketMatchCodec.Status:
                        var status = MatchProtocol.Decode<NetworkStatus>(Limit(payload, 2048));
                        if (status?.message != null) MatchProtocol.Emit(StatusChanged, status.message);
                        break;
                    default: throw new InvalidOperationException("Unknown server message.");
                }
            }
            catch (Exception) { Fail("The server sent an invalid match message."); }
        }

        private static byte[] Limit(byte[] bytes, int max)
        {
            if (bytes.Length == 0 || bytes.Length > max) throw new InvalidOperationException("Invalid network message length.");
            return bytes;
        }

        private void ReceiveWelcome(byte[] bytes)
        {
            var value = MatchProtocol.Decode<MatchWelcome>(bytes);
            if (welcome != null || value == null || value.protocol != MatchProtocol.Version || value.player < 0 || value.player > 1
                || !string.IsNullOrEmpty(expectedMatchId) && value.matchId != expectedMatchId
                || double.IsNaN(value.countdownSeconds) || double.IsInfinity(value.countdownSeconds) || value.countdownSeconds < 0 || value.countdownSeconds > 10
                || double.IsNaN(value.startServerTime) || double.IsInfinity(value.startServerTime) || value.startServerTime < 0
                || Resources.Load<TextAsset>("Maps/" + value.mapId) == null)
                throw new InvalidOperationException("Invalid match welcome.");
            welcome = value; lastStateAt = Time.realtimeSinceStartupAsDouble; startsAt = lastStateAt + value.countdownSeconds;
            MatchProtocol.Emit(Started, value);
            if (bufferedView != null) { var initial = bufferedView; bufferedView = null; Publish(initial); }
            MatchProtocol.Emit(StatusChanged, "Match started.");
        }

        private void ReceiveSnapshot(byte[] bytes)
        {
            if (ended || !snapshots.Add(bytes, out _, out var packed, out int rawLength)) return;
            var view = MatchProtocol.Decode<PlayerView>(NetworkWire.Decompress(packed, rawLength, NetworkWire.MaxSnapshotBytes));
            if (welcome == null) bufferedView = view; else Publish(view);
        }

        private void Publish(PlayerView view)
        {
            if (view == null || welcome == null || view.player != welcome.player || view.tick < 0 || view.entities == null || view.visibility == null
                || view.resources == null || view.attacks == null || view.abilityEvents == null || view.deaths == null)
                throw new InvalidOperationException("Invalid player state.");
            lastStateAt = Time.realtimeSinceStartupAsDouble;
            MatchProtocol.Emit(StateReceived, view);
        }

        private void ReceiveAcknowledgement(byte[] bytes)
        {
            var ack = MatchProtocol.Decode<CommandAck>(bytes);
            if (ack == null || ack.sequence <= 0) throw new InvalidOperationException("Invalid command acknowledgement.");
            if (acknowledged.TryGetValue(ack.sequence, out var earlier))
            {
                if (ack.accepted != earlier.accepted || ack.tick != earlier.tick || ack.message != earlier.message)
                    throw new InvalidOperationException("Conflicting command results.");
            }
            else
            {
                if (!pending.Remove(ack.sequence)) return;
                acknowledged.Add(ack.sequence, ack); acknowledgementOrder.Enqueue(ack.sequence);
                while (acknowledgementOrder.Count > 512) acknowledged.Remove(acknowledgementOrder.Dequeue());
            }
            MatchProtocol.Emit(CommandAcknowledged, ack);
        }

        private void ReceiveResult(byte[] bytes)
        {
            if (ended) return;
            bool complete = results.Add(bytes, out _, out var packed, out int rawLength, out bool progressed);
            if (progressed) lastStateAt = Time.realtimeSinceStartupAsDouble;
            if (!complete) return;
            var result = MatchProtocol.Decode<MatchResultEnvelope>(NetworkWire.Decompress(packed, rawLength, NetworkWire.MaxResultBytes));
            if (result?.view == null || result.view.winner < 0 || result.view.winner > 2
                || !result.reportUnavailable && (result.report == null || result.view.winner != result.report.winner || result.view.tick != result.report.tick))
                throw new InvalidOperationException("Incomplete final result.");
            var report = result.reportUnavailable ? null : result.report.Restore();
            Publish(result.view); ended = true; pending.Clear();
            SendFrame(WebSocketMatchCodec.ResultAck, MatchProtocol.Signal);
            MatchProtocol.Emit(Finished, report);
        }

        private void Update()
        {
            if (socket == null || closing || ended) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!IsConnected && now - connectedAt > 20) { Fail("Connection timed out."); return; }
            if (welcome != null && now - lastStateAt > 15) Fail("The server stopped sending match state.");
            else if (admitted && welcome == null && now - connectedAt > 60) Fail("The match did not start. Please search again.");
        }

        private void ConnectionLost() => Fail("Connection to the server was lost.");

        private void Fail(string reason)
        {
            if (disconnectReported || closing) return;
            disconnectReported = true; Disconnect(); MatchProtocol.Emit(Disconnected, reason);
        }

        public void Disconnect()
        {
            closing = true; admitted = false; pending.Clear(); bufferedView = null; hello = null;
            if (socket == null) return;
            var old = socket; socket = null;
            old.Opened -= Connected; old.Received -= Dispatch; old.Closed -= ConnectionLost; old.Failed -= ConnectionLost;
            old.Close(); Destroy(old.gameObject);
        }
        private void OnDestroy() => Disconnect();
    }
}
#endif
