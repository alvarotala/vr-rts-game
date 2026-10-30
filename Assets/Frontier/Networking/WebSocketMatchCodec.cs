using System;

namespace Frontier.Networking
{
    // Browser transport framing for the dedicated match protocol. Each websocket
    // binary message is one kind byte followed by the same payload bytes that the
    // NGO named messages carry, so snapshots, acknowledgements and results share
    // their encoding, compression and chunk assembly across both transports.
    public static class WebSocketMatchCodec
    {
        // Client to server.
        public const byte Hello = 0x01, Ready = 0x02, Command = 0x03, ResultAck = 0x04;
        // Server to client. Admitted replaces NGO's connection-approved callback.
        public const byte Welcome = 0x11, Snapshot = 0x12, Ack = 0x13, Result = 0x14, Status = 0x15, Admitted = 0x16;
        public const int MaxClientMessageBytes = 1 + 4096;

        public static byte[] Envelope(byte kind, byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            var frame = new byte[payload.Length + 1];
            frame[0] = kind;
            Buffer.BlockCopy(payload, 0, frame, 1, payload.Length);
            return frame;
        }

        public static bool TryOpen(byte[] frame, out byte kind, out byte[] payload)
        {
            kind = 0; payload = null;
            if (frame == null || frame.Length < 2) return false;
            kind = frame[0];
            payload = new byte[frame.Length - 1];
            Buffer.BlockCopy(frame, 1, payload, 0, payload.Length);
            return true;
        }

        public static bool IsClientKind(byte kind) => kind == Hello || kind == Ready || kind == Command || kind == ResultAck;
    }
}
