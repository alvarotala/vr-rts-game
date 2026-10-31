using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Frontier.Networking
{
    // Browser players join through the websocket gateway and occupy the same two
    // seats as NGO players: identical hello validation, admission, readiness,
    // commands, snapshots and results. Only the transport differs.
    public sealed partial class NgoMatchServer
    {
        private sealed class WebSocketClient
        {
            public double openedAt;
            public bool helloReceived;
        }
        public const double WebSocketHelloTimeoutSeconds = 5;
        // Default websocket port: the UDP game port plus this offset (7777 -> 7877, 7778 -> 7878).
        public const int WebSocketPortOffset = 100;
        public int WebSocketPort => gateway?.Port ?? 0;
        private WebSocketGateway gateway;
        private readonly Dictionary<ulong, WebSocketClient> webSocketClients = new Dictionary<ulong, WebSocketClient>();
        private readonly List<WebSocketGateway.Event> gatewayEvents = new List<WebSocketGateway.Event>();

        private static bool IsWebSocketClient(ulong client) => client >= WebSocketGateway.FirstClientId;

        private static byte WebSocketKind(string name)
        {
            if (name == MatchProtocol.Welcome) return WebSocketMatchCodec.Welcome;
            if (name == MatchProtocol.Snapshot) return WebSocketMatchCodec.Snapshot;
            if (name == MatchProtocol.Ack) return WebSocketMatchCodec.Ack;
            if (name == MatchProtocol.Result) return WebSocketMatchCodec.Result;
            if (name == MatchProtocol.Status) return WebSocketMatchCodec.Status;
            throw new ArgumentException("Unknown server message: " + name);
        }

        private void StartWebSockets(ushort port, string listenAddress)
        {
            var listener = new WebSocketGateway(listenAddress, port, 8);
            listener.Log = message => UnityEngine.Debug.LogWarning("FRONTIER_SERVER " + message);
            listener.Start();
            gateway = listener;
            webSocketClients.Clear();
        }

        private void StopWebSockets()
        {
            var listener = gateway; gateway = null;
            webSocketClients.Clear();
            listener?.Stop();
        }

        // Called from Update on the main thread; the gateway threads never touch match state.
        private void PollWebSockets()
        {
            if (gateway == null) return;
            double now = Time.realtimeSinceStartupAsDouble;
            gatewayEvents.Clear();
            gateway.Poll(gatewayEvents);
            foreach (var item in gatewayEvents)
            {
                if (closing) return;
                switch (item.Kind)
                {
                    case WebSocketGateway.EventKind.Opened:
                        webSocketClients[item.Client] = new WebSocketClient { openedAt = now };
                        break;
                    case WebSocketGateway.EventKind.Message:
                        ReceiveWebSocketMessage(item.Client, item.Payload);
                        break;
                    case WebSocketGateway.EventKind.Closed:
                        webSocketClients.Remove(item.Client);
                        HandleDisconnect(item.Client, "WebSocket", item.Reason);
                        break;
                }
            }
            foreach (var pair in webSocketClients.ToArray())
                if (!pair.Value.helloReceived && now - pair.Value.openedAt > WebSocketHelloTimeoutSeconds)
                    Kick(pair.Key, "Timed out waiting for the connection request.");
        }

        private void ReceiveWebSocketMessage(ulong client, byte[] frame)
        {
            if (!webSocketClients.TryGetValue(client, out var state)) { Kick(client, "Unknown connection."); return; }
            if (!WebSocketMatchCodec.TryOpen(frame, out byte kind, out byte[] payload) || !WebSocketMatchCodec.IsClientKind(kind))
            { Kick(client, "Malformed network message."); return; }
            if (!state.helloReceived)
            {
                if (kind != WebSocketMatchCodec.Hello) { Kick(client, "Expected a connection request."); return; }
                state.helloReceived = true;
                ReceiveWebSocketHello(client, payload);
                return;
            }
            switch (kind)
            {
                case WebSocketMatchCodec.Hello: Kick(client, "Duplicate connection request."); break;
                case WebSocketMatchCodec.Ready: Ready(client, payload); break;
                case WebSocketMatchCodec.Command: Command(client, payload); break;
                case WebSocketMatchCodec.ResultAck: ResultAcknowledged(client, payload); break;
            }
        }

        // The hello is the websocket equivalent of NGO's connection approval payload.
        private void ReceiveWebSocketHello(ulong client, byte[] payload)
        {
            string reason;
            try { reason = ValidateHello(payload, out var hello); if (reason == null) { AdmitWebSocket(client, hello); return; } }
            catch { reason = "Invalid connection request."; }
            Kick(client, reason);
        }

        private void AdmitWebSocket(ulong client, ConnectionHello hello)
        {
            if (AllocatedMode)
            {
                if (!BeginAllocatedAdmission(client, hello, null, (approved, denial) =>
                {
                    if (approved) Connected(client); else Kick(client, denial);
                }, out string reason)) Kick(client, reason);
                return;
            }
            AddDirectSeat(client, hello.faction);
            Connected(client);
        }

        private bool ClientIsPending(ulong client)
        {
            if (IsWebSocketClient(client)) return gateway != null && gateway.IsOpen(client) && webSocketClients.ContainsKey(client) && !seats.ContainsKey(client);
            return manager != null && manager.PendingClients.ContainsKey(client);
        }
    }
}
