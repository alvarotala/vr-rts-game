// Mirrors Frontier.Networking.WebSocketMatchCodec: one kind byte, then the same payload
// bytes the NGO named messages carry.
export const Kind = {
  Hello: 0x01, Ready: 0x02, Command: 0x03, ResultAck: 0x04,
  Welcome: 0x11, Snapshot: 0x12, Ack: 0x13, Result: 0x14, Status: 0x15, Admitted: 0x16,
} as const;

export const MaxHelloBytes = 512;
export const MaxCommandBytes = 4096;

export function envelope(kind: number, payload: Uint8Array): Uint8Array<ArrayBuffer> {
  const frame = new Uint8Array(payload.length + 1);
  frame[0] = kind;
  frame.set(payload, 1);
  return frame;
}

export function open(frame: Uint8Array): { kind: number; payload: Uint8Array } | null {
  if (frame.length < 2) return null;
  return { kind: frame[0], payload: frame.subarray(1) };
}

export const Signal = new Uint8Array([1]);
