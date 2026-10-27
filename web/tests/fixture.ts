import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

export interface ProtocolFixture {
  protocol: number; rules: number; contentHash: string; compatibilityKey: string; frame: number; rawLength: number; ticks: number;
  viewJson: string; chunks: string[]; resultChunks: string[]; resultRawLength: number; resultRawJson: string; commandKinds: string[];
}

// Produced by `node scripts/protocol-fixture.mjs` (runs before `npm test`).
export const fixture: ProtocolFixture = JSON.parse(readFileSync(fileURLToPath(new URL('../../.cache/web-fixtures/protocol.json', import.meta.url)), 'utf8'));
export const base64 = (text: string) => Uint8Array.from(Buffer.from(text, 'base64'));
