using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Frontier.Core;
using Frontier.Harness.Shim;
using Frontier.Networking;
using Frontier.Runtime;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool pass, string message) { if (!pass) throw new Exception(message); checks++; }
    private static byte[] Json<T>(T value) => MatchProtocol.Encode(value, NetworkWire.MaxResultBytes);
    private static MatchWelcome Welcome(double countdown = 0, string matchId = "match")
        => new MatchWelcome { protocol = MatchProtocol.Version, player = 0, mapId = "rift", matchId = matchId,
            faction0 = "industrial", faction1 = "biological", countdownSeconds = countdown, startServerTime = 3 };
    private static PlayerView View(int winner = -1) => new PlayerView { player = 0, tick = 1, winner = winner };

    private static BrowserMatchClient Connect(out BrowserMatchSocket socket, bool admitted = true)
    {
        var client = new GameObject("WebGL test client").AddComponent<BrowserMatchClient>();
        Check(client.Connect("localhost", 7877, "industrial", "match", "ticket"), "connect accepted");
        socket = BrowserMatchSocket.Latest; socket.Open();
        if (admitted) socket.Receive(WebSocketMatchCodec.Admitted, MatchProtocol.Signal);
        return client;
    }

    private static void Packed(BrowserMatchSocket socket, byte kind, object value)
    {
        var raw = Json(value); var packed = NetworkWire.Compress(raw, NetworkWire.MaxResultBytes, NetworkWire.MaxResultCompressed);
        for (int i = 0; i < NetworkWire.ChunkCount(packed.Length); i++)
            socket.Receive(kind, NetworkWire.Chunk(1, packed, raw.Length, i));
    }

    public static int Main()
    {
        try
        {
            Resources.Root = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Frontier", "Resources");
            Check(BrowserMatchClient.Endpoint("localhost", 7877, false, "http://localhost/") == "ws://localhost:7877/", "local HTTP uses WS");
            Check(BrowserMatchClient.Endpoint("127.0.0.1", 7877, false, "https://game.invalid/") == "wss://127.0.0.1:7877/", "secure page never mixed content");
            Check(BrowserMatchClient.Endpoint("192.168.1.2", 7877, true, "http://localhost/") == "wss://192.168.1.2:7877/", "backend TLS survives localhost page");
            Check(BrowserMatchClient.Endpoint("game.invalid", 443, false, "http://localhost/") == "wss://game.invalid:443/", "public host requires TLS");
            Check(BrowserMatchClient.Endpoint("[::1]", 7877, false, "http://localhost/") == "ws://[::1]:7877/", "IPv6 URI brackets");
            foreach (string invalid in new[] { "bad/path", "user@host", "https://host", "0.0.0.0", "::" })
            {
                bool rejected = false;
                try { BrowserMatchClient.Endpoint(invalid, 7877, false, ""); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "invalid host rejected: " + invalid);
            }

            var client = Connect(out var socket, admitted: false);
            Check(!client.IsConnected && socket.Sent.Count == 1, "open sends hello but waits for admission");
            WebSocketMatchCodec.TryOpen(socket.Sent[0], out byte kind, out var payload);
            var hello = MatchProtocol.Decode<ConnectionHello>(payload);
            Check(kind == WebSocketMatchCodec.Hello && hello.matchId == "match" && hello.joinTicket == "ticket"
                && hello.protocol == MatchProtocol.Version && hello.rules == Simulation.ReplayRulesVersion
                && hello.contentHash == MatchProtocol.ContentHash(), "same compatibility and ticket as NGO");
            socket.Receive(WebSocketMatchCodec.Admitted, MatchProtocol.Signal);
            Check(client.IsConnected && socket.Sent[1][0] == WebSocketMatchCodec.Ready, "admission sends ready");
            int started = 0, states = 0, acks = 0, finished = 0, lost = 0;
            client.Started += _ => started++; client.StateReceived += _ => states++;
            client.CommandAcknowledged += _ => acks++; client.Finished += report => { Check(report == null, "unavailable report stays null"); finished++; };
            client.Disconnected += _ => lost++;
            Packed(socket, WebSocketMatchCodec.Snapshot, View());
            Check(states == 0, "snapshot before welcome is buffered");
            socket.Receive(WebSocketMatchCodec.Welcome, Json(Welcome()));
            Check(started == 1 && states == 1, "welcome publishes buffered snapshot");
            var command = new GameCommand { sequence = 1, kind = CommandKind.Move };
            socket.RefuseSend = true;
            Check(!client.Send(command), "backpressure fails without adding pending command");
            socket.RefuseSend = false;
            Check(client.Send(command), "same command can retry after backpressure");
            Check(socket.Sent.Last()[0] == WebSocketMatchCodec.Command, "binary command envelope");
            var ack = new CommandAck { sequence = 1, tick = 1, accepted = true, message = "ok" };
            socket.Receive(WebSocketMatchCodec.Ack, Json(ack));
            socket.Receive(WebSocketMatchCodec.Ack, Json(ack));
            Check(acks == 2, "identical repeated ack is safe");
            Packed(socket, WebSocketMatchCodec.Result, new MatchResultEnvelope { view = View(0), reportUnavailable = true });
            Check(finished == 1 && states == 2 && socket.Sent.Last()[0] == WebSocketMatchCodec.ResultAck, "final state and result acknowledgement");
            Check(!client.Send(new GameCommand { sequence = 2 }), "commands blocked after result");
            socket.Lose(); socket.Error();
            Check(lost == 1, "disconnect reported once");
            UnityEngine.Object.Destroy(client.gameObject);

            client = Connect(out socket);
            socket.Receive(WebSocketMatchCodec.Welcome, Json(Welcome(3)));
            Check(client.CountdownRemaining > 2.9 && !client.Send(new GameCommand { sequence = 1 }), "browser uses relative countdown and blocks premature orders");
            client.Disconnect(); UnityEngine.Object.Destroy(client.gameObject);

            foreach (var invalidWelcome in new[] { Welcome(0, "wrong-match"), Welcome(-1), Welcome(11) })
            {
                client = Connect(out socket); lost = 0; client.Disconnected += _ => lost++;
                socket.Receive(WebSocketMatchCodec.Welcome, Json(invalidWelcome));
                Check(lost == 1 && !client.IsConnected, "mismatched allocation or invalid countdown fails");
                UnityEngine.Object.Destroy(client.gameObject);
            }
            client = Connect(out socket); lost = 0; client.Disconnected += _ => lost++;
            socket.Receive(WebSocketMatchCodec.Welcome, new byte[1025]);
            Check(lost == 1, "welcome size bounded before decoding");
            UnityEngine.Object.Destroy(client.gameObject);
            client = Connect(out socket); lost = 0; client.Disconnected += _ => lost++;
            socket.Receive(0xff, new byte[] { 1 });
            Check(lost == 1, "unknown frame fails");
            UnityEngine.Object.Destroy(client.gameObject);
            client = Connect(out socket, false); lost = 0; client.Disconnected += _ => lost++;
            socket.Receive(WebSocketMatchCodec.Welcome, Json(Welcome()));
            Check(lost == 1, "welcome cannot bypass admission");
            UnityEngine.Object.Destroy(client.gameObject);

            client = Connect(out socket); lost = 0; client.Disconnected += _ => lost++;
            socket.Receive(WebSocketMatchCodec.Welcome, Json(Welcome())); client.Send(command);
            socket.Receive(WebSocketMatchCodec.Ack, Json(ack)); ack.accepted = false;
            socket.Receive(WebSocketMatchCodec.Ack, Json(ack));
            Check(lost == 1, "conflicting repeated ack fails");
            UnityEngine.Object.Destroy(client.gameObject);
            client = Connect(out socket); lost = 0; client.Disconnected += _ => lost++;
            socket.Receive(WebSocketMatchCodec.Welcome, Json(Welcome()));
            Packed(socket, WebSocketMatchCodec.Result, new MatchResultEnvelope { view = View(-1), reportUnavailable = true });
            Check(lost == 1, "unfinished result cannot end match");
            UnityEngine.Object.Destroy(client.gameObject);

            var cancel = new CancellationTokenSource();
            var delay = WebGlAsync.Delay(30000, cancel.Token); cancel.Cancel(); ShimRuntime.Tick();
            Check(delay.IsCanceled, "frame-driven delay observes cancellation");
            Check(WebGlAsync.Delay(0, CancellationToken.None).IsCompletedSuccessfully, "zero delay completes without a timer");
            Check(WebGlAsync.WithTimeout(Task.FromResult(42), 1000, CancellationToken.None).Result == 42, "completed request preserves result");
            var pendingRequest = new TaskCompletionSource<int>();
            var timedOut = WebGlAsync.WithTimeout(pendingRequest.Task, 0, CancellationToken.None);
            Check(timedOut.IsFaulted && timedOut.Exception.InnerException is TimeoutException, "unanswered request has frame deadline");
            pendingRequest.TrySetException(new Exception("late response"));
            ShimRuntime.DestroyAll();
            Console.WriteLine("FRONTIER_WEBGL_CLIENT_CHECKS passed " + checks);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
