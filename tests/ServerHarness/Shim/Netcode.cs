using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frontier.Harness.Shim;
using UnityEngine;

namespace Unity.Collections
{
    public enum Allocator { Invalid = 0, None = 1, Temp = 2, TempJob = 3, Persistent = 4 }
}

namespace Unity.Netcode
{
    public enum NetworkDelivery { Unreliable, UnreliableSequenced, Reliable, ReliableSequenced, ReliableFragmentedSequenced }

    public struct FastBufferReader : IDisposable
    {
        private sealed class State { public byte[] data; public int position; }
        private readonly State state;
        internal FastBufferReader(byte[] data) { state = new State { data = data ?? Array.Empty<byte>() }; }
        public int Length => state?.data.Length ?? 0;
        public int Position => state?.position ?? 0;
        public void ReadBytesSafe(ref byte[] value, int bytesToRead, int offset = 0)
        {
            if (bytesToRead < 0 || state.position + bytesToRead > state.data.Length) throw new OverflowException("Reading past the end of the buffer.");
            if (value == null || value.Length < offset + bytesToRead) value = new byte[offset + bytesToRead];
            Buffer.BlockCopy(state.data, state.position, value, offset, bytesToRead);
            state.position += bytesToRead;
        }
        public void Dispose() { }
    }

    public struct FastBufferWriter : IDisposable
    {
        private readonly MemoryStream stream;
        public FastBufferWriter(int size, Unity.Collections.Allocator allocator, int maxSize = -1) { stream = new MemoryStream(Math.Max(0, size)); }
        public void WriteBytesSafe(byte[] value, int size = -1, int offset = 0) => stream.Write(value, offset, size < 0 ? value.Length - offset : size);
        internal byte[] ToArray() => stream.ToArray();
        public void Dispose() { }
    }

    public abstract class NetworkTransport : MonoBehaviour
    {
        public enum DisconnectEvents { TransportClosed, ProtocolError, MaxConnectionAttempts, Disconnected, Timeout }
        public abstract ulong GetCurrentRtt(ulong clientId);
    }

    public sealed class NetworkConfig
    {
        public NetworkTransport NetworkTransport;
        public bool EnableSceneManagement = true, ConnectionApproval, ForceSamePrefabs = true;
        public uint TickRate = 30;
        public int ClientConnectionBufferTimeout = 10;
        public ushort ProtocolVersion;
        public byte[] ConnectionData = Array.Empty<byte>();
    }

    public struct NetworkTime { public double Time; }
    public sealed class PendingClient { public ulong ClientId; }

    public sealed class CustomMessagingManager
    {
        public delegate void HandleNamedMessageDelegate(ulong senderClientId, FastBufferReader messagePayload);
        internal readonly Dictionary<string, HandleNamedMessageDelegate> handlers = new Dictionary<string, HandleNamedMessageDelegate>();
        internal NetworkManager owner;
        public void RegisterNamedMessageHandler(string name, HandleNamedMessageDelegate callback) => handlers[name] = callback;
        public void UnregisterNamedMessageHandler(string name) => handlers.Remove(name);
        public void SendNamedMessage(string messageName, ulong clientId, FastBufferWriter messageStream, NetworkDelivery networkDelivery = NetworkDelivery.ReliableSequenced)
            => owner.Route(messageName, clientId, messageStream.ToArray());
    }

    // In-process NGO stand-in: real connection approval (including pending responses
    // and the buffer timeout), named messages delivered in order on the next frame,
    // and disconnect callbacks on both peers. Only for the development harness.
    public class NetworkManager : MonoBehaviour
    {
        public sealed class ConnectionApprovalRequest { public byte[] Payload; public ulong ClientNetworkId; }
        public sealed class ConnectionApprovalResponse
        {
            public bool Approved, CreatePlayerObject, Pending;
            public uint? PlayerPrefabHash;
            public string Reason;
        }
        private sealed class Approval { public NetworkManager client; public ConnectionApprovalResponse response; public double started; }

        public const ulong ServerClientId = 0;
        private static readonly Dictionary<ushort, NetworkManager> servers = new Dictionary<ushort, NetworkManager>();
        private static readonly List<NetworkManager> managers = new List<NetworkManager>();
        private static ulong nextClientId = 1;
        private static bool hooked;

        // The shim allows a server and several clients in one process.
        public static NetworkManager Singleton => null;
        public NetworkConfig NetworkConfig;
        public Action<ConnectionApprovalRequest, ConnectionApprovalResponse> ConnectionApprovalCallback;
        public event Action<ulong> OnClientConnectedCallback;
        public event Action<ulong> OnClientDisconnectCallback;
        public CustomMessagingManager CustomMessagingManager { get; }
        public bool IsListening { get; private set; }
        public bool IsServer { get; private set; }
        public bool IsClient { get; private set; }
        public bool IsConnectedClient { get; private set; }
        public ulong LocalClientId { get; private set; }
        public string DisconnectReason { get; private set; } = "";
        public NetworkTransport.DisconnectEvents DisconnectEvent { get; private set; }
        public NetworkTime ServerTime => new NetworkTime { Time = ShimRuntime.Clock };
        private readonly Dictionary<ulong, PendingClient> pendingClients = new Dictionary<ulong, PendingClient>();
        public Dictionary<ulong, PendingClient> PendingClients => pendingClients;
        private readonly Dictionary<ulong, Approval> approvals = new Dictionary<ulong, Approval>();
        private readonly Dictionary<ulong, NetworkManager> clients = new Dictionary<ulong, NetworkManager>();
        private readonly Queue<Action> inbox = new Queue<Action>();
        private NetworkManager server;
        private ushort port;

        public NetworkManager()
        {
            CustomMessagingManager = new CustomMessagingManager { owner = this };
            managers.Add(this);
            if (!hooked) { hooked = true; ShimRuntime.EarlyUpdate += ProcessAll; }
        }

        private ushort Port() => (NetworkConfig?.NetworkTransport as Unity.Netcode.Transports.UTP.UnityTransport)?.Port ?? 0;

        public bool StartServer()
        {
            port = Port();
            if (port == 0 || servers.ContainsKey(port)) return false;
            servers.Add(port, this); IsServer = IsListening = true;
            return true;
        }

        public bool StartClient()
        {
            IsClient = IsListening = true;
            ushort target = Port();
            if (!servers.TryGetValue(target, out var host) || !host.IsListening)
            {
                inbox.Enqueue(() => Lost("Failed to connect to server."));
                return true;
            }
            server = host; LocalClientId = nextClientId++;
            host.inbox.Enqueue(() => host.BeginApproval(this));
            return true;
        }

        private void BeginApproval(NetworkManager client)
        {
            if (!IsListening || client.server != this) return;
            ulong id = client.LocalClientId;
            pendingClients[id] = new PendingClient { ClientId = id };
            var approval = new Approval { client = client, response = new ConnectionApprovalResponse(), started = ShimRuntime.Clock };
            approvals[id] = approval;
            if (NetworkConfig != null && NetworkConfig.ConnectionApproval && ConnectionApprovalCallback != null)
                ConnectionApprovalCallback(new ConnectionApprovalRequest { ClientNetworkId = id, Payload = client.NetworkConfig.ConnectionData }, approval.response);
            else approval.response.Approved = true;
        }

        private void CheckApprovals()
        {
            foreach (var pair in approvals.ToArray())
            {
                var approval = pair.Value;
                bool expired = ShimRuntime.Clock - approval.started > (NetworkConfig?.ClientConnectionBufferTimeout ?? 10);
                if (approval.response.Pending && !expired) continue;
                approvals.Remove(pair.Key); pendingClients.Remove(pair.Key);
                var client = approval.client;
                if (approval.response.Approved && !approval.response.Pending)
                {
                    clients[pair.Key] = client;
                    OnClientConnectedCallback?.Invoke(pair.Key);
                    client.inbox.Enqueue(() => { client.IsConnectedClient = true; client.OnClientConnectedCallback?.Invoke(client.LocalClientId); });
                }
                else
                {
                    string reason = approval.response.Pending ? "Connection approval timed out." : approval.response.Reason ?? "";
                    client.inbox.Enqueue(() => client.Lost(reason));
                }
            }
        }

        internal void Route(string name, ulong target, byte[] bytes)
        {
            if (IsServer)
            {
                if (!clients.TryGetValue(target, out var client)) return;
                client.inbox.Enqueue(() => client.Deliver(name, ServerClientId, bytes));
            }
            else if (server != null && IsConnectedClient && target == ServerClientId)
            {
                var host = server; ulong sender = LocalClientId;
                host.inbox.Enqueue(() => { if (host.clients.ContainsKey(sender)) host.Deliver(name, sender, bytes); });
            }
        }

        private void Deliver(string name, ulong sender, byte[] bytes)
        {
            if (CustomMessagingManager.handlers.TryGetValue(name, out var handler)) handler(sender, new FastBufferReader(bytes));
        }

        public void DisconnectClient(ulong clientId, string reason)
        {
            if (!IsServer) return;
            if (clients.TryGetValue(clientId, out var client))
            {
                clients.Remove(clientId);
                client.inbox.Enqueue(() => client.Lost(reason));
                inbox.Enqueue(() => OnClientDisconnectCallback?.Invoke(clientId));
            }
            else if (approvals.TryGetValue(clientId, out var approval))
            {
                approvals.Remove(clientId); pendingClients.Remove(clientId);
                approval.client.inbox.Enqueue(() => approval.client.Lost(reason));
            }
        }

        private void Lost(string reason)
        {
            if (!IsClient) return;
            bool wasListening = IsListening;
            IsConnectedClient = false; IsListening = false; server = null;
            DisconnectReason = reason ?? ""; DisconnectEvent = NetworkTransport.DisconnectEvents.Disconnected;
            if (wasListening) OnClientDisconnectCallback?.Invoke(LocalClientId);
        }

        public void Shutdown()
        {
            if (IsServer)
            {
                foreach (var pair in clients.ToArray()) pair.Value.inbox.Enqueue(() => pair.Value.Lost("The server shut down."));
                foreach (var pair in approvals.ToArray()) pair.Value.client.inbox.Enqueue(() => pair.Value.client.Lost("The server shut down."));
                clients.Clear(); approvals.Clear(); pendingClients.Clear();
                if (servers.TryGetValue(port, out var registered) && registered == this) servers.Remove(port);
            }
            else if (server != null)
            {
                var host = server; ulong id = LocalClientId;
                host.inbox.Enqueue(() =>
                {
                    if (host.clients.Remove(id)) host.OnClientDisconnectCallback?.Invoke(id);
                    else if (host.approvals.Remove(id)) host.pendingClients.Remove(id);
                });
            }
            IsListening = IsServer = IsClient = IsConnectedClient = false; server = null;
            inbox.Clear();
        }

        private void OnDestroy() { if (IsListening) Shutdown(); managers.Remove(this); }

        private static void ProcessAll()
        {
            foreach (var manager in managers.ToArray())
            {
                int budget = manager.inbox.Count;
                while (budget-- > 0 && manager.inbox.Count > 0)
                {
                    try { manager.inbox.Dequeue()(); }
                    catch (Exception error) { UnityEngine.Debug.LogException(error); }
                }
                if (manager.IsServer) manager.CheckApprovals();
            }
        }
    }
}

namespace Unity.Netcode.Transports.UTP
{
    public sealed class UnityTransport : NetworkTransport
    {
        public int MaxPacketQueueSize { get; set; } = 128;
        internal ushort Port;
        internal string Address, ListenAddress;
        public void SetConnectionData(string ipv4Address, ushort port, string listenAddress = null)
        { Address = ipv4Address; Port = port; ListenAddress = listenAddress; }
        public override ulong GetCurrentRtt(ulong clientId) => 0;
    }
}
