using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Frontier.Networking
{
    // The wire layer is independent of Unity so malformed/lost/reordered frames can be tested.
    public static class NetworkWire
    {
        public const int ChunkBytes = 900, HeaderBytes = 24;
        public const int MaxSnapshotBytes = 1024 * 1024, MaxSnapshotCompressed = 128 * 1024;
        public const int MaxResultBytes = 8 * 1024 * 1024, MaxResultCompressed = 2 * 1024 * 1024;
        private static readonly byte[] ResultPrefix = Encoding.UTF8.GetBytes("{\"view\":"), ReportPrefix = Encoding.UTF8.GetBytes(",\"report\":");
        private static readonly byte[] ResultComplete = Encoding.UTF8.GetBytes(",\"reportUnavailable\":false}"), ResultUnavailable = Encoding.UTF8.GetBytes("null,\"reportUnavailable\":true}");

        // The shared report is serialized once by the server; each envelope still
        // carries only that seat's final view. Size limits never hide the outcome.
        public static byte[] PackResult(byte[] viewJson, byte[] reportJson, out int rawLength, out bool reportUnavailable)
        {
            if (viewJson == null || viewJson.Length == 0 || viewJson.Length > MaxSnapshotBytes)
                throw new InvalidDataException("Invalid final player view length.");
            reportUnavailable = reportJson == null;
            try
            {
                var raw = ResultEnvelope(viewJson, reportJson);
                var packed = Compress(raw, MaxResultBytes, MaxResultCompressed);
                rawLength = raw.Length; return packed;
            }
            catch (InvalidDataException) when (reportJson != null)
            {
                return PackResult(viewJson, null, out rawLength, out reportUnavailable);
            }
        }

        private static byte[] ResultEnvelope(byte[] viewJson, byte[] reportJson)
        {
            var suffix = reportJson == null ? ResultUnavailable : ResultComplete;
            long length = (long)ResultPrefix.Length + viewJson.Length + ReportPrefix.Length + (reportJson?.Length ?? 0) + suffix.Length;
            if (length > MaxResultBytes) throw new InvalidDataException("Final report exceeds the network frame limit.");
            var raw = new byte[(int)length]; int offset = 0;
            foreach (var part in new[] { ResultPrefix, viewJson, ReportPrefix, reportJson, suffix })
            {
                if (part == null) continue;
                Buffer.BlockCopy(part, 0, raw, offset, part.Length); offset += part.Length;
            }
            return raw;
        }

        public static byte[] Compress(byte[] raw, int maxRaw, int maxCompressed)
        {
            if (raw == null || raw.Length == 0 || raw.Length > maxRaw) throw new InvalidDataException("Network frame is too large.");
            using (var output = new MemoryStream())
            {
                using (var zip = new DeflateStream(output, CompressionLevel.Fastest, true)) zip.Write(raw, 0, raw.Length);
                if (output.Length > maxCompressed) throw new InvalidDataException("Compressed network frame is too large.");
                return output.ToArray();
            }
        }

        public static byte[] Decompress(byte[] compressed, int rawLength, int maxRaw)
        {
            if (rawLength <= 0 || rawLength > maxRaw) throw new InvalidDataException("Invalid decoded frame length.");
            var raw = new byte[rawLength];
            using (var input = new MemoryStream(compressed, false))
            using (var zip = new DeflateStream(input, CompressionMode.Decompress))
            {
                int read = 0;
                while (read < raw.Length)
                {
                    int count = zip.Read(raw, read, raw.Length - read);
                    if (count == 0) throw new InvalidDataException("Truncated network frame.");
                    read += count;
                }
                if (zip.ReadByte() != -1) throw new InvalidDataException("Decoded frame exceeds its declared length.");
            }
            return raw;
        }

        public static int ChunkCount(int length) => (length + ChunkBytes - 1) / ChunkBytes;

        public static byte[] Chunk(long frame, byte[] compressed, int rawLength, int index)
        {
            int count = ChunkCount(compressed.Length);
            if (frame <= 0 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            int length = Math.Min(ChunkBytes, compressed.Length - index * ChunkBytes);
            using (var stream = new MemoryStream(HeaderBytes + length))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(frame); writer.Write(compressed.Length); writer.Write(rawLength);
                writer.Write(index); writer.Write(count);
                writer.Write(compressed, index * ChunkBytes, length);
                return stream.ToArray();
            }
        }
    }

    public sealed class FrameAssembler
    {
        private readonly int maxCompressed, maxRaw;
        private long current, completed;
        private byte[] bytes;
        private bool[] received;
        private int receivedCount, rawLength;
        public FrameAssembler(int maxCompressed, int maxRaw) { this.maxCompressed = maxCompressed; this.maxRaw = maxRaw; }

        // Only one incomplete frame is retained; a newer snapshot makes the old one obsolete.
        public bool Add(byte[] packet, out long frame, out byte[] compressed, out int decodedLength)
            => Add(packet, out frame, out compressed, out decodedLength, out _);

        // Progress excludes duplicate, obsolete and malformed chunks, so those
        // packets cannot keep an incomplete final transfer alive indefinitely.
        public bool Add(byte[] packet, out long frame, out byte[] compressed, out int decodedLength, out bool progressed)
        {
            frame = 0; compressed = null; decodedLength = 0; progressed = false;
            if (packet == null || packet.Length <= NetworkWire.HeaderBytes || packet.Length > NetworkWire.HeaderBytes + NetworkWire.ChunkBytes) return false;
            using (var stream = new MemoryStream(packet, false))
            using (var reader = new BinaryReader(stream))
            {
                long id = reader.ReadInt64(); int total = reader.ReadInt32(), decoded = reader.ReadInt32();
                int index = reader.ReadInt32(), count = reader.ReadInt32();
                if (id <= completed || id < current || total <= 0 || total > maxCompressed || decoded <= 0 || decoded > maxRaw
                    || count != NetworkWire.ChunkCount(total) || index < 0 || index >= count
                    || packet.Length - NetworkWire.HeaderBytes != Math.Min(NetworkWire.ChunkBytes, total - index * NetworkWire.ChunkBytes)) return false;
                if (id != current)
                {
                    current = id; bytes = new byte[total]; received = new bool[count]; receivedCount = 0; rawLength = decoded;
                }
                if (bytes == null || bytes.Length != total || received.Length != count || rawLength != decoded || received[index]) return false;
                Buffer.BlockCopy(packet, NetworkWire.HeaderBytes, bytes, index * NetworkWire.ChunkBytes, packet.Length - NetworkWire.HeaderBytes);
                received[index] = true; progressed = true;
                if (++receivedCount != count) return false;
                frame = id; compressed = bytes; decodedLength = rawLength; completed = id;
                bytes = null; received = null; return true;
            }
        }
    }
}
