import { inflateSync } from 'fflate';

// Port of Frontier.Networking.NetworkWire / FrameAssembler. Snapshots and results are
// raw-deflate (System.IO.Compression.DeflateStream) JSON split into 900-byte chunks,
// each with a 24-byte little-endian header: int64 frame, int32 compressed length,
// int32 decoded length, int32 index, int32 count.
export const ChunkBytes = 900;
export const HeaderBytes = 24;
export const MaxSnapshotBytes = 1024 * 1024;
export const MaxSnapshotCompressed = 128 * 1024;
export const MaxResultBytes = 8 * 1024 * 1024;
export const MaxResultCompressed = 2 * 1024 * 1024;

export const chunkCount = (length: number) => Math.floor((length + ChunkBytes - 1) / ChunkBytes);

export interface AssembledFrame { frame: number; compressed: Uint8Array; rawLength: number }

export class FrameAssembler {
  private current = 0;
  private completed = 0;
  private bytes: Uint8Array | null = null;
  private received: boolean[] | null = null;
  private receivedCount = 0;
  private rawLength = 0;
  constructor(private readonly maxCompressed: number, private readonly maxRaw: number) {}

  // Only one incomplete frame is retained; a newer frame makes the old one obsolete.
  add(packet: Uint8Array): AssembledFrame | null {
    if (packet.length <= HeaderBytes || packet.length > HeaderBytes + ChunkBytes) return null;
    const view = new DataView(packet.buffer, packet.byteOffset, packet.byteLength);
    const low = view.getUint32(0, true), high = view.getInt32(4, true);
    if (high !== 0) return null; // frame ids never exceed 2^32 in practice; reject rather than lose precision
    const id = low;
    const total = view.getInt32(8, true), decoded = view.getInt32(12, true);
    const index = view.getInt32(16, true), count = view.getInt32(20, true);
    if (id <= this.completed || id < this.current || total <= 0 || total > this.maxCompressed || decoded <= 0 || decoded > this.maxRaw
      || count !== chunkCount(total) || index < 0 || index >= count
      || packet.length - HeaderBytes !== Math.min(ChunkBytes, total - index * ChunkBytes)) return null;
    if (id !== this.current) {
      this.current = id; this.bytes = new Uint8Array(total); this.received = new Array(count).fill(false);
      this.receivedCount = 0; this.rawLength = decoded;
    }
    if (!this.bytes || !this.received || this.bytes.length !== total || this.received.length !== count || this.rawLength !== decoded || this.received[index]) return null;
    this.bytes.set(packet.subarray(HeaderBytes), index * ChunkBytes);
    this.received[index] = true;
    if (++this.receivedCount !== count) return null;
    const result = { frame: id, compressed: this.bytes, rawLength: this.rawLength };
    this.completed = id; this.bytes = null; this.received = null;
    return result;
  }
}

export function inflate(compressed: Uint8Array, rawLength: number, maxRaw: number): Uint8Array {
  if (rawLength <= 0 || rawLength > maxRaw) throw new Error('Invalid decoded frame length.');
  const raw = inflateSync(compressed);
  if (raw.length !== rawLength) throw new Error('Decoded frame length does not match its header.');
  return raw;
}

const decoder = new TextDecoder();
export function decodeJson(bytes: Uint8Array): unknown {
  return JSON.parse(decoder.decode(bytes));
}
