import { Catalog, TickRate, UnitLimit } from './catalog';
import { Terrain, parseMap, type MapDefinition } from './terrain';
import { PlacementValidity } from './placement';
import { Presentation } from './presentation';
import {
  CommandKind, makeCommand, type CommandAck, type GameCommand, type MatchReport, type PlayerView, type Point, type VisibleEntity,
} from '../protocol/types';
import { t, tf } from '../i18n/i18n';

export type MatchMode = 'offline' | 'direct' | 'public';
export type Phase = 'connecting' | 'countdown' | 'playing' | 'finished' | 'closed';

export interface TransportHandlers {
  started(info: { player: number; mapId: string; faction0: string; faction1: string }): void;
  view(view: PlayerView): void;
  ack(ack: CommandAck): void;
  finished(result: { view: PlayerView; report: MatchReport | null }): void;
  status(message: string): void;
  notice(message: string): void;
  closed(reason: string): void;
}

// One interface for the offline WebAssembly simulation and the dedicated server.
export interface MatchTransport {
  readonly mode: MatchMode;
  start(handlers: TransportHandlers): void;
  send(command: GameCommand): boolean;
  countdownRemaining(): number;
  setPaused(paused: boolean): void;
  leave(): void;
}

export interface Announcement { text: string; error: boolean; version: number; at: number }
export const ControlGroupCount = 10;
const DoubleSelectRadius = 12;

const isMobileCommand = (kind: CommandKind) => kind === CommandKind.Move || kind === CommandKind.Attack || kind === CommandKind.AttackMove
  || kind === CommandKind.Gather || kind === CommandKind.Repair || kind === CommandKind.Build || kind === CommandKind.Stop || kind === CommandKind.Hold
  || kind === CommandKind.Cast || kind === CommandKind.CancelUnitOrder;

// Port of Frontier.Runtime.GameSession for the desktop client: selection, control
// groups, targeting modes and contextual orders. Every order is a GameCommand that
// the authority (server or offline simulation) validates; nothing is applied locally.
export class GameSession {
  readonly catalog: Catalog;
  terrain!: Terrain;
  map!: MapDefinition;
  mapId = '';
  view: PlayerView | null = null;
  localPlayer = 0;
  faction = 'industrial';
  enemyFaction = 'biological';
  phase: Phase = 'connecting';
  readonly mode: MatchMode;
  readonly selection = new Set<number>();
  selectedResource = 0;
  inspected = 0;
  readonly groups: number[][] = Array.from({ length: ControlGroupCount }, () => []);
  buildDefinition: string | null = null;
  attackMoveMode = false;
  abilityId: string | null = null;
  private rallySelection: Set<number> | null = null;
  lastCommand: { kind: CommandKind; point: Point; sequence: number; time: number } | null = null;
  readonly presentation = new Presentation();
  report: MatchReport | null = null;
  reportUnavailable = false;
  announcement: Announcement = { text: '', error: false, version: 0, at: 0 };
  closedReason = '';
  private sequence = 0;
  private readonly pending = new Map<number, GameCommand>();
  private readonly listeners = new Set<() => void>();
  private placement: PlacementValidity | null = null;
  private offlineCountdownUntil = 0;
  private paused = false;
  private readonly maps: Record<string, string>;
  onFinished: (() => void) | null = null;
  onCommandResult: ((accepted: boolean, kind: CommandKind) => void) | null = null;

  constructor(catalogText: string, maps: Record<string, string>, readonly transport: MatchTransport, mapId: string,
    private readonly clock: () => number = () => performance.now() / 1000) {
    this.catalog = new Catalog(catalogText);
    this.maps = maps;
    this.mode = transport.mode;
    this.loadMap(mapId);
  }

  loadMap(id: string): void {
    const json = this.maps[id];
    if (!json) throw new Error('Map not found: ' + id);
    this.map = parseMap(json); this.mapId = id;
    this.terrain = new Terrain(this.map);
    this.placement = null;
  }

  // ---- lifecycle -------------------------------------------------------------
  start(): void {
    this.phase = 'connecting';
    this.transport.start({
      started: (info) => {
        if (info.mapId !== this.mapId) this.loadMap(info.mapId);
        this.localPlayer = info.player;
        this.faction = info.player === 0 ? info.faction0 : info.faction1;
        this.enemyFaction = info.player === 0 ? info.faction1 : info.faction0;
        this.phase = 'countdown';
        if (this.mode === 'offline') this.offlineCountdownUntil = this.clock() + 3;
        this.announce(this.mode === 'offline' ? t('Expand your base. Scout before attacking.') : t('Connected · Your opponent is another player.'));
        this.changed();
      },
      view: (view) => this.applyView(view),
      ack: (ack) => this.acknowledged(ack),
      finished: (result) => {
        this.applyView(result.view);
        this.report = result.report; this.reportUnavailable = result.report === null;
        this.phase = 'finished';
        this.announce(this.outcomeText(), false);
        this.onFinished?.();
        this.changed();
      },
      status: (message) => { if (this.phase === 'connecting') this.announce(t(message)); },
      notice: (message) => this.announce(t(message)),
      closed: (reason) => {
        this.closedReason = reason;
        if (this.phase !== 'finished') {
          this.phase = 'closed';
          this.announce(t(reason), true);
          this.onFinished?.();
        }
        this.changed();
      },
    });
  }

  leave(): void { this.transport.leave(); this.phase = 'closed'; this.changed(); }

  get running() { return this.view !== null && (this.phase === 'countdown' || this.phase === 'playing' || this.phase === 'finished'); }
  get winner() { return this.view?.winner ?? -1; }
  get countdownRemaining(): number {
    if (this.phase !== 'countdown') return 0;
    return this.mode === 'offline' ? Math.max(0, this.offlineCountdownUntil - this.clock()) : this.transport.countdownRemaining();
  }
  get isCountingDown() { return this.countdownRemaining > 0; }
  get canIssueOrders() { return this.phase === 'playing' && this.view !== null && this.winner === -1 && !this.paused; }
  get isPaused() { return this.paused; }

  // Offline pauses the simulation; online play never pauses the opponent.
  setPaused(paused: boolean): void {
    this.paused = paused && this.mode === 'offline';
    this.transport.setPaused(this.paused || this.isCountingDown);
    this.changed();
  }

  update(): void {
    const now = this.clock();
    if (this.phase === 'countdown' && this.view && this.countdownRemaining <= 0) {
      this.phase = 'playing';
      this.transport.setPaused(this.paused);
      this.announce(this.mapId === 'training' ? tf('1 · Select a worker. Build: {0} + {1}.', this.catalog.role(this.faction, 'supply').name, this.catalog.role(this.faction, 'barracks').name)
        : t('Expand your base. Scout before attacking.'));
      this.changed();
    }
    this.presentation.advance(now, this.paused || this.isCountingDown);
  }

  subscribe(listener: () => void): () => void { this.listeners.add(listener); return () => this.listeners.delete(listener); }
  changed(): void { for (const listener of this.listeners) listener(); }

  announce(text: string, error = false): void {
    this.announcement = { text, error, version: this.announcement.version + 1, at: this.clock() };
  }

  outcomeText(): string {
    const winner = this.winner;
    if (winner === 2) return t('DRAW');
    return winner === this.localPlayer ? t('VICTORY · The territory is yours.') : t('DEFEAT · Your colony has fallen.');
  }

  private applyView(view: PlayerView): void {
    this.presentation.receive(view, this.clock(), this.paused || this.isCountingDown);
    this.view = view;
    for (const id of [...this.selection]) if (!view.entities.some((e) => e.id === id && e.owner === view.player && !e.remembered)) this.selection.delete(id);
    if (this.inspected && !view.entities.some((e) => e.id === this.inspected)) this.inspected = 0;
    if (this.selectedResource && !view.resources.some((r) => r.id === this.selectedResource)) this.selectedResource = 0;
    this.validateAbilitySelection();
    this.changed();
  }

  // ---- queries ---------------------------------------------------------------
  entity(id: number): VisibleEntity | undefined { return this.view?.entities.find((e) => e.id === id); }
  own(e: VisibleEntity) { return this.view !== null && e.owner === this.view.player; }
  type(e: VisibleEntity) { return this.catalog.get(e.definition); }
  selected(): VisibleEntity[] { return this.view ? this.view.entities.filter((e) => this.selection.has(e.id) && this.own(e)) : []; }
  selectedMobileUnits(): VisibleEntity[] {
    return this.selected().filter((e) => !e.remembered && e.health > 0 && e.complete && !this.type(e).building);
  }
  rallyPointProducers(): VisibleEntity[] {
    return this.selected().filter((e) => !e.remembered && e.health > 0 && e.complete && this.catalog.canProduceUnits(e.definition));
  }
  get canSetRallyPoint(): boolean {
    if (!this.view || this.selection.size === 0) return false;
    const selected = this.selected();
    return selected.length === this.selection.size && selected.every((e) => !e.remembered && e.health > 0 && this.type(e).building) && this.rallyPointProducers().length > 0;
  }
  private selectableUnits(): VisibleEntity[] {
    return this.view ? this.view.entities.filter((e) => this.own(e) && !e.remembered && e.health > 0 && e.complete && !this.type(e).building) : [];
  }
  idleWorkers(): VisibleEntity[] {
    return this.selectableUnits().filter((e) => this.type(e).role === 'worker' && (e.order === CommandKind.Stop || e.order === CommandKind.Hold)
      && e.pendingOrders.length === 0 && !e.working && !e.depositing);
  }
  army(): VisibleEntity[] { return this.selectableUnits().filter((e) => this.type(e).role !== 'worker'); }
  get hasTargetingMode() { return this.buildDefinition !== null || this.attackMoveMode || this.abilityId !== null || this.rallySelection !== null; }
  get rallyPointMode() { return this.rallySelection !== null; }
  get pendingAbility() { return this.catalog.ability(this.abilityId); }
  get pendingCount() { return this.pending.size; }

  abilityCasters(id: string): VisibleEntity[] {
    return this.selected().filter((e) => e.health > 0 && e.complete && this.type(e).abilities.includes(id));
  }
  abilityReady(caster: VisibleEntity, abilityId: string): boolean {
    const ability = this.catalog.ability(abilityId);
    return !!ability && !!this.view && caster.energy >= ability.energyCost && !caster.abilityCooldowns.some((c) => c.ability === ability.id && c.readyTick > this.view!.tick);
  }
  abilityStatus(id: string): string {
    const ability = this.catalog.ability(id);
    const casters = this.abilityCasters(id);
    if (!ability || casters.length === 0 || !this.view) return t('Select a caster');
    const ready = casters.filter((e) => this.abilityReady(e, id)).length;
    if (ready > 0) return tf('{0} energy · {1} ready', ability.energyCost, ready);
    const wait = Math.min(...casters.map((e) => Math.max(0, ...e.abilityCooldowns.filter((c) => c.ability === id).map((c) => c.readyTick - this.view!.tick), 0)));
    return wait > 0 ? tf('{0} energy · {1} s cooldown', ability.energyCost, Math.ceil(wait / TickRate)) : tf('Needs {0} energy', ability.energyCost);
  }

  // ---- selection (local presentation state only) -------------------------------
  select(id: number, additive: boolean, resourceId = 0): void {
    if (this.isCountingDown || !this.view) return;
    if (!additive) this.selection.clear();
    this.selectedResource = 0; this.inspected = 0;
    const entity = this.entity(id);
    if (entity && this.own(entity) && !entity.remembered) {
      if (!this.selection.has(id)) this.selection.add(id); else if (additive) this.selection.delete(id);
    } else if (!additive && resourceId > 0 && this.view.resources.some((r) => r.id === resourceId)) this.selectedResource = resourceId;
    else if (!additive && entity) this.inspected = entity.id;
    this.validateAbilitySelection(); this.changed();
  }

  // Desktop refinement: a box that contains own mobile units selects only those units.
  selectRectangle(a: Point, b: Point, additive: boolean): void {
    if (this.isCountingDown || !this.view) return;
    this.selectedResource = 0; this.inspected = 0;
    if (!additive) this.selection.clear();
    const minX = Math.min(a.x, b.x), maxX = Math.max(a.x, b.x), minY = Math.min(a.y, b.y), maxY = Math.max(a.y, b.y);
    const inside = this.view.entities.filter((e) => {
      if (!this.own(e) || e.remembered || e.health <= 0) return false;
      const c = this.catalog.center(e.definition, e.position);
      return c.x >= minX && c.x <= maxX && c.y >= minY && c.y <= maxY;
    });
    const units = inside.filter((e) => !this.type(e).building);
    for (const e of units.length > 0 ? units : inside) this.selection.add(e.id);
    this.validateAbilitySelection(); this.changed();
  }

  selectEntities(ids: number[], additive: boolean): void {
    if (this.isCountingDown || !this.view) return;
    if (!additive) this.selection.clear();
    this.selectedResource = 0; this.inspected = 0;
    for (const id of ids) { const e = this.entity(id); if (e && this.own(e) && !e.remembered && e.health > 0) this.selection.add(id); }
    this.validateAbilitySelection(); this.changed();
  }

  // Same definition within twelve cells, as on Quest.
  selectSameTypeOnMap(id: number, additive: boolean): void {
    const source = this.entity(id);
    if (!source || !this.view || !this.own(source) || source.remembered || source.health <= 0 || this.isCountingDown) return;
    const ids = this.view.entities.filter((e) => this.own(e) && !e.remembered && e.health > 0 && e.definition === source.definition
      && Math.hypot(e.position.x - source.position.x, e.position.y - source.position.y) <= DoubleSelectRadius).map((e) => e.id);
    this.selectEntities(ids, additive);
  }

  selectTypeInSelection(definition: string, remove: boolean): void {
    const members = this.selected().filter((e) => !e.remembered && e.definition === definition).map((e) => e.id);
    if (members.length === 0) return;
    if (remove) for (const id of members) this.selection.delete(id);
    else { this.selection.clear(); for (const id of members) this.selection.add(id); }
    this.selectedResource = 0; this.validateAbilitySelection(); this.changed();
  }

  deselect(id: number): void { if (this.selection.delete(id)) { this.validateAbilitySelection(); this.changed(); } }

  selectIdleWorkers(): boolean { return this.selectUnits(this.idleWorkers(), 'No idle workers available.', '{0} idle workers selected.'); }
  selectArmy(): boolean { return this.selectUnits(this.army(), 'No army units available.', '{0} army units selected.'); }
  private selectUnits(units: VisibleEntity[], empty: string, selected: string): boolean {
    if (!this.running || this.isCountingDown) return false;
    if (units.length === 0) { this.announce(t(empty), true); this.changed(); return false; }
    this.cancelMode(); this.selectedResource = 0; this.inspected = 0; this.selection.clear();
    for (const e of units) this.selection.add(e.id);
    this.announce(tf(selected, units.length)); this.changed(); return true;
  }

  // Local control groups: recall, replace (save) or add to a group.
  group(index: number, mode: 'recall' | 'save' | 'add'): number {
    if (!this.view || index < 0 || index >= this.groups.length || this.isCountingDown) return 0;
    const alive = (id: number) => this.view!.entities.some((e) => e.id === id && this.own(e) && !e.remembered);
    if (mode === 'save' || mode === 'add') {
      const members = mode === 'save' ? [] : this.groups[index].filter(alive);
      for (const id of this.selection) if (alive(id) && !members.includes(id)) members.push(id);
      this.groups[index] = members;
      this.announce(tf(mode === 'save' ? 'Group {0} saved.' : 'Added to group {0}.', (index + 1) % 10));
    } else {
      this.selection.clear(); this.selectedResource = 0; this.inspected = 0;
      for (const id of this.groups[index]) if (alive(id)) this.selection.add(id);
      if (this.selection.size === 0) this.announce(tf('Group {0} is empty.', (index + 1) % 10), true);
    }
    this.validateAbilitySelection(); this.changed();
    return this.selection.size;
  }
  groupSize(index: number): number {
    if (!this.view) return 0;
    return this.groups[index].filter((id) => this.view!.entities.some((e) => e.id === id && this.own(e) && !e.remembered)).length;
  }

  // ---- targeting modes --------------------------------------------------------
  chooseBuild(id: string): void {
    if (!this.canIssueOrders) return;
    this.rallySelection = null; this.buildDefinition = id; this.attackMoveMode = false; this.abilityId = null;
    this.announce(tf('Place {0} · Point at the terrain and confirm construction.', this.catalog.get(id).name)); this.changed();
  }
  chooseAttackMove(): void {
    if (!this.canIssueOrders) return;
    this.rallySelection = null; this.buildDefinition = null; this.attackMoveMode = true; this.abilityId = null;
    this.announce(t('Attack Move · Choose a point on the battlefield.')); this.changed();
  }
  chooseAbility(id: string): boolean {
    if (!this.canIssueOrders) return false;
    const ability = this.catalog.ability(id);
    if (!ability || !this.abilityCasters(id).some((e) => this.abilityReady(e, id))) { this.announce(this.abilityStatus(id), true); this.changed(); return false; }
    this.rallySelection = null; this.buildDefinition = null; this.attackMoveMode = false; this.abilityId = id;
    this.announce(tf('{0} · Choose a visible point. One caster will move into range and cast.', ability.name)); this.changed();
    return true;
  }
  chooseRallyPoint(): boolean {
    if (!this.canIssueOrders || !this.canSetRallyPoint) return false;
    this.buildDefinition = null; this.attackMoveMode = false; this.abilityId = null;
    this.rallySelection = new Set(this.selection);
    this.announce(t('Set rally point · Click the terrain.')); this.changed();
    return true;
  }
  cancelMode(): void {
    if (this.buildDefinition) this.announce(t('Construction placement cancelled.'));
    else if (this.attackMoveMode) this.announce(t('Attack destination cancelled.'));
    else if (this.pendingAbility) this.announce(tf('{0} targeting cancelled.', this.pendingAbility.name));
    else if (this.rallySelection) this.announce(t('Rally point placement cancelled.'));
    this.rallySelection = null; this.buildDefinition = null; this.attackMoveMode = false; this.abilityId = null; this.changed();
  }
  private validateAbilitySelection(): void {
    if (this.abilityId && this.abilityCasters(this.abilityId).length === 0) this.abilityId = null;
    if (this.rallySelection && (!this.canSetRallyPoint || this.rallySelection.size !== this.selection.size || [...this.rallySelection].some((id) => !this.selection.has(id)))) this.rallySelection = null;
    if (this.buildDefinition && !this.selected().some((e) => this.type(e).role === 'worker' && e.complete)) this.buildDefinition = null;
  }

  // ---- orders -----------------------------------------------------------------
  // Port of GameSession.Context: the contextual order for a map click.
  context(point: Point, entityId: number, resourceId: number, append = false, attack = false, replaceSequence = 0): number {
    if (!this.view || !this.canIssueOrders || this.selection.size === 0) return 0;
    if (this.rallySelection && (!this.canSetRallyPoint || this.rallySelection.size !== this.selection.size)) { this.rallySelection = null; this.changed(); return 0; }
    const ability = this.pendingAbility;
    if (ability) {
      if (!this.send(CommandKind.Cast, { definition: ability.id, position: point })) return 0;
      this.abilityId = null; this.announce(tf('{0} requested.', ability.name)); this.changed(); return this.sequence;
    }
    if (this.canSetRallyPoint) {
      if (!this.send(CommandKind.SetRallyPoint, { position: point })) return 0;
      this.rallySelection = null; this.announce(t('Rally point requested.')); this.changed(); return this.sequence;
    }
    if (this.buildDefinition) {
      const definition = this.buildDefinition;
      if (!this.send(CommandKind.Build, { definition, position: this.buildPlacement(point, resourceId), append })) return 0;
      this.announce(tf('{0} construction requested.', this.catalog.get(definition).name));
      // Desktop convenience: Shift keeps the structure armed for the next foundation.
      if (!append) this.buildDefinition = null;
      this.changed(); return this.sequence;
    }
    if (this.attackMoveMode) {
      const sent = this.send(CommandKind.AttackMove, { position: point, append, replaceSequence });
      if (sent && !append) this.attackMoveMode = false;
      this.changed(); return sent ? this.sequence : 0;
    }
    const entity = this.view.entities.find((e) => e.id === entityId && !e.remembered);
    const selected = this.selectedMobileUnits();
    if (selected.length === 0) return 0;
    if (attack) {
      const sent = entity && entity.owner !== this.localPlayer
        ? this.send(CommandKind.Attack, { target: entity.id, append, replaceSequence })
        : this.send(CommandKind.AttackMove, { position: point, append, replaceSequence });
      return sent ? this.sequence : 0;
    }
    if (entity && this.own(entity) && this.type(entity).role === 'extractor' && entity.complete)
      resourceId = this.view.resources.find((r) => r.extractorId === entity.id)?.id ?? resourceId;
    const workers = selected.every((e) => this.type(e).role === 'worker');
    let sent: boolean;
    if (entity && entity.owner !== this.localPlayer) sent = this.send(CommandKind.Attack, { target: entity.id, append, replaceSequence });
    else if (resourceId > 0 && workers) sent = this.send(CommandKind.Gather, { target: resourceId, append, replaceSequence });
    else if (entity && this.own(entity) && (!entity.complete || entity.health < this.type(entity).health) && workers)
      sent = this.send(CommandKind.Repair, { target: entity.id, append, replaceSequence });
    else sent = this.send(CommandKind.Move, { position: point, append, replaceSequence });
    return sent ? this.sequence : 0;
  }

  send(kind: CommandKind, options: { definition?: string; target?: number; position?: Point; queueOrderId?: number; append?: boolean;
    replaceSequence?: number; unitOrderSequence?: number; entityIds?: number[] } = {}): boolean {
    if (!this.view || !(this.canIssueOrders || (kind === CommandKind.Surrender && this.phase !== 'finished' && this.phase !== 'closed'))) return false;
    if (options.entityIds && (options.entityIds.length === 0 || options.entityIds.some((id) => !this.selection.has(id)))) return false;
    let selected = [...new Set(options.entityIds ?? this.selection)].sort((a, b) => a - b);
    if (isMobileCommand(kind)) {
      const mobiles = new Set(this.selectedMobileUnits().map((e) => e.id));
      selected = selected.filter((id) => mobiles.has(id));
    } else if (!options.entityIds && (kind === CommandKind.SetRallyPoint || kind === CommandKind.ClearRallyPoint) && this.canSetRallyPoint)
      selected = this.rallyPointProducers().map((e) => e.id).sort((a, b) => a - b);
    if (kind !== CommandKind.Surrender) selected = selected.slice(0, UnitLimit);
    const command = makeCommand(kind, ++this.sequence, this.localPlayer, {
      definition: options.definition ?? '', target: options.target ?? 0, position: options.position ?? { x: 0, y: 0 },
      queueOrderId: options.queueOrderId ?? 0, entities: kind === CommandKind.Surrender ? [] : selected, append: !!options.append,
      replacePendingSequence: options.replaceSequence ?? 0, unitOrderSequence: options.unitOrderSequence ?? 0,
    });
    if (this.pending.size >= 128 || !this.transport.send(command)) {
      this.announce(t('Order could not be sent. Check the connection.'), true); this.onCommandResult?.(false, kind); this.changed(); return false;
    }
    this.pending.set(command.sequence, command);
    if (kind === CommandKind.Move || kind === CommandKind.Attack || kind === CommandKind.AttackMove) {
      const focus = kind === CommandKind.Attack ? this.view.entities.find((e) => e.id === command.target && !e.remembered) : undefined;
      const point = focus ? this.catalog.center(focus.definition, focus.position) : command.position;
      this.lastCommand = { kind, point, sequence: command.sequence, time: this.clock() };
    }
    this.changed();
    return true;
  }

  private acknowledged(ack: CommandAck): void {
    const command = this.pending.get(ack.sequence);
    if (!command) return;
    this.pending.delete(ack.sequence);
    if (!ack.accepted) {
      if (this.lastCommand?.sequence === ack.sequence) this.lastCommand = null;
      this.announce(t(ack.message || 'The action is no longer available.'), true);
    }
    this.onCommandResult?.(ack.accepted, command.kind);
    this.changed();
  }

  surrender(): boolean { return this.send(CommandKind.Surrender); }
  clearRallyPoint(): boolean {
    if (!this.send(CommandKind.ClearRallyPoint)) return false;
    this.cancelMode(); this.announce(t('Clear rally point requested.')); return true;
  }

  // Resolve the same origin for the ghost and the command: extractors snap to an observed gas deposit.
  buildPlacement(point: Point, resourceId = 0): Point {
    const origin = { x: Math.floor(point.x), y: Math.floor(point.y) };
    if (!this.view || !this.buildDefinition || this.catalog.get(this.buildDefinition).role !== 'extractor') return origin;
    let nearest = null as null | { id: number; position: Point }; let distance = 2.5;
    for (const resource of this.view.resources) {
      if (resource.kind !== 'catalyst' || resource.remaining === 0) continue;
      if (resource.id === resourceId) { nearest = resource; break; }
      const candidate = Math.hypot(resource.position.x - point.x, resource.position.y - point.y);
      if (candidate < distance || (candidate === distance && (!nearest || resource.id < nearest.id))) { nearest = resource; distance = candidate; }
    }
    if (!nearest) return origin;
    const half = this.catalog.get(this.buildDefinition).footprint / 2;
    return { x: Math.floor(nearest.position.x - half), y: Math.floor(nearest.position.y - half) };
  }

  placementMask(definition: string): Uint8Array {
    if (!this.placement) this.placement = new PlacementValidity(this.terrain);
    return this.placement.mask(this.view, this.catalog, definition, this.catalog.get(definition).footprint);
  }

  canPreviewBuild(origin: Point, definition: string): boolean {
    const x = Math.floor(origin.x), y = Math.floor(origin.y);
    if (!this.view || x < 0 || y < 0 || x >= this.map.width || y >= this.map.height) return false;
    return this.placementMask(definition)[y * this.map.width + x] === 1;
  }
}
