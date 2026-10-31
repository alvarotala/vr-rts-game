using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Frontier.Core;
using Frontier.Networking;
using UnityEngine;

namespace Frontier.Harness
{
    // C# counterpart of the browser transport: the same envelopes, chunk assembly,
    // decompression and JSON payloads as BrowserMatchClient. Used by harness bots.
    public sealed class WebSocketMatchClient : MonoBehaviour
    {
        public event Action<MatchWelcome> Started;
        public event Action<PlayerView> StateReceived;
        public event Action<CommandAck> CommandAcknowledged;
        public event Action<MatchReport> Finished;
        public event Action<string> StatusChanged;
        public event Action<string> Disconnected;
        public bool Admitted { get; private set; }
        // Test hook: a client which never declares readiness.
        public bool AutoReady = true;
        public bool IsConnected => socket != null && socket.State == WebSocketState.Open && Admitted && !failed;
        public double CountdownRemaining => welcome == null || ended ? 0 : Math.Max(0, startsAt - Time.realtimeSinceStartupAsDouble);
        private ClientWebSocket socket;
        private readonly ConcurrentQueue<byte[]> incoming = new ConcurrentQueue<byte[]>();
        private Channel<byte[]> outgoing;
        private CancellationTokenSource lifetime;
        private volatile string closeReason;
        private volatile bool closed;
        private bool failed, ended;
        private MatchWelcome welcome;
        private double startsAt;
        private PlayerView buffered;
        private FrameAssembler snapshots, results;

        public static WebSocketMatchClient Create(string name)
            => new GameObject(name).AddComponent<WebSocketMatchClient>();

        public void Connect(string url, string faction, string matchId = null, string joinTicket = null)
        {
            snapshots = new FrameAssembler(NetworkWire.MaxSnapshotCompressed, NetworkWire.MaxSnapshotBytes);
            results = new FrameAssembler(NetworkWire.MaxResultCompressed, NetworkWire.MaxResultBytes);
            var hello = new ConnectionHello { protocol = MatchProtocol.Version, rules = Simulation.ReplayRulesVersion, faction = faction,
                contentHash = MatchProtocol.ContentHash(), matchId = matchId, joinTicket = joinTicket };
            ConnectRaw(url, WebSocketMatchCodec.Envelope(WebSocketMatchCodec.Hello, MatchProtocol.Encode(hello, MatchProtocol.MaxHelloBytes)));
        }

        // Test hook: connect and send an arbitrary first frame (or none).
        public void ConnectRaw(string url, byte[] firstFrame)
        {
            socket = new ClientWebSocket();
            lifetime = new CancellationTokenSource();
            outgoing = Channel.CreateUnbounded<byte[]>(new UnboundedChannelOptions { SingleReader = true });
            if (firstFrame != null) outgoing.Writer.TryWrite(firstFrame);
            _ = Run(url, lifetime.Token);
        }

        public void SendRaw(byte[] frame) => outgoing?.Writer.TryWrite(frame);

        private async Task Run(string url, CancellationToken token)
        {
            try
            {
                await socket.ConnectAsync(new Uri(url), token).ConfigureAwait(false);
                _ = Pump(token);
                var buffer = new byte[64 * 1024];
                while (!token.IsCancellationRequested)
                {
                    var message = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            closeReason = string.IsNullOrEmpty(socket.CloseStatusDescription) ? "Connection closed by the server." : socket.CloseStatusDescription;
                            try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).ConfigureAwait(false); } catch (Exception) { }
                            closed = true; return;
                        }
                        message.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);
                    incoming.Enqueue(message.ToArray());
                }
            }
            catch (Exception error)
            {
                if (closeReason == null) closeReason = token.IsCancellationRequested ? "Disconnected." : "Connection lost: " + error.GetType().Name;
                closed = true;
            }
        }

        private async Task Pump(CancellationToken token)
        {
            try
            {
                while (await outgoing.Reader.WaitToReadAsync(token).ConfigureAwait(false))
                    while (outgoing.Reader.TryRead(out var frame))
                        await socket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, token).ConfigureAwait(false);
            }
            catch (Exception) { }
        }

        public bool Send(GameCommand command)
        {
            if (!IsConnected || welcome == null || CountdownRemaining > 0 || ended || command == null || command.sequence <= 0) return false;
            var copy = RecordedCommand.Copy(command);
            SendRaw(WebSocketMatchCodec.Envelope(WebSocketMatchCodec.Command, MatchProtocol.Encode(copy, MatchProtocol.MaxCommandBytes)));
            return true;
        }

        private void Update()
        {
            while (!failed && incoming.TryDequeue(out var frame)) Dispatch(frame);
            if (!failed && closed && incoming.IsEmpty) Fail(closeReason ?? "Connection closed.");
        }

        private void Dispatch(byte[] frame)
        {
            if (!WebSocketMatchCodec.TryOpen(frame, out byte kind, out byte[] payload)) { Fail("Malformed server frame."); return; }
            try
            {
                switch (kind)
                {
                    case WebSocketMatchCodec.Admitted:
                        if (Admitted) return;
                        Admitted = true;
                        if (AutoReady) SendRaw(WebSocketMatchCodec.Envelope(WebSocketMatchCodec.Ready, MatchProtocol.Signal));
                        break;
                    case WebSocketMatchCodec.Welcome:
                        var value = MatchProtocol.Decode<MatchWelcome>(payload);
                        if (welcome != null || value == null || value.protocol != MatchProtocol.Version || value.player < 0 || value.player > 1)
                            throw new InvalidOperationException("Invalid match welcome.");
                        welcome = value; startsAt = Time.realtimeSinceStartupAsDouble + value.countdownSeconds;
                        Started?.Invoke(value);
                        if (buffered != null) { var initial = buffered; buffered = null; Publish(initial); }
                        break;
                    case WebSocketMatchCodec.Snapshot:
                        if (ended || !snapshots.Add(payload, out _, out var packed, out int raw)) return;
                        var view = MatchProtocol.Decode<PlayerView>(NetworkWire.Decompress(packed, raw, NetworkWire.MaxSnapshotBytes));
                        if (welcome == null) buffered = view; else Publish(view);
                        break;
                    case WebSocketMatchCodec.Ack:
                        CommandAcknowledged?.Invoke(MatchProtocol.Decode<CommandAck>(payload));
                        break;
                    case WebSocketMatchCodec.Result:
                        if (ended || !results.Add(payload, out _, out var compressed, out int length)) return;
                        var envelope = MatchProtocol.Decode<MatchResultEnvelope>(NetworkWire.Decompress(compressed, length, NetworkWire.MaxResultBytes));
                        if (envelope?.view == null || envelope.view.winner < 0) throw new InvalidOperationException("Incomplete final result.");
                        var report = envelope.reportUnavailable ? null : envelope.report.Restore();
                        Publish(envelope.view); ended = true;
                        SendRaw(WebSocketMatchCodec.Envelope(WebSocketMatchCodec.ResultAck, MatchProtocol.Signal));
                        Finished?.Invoke(report);
                        break;
                    case WebSocketMatchCodec.Status:
                        StatusChanged?.Invoke(MatchProtocol.Decode<NetworkStatus>(payload)?.message);
                        break;
                    default:
                        throw new InvalidOperationException("Unknown server frame kind " + kind + ".");
                }
            }
            catch (Exception error) { Fail("Invalid server message: " + error.Message); }
        }

        private void Publish(PlayerView view)
        {
            if (view == null || welcome == null || view.player != welcome.player || view.entities == null || view.visibility == null)
                throw new InvalidOperationException("Invalid player state.");
            StateReceived?.Invoke(view);
        }

        private void Fail(string reason)
        {
            if (failed) return;
            failed = true;
            Disconnected?.Invoke(reason);
        }

        public void Disconnect()
        {
            failed = true;
            try
            {
                if (socket != null && socket.State == WebSocketState.Open)
                    socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Client left.", CancellationToken.None).Wait(1000);
            }
            catch (Exception) { }
            lifetime?.Cancel();
        }

        // Abrupt transport loss, as when a browser tab is closed without a close frame.
        public void Abort() { failed = true; try { socket?.Abort(); } catch (Exception) { } lifetime?.Cancel(); }

        private void OnDestroy() => Disconnect();
    }
}
