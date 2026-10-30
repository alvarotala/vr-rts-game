using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using Frontier.Networking;

// Real TCP round trips against the Unity-free gateway: handshake, framing,
// fragmentation, limits, close semantics and bounded send queues.
static class WebSocketChecks
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public static int Run(Action<bool, string> check)
    {
        int count = 0;
        void Check(bool condition, string message) { check(condition, message); count++; }

        CodecChecks(Check);
        using (var gateway = new WebSocketGateway("127.0.0.1", 0, 3))
        {
            gateway.Start();
            Check(gateway.Port > 0 && gateway.IsListening, "Gateway binds an ephemeral port.");
            var events = new List<WebSocketGateway.Event>();

            using var client = Connect(gateway.Port);
            var opened = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Opened);
            Check(opened.Client >= WebSocketGateway.FirstClientId && gateway.IsOpen(opened.Client), "A completed handshake opens a client with a websocket-range id.");
            Check(gateway.OpenConnections == 1, "One live connection is tracked.");

            byte[] hello = WebSocketMatchCodec.Envelope(WebSocketMatchCodec.Hello, Encoding.UTF8.GetBytes("{\"protocol\":3}"));
            Send(client, hello);
            var message = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Message);
            Check(message.Client == opened.Client && message.Payload.SequenceEqual(hello), "A binary message arrives unmasked and intact.");

            byte[] big = new byte[12000]; new Random(3).NextBytes(big);
            Check(gateway.Send(opened.Client, big), "The server can send a message larger than one chunk.");
            Check(Receive(client, out var received) == WebSocketMessageType.Binary && received.SequenceEqual(big), "Server frames with 16-bit lengths reach the client intact.");
            byte[] huge = new byte[70000]; new Random(4).NextBytes(huge);
            Check(gateway.Send(opened.Client, huge), "The server can send a 64-bit length frame.");
            Check(Receive(client, out received) == WebSocketMessageType.Binary && received.SequenceEqual(huge), "Server frames with 64-bit lengths reach the client intact.");

            byte[] part1 = Enumerable.Range(0, 5000).Select(i => (byte)i).ToArray(), part2 = Enumerable.Range(0, 3000).Select(i => (byte)(i * 7)).ToArray();
            client.SendAsync(part1, WebSocketMessageType.Binary, false, Token()).GetAwaiter().GetResult();
            client.SendAsync(Array.Empty<byte>(), WebSocketMessageType.Binary, false, Token()).GetAwaiter().GetResult();
            client.SendAsync(part2, WebSocketMessageType.Binary, true, Token()).GetAwaiter().GetResult();
            var assembled = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Message);
            Check(assembled.Payload.SequenceEqual(part1.Concat(part2)), "Fragmented client messages are delivered once, in order and complete.");

            client.SendAsync(Encoding.UTF8.GetBytes("text"), WebSocketMessageType.Text, true, Token()).GetAwaiter().GetResult();
            var text = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Message);
            Check(Encoding.UTF8.GetString(text.Payload) == "text", "Text frames are delivered as bytes too.");

            using (var raw = new RawClient(gateway.Port))
            {
                var second = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Opened);
                Check(second.Client > opened.Client, "A second client receives the next id.");
                raw.SendFrame(9, Encoding.ASCII.GetBytes("ping"), true);
                var pong = raw.ReadFrame();
                Check(pong.opcode == 10 && Encoding.ASCII.GetString(pong.payload) == "ping", "A client ping is answered with a pong carrying the same payload.");
                raw.SendFrame(2, new byte[WebSocketGateway.MaxInboundBytes + 1], true);
                var close = raw.ReadFrame();
                Check(close.opcode == 8 && close.payload.Length >= 2 && ((close.payload[0] << 8) | close.payload[1]) == 1009, "An oversized message is refused with close code 1009.");
                var closed = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Closed && e.Client == second.Client);
                Check(closed.Reason == "Message too large.", "The owner learns why the oversized client was dropped.");
                Check(!gateway.IsOpen(second.Client), "A closed client is no longer open.");
            }
            using (var raw = new RawClient(gateway.Port))
            {
                var third = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Opened);
                raw.SendFrame(2, new byte[] { 1, 2, 3 }, false);
                var close = raw.ReadFrame();
                Check(close.opcode == 8 && ((close.payload[0] << 8) | close.payload[1]) == 1002, "An unmasked client frame is a protocol error (1002).");
                WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Closed && e.Client == third.Client);
            }
            using (var raw = new RawClient(gateway.Port))
            {
                var fourth = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Opened);
                raw.SendFrame(8, new byte[] { 0x03, 0xE9, (byte)'b', (byte)'y', (byte)'e' }, true);
                var reply = raw.ReadFrame();
                Check(reply.opcode == 8 && ((reply.payload[0] << 8) | reply.payload[1]) == 1001, "A client close is echoed with its code.");
                var closed = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Closed && e.Client == fourth.Client);
                Check(closed.Reason == "bye", "The client's close reason is reported to the owner.");
            }

            Check(Http(gateway.Port, "GET /health HTTP/1.1\r\nHost: x\r\n\r\n").StartsWith("HTTP/1.1 200"), "A plain health probe is answered without opening a client.");
            Check(Http(gateway.Port, "GET / HTTP/1.1\r\nHost: x\r\n\r\n").StartsWith("HTTP/1.1 426"), "A non-websocket request is refused.");
            Check(Http(gateway.Port, "POST / HTTP/1.1\r\nHost: x\r\nUpgrade: websocket\r\n\r\n").StartsWith("HTTP/1.1 400"), "A non-GET upgrade is refused.");
            Check(Http(gateway.Port, "GET / HTTP/1.1\r\nHost: x\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 8\r\nSec-WebSocket-Key: dGhlIHNhbXBsZSBub25jZQ==\r\n\r\n").StartsWith("HTTP/1.1 400"), "An unsupported websocket version is refused.");
            Check(Http(gateway.Port, "GET / HTTP/1.1\r\nHost: x\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: short\r\n\r\n").StartsWith("HTTP/1.1 400"), "An invalid websocket key is refused.");
            Check(Http(gateway.Port, new string('x', WebSocketGateway.MaxRequestBytes + 10)).StartsWith("HTTP/1.1 400"), "An oversized request is refused.");
            gateway.Poll(events);
            Check(!events.Any(e => e.Kind == WebSocketGateway.EventKind.Opened), "Failed handshakes never produce events.");
            Check(gateway.OpenConnections == 1, "Rejected and closed connections release their slots.");

            using (var extra1 = Connect(gateway.Port))
            using (var extra2 = Connect(gateway.Port))
            {
                WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Opened);
                WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Opened);
                bool refused = false;
                try { using var overflow = Connect(gateway.Port); Receive(overflow, out _); }
                catch (Exception) { refused = true; }
                Check(refused, "Connections beyond the configured maximum are refused.");
            }
            WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Closed);
            WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Closed);

            gateway.Close(opened.Client, "Kicked for the test.");
            Check(Receive(client, out _) == WebSocketMessageType.Close && client.CloseStatus == WebSocketCloseStatus.NormalClosure && client.CloseStatusDescription == "Kicked for the test.", "A server close delivers its reason to the client.");
            client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", Token()).GetAwaiter().GetResult();
            var kicked = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Closed && e.Client == opened.Client);
            Check(kicked.Reason == "Kicked for the test." && !gateway.IsOpen(opened.Client), "The owner sees exactly one close event with the kick reason.");
            Check(!gateway.Send(opened.Client, new byte[] { 1 }), "Sending to a closed client fails without throwing.");

            using (var slow = new RawClient(gateway.Port))
            {
                var lazy = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Opened);
                byte[] payload = new byte[64 * 1024];
                bool overflowed = false;
                for (int i = 0; i < 4096 && !overflowed; i++) overflowed = !gateway.Send(lazy.Client, payload);
                Check(overflowed, "A peer that stops reading cannot grow the outbound queue without bound.");
                var dropped = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Closed && e.Client == lazy.Client, 10);
                Check(dropped.Reason == "Send queue overflow.", "The overflowing peer is dropped with an explicit reason.");
            }

            using var survivor = Connect(gateway.Port);
            var last = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Opened);
            gateway.Stop();
            Check(!gateway.IsListening && !gateway.IsOpen(last.Client), "Stop closes the listener and every connection.");
            var stopped = WaitFor(gateway, events, e => e.Kind == WebSocketGateway.EventKind.Closed && e.Client == last.Client);
            Check(stopped.Reason == "Server stopped.", "Stop reports the remaining connections as closed.");
            bool connectable = true;
            try { using var late = Connect(gateway.Port); } catch (Exception) { connectable = false; }
            Check(!connectable, "A stopped gateway accepts no further connections.");
        }
        return count;
    }

    private static void CodecChecks(Action<bool, string> check)
    {
        byte[] payload = { 9, 8, 7 };
        byte[] frame = WebSocketMatchCodec.Envelope(WebSocketMatchCodec.Snapshot, payload);
        check(frame.Length == 4 && frame[0] == WebSocketMatchCodec.Snapshot && frame.Skip(1).SequenceEqual(payload), "Envelope prefixes one kind byte.");
        check(WebSocketMatchCodec.TryOpen(frame, out byte kind, out byte[] opened) && kind == WebSocketMatchCodec.Snapshot && opened.SequenceEqual(payload), "TryOpen restores the kind and payload.");
        check(!WebSocketMatchCodec.TryOpen(new byte[] { 1 }, out _, out _) && !WebSocketMatchCodec.TryOpen(null, out _, out _), "Frames without a payload are rejected.");
        check(WebSocketMatchCodec.IsClientKind(WebSocketMatchCodec.Command) && !WebSocketMatchCodec.IsClientKind(WebSocketMatchCodec.Snapshot), "Client kinds are distinguished from server kinds.");
    }

    private static CancellationToken Token() => new CancellationTokenSource(Timeout).Token;

    private static ClientWebSocket Connect(int port)
    {
        var client = new ClientWebSocket();
        client.ConnectAsync(new Uri("ws://127.0.0.1:" + port + "/"), Token()).GetAwaiter().GetResult();
        return client;
    }

    private static void Send(ClientWebSocket client, byte[] payload)
        => client.SendAsync(payload, WebSocketMessageType.Binary, true, Token()).GetAwaiter().GetResult();

    private static WebSocketMessageType Receive(ClientWebSocket client, out byte[] payload)
    {
        var buffer = new byte[128 * 1024]; var stream = new MemoryStream();
        while (true)
        {
            var result = client.ReceiveAsync(buffer, Token()).GetAwaiter().GetResult();
            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) { payload = stream.ToArray(); return result.MessageType; }
        }
    }

    private static WebSocketGateway.Event WaitFor(WebSocketGateway gateway, List<WebSocketGateway.Event> events, Func<WebSocketGateway.Event, bool> predicate, int seconds = 5)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
        while (DateTime.UtcNow < deadline)
        {
            gateway.Poll(events);
            int index = events.FindIndex(e => predicate(e));
            if (index >= 0) { var item = events[index]; events.RemoveAt(index); return item; }
            Thread.Sleep(10);
        }
        throw new TimeoutException("Gateway event did not arrive.");
    }

    private static string Http(int port, string request)
    {
        using var socket = new TcpClient("127.0.0.1", port);
        socket.ReceiveTimeout = 5000;
        var stream = socket.GetStream();
        byte[] bytes = Encoding.ASCII.GetBytes(request);
        stream.Write(bytes, 0, bytes.Length);
        var response = new MemoryStream(); var buffer = new byte[1024];
        try { int read; while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) response.Write(buffer, 0, read); }
        catch (IOException) { }
        return Encoding.ASCII.GetString(response.ToArray());
    }

    // A deliberately low-level peer, able to violate the protocol or stop reading.
    private sealed class RawClient : IDisposable
    {
        private readonly TcpClient socket;
        private readonly NetworkStream stream;
        private readonly Random random = new Random(11);
        public RawClient(int port)
        {
            socket = new TcpClient("127.0.0.1", port); socket.ReceiveTimeout = 5000; stream = socket.GetStream();
            string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            byte[] request = Encoding.ASCII.GetBytes("GET /match HTTP/1.1\r\nHost: 127.0.0.1\r\nUpgrade: websocket\r\nConnection: keep-alive, Upgrade\r\nSec-WebSocket-Key: " + key + "\r\nSec-WebSocket-Version: 13\r\n\r\n");
            stream.Write(request, 0, request.Length);
            var response = new StringBuilder();
            while (!response.ToString().EndsWith("\r\n\r\n")) { int b = stream.ReadByte(); if (b < 0) throw new IOException("Handshake failed."); response.Append((char)b); }
            string expected = Convert.ToBase64String(SHA1.HashData(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            if (!response.ToString().StartsWith("HTTP/1.1 101") || !response.ToString().Contains("Sec-WebSocket-Accept: " + expected)) throw new IOException("Handshake rejected: " + response);
        }
        public void SendFrame(byte opcode, byte[] payload, bool masked)
        {
            var frame = new MemoryStream();
            frame.WriteByte((byte)(0x80 | opcode));
            int lengthBits = masked ? 0x80 : 0;
            if (payload.Length < 126) frame.WriteByte((byte)(lengthBits | payload.Length));
            else if (payload.Length <= ushort.MaxValue) { frame.WriteByte((byte)(lengthBits | 126)); frame.WriteByte((byte)(payload.Length >> 8)); frame.WriteByte((byte)payload.Length); }
            else { frame.WriteByte((byte)(lengthBits | 127)); for (int i = 7; i >= 0; i--) frame.WriteByte((byte)(((long)payload.Length >> (8 * i)) & 0xFF)); }
            var mask = new byte[4]; random.NextBytes(mask);
            if (masked) frame.Write(mask, 0, 4);
            for (int i = 0; i < payload.Length; i++) frame.WriteByte(masked ? (byte)(payload[i] ^ mask[i & 3]) : payload[i]);
            byte[] bytes = frame.ToArray(); stream.Write(bytes, 0, bytes.Length); stream.Flush();
        }
        public (int opcode, byte[] payload) ReadFrame()
        {
            int first = stream.ReadByte(), second = stream.ReadByte();
            if (first < 0 || second < 0) throw new IOException("Connection closed.");
            long length = second & 0x7F;
            if (length == 126) length = (stream.ReadByte() << 8) | stream.ReadByte();
            else if (length == 127) { length = 0; for (int i = 0; i < 8; i++) length = (length << 8) | (byte)stream.ReadByte(); }
            var payload = new byte[length]; int offset = 0;
            while (offset < payload.Length) { int read = stream.Read(payload, offset, payload.Length - offset); if (read <= 0) throw new IOException("Truncated frame."); offset += read; }
            return (first & 0x0F, payload);
        }
        public void Dispose() { socket.Close(); }
    }
}
