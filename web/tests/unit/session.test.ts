import { describe, expect, it, beforeEach } from 'vitest';
import { catalogText, mapTexts } from 'virtual:frontier-content';
import { GameSession, type MatchTransport, type TransportHandlers } from '../../src/game/session';
import { CommandKind, normalizeView, type GameCommand, type PlayerView } from '../../src/protocol/types';
import { setLanguage } from '../../src/i18n/i18n';
import { fixture } from '../fixture';

class FakeTransport implements MatchTransport {
  readonly mode = 'direct' as const;
  handlers!: TransportHandlers;
  sent: GameCommand[] = [];
  start(handlers: TransportHandlers) { this.handlers = handlers; }
  send(command: GameCommand) { this.sent.push(command); return true; }
  countdownRemaining() { return 0; }
  setPaused() {}
  leave() {}
}

function ready(): { session: GameSession; transport: FakeTransport; view: PlayerView } {
  const transport = new FakeTransport();
  let time = 0;
  const session = new GameSession(catalogText, mapTexts, transport, 'rift', () => time);
  session.start();
  transport.handlers.started({ player: 0, mapId: 'rift', faction0: 'industrial', faction1: 'biological' });
  const view = normalizeView(JSON.parse(fixture.viewJson));
  transport.handlers.view(view);
  time = 5; session.update();
  return { session, transport, view };
}

describe('GameSession orders', () => {
  beforeEach(() => setLanguage('en', false));
  it('starts playing after the welcome and the first view', () => {
    const { session } = ready();
    expect(session.phase).toBe('playing');
    expect(session.canIssueOrders).toBe(true);
  });
  it('turns a right click into Gather, Move, Attack Move or Repair like GameSession.Context', () => {
    const { session, transport, view } = ready();
    const worker = view.entities.find((e) => e.owner === 0 && e.definition === 'industrial.worker' && e.complete)!;
    const mineral = view.resources.find((r) => r.kind === 'mineral' && r.remaining > 0)!;
    session.select(worker.id, false);
    expect(session.context(mineral.position, 0, mineral.id)).toBeGreaterThan(0);
    expect(transport.sent.at(-1)).toMatchObject({ kind: CommandKind.Gather, target: mineral.id, entities: [worker.id], player: 0 });
    session.context({ x: 30.5, y: 30.5 }, 0, 0, true);
    expect(transport.sent.at(-1)).toMatchObject({ kind: CommandKind.Move, append: true, position: { x: 30.5, y: 30.5 } });
    session.context({ x: 31, y: 31 }, 0, 0, false, true);
    expect(transport.sent.at(-1)).toMatchObject({ kind: CommandKind.AttackMove });
    const sequences = transport.sent.map((c) => c.sequence);
    expect(new Set(sequences).size).toBe(sequences.length);
  });
  it('sets rally points for selected producers and builds with snapped extractors', () => {
    const { session, transport, view } = ready();
    const hq = view.entities.find((e) => e.owner === 0 && e.definition === 'industrial.hq')!;
    session.select(hq.id, false);
    expect(session.canSetRallyPoint).toBe(true);
    session.context({ x: 20.4, y: 20.6 }, 0, 0);
    expect(transport.sent.at(-1)).toMatchObject({ kind: CommandKind.SetRallyPoint, entities: [hq.id] });
    const worker = view.entities.find((e) => e.owner === 0 && e.definition === 'industrial.worker')!;
    session.select(worker.id, false);
    const gas = view.resources.find((r) => r.kind === 'catalyst')!;
    session.chooseBuild('industrial.extractor');
    session.context({ x: gas.position.x + 0.8, y: gas.position.y - 0.6 }, 0, 0);
    const built = transport.sent.at(-1)!;
    expect(built).toMatchObject({ kind: CommandKind.Build, definition: 'industrial.extractor' });
    expect(built.position).toEqual({ x: Math.floor(gas.position.x - 1.5), y: Math.floor(gas.position.y - 1.5) });
    expect(session.buildDefinition).toBeNull();
  });
  it('prefers units in a box and keeps control groups local', () => {
    const { session, view } = ready();
    session.selectRectangle({ x: 0, y: 0 }, { x: 30, y: 30 }, false);
    const selected = session.selected();
    expect(selected.length).toBeGreaterThan(0);
    expect(selected.every((e) => !session.catalog.get(e.definition).building)).toBe(true);
    session.group(0, 'save');
    session.select(view.entities.find((e) => e.owner === 0 && e.definition === 'industrial.hq')!.id, false);
    expect(session.group(0, 'recall')).toBe(selected.length);
  });
  it('announces server rejections in the chosen language', () => {
    const { session, transport, view } = ready();
    setLanguage('es', false);
    session.select(view.entities.find((e) => e.owner === 0 && e.definition === 'industrial.worker')!.id, false);
    session.context({ x: 40, y: 40 }, 0, 0);
    transport.handlers.ack({ sequence: transport.sent.at(-1)!.sequence, tick: 1, accepted: false, message: 'Not enough resources.' });
    expect(session.announcement.text).toBe('Recursos insuficientes.');
    expect(session.announcement.error).toBe(true);
  });
});
