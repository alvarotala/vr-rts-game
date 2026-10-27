import { describe, expect, it } from 'vitest';
import { contentHash, protocolVersion, rulesVersion } from 'virtual:frontier-content';
import { FrameAssembler, inflate, decodeJson, MaxSnapshotBytes, MaxSnapshotCompressed, MaxResultBytes, MaxResultCompressed, ChunkBytes, HeaderBytes } from '../../src/protocol/wire';
import { CommandKind, decodeBytes, encodeCommand, makeCommand, normalizeReport, normalizeView } from '../../src/protocol/types';
import { Kind, envelope, open } from '../../src/protocol/codec';
import { base64, fixture } from '../fixture';

describe('compatibility with the C# protocol', () => {
  it('uses the same protocol, rules and content hash as MatchProtocol', () => {
    expect(protocolVersion).toBe(fixture.protocol);
    expect(rulesVersion).toBe(fixture.rules);
    expect(contentHash).toBe(fixture.contentHash);
    expect(`${protocolVersion}:${rulesVersion}:${contentHash}`).toBe(fixture.compatibilityKey);
  });
  it('keeps CommandKind in the same order as Frontier.Core', () => {
    fixture.commandKinds.forEach((name, index) => expect(CommandKind[name as keyof typeof CommandKind]).toBe(index));
    expect(Object.keys(CommandKind).filter((k) => isNaN(Number(k))).length).toBe(fixture.commandKinds.length);
  });
});

describe('snapshot transport', () => {
  const chunks = fixture.chunks.map(base64);
  it('assembles C# chunks in any order and decodes the exact JSON', () => {
    const assembler = new FrameAssembler(MaxSnapshotCompressed, MaxSnapshotBytes);
    const reversed = [...chunks].reverse();
    let frame = null;
    reversed.forEach((chunk, i) => { const result = assembler.add(chunk); if (i < reversed.length - 1) expect(result).toBeNull(); else frame = result; });
    expect(frame).not.toBeNull();
    const assembled = frame!;
    expect(assembled.frame).toBe(fixture.frame);
    expect(assembled.rawLength).toBe(fixture.rawLength);
    const raw = inflate(assembled.compressed, assembled.rawLength, MaxSnapshotBytes);
    expect(new TextDecoder().decode(raw)).toBe(fixture.viewJson);
  });
  it('rejects duplicates, older frames and malformed headers like FrameAssembler', () => {
    const assembler = new FrameAssembler(MaxSnapshotCompressed, MaxSnapshotBytes);
    expect(assembler.add(chunks[0])).toBeNull();
    expect(assembler.add(chunks[0])).toBeNull();
    for (const chunk of chunks.slice(1)) assembler.add(chunk);
    for (const chunk of chunks) expect(assembler.add(chunk)).toBeNull();
    const damaged = chunks[0].slice(); new DataView(damaged.buffer).setInt32(8, 0x7fffffff, true);
    expect(new FrameAssembler(MaxSnapshotCompressed, MaxSnapshotBytes).add(damaged)).toBeNull();
    expect(new FrameAssembler(MaxSnapshotCompressed, MaxSnapshotBytes).add(new Uint8Array(HeaderBytes))).toBeNull();
    expect(new FrameAssembler(MaxSnapshotCompressed, MaxSnapshotBytes).add(new Uint8Array(HeaderBytes + ChunkBytes + 1))).toBeNull();
    const random = new Uint8Array(600); for (let i = 0; i < 200; i++) { crypto.getRandomValues(random); expect(new FrameAssembler(MaxSnapshotCompressed, MaxSnapshotBytes).add(random)).toBeNull(); }
  });
  it('normalizes the Unity JsonUtility view', () => {
    const view = normalizeView(JSON.parse(fixture.viewJson));
    expect(view.tick).toBe(fixture.ticks);
    expect(view.player).toBe(0);
    expect(view.visibility.length).toBe(128 * 128);
    expect([...new Set(view.visibility)].every((v) => v <= 2)).toBe(true);
    expect(view.entities.some((e) => e.definition === 'industrial.hq' && e.owner === 0)).toBe(true);
    expect(view.resources.length).toBeGreaterThan(0);
  });
  it('decodes byte arrays from both JSON encoders', () => {
    const bytes = [0, 1, 2, 2, 1, 0, 255];
    expect([...decodeBytes(bytes)]).toEqual(bytes);
    expect([...decodeBytes(Buffer.from(bytes).toString('base64'))]).toEqual(bytes);
    expect([...decodeBytes(Buffer.from(bytes).toString('hex'))]).toEqual(bytes);
    expect([...decodeBytes(Buffer.from([0, 0, 0, 2, 2, 2]).toString('base64'))]).toEqual([0, 0, 0, 2, 2, 2]);
  });
});

describe('final result', () => {
  it('delivers the view and the shared report with matching winner and tick', () => {
    const assembler = new FrameAssembler(MaxResultCompressed, MaxResultBytes);
    let assembled = null;
    for (const chunk of fixture.resultChunks.map(base64)) assembled = assembler.add(chunk) ?? assembled;
    expect(assembled).not.toBeNull();
    const envelopeJson = decodeJson(inflate(assembled!.compressed, assembled!.rawLength, MaxResultBytes)) as Record<string, unknown>;
    expect(JSON.stringify(envelopeJson)).toBe(JSON.stringify(JSON.parse(fixture.resultRawJson)));
    const view = normalizeView(envelopeJson.view);
    const report = normalizeReport(envelopeJson.report)!;
    expect(envelopeJson.reportUnavailable).toBe(false);
    expect(view.winner).toBe(0);
    expect(report.winner).toBe(view.winner);
    expect(report.tick).toBe(view.tick);
    expect(report.samples.at(-1)!.players).toHaveLength(2);
    expect(report.buildOrders).toHaveLength(2);
  });
});

describe('client messages', () => {
  it('encodes every GameCommand field with integer enums', () => {
    const json = JSON.parse(encodeCommand(makeCommand(CommandKind.Gather, 7, 1, { entities: [3, 4], target: 12, append: true })));
    expect(Object.keys(json).sort()).toEqual(['append', 'definition', 'entities', 'kind', 'player', 'position', 'queueOrderId', 'replacePendingSequence', 'sequence', 'target', 'unitOrderSequence'].sort());
    expect(json).toMatchObject({ kind: 3, sequence: 7, player: 1, entities: [3, 4], target: 12, append: true, definition: '', position: { x: 0, y: 0 } });
  });
  it('wraps payloads with one kind byte', () => {
    const frame = envelope(Kind.Command, new Uint8Array([5, 6]));
    expect([...frame]).toEqual([Kind.Command, 5, 6]);
    expect(open(frame)).toEqual({ kind: Kind.Command, payload: new Uint8Array([5, 6]) });
    expect(open(new Uint8Array([Kind.Hello]))).toBeNull();
  });
});
