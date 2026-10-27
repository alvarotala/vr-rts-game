// Wire contract shared with Frontier.Core / Frontier.Networking. Field names follow the
// C# fields exactly; enums travel as integers. Unity's JsonUtility (dedicated server)
// writes null strings as "" and byte[] as numbers, while System.Text.Json (offline
// WebAssembly) writes null and base64, so every payload goes through normalize*.

export enum CommandKind {
  Move, Attack, AttackMove, Gather, Repair, Build, Train, Research, Cancel, Stop, Hold,
  Surrender, SetRallyPoint, ClearRallyPoint, Cast, CancelUnitOrder,
}

export interface Point { x: number; y: number }

export interface GameCommand {
  kind: CommandKind;
  player: number;
  sequence: number;
  append: boolean;
  replacePendingSequence: number;
  unitOrderSequence: number;
  entities: number[];
  target: number;
  queueOrderId: number;
  position: Point;
  definition: string;
}

export interface AbilityCooldown { ability: string; readyTick: number }
export interface ProductionOrder { id: number; definition: string; research: boolean; remaining: number; total: number }
export interface UnitOrder { sourceSequence: number; target: number; kind: CommandKind; position: Point; definition: string }

export interface VisibleEntity {
  id: number; owner: number; target: number; attackTick: number; damageTick: number;
  definition: string; position: Point; health: number; progress: number; energy: number;
  abilityCooldowns: AbilityCooldown[]; complete: boolean; remembered: boolean; awaitingClearance: boolean;
  order: CommandKind; cargo: number; cargoKind: string; depositing: boolean; working: boolean;
  queue: ProductionOrder[]; activeOrderSequence: number; destination: Point;
  hasRallyPoint: boolean; rallyPoint: Point; pendingOrders: UnitOrder[];
}

export interface AttackEvent {
  id: number; tick: number; attacker: number; target: number; owner: number;
  definition: string; targetDefinition: string; origin: Point; destination: Point;
}
export interface AbilityEvent { id: number; tick: number; caster: number; owner: number; ability: string; position: Point }
export interface DeathEvent { unit: number; tick: number; owner: number; definition: string; position: Point; direction: Point }
export interface ResourceState {
  id: number; remaining: number; definition: string; kind: string; position: Point;
  assignedWorkers: number; optimalWorkers: number; baseId: number; extractorId: number;
}

export interface PlayerView {
  player: number; tick: number; winner: number; minerals: number; catalyst: number;
  supply: number; capacity: number; faction: string;
  entities: VisibleEntity[]; attacks: AttackEvent[]; abilityEvents: AbilityEvent[]; deaths: DeathEvent[];
  resources: ResourceState[]; visibility: Uint8Array; researched: string[];
}

export interface MatchWelcome {
  protocol: number; player: number; matchId: string; mapId: string;
  faction0: string; faction1: string; startServerTime: number; countdownSeconds: number;
}
export interface CommandAck { sequence: number; tick: number; accepted: boolean; message: string }

export interface PlayerStats {
  Faction: string; Workers: number; Army: number; Structures: number; PeakWorkers: number; PeakArmy: number;
  WorkersProduced: number; ArmyProduced: number; StructuresCompleted: number; WorkersLost: number; ArmyLost: number;
  StructuresLost: number; Minerals: number; Catalyst: number; MineralsCollected: number; CatalystCollected: number;
  MineralsSpent: number; CatalystSpent: number; MineralsRefunded: number; CatalystRefunded: number;
  Actions: number; PeakActionsPerSecond: number; AverageApm: number; RecentApm: number;
}
export interface MatchSample { tick: number; players: PlayerStats[] }
export interface BuildOrderEntry { tick: number; supply: number; endTick: number; definition: string; kind: CommandKind; status: number }
export interface MatchReport { schemaVersion: number; tick: number; winner: number; samples: MatchSample[]; buildOrders: { entries: BuildOrderEntry[] }[] }
export interface MatchResult { view: PlayerView; report: MatchReport | null }

type Json = Record<string, unknown>;
const object = (value: unknown): Json => (value !== null && typeof value === 'object' ? (value as Json) : {});
const num = (value: unknown, fallback = 0): number => (typeof value === 'number' && Number.isFinite(value) ? value : fallback);
const str = (value: unknown): string => (typeof value === 'string' ? value : '');
const bool = (value: unknown): boolean => value === true;
const list = <T>(value: unknown, map: (item: unknown) => T): T[] => (Array.isArray(value) ? value.map(map) : []);
const point = (value: unknown): Point => { const p = object(value); return { x: num(p.x), y: num(p.y) }; };

// byte[]: Unity JsonUtility writes a number array; System.Text.Json writes base64.
// Lower-case hexadecimal (Unity's binary text form) is accepted as well.
export function decodeBytes(value: unknown): Uint8Array {
  if (Array.isArray(value)) return Uint8Array.from(value, (v) => num(v) & 0xff);
  if (typeof value === 'string' && value.length > 0) {
    if (value.length % 2 === 0 && /^[0-9a-f]+$/.test(value)) {
      const bytes = new Uint8Array(value.length / 2);
      for (let i = 0; i < bytes.length; i++) bytes[i] = parseInt(value.substr(i * 2, 2), 16);
      return bytes;
    }
    const binary = atob(value);
    const bytes = new Uint8Array(binary.length);
    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
    return bytes;
  }
  return new Uint8Array(0);
}

export function normalizeEntity(raw: unknown): VisibleEntity {
  const e = object(raw);
  return {
    id: num(e.id), owner: num(e.owner), target: num(e.target), attackTick: num(e.attackTick, -1), damageTick: num(e.damageTick, -1),
    definition: str(e.definition), position: point(e.position), health: num(e.health), progress: num(e.progress), energy: num(e.energy),
    abilityCooldowns: list(e.abilityCooldowns, (c) => { const v = object(c); return { ability: str(v.ability), readyTick: num(v.readyTick) }; }),
    complete: bool(e.complete), remembered: bool(e.remembered), awaitingClearance: bool(e.awaitingClearance),
    order: num(e.order, CommandKind.Stop), cargo: num(e.cargo), cargoKind: str(e.cargoKind), depositing: bool(e.depositing), working: bool(e.working),
    queue: list(e.queue, (q) => { const v = object(q); return { id: num(v.id), definition: str(v.definition), research: bool(v.research), remaining: num(v.remaining), total: num(v.total) }; }),
    activeOrderSequence: num(e.activeOrderSequence), destination: point(e.destination),
    hasRallyPoint: bool(e.hasRallyPoint), rallyPoint: point(e.rallyPoint),
    pendingOrders: list(e.pendingOrders, (o) => { const v = object(o); return { sourceSequence: num(v.sourceSequence), target: num(v.target), kind: num(v.kind), position: point(v.position), definition: str(v.definition) }; }),
  };
}

export function normalizeView(raw: unknown): PlayerView {
  const v = object(raw);
  return {
    player: num(v.player), tick: num(v.tick), winner: num(v.winner, -1), minerals: num(v.minerals), catalyst: num(v.catalyst),
    supply: num(v.supply), capacity: num(v.capacity), faction: str(v.faction),
    entities: list(v.entities, normalizeEntity),
    attacks: list(v.attacks, (a) => { const x = object(a); return { id: num(x.id), tick: num(x.tick), attacker: num(x.attacker), target: num(x.target), owner: num(x.owner), definition: str(x.definition), targetDefinition: str(x.targetDefinition), origin: point(x.origin), destination: point(x.destination) }; }),
    abilityEvents: list(v.abilityEvents, (a) => { const x = object(a); return { id: num(x.id), tick: num(x.tick), caster: num(x.caster), owner: num(x.owner), ability: str(x.ability), position: point(x.position) }; }),
    deaths: list(v.deaths, (d) => { const x = object(d); return { unit: num(x.unit), tick: num(x.tick), owner: num(x.owner), definition: str(x.definition), position: point(x.position), direction: point(x.direction) }; }),
    resources: list(v.resources, (r) => { const x = object(r); return { id: num(x.id), remaining: num(x.remaining), definition: str(x.definition), kind: str(x.kind), position: point(x.position), assignedWorkers: num(x.assignedWorkers), optimalWorkers: num(x.optimalWorkers), baseId: num(x.baseId), extractorId: num(x.extractorId) }; }),
    visibility: decodeBytes(v.visibility), researched: list(v.researched, str),
  };
}

export function normalizeWelcome(raw: unknown): MatchWelcome {
  const w = object(raw);
  return {
    protocol: num(w.protocol), player: num(w.player, -1), matchId: str(w.matchId), mapId: str(w.mapId),
    faction0: str(w.faction0), faction1: str(w.faction1), startServerTime: num(w.startServerTime), countdownSeconds: num(w.countdownSeconds, 3),
  };
}

export function normalizeAck(raw: unknown): CommandAck {
  const a = object(raw);
  return { sequence: num(a.sequence), tick: num(a.tick), accepted: bool(a.accepted), message: str(a.message) };
}

export function normalizeReport(raw: unknown): MatchReport | null {
  const r = object(raw);
  if (!Array.isArray(r.samples) || r.samples.length === 0) return null;
  const stats = (p: unknown): PlayerStats => {
    const s = object(p);
    const keys: (keyof PlayerStats)[] = ['Workers', 'Army', 'Structures', 'PeakWorkers', 'PeakArmy', 'WorkersProduced', 'ArmyProduced', 'StructuresCompleted',
      'WorkersLost', 'ArmyLost', 'StructuresLost', 'Minerals', 'Catalyst', 'MineralsCollected', 'CatalystCollected', 'MineralsSpent', 'CatalystSpent',
      'MineralsRefunded', 'CatalystRefunded', 'Actions', 'PeakActionsPerSecond', 'AverageApm', 'RecentApm'];
    const result = { Faction: str(s.Faction) } as PlayerStats;
    for (const key of keys) (result as unknown as Record<string, number>)[key] = num(s[key]);
    return result;
  };
  return {
    schemaVersion: num(r.schemaVersion, 1), tick: num(r.tick), winner: num(r.winner, -1),
    samples: list(r.samples, (s) => { const x = object(s); return { tick: num(x.tick), players: list(x.players, stats) }; }),
    buildOrders: list(r.buildOrders, (o) => ({ entries: list(object(o).entries, (e) => { const x = object(e); return { tick: num(x.tick), supply: num(x.supply), endTick: num(x.endTick, -1), definition: str(x.definition), kind: num(x.kind), status: num(x.status) }; }) })),
  };
}

export function makeCommand(kind: CommandKind, sequence: number, player: number, fields: Partial<GameCommand> = {}): GameCommand {
  return {
    kind, player, sequence, append: false, replacePendingSequence: 0, unitOrderSequence: 0, entities: [], target: 0, queueOrderId: 0,
    position: { x: 0, y: 0 }, definition: '', ...fields,
  };
}

// JsonUtility-compatible encoding (every field present, enums as integers).
export function encodeCommand(command: GameCommand): string {
  return JSON.stringify({
    kind: command.kind | 0, player: command.player | 0, sequence: command.sequence | 0, append: !!command.append,
    replacePendingSequence: command.replacePendingSequence | 0, unitOrderSequence: command.unitOrderSequence | 0,
    entities: command.entities.map((id) => id | 0), target: command.target | 0, queueOrderId: command.queueOrderId | 0,
    position: { x: Number.isFinite(command.position.x) ? command.position.x : 0, y: Number.isFinite(command.position.y) ? command.position.y : 0 },
    definition: command.definition ?? '',
  });
}
