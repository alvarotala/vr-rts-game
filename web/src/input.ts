import type { BattleView, PointerTarget } from './render/view';
import type { GameSession } from './game/session';
import type { Point } from './protocol/types';

// Desktop port of CommanderInput: left button selects (click, drag box, Shift adds or
// toggles, double-click selects the same type nearby, Ctrl+click the same type on
// screen); right button gives the contextual order (Shift queues, a quick second
// right-click turns it into an attack); targeting modes confirm with either button.
export class InputController {
  private readonly keys = new Set<string>();
  private mouse = { x: 0, y: 0, inside: false };
  private dragStart: { x: number; y: number; plane: Point | null; entity: number; resource: number } | null = null;
  private dragging = false;
  private middle: { x: number; y: number } | null = null;
  private lastSelect = { time: -1, entity: 0 };
  private action = { time: -1, sequence: 0, entity: 0, resource: 0, point: { x: 0, y: 0 } as Point, append: false, selection: '', confirmed: false };
  private lastGroup = { index: -1, time: 0 };
  private target: PointerTarget = { point: null, plane: null, entity: 0, resource: 0 };
  readonly box: HTMLDivElement;
  enabled = true;
  edgeScroll = true;
  scrollSpeed = 1;
  onMenu: (() => void) | null = null;
  onAction: ((hotkey: string) => boolean) | null = null;
  onFeedback: ((kind: 'select' | 'order' | 'error') => void) | null = null;
  private readonly disposers: (() => void)[] = [];

  constructor(private readonly view: BattleView, private readonly session: GameSession, private readonly surface: HTMLElement) {
    this.box = document.createElement('div');
    this.box.className = 'selection-box';
    document.body.appendChild(this.box);
    const on = <K extends keyof WindowEventMap>(target: Window | HTMLElement, name: K, handler: (event: WindowEventMap[K]) => void, options?: AddEventListenerOptions) => {
      target.addEventListener(name, handler as EventListener, options);
      this.disposers.push(() => target.removeEventListener(name, handler as EventListener, options));
    };
    on(surface, 'mousedown', (e) => this.down(e));
    on(window, 'mouseup', (e) => this.up(e));
    on(window, 'mousemove', (e) => { this.mouse.x = e.clientX; this.mouse.y = e.clientY; this.mouse.inside = true; this.move(e); });
    on(surface, 'mouseleave', () => { this.mouse.inside = false; });
    on(document.documentElement as unknown as Window, 'mouseleave', () => { this.mouse.inside = false; });
    on(surface, 'contextmenu', (e) => e.preventDefault());
    on(surface, 'wheel', (e) => { e.preventDefault(); this.view.rts.zoom(e.deltaY); }, { passive: false });
    on(window, 'keydown', (e) => this.keyDown(e));
    on(window, 'keyup', (e) => { this.keys.delete(e.code); if (e.key === 'Alt') this.view.entities.showAllBars = false; });
    on(window, 'blur', () => { this.keys.clear(); this.cancelDrag(); });
  }

  get pointer() { return this.target; }

  private down(e: MouseEvent) {
    if (!this.enabled) return;
    if (e.button === 1) { e.preventDefault(); this.middle = { x: e.clientX, y: e.clientY }; return; }
    this.target = this.view.resolve(e.clientX, e.clientY);
    const session = this.session;
    if (e.button === 0) {
      if (session.hasTargetingMode) { if (this.target.point) this.order(e.shiftKey, true); return; }
      this.dragStart = { x: e.clientX, y: e.clientY, plane: this.target.plane, entity: this.target.entity, resource: this.target.resource };
      this.dragging = false;
    } else if (e.button === 2) {
      if (this.dragStart) { this.cancelDrag(); return; }
      if (this.target.point) this.order(e.shiftKey, false);
    }
  }

  private up(e: MouseEvent) {
    if (e.button === 1) { this.middle = null; return; }
    if (e.button !== 0 || !this.dragStart) return;
    const start = this.dragStart; this.dragStart = null;
    this.box.style.display = 'none';
    if (!this.enabled) return;
    const target = this.view.resolve(e.clientX, e.clientY);
    const additive = e.shiftKey;
    if (this.dragging) {
      const a = start.plane, b = target.plane;
      if (a && b) {
        const clamp = (p: Point): Point => ({ x: Math.max(0, Math.min(this.session.map.width, p.x)), y: Math.max(0, Math.min(this.session.map.height, p.y)) });
        this.selectScreenBox(start, { x: e.clientX, y: e.clientY }, additive, clamp(a), clamp(b));
      }
      this.dragging = false;
      return;
    }
    const now = performance.now() / 1000;
    const entity = target.entity || start.entity;
    if (e.ctrlKey || e.metaKey) {
      if (entity) this.selectSameTypeOnScreen(entity, additive);
    } else if (entity && entity === this.lastSelect.entity && now - this.lastSelect.time <= 0.35) {
      this.session.selectSameTypeOnMap(entity, additive);
      this.lastSelect = { time: -1, entity: 0 };
    } else {
      this.session.select(entity, additive, entity ? 0 : target.resource || start.resource);
      this.lastSelect = { time: now, entity };
    }
    this.onFeedback?.('select');
  }

  private move(e: MouseEvent) {
    if (this.middle) {
      const scale = this.view.rts.panSpeed / Math.max(400, window.innerHeight);
      this.view.rts.pan(-(e.clientX - this.middle.x) * scale, (e.clientY - this.middle.y) * scale);
      this.middle = { x: e.clientX, y: e.clientY };
    }
    if (this.dragStart) {
      const dx = e.clientX - this.dragStart.x, dy = e.clientY - this.dragStart.y;
      if (!this.dragging && Math.hypot(dx, dy) > 6) this.dragging = true;
      if (this.dragging) {
        const left = Math.min(e.clientX, this.dragStart.x), top = Math.min(e.clientY, this.dragStart.y);
        Object.assign(this.box.style, { display: 'block', left: left + 'px', top: top + 'px', width: Math.abs(dx) + 'px', height: Math.abs(dy) + 'px' });
      }
    }
  }

  // Screen-space box: entities whose projected position is inside the rectangle.
  private selectScreenBox(start: { x: number; y: number }, end: { x: number; y: number }, additive: boolean, a: Point, b: Point) {
    const session = this.session, view = session.view;
    if (!view) return;
    const left = Math.min(start.x, end.x), right = Math.max(start.x, end.x), top = Math.min(start.y, end.y), bottom = Math.max(start.y, end.y);
    const ids: number[] = [];
    for (const entity of view.entities) {
      if (entity.owner !== view.player || entity.remembered || entity.health <= 0) continue;
      const type = session.catalog.get(entity.definition);
      const p = type.building ? session.catalog.center(entity.definition, entity.position) : session.presentation.position(entity);
      const s = this.view.project(p);
      if (s.x >= left && s.x <= right && s.y >= top && s.y <= bottom) ids.push(entity.id);
    }
    if (ids.length === 0) { session.selectRectangle(a, b, additive); return; }
    const units = ids.filter((id) => !session.catalog.get(session.entity(id)!.definition).building);
    session.selectEntities(units.length > 0 ? units : ids, additive);
  }

  private selectSameTypeOnScreen(id: number, additive: boolean) {
    const session = this.session, view = session.view, source = session.entity(id);
    if (!view || !source || source.owner !== view.player) return;
    const rect = this.surface.getBoundingClientRect();
    const ids = view.entities.filter((e) => e.owner === view.player && !e.remembered && e.health > 0 && e.definition === source.definition).filter((e) => {
      const s = this.view.project(session.catalog.center(e.definition, session.presentation.position(e)));
      return s.x >= rect.left && s.x <= rect.right && s.y >= rect.top && s.y <= rect.bottom;
    }).map((e) => e.id);
    session.selectEntities(ids, additive);
  }

  // Port of CommanderInput.OrderClick, including the double-tap attack conversion.
  private order(append: boolean, leftConfirm: boolean) {
    const session = this.session, target = this.target;
    if (!target.point || !session.canIssueOrders) return;
    const now = performance.now() / 1000;
    const armedAttack = session.attackMoveMode, armedAbility = session.pendingAbility !== undefined;
    const mode = session.hasTargetingMode || session.canSetRallyPoint;
    const selectionKey = [...session.selection].sort((a, b) => a - b).join(',');
    const a = this.action;
    const nearby = Math.hypot(a.point.x - target.point.x, a.point.y - target.point.y) <= 1.5;
    let sameTarget = a.entity === target.entity && a.resource === target.resource && (target.entity > 0 || nearby);
    const view = session.view;
    const previousGone = a.entity > 0 && !!view && !view.entities.some((e) => e.id === a.entity && !e.remembered && e.health > 0);
    const previousEnemyAlive = a.entity > 0 && !!view && view.entities.some((e) => e.id === a.entity && e.owner !== view.player && !e.remembered && e.health > 0);
    if (nearby && (a.confirmed || previousGone || (!previousEnemyAlive && (a.resource === 0 || target.resource === 0)))) sameTarget = true;
    const twice = !mode && !leftConfirm && a.time >= 0 && now - a.time <= 0.55 && a.sequence > 0 && a.append === append && sameTarget && a.selection === selectionKey;
    const replace = twice && append ? a.sequence : 0;
    const confirmed = a.confirmed;
    this.action = { time: -1, sequence: 0, entity: 0, resource: 0, point: { x: 0, y: 0 }, append: false, selection: '', confirmed: false };
    if (twice && confirmed) return;
    const sequence = session.context(target.point, target.entity, target.resource, append, twice, replace);
    if (sequence === 0) { if (session.selection.size > 0) this.onFeedback?.('error'); return; }
    this.onFeedback?.('order');
    if (twice || (mode && !armedAttack && !armedAbility)) return;
    this.action = { time: now, sequence, entity: target.entity, resource: target.resource, point: target.point, append, selection: selectionKey, confirmed: armedAttack || armedAbility };
  }

  private cancelDrag() { this.dragStart = null; this.dragging = false; this.box.style.display = 'none'; }

  private keyDown(e: KeyboardEvent) {
    if (!this.enabled) return;
    const element = e.target as HTMLElement | null;
    if (element && (element.tagName === 'INPUT' || element.tagName === 'SELECT' || element.tagName === 'TEXTAREA')) return;
    this.keys.add(e.code);
    const session = this.session;
    if (e.key === 'Alt') { this.view.entities.showAllBars = true; e.preventDefault(); return; }
    if (e.code === 'Escape' || e.code === 'F10') {
      e.preventDefault();
      if (this.dragStart) { this.cancelDrag(); return; }
      if (session.hasTargetingMode) { session.cancelMode(); return; }
      this.onMenu?.(); return;
    }
    const digit = /^Digit(\d)$/.exec(e.code);
    if (digit) {
      e.preventDefault();
      const index = (Number(digit[1]) + 9) % 10;
      if (e.ctrlKey || e.metaKey) session.group(index, 'save');
      else if (e.shiftKey) session.group(index, 'add');
      else {
        const now = performance.now() / 1000;
        const count = session.group(index, 'recall');
        if (count > 0 && this.lastGroup.index === index && now - this.lastGroup.time < 0.4) this.centerOnSelection();
        this.lastGroup = { index, time: now };
      }
      return;
    }
    if (e.code === 'F1') { e.preventDefault(); session.selectIdleWorkers(); return; }
    if (e.code === 'F2') { e.preventDefault(); session.selectArmy(); return; }
    if (e.code === 'Space') { e.preventDefault(); this.centerOnSelection(); return; }
    if (e.code === 'Home') { e.preventDefault(); this.centerOnBase(); return; }
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    const letter = /^Key([A-Z])$/.exec(e.code);
    if (letter && this.onAction?.(letter[1])) e.preventDefault();
  }

  centerOnSelection(): void {
    const session = this.session;
    const selected = session.selected();
    if (selected.length === 0) { this.centerOnBase(); return; }
    let x = 0, y = 0;
    for (const e of selected) { const p = session.catalog.center(e.definition, session.presentation.position(e)); x += p.x; y += p.y; }
    this.view.rts.centerOn(x / selected.length, y / selected.length);
  }

  centerOnBase(): void {
    const session = this.session, view = session.view;
    const hq = view?.entities.find((e) => e.owner === view.player && !e.remembered && session.catalog.get(e.definition).role === 'hq');
    if (hq) { const c = session.catalog.center(hq.definition, hq.position); this.view.rts.centerOn(c.x, c.y); }
  }

  // Per-frame: hover target, keyboard and edge scrolling.
  update(dt: number): void {
    if (this.mouse.inside && !this.dragStart) {
      this.target = this.view.resolve(this.mouse.x, this.mouse.y);
      this.view.entities.hovered = this.enabled ? this.target.entity : 0;
      this.view.overlays.pointer = this.enabled ? this.target.point : null;
      this.view.overlays.pointerResource = this.target.resource;
    } else if (!this.mouse.inside) { this.view.entities.hovered = 0; this.view.overlays.pointer = null; }
    if (!this.enabled) return;
    const speed = this.view.rts.panSpeed * dt * this.scrollSpeed;
    let dx = 0, dy = 0;
    if (this.keys.has('ArrowLeft')) dx -= 1;
    if (this.keys.has('ArrowRight')) dx += 1;
    if (this.keys.has('ArrowUp')) dy += 1;
    if (this.keys.has('ArrowDown')) dy -= 1;
    if (this.edgeScroll && this.mouse.inside && document.hasFocus() && !this.middle) {
      const margin = 6;
      if (this.mouse.x <= margin) dx -= 1; else if (this.mouse.x >= window.innerWidth - margin) dx += 1;
      if (this.mouse.y <= margin) dy += 1; else if (this.mouse.y >= window.innerHeight - margin) dy -= 1;
    }
    if (dx || dy) this.view.rts.pan(dx * speed, dy * speed);
  }

  dispose(): void { for (const dispose of this.disposers) dispose(); this.box.remove(); }
}
