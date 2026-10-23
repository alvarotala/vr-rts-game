using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Frontier.Core;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Frontier.Networking
{
    internal static class MatchProtocol
    {
        public const int Version = 3, MaxCommandBytes = 4096, MaxHelloBytes = 512;
        public const int StartCountdownSeconds = 3;
        public const string Ready = "frontier.ready", Welcome = "frontier.welcome", Command = "frontier.command", Ack = "frontier.ack";
        public const string Snapshot = "frontier.snapshot", Result = "frontier.result", ResultAck = "frontier.resultAck", Status = "frontier.status";
        public static readonly byte[] Signal = { 1 };
        private static string contentHash;

        public static string ContentHash()
        {
            if (contentHash != null) return contentHash;
            var catalog = Resources.Load<TextAsset>("catalog");
            if (catalog == null) throw new InvalidOperationException("Catalog is missing.");
            var text = new StringBuilder(catalog.text);
            foreach (var map in Resources.LoadAll<TextAsset>("Maps").OrderBy(m => m.name, StringComparer.Ordinal)) text.Append('\n').Append(map.name).Append('\n').Append(map.text);
            using (var sha = SHA256.Create()) contentHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "");
            return contentHash;
        }

        public static byte[] Encode<T>(T value, int maxBytes)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(value));
            if (bytes.Length > maxBytes) throw new InvalidOperationException("Network message is too large.");
            return bytes;
        }

        public static T Decode<T>(byte[] bytes) => JsonUtility.FromJson<T>(Encoding.UTF8.GetString(bytes));

        public static byte[] Read(FastBufferReader reader, int limit)
        {
            int length = reader.Length - reader.Position;
            if (length <= 0 || length > limit) throw new InvalidOperationException("Invalid network message length.");
            var bytes = new byte[length]; reader.ReadBytesSafe(ref bytes, length); return bytes;
        }

        public static void Send(NetworkManager manager, string name, ulong target, byte[] bytes, NetworkDelivery delivery = NetworkDelivery.ReliableFragmentedSequenced)
        {
            using (var writer = new FastBufferWriter(bytes.Length, Allocator.Temp))
            {
                writer.WriteBytesSafe(bytes);
                manager.CustomMessagingManager.SendNamedMessage(name, target, writer, delivery);
            }
        }

        public static NetworkManager CreateManager(string label, string address, ushort port, string listenAddress = null)
        {
            if (NetworkManager.Singleton != null) throw new InvalidOperationException("A network session is already active.");
            var go = new GameObject(label);
            var manager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            transport.MaxPacketQueueSize = 512;
            transport.SetConnectionData(address, port, listenAddress);
            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport, EnableSceneManagement = false, ConnectionApproval = true,
                TickRate = Simulation.TickRate, ForceSamePrefabs = false, ClientConnectionBufferTimeout = 10,
                ProtocolVersion = Version
            };
            return manager;
        }

        public static void Emit<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList())
                try { handler(value); } catch (Exception error) { Debug.LogException(error); }
        }
    }
}
