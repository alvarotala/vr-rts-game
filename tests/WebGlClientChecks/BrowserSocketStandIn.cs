using System;
using System.Collections.Generic;
using UnityEngine;

namespace Frontier.Networking
{
    // The tests drive browser events explicitly. No network or browser API is
    // replaced in the shipped player, which uses BrowserMatchSocket.cs + .jslib.
    public sealed class BrowserMatchSocket : MonoBehaviour
    {
        public static BrowserMatchSocket Latest;
        public event Action Opened, Closed, Failed;
        public event Action<byte[]> Received;
        public bool IsOpen { get; private set; }
        public readonly List<byte[]> Sent = new List<byte[]>();
        public bool RefuseSend;
        public string Url;
        public bool Connect(string url) { Url = url; Latest = this; return true; }
        public bool Send(byte[] bytes)
        {
            if (!IsOpen || RefuseSend) return false;
            Sent.Add(bytes); return true;
        }
        public void Close() { IsOpen = false; }
        public void Open() { IsOpen = true; Opened?.Invoke(); }
        public void Receive(byte kind, byte[] bytes) => Received?.Invoke(WebSocketMatchCodec.Envelope(kind, bytes));
        public void Lose() { IsOpen = false; Closed?.Invoke(); }
        public void Error() { IsOpen = false; Failed?.Invoke(); }
    }
}
