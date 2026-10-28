#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace Frontier.Networking
{
    // Browser WebSocket callbacks enter Unity on its main thread. Each binary
    // message is bounded before crossing the JS/WASM boundary; no worker thread,
    // TCP socket or unbounded receive queue is needed by the WebGL player.
    [Preserve]
    public sealed class BrowserMatchSocket : MonoBehaviour
    {
        public const int MaxMessageBytes = 4097, MaxBufferedBytes = 65536;
        public event Action Opened;
        public event Action<byte[]> Received;
        public event Action Closed;
        public event Action Failed;
        public bool IsOpen { get; private set; }
        private static int nextReference;
        private int reference;
        private bool closing;

        public bool Connect(string url)
        {
            if (reference != 0 || closing) return false;
            reference = ++nextReference;
            gameObject.name = "Frontier browser socket " + reference;
            return FrontierBrowserCreate(reference, gameObject.name, url, MaxMessageBytes, MaxBufferedBytes) != 0;
        }

        public bool Send(byte[] bytes)
            => IsOpen && bytes != null && bytes.Length >= 2 && bytes.Length <= MaxMessageBytes
                && FrontierBrowserSend(reference, bytes, bytes.Length) != 0;

        public void Close()
        {
            if (closing) return;
            closing = true; IsOpen = false;
            if (reference != 0) FrontierBrowserClose(reference);
            reference = 0;
        }

        [Preserve] private void BrowserSocketOpened(string unused)
        {
            if (closing) return;
            IsOpen = true; Opened?.Invoke();
        }

        [Preserve] private void BrowserSocketMessage(string payload)
        {
            if (closing || !IsOpen) return;
            // Base64 is only the SendMessage bridge representation, never the
            // network protocol. The socket always receives/sends binary frames.
            if (string.IsNullOrEmpty(payload) || payload.Length > ((MaxMessageBytes + 2) / 3) * 4)
            { BrowserSocketFailed(""); return; }
            byte[] bytes;
            try { bytes = Convert.FromBase64String(payload); }
            catch (FormatException) { BrowserSocketFailed(""); return; }
            if (bytes.Length < 2 || bytes.Length > MaxMessageBytes) { BrowserSocketFailed(""); return; }
            Received?.Invoke(bytes);
        }

        [Preserve] private void BrowserSocketClosed(string unused)
        {
            if (closing) return;
            IsOpen = false; Closed?.Invoke();
        }

        [Preserve] private void BrowserSocketFailed(string unused)
        {
            if (closing) return;
            IsOpen = false; Failed?.Invoke();
        }

        private void OnDestroy() => Close();
        [DllImport("__Internal")] private static extern int FrontierBrowserCreate(int reference, string receiver, string url, int maxMessage, int maxBuffered);
        [DllImport("__Internal")] private static extern int FrontierBrowserSend(int reference, byte[] bytes, int length);
        [DllImport("__Internal")] private static extern void FrontierBrowserClose(int reference);
    }
}
#endif
