using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Frontier.Networking
{
    // Minimal RFC 6455 server so browser clients can join the dedicated match
    // over TCP. It is independent of Unity: sockets and threads only. Events are
    // drained on the caller's thread through Poll, so match state is never
    // touched concurrently. Payload limits bound memory per connection.
    public sealed class WebSocketGateway : IDisposable
    {
        public enum EventKind { Opened, Message, Closed }
        public struct Event
        {
            public EventKind Kind;
            public ulong Client;
            public byte[] Payload;
            public string Reason;
        }
        // Browser connections use identifiers disjoint from NGO's small client ids.
        public const ulong FirstClientId = 1UL << 40;
        public const int MaxInboundBytes = 16 * 1024, MaxRequestBytes = 4096;
        public const int MaxOutboundMessages = 1024, MaxOutboundBytes = 8 * 1024 * 1024;
        public const int HandshakeTimeoutMs = 5000, IdleTimeoutMs = 60000, PingIntervalMs = 15000, CloseTimeoutMs = 3000;
        private const string AcceptGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        private sealed class ProtocolViolation : Exception
        {
            public readonly ushort Code;
            public ProtocolViolation(ushort code, string message) : base(message) { Code = code; }
        }
        private struct Frame { public byte Opcode; public byte[] Payload; }
        private sealed class Connection
        {
            public ulong Id;
            public Socket Socket;
            public NetworkStream Stream;
            public Thread Reader, Writer;
            public readonly BlockingCollection<Frame> Outbound = new BlockingCollection<Frame>(new ConcurrentQueue<Frame>());
            public long OutboundBytes;
            public int Closing, Opened, ClosedReported, Released;
            public volatile string CloseReason;
        }

        private readonly ConcurrentQueue<Event> events = new ConcurrentQueue<Event>();
        private readonly Dictionary<ulong, Connection> connections = new Dictionary<ulong, Connection>();
        private readonly object sync = new object();
        private readonly string listenAddress;
        private readonly int requestedPort, maxConnections;
        private TcpListener listener;
        private Thread acceptor;
        private ulong nextClient = FirstClientId;
        private volatile bool stopped;
        public Action<string> Log;
        public int Port { get; private set; }
        public bool IsListening => listener != null && !stopped;
        public int OpenConnections { get { lock (sync) return connections.Count; } }

        public WebSocketGateway(string listenAddress, int port, int maxConnections = 8)
        {
            if (port < 0 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
            if (maxConnections < 1) throw new ArgumentOutOfRangeException(nameof(maxConnections));
            this.listenAddress = string.IsNullOrWhiteSpace(listenAddress) ? "0.0.0.0" : listenAddress.Trim();
            requestedPort = port; this.maxConnections = maxConnections;
        }

        public void Start()
        {
            if (listener != null) throw new InvalidOperationException("The gateway is already listening.");
            if (!IPAddress.TryParse(listenAddress, out var address)) throw new ArgumentException("Invalid listen address: " + listenAddress);
            var socket = new TcpListener(address, requestedPort);
            socket.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            socket.Start(16);
            listener = socket; Port = ((IPEndPoint)socket.LocalEndpoint).Port;
            acceptor = new Thread(Accept) { IsBackground = true, Name = "frontier-ws-accept" };
            acceptor.Start();
        }

        public void Stop()
        {
            if (stopped) return;
            stopped = true;
            try { listener?.Stop(); } catch (Exception) { }
            Connection[] open;
            lock (sync) open = new List<Connection>(connections.Values).ToArray();
            foreach (var connection in open) { connection.CloseReason = connection.CloseReason ?? "Server stopped."; Shutdown(connection, true); }
            var thread = acceptor; acceptor = null;
            if (thread != null && thread != Thread.CurrentThread) thread.Join(1000);
        }

        public void Dispose() => Stop();

        // Drained on the owner's thread. Failed handshakes produce no events.
        public void Poll(List<Event> into)
        {
            while (events.TryDequeue(out var item)) into.Add(item);
        }

        public bool IsOpen(ulong client)
        {
            lock (sync) return connections.TryGetValue(client, out var connection) && connection.Closing == 0;
        }

        public bool Send(ulong client, byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            Connection connection;
            lock (sync) if (!connections.TryGetValue(client, out connection)) return false;
            if (connection.Closing != 0) return false;
            // A receiver which stops reading cannot grow the server's memory without bound.
            if (connection.Outbound.Count >= MaxOutboundMessages || Interlocked.Read(ref connection.OutboundBytes) + payload.Length > MaxOutboundBytes)
            {
                // A close frame would queue behind the backlog; the peer is not reading anyway.
                connection.CloseReason = "Send queue overflow.";
                Shutdown(connection, true); return false;
            }
            return Enqueue(connection, 2, payload);
        }

        public void Close(ulong client, string reason, ushort code = 1000)
        {
            Connection connection;
            lock (sync) if (!connections.TryGetValue(client, out connection)) return;
            if (Interlocked.CompareExchange(ref connection.Closing, 1, 0) != 0) return;
            connection.CloseReason = string.IsNullOrEmpty(reason) ? "Connection closed by the server." : reason;
            try { connection.Socket.ReceiveTimeout = CloseTimeoutMs; } catch (Exception) { }
            if (!Enqueue(connection, 8, ClosePayload(code, reason))) Shutdown(connection, true);
        }

        private bool Enqueue(Connection connection, byte opcode, byte[] payload)
        {
            try
            {
                Interlocked.Add(ref connection.OutboundBytes, payload.Length);
                if (connection.Outbound.TryAdd(new Frame { Opcode = opcode, Payload = payload })) return true;
                Interlocked.Add(ref connection.OutboundBytes, -payload.Length);
            }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            return false;
        }

        private void Accept()
        {
            while (!stopped)
            {
                Socket socket;
                try { socket = listener.AcceptSocket(); }
                catch (Exception) { if (stopped) return; continue; }
                Connection connection = null;
                lock (sync)
                {
                    if (connections.Count < maxConnections && !stopped)
                    {
                        connection = new Connection { Id = nextClient++, Socket = socket };
                        connections.Add(connection.Id, connection);
                    }
                }
                if (connection == null) { try { socket.Close(); } catch (Exception) { } continue; }
                try
                {
                    socket.NoDelay = true;
                    connection.Stream = new NetworkStream(socket, false);
                    connection.Reader = new Thread(() => Serve(connection)) { IsBackground = true, Name = "frontier-ws-reader" };
                    connection.Reader.Start();
                }
                catch (Exception error)
                {
                    Log?.Invoke("WebSocket accept failed: " + error.Message);
                    Shutdown(connection, false);
                }
            }
        }

        private void Serve(Connection connection)
        {
            try
            {
                connection.Socket.ReceiveTimeout = HandshakeTimeoutMs;
                if (!Handshake(connection)) { Shutdown(connection, false); return; }
                connection.Socket.ReceiveTimeout = IdleTimeoutMs;
                Interlocked.Exchange(ref connection.Opened, 1);
                events.Enqueue(new Event { Kind = EventKind.Opened, Client = connection.Id });
                connection.Writer = new Thread(() => Write(connection)) { IsBackground = true, Name = "frontier-ws-writer" };
                connection.Writer.Start();
                ReadFrames(connection);
            }
            catch (ProtocolViolation violation)
            {
                if (connection.CloseReason == null) connection.CloseReason = violation.Message;
                // A violating peer still receives the reason before the socket closes.
                if (Interlocked.CompareExchange(ref connection.Closing, 1, 0) == 0) Enqueue(connection, 8, ClosePayload(violation.Code, violation.Message));
            }
            catch (Exception error)
            {
                if (connection.CloseReason == null) connection.CloseReason = Describe(error);
            }
            finally { Finish(connection); }
        }

        // Graceful close from the reader thread: flush queued frames (including a
        // close), send FIN, then drain so the peer reads our close before any RST.
        private void Finish(Connection connection)
        {
            if (connection.Released != 0) return;
            Interlocked.Exchange(ref connection.Closing, 1);
            try { connection.Outbound.CompleteAdding(); } catch (Exception) { }
            var writer = connection.Writer;
            if (writer != null && writer != Thread.CurrentThread) writer.Join(CloseTimeoutMs);
            try { connection.Socket.Shutdown(SocketShutdown.Send); } catch (Exception) { }
            try
            {
                connection.Socket.ReceiveTimeout = CloseTimeoutMs;
                var buffer = new byte[1024];
                while (connection.Released == 0 && connection.Stream.Read(buffer, 0, buffer.Length) > 0) { }
            }
            catch (Exception) { }
            Shutdown(connection, true);
        }

        private bool Handshake(Connection connection)
        {
            var request = new MemoryStream();
            var buffer = new byte[1];
            while (true)
            {
                int read = connection.Stream.Read(buffer, 0, 1);
                if (read <= 0) return false;
                request.WriteByte(buffer[0]);
                if (request.Length > MaxRequestBytes) { Respond(connection, "400 Bad Request", "Request too large."); return false; }
                if (request.Length >= 4 && EndsWithBlankLine(request)) break;
            }
            string text = Encoding.ASCII.GetString(request.GetBuffer(), 0, (int)request.Length);
            string[] lines = text.Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] requestLine = lines[0].Split(' ');
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < lines.Length; i++)
            {
                int colon = lines[i].IndexOf(':');
                if (colon <= 0) continue;
                headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
            }
            if (requestLine.Length != 3 || requestLine[0] != "GET" || !requestLine[2].StartsWith("HTTP/1.", StringComparison.Ordinal))
            { Respond(connection, "400 Bad Request", "Expected an HTTP GET upgrade."); return false; }
            if (!headers.ContainsKey("Upgrade"))
            {
                // Docker/compose health checks can probe the listener without a websocket client.
                if (requestLine[1] == "/health") Respond(connection, "200 OK", "ok");
                else Respond(connection, "426 Upgrade Required", "This endpoint only accepts websocket connections.");
                return false;
            }
            if (!headers.TryGetValue("Upgrade", out string upgrade) || !upgrade.Equals("websocket", StringComparison.OrdinalIgnoreCase)
                || !headers.TryGetValue("Connection", out string connectionHeader) || connectionHeader.IndexOf("upgrade", StringComparison.OrdinalIgnoreCase) < 0
                || !headers.TryGetValue("Sec-WebSocket-Version", out string version) || version.Trim() != "13"
                || !headers.TryGetValue("Sec-WebSocket-Key", out string key) || !ValidKey(key))
            { Respond(connection, "400 Bad Request", "Invalid websocket handshake."); return false; }
            string accept;
            using (var sha = SHA1.Create()) accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key.Trim() + AcceptGuid)));
            byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n");
            connection.Stream.Write(response, 0, response.Length); connection.Stream.Flush();
            return true;
        }

        private static bool EndsWithBlankLine(MemoryStream request)
        {
            byte[] bytes = request.GetBuffer(); long n = request.Length;
            return bytes[n - 4] == '\r' && bytes[n - 3] == '\n' && bytes[n - 2] == '\r' && bytes[n - 1] == '\n';
        }

        private static bool ValidKey(string key)
        {
            try { return Convert.FromBase64String(key.Trim()).Length == 16; }
            catch (FormatException) { return false; }
        }

        private static void Respond(Connection connection, string status, string body)
        {
            try
            {
                byte[] content = Encoding.UTF8.GetBytes(body);
                byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + "\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: " + content.Length + "\r\nConnection: close\r\n\r\n");
                connection.Stream.Write(head, 0, head.Length); connection.Stream.Write(content, 0, content.Length); connection.Stream.Flush();
            }
            catch (Exception) { }
        }

        private void ReadFrames(Connection connection)
        {
            var header = new byte[14]; var mask = new byte[4];
            MemoryStream fragments = null;
            while (!stopped)
            {
                ReadExact(connection.Stream, header, 0, 2);
                bool fin = (header[0] & 0x80) != 0; int opcode = header[0] & 0x0F;
                bool masked = (header[1] & 0x80) != 0; long length = header[1] & 0x7F;
                if ((header[0] & 0x70) != 0) throw new ProtocolViolation(1002, "Reserved bits are not supported.");
                if (!masked) throw new ProtocolViolation(1002, "Client frames must be masked.");
                if (length == 126) { ReadExact(connection.Stream, header, 2, 2); length = (header[2] << 8) | header[3]; }
                else if (length == 127)
                {
                    ReadExact(connection.Stream, header, 2, 8);
                    length = 0; for (int i = 2; i < 10; i++) length = (length << 8) | header[i];
                    if (length < 0) throw new ProtocolViolation(1009, "Message too large.");
                }
                bool control = opcode >= 8;
                if (control && (!fin || length > 125)) throw new ProtocolViolation(1002, "Invalid control frame.");
                long pending = fragments?.Length ?? 0;
                if (length > MaxInboundBytes || (!control && pending + length > MaxInboundBytes)) throw new ProtocolViolation(1009, "Message too large.");
                ReadExact(connection.Stream, mask, 0, 4);
                var payload = new byte[length];
                ReadExact(connection.Stream, payload, 0, payload.Length);
                for (int i = 0; i < payload.Length; i++) payload[i] ^= mask[i & 3];
                switch (opcode)
                {
                    case 0:
                        if (fragments == null) throw new ProtocolViolation(1002, "Unexpected continuation frame.");
                        fragments.Write(payload, 0, payload.Length);
                        if (fin) { Deliver(connection, fragments.ToArray()); fragments = null; }
                        break;
                    case 1:
                    case 2:
                        if (fragments != null) throw new ProtocolViolation(1002, "Interleaved message fragments.");
                        if (fin) Deliver(connection, payload);
                        else { fragments = new MemoryStream(); fragments.Write(payload, 0, payload.Length); }
                        break;
                    case 8:
                        ushort code = payload.Length >= 2 ? (ushort)((payload[0] << 8) | payload[1]) : (ushort)1005;
                        string reason = payload.Length > 2 ? Encoding.UTF8.GetString(payload, 2, payload.Length - 2) : "";
                        if (connection.CloseReason == null)
                            connection.CloseReason = string.IsNullOrEmpty(reason) ? "Connection closed by the client (" + code + ")." : reason;
                        if (Interlocked.CompareExchange(ref connection.Closing, 1, 0) == 0)
                            Enqueue(connection, 8, ClosePayload(code == 1005 ? (ushort)1000 : code, ""));
                        return;
                    case 9:
                        if (connection.Closing == 0) Enqueue(connection, 10, payload);
                        break;
                    case 10:
                        break;
                    default:
                        throw new ProtocolViolation(1002, "Unknown frame opcode.");
                }
            }
        }

        private void Deliver(Connection connection, byte[] payload)
        {
            if (connection.Closing != 0) return;
            events.Enqueue(new Event { Kind = EventKind.Message, Client = connection.Id, Payload = payload });
        }

        private void Write(Connection connection)
        {
            try
            {
                while (true)
                {
                    if (!connection.Outbound.TryTake(out Frame frame, PingIntervalMs))
                    {
                        if (connection.Outbound.IsCompleted) return;
                        // Keep NAT mappings and browsers' idle timers alive; a dead peer fails the write.
                        frame = new Frame { Opcode = 9, Payload = Array.Empty<byte>() };
                    }
                    else Interlocked.Add(ref connection.OutboundBytes, -frame.Payload.Length);
                    WriteFrame(connection.Stream, frame.Opcode, frame.Payload);
                    // After a close frame the reader finishes the handshake: it waits for the
                    // peer's close or the close timeout, then releases the socket.
                    if (frame.Opcode == 8) return;
                }
            }
            catch (Exception error)
            {
                if (connection.CloseReason == null) connection.CloseReason = Describe(error);
                Shutdown(connection, true);
            }
        }

        private static void WriteFrame(NetworkStream stream, byte opcode, byte[] payload)
        {
            var header = new byte[10]; int size = 2;
            header[0] = (byte)(0x80 | opcode);
            if (payload.Length < 126) header[1] = (byte)payload.Length;
            else if (payload.Length <= ushort.MaxValue) { header[1] = 126; header[2] = (byte)(payload.Length >> 8); header[3] = (byte)payload.Length; size = 4; }
            else
            {
                header[1] = 127; long length = payload.Length;
                for (int i = 9; i >= 2; i--) { header[i] = (byte)(length & 0xFF); length >>= 8; }
                size = 10;
            }
            lock (stream)
            {
                stream.Write(header, 0, size);
                if (payload.Length > 0) stream.Write(payload, 0, payload.Length);
                stream.Flush();
            }
        }

        private static byte[] ClosePayload(ushort code, string reason)
        {
            byte[] text = Encoding.UTF8.GetBytes(reason ?? "");
            if (text.Length > 123) Array.Resize(ref text, 123);
            var payload = new byte[2 + text.Length];
            payload[0] = (byte)(code >> 8); payload[1] = (byte)code;
            Buffer.BlockCopy(text, 0, payload, 2, text.Length);
            return payload;
        }

        private static void ReadExact(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = stream.Read(buffer, offset, count);
                if (read <= 0) throw new EndOfStreamException("Connection closed.");
                offset += read; count -= read;
            }
        }

        private static string Describe(Exception error)
        {
            if (error is EndOfStreamException) return "Connection closed.";
            if (error is IOException io && io.InnerException is SocketException socket)
                return socket.SocketErrorCode == SocketError.TimedOut ? "Connection timed out." : "Connection lost (" + socket.SocketErrorCode + ").";
            if (error is ObjectDisposedException) return "Connection closed.";
            return "Connection lost.";
        }

        private void Shutdown(Connection connection, bool report)
        {
            if (Interlocked.CompareExchange(ref connection.Released, 1, 0) != 0) return;
            Interlocked.Exchange(ref connection.Closing, 1);
            lock (sync) connections.Remove(connection.Id);
            try { connection.Outbound.CompleteAdding(); } catch (Exception) { }
            try { connection.Socket.Shutdown(SocketShutdown.Both); } catch (Exception) { }
            try { connection.Socket.Close(); } catch (Exception) { }
            if (report && connection.Opened != 0 && Interlocked.CompareExchange(ref connection.ClosedReported, 1, 0) == 0)
                events.Enqueue(new Event { Kind = EventKind.Closed, Client = connection.Id, Reason = connection.CloseReason ?? "Connection closed." });
        }
    }
}
