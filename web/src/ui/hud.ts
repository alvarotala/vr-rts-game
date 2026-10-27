import { CommandKind, type VisibleEntity } from '../protocol/types';
import type { GameSession } from '../game/session';
import { selectionActions, roleName, type SelectionAction } from '../game/actions';
import { TickRate, optimalWorkers } from '../game/catalog';
import type { IconRenderer } from './icons';
import type { Minimap } from './minimap';
import { h, clear, fill, formatTime } from './dom';
import { t, tf } from '../i18n/i18n';

const Slots = ['Q', 'W', 'E', 'R', 'T', 'A', 'S', 'D', 'F', 'G', 'Z', 'X', 'C', 'V', 'B'];
const Glyphs: Record<string, string> = { 'attack-move': '⚔', stop: '■', hold: '⛨', 'set-rally-point': '⚑', 'clear-rally-point': '⚐', cancel: '✕', 'cancel-construction': '✕' };

export function orderText(session: GameSession, entity: VisibleEntity): string {
  const type = session.catalog.get(entity.definition);
  if (entity.remembered) return t('Last seen');
  if (!entity.complete) return entity.awaitingClearance ? t('CLEARING AREA') : tf('Under construction · {0}%', Math.floor(entity.progress * 100));
  if (type.role === 'worker' && entity.order === CommandKind.Gather)
    return entity.depositing ? tf('Delivering {0}', entity.cargoKind === 'catalyst' ? t('gas') : t('minerals')) : entity.cargoKind === 'catalyst' ? t('Gathering gas') : t('Gathering minerals');
  switch (entity.order) {
    case CommandKind.Move: return t('Moving');
    case CommandKind.Attack: return t('Attacking');
    case CommandKind.AttackMove: return t('Attack moving');
    case CommandKind.Gather: return t('Gathering');
    case CommandKind.Repair: return t('Repairing');
    case CommandKind.Build: return t('Constructing');
    case CommandKind.Hold: return t('Holding position');
    case CommandKind.Cast: return t('Casting');
    default: return type.building ? (entity.queue.length > 0 ? t('Producing') : t('Ready')) : t('Idle');
  }
}

// Desktop HUD: resources and supply, match clock, selection details, production queue,
// the 5×3 command card with hotkeys, control groups and the minimap.
export class Hud {
  readonly root: HTMLDivElement;
  private readonly minerals = h('span', { class: 'value' });
  private readonly gas = h('span', { class: 'value' });
  private readonly supply = h('span', { class: 'value' });
  private readonly clock = h('span', { class: 'clock' });
  private readonly modeLabel = h('span', { class: 'mode' });
  private readonly idleButton = h('button', { class: 'chip', title: 'F1' });
  private readonly armyButton = h('button', { class: 'chip', title: 'F2' });
  private readonly message = h('div', { class: 'message' });
  private readonly countdown = h('div', { class: 'countdown' });
  private readonly groups = h('div', { class: 'groups' });
  private readonly info = h('div', { class: 'info panel' });
  private readonly heading = h('div', { class: 'heading' });
  private readonly card = h('div', { class: 'command-card' });
  private readonly tooltip = h('div', { class: 'tooltip' });
  private readonly buttons: HTMLButtonElement[] = [];
  private actions: SelectionAction[] = [];
  private infoKey = '';
  private cardKey = '';
  private groupsKey = '';
  private lastInfo = 0;
  private messageVersion = -1;
  private messageUntil = 0;
  private hoverAction: SelectionAction | null = null;
  onMenu: (() => void) | null = null;
  onActionResult: ((ok: boolean) => void) | null = null;

  constructor(private readonly session: GameSession, private readonly icons: IconRenderer, minimap: Minimap, modeText: string, private readonly centerOn: (x: number, y: number) => void) {
    this.modeLabel.textContent = modeText;
    this.idleButton.addEventListener('click', () => session.selectIdleWorkers());
    this.armyButton.addEventListener('click', () => session.selectArmy());
    const top = h('div', { class: 'topbar panel' },
      h('button', { class: 'menu-button', onclick: () => this.onMenu?.(), title: 'Esc' }, '☰ ' + t('Menu')),
      h('div', { class: 'resource minerals', title: t('Minerals') }, h('i'), this.minerals),
      h('div', { class: 'resource gas', title: t('Gas') }, h('i'), this.gas),
      h('div', { class: 'resource supply', title: t('Supply') }, h('i'), this.supply),
      this.idleButton, this.armyButton, h('div', { class: 'spacer' }), this.modeLabel, this.clock);
    for (let i = 0; i < 15; i++) {
      const button = h('button', { class: 'action empty', dataset: { slot: String(i) } });
      button.addEventListener('click', () => this.execute(this.actions.find((a) => a.hotkey === Slots[i])));
      button.addEventListener('mouseenter', () => { this.hoverAction = this.actions.find((a) => a.hotkey === Slots[i]) ?? null; this.renderTooltip(button); });
      button.addEventListener('mouseleave', () => { this.hoverAction = null; this.tooltip.style.display = 'none'; });
      this.buttons.push(button); this.card.append(button);
    }
    const commands = h('div', { class: 'commands panel' }, this.heading, this.card);
    const minimapPanel = h('div', { class: 'minimap-panel panel' }, minimap.element);
    const bottom = h('div', { class: 'bottom' }, minimapPanel, this.info, commands);
    this.root = h('div', { class: 'hud' }, top, this.message, this.countdown, this.groups, bottom, this.tooltip);
    for (const element of [bottom, top, this.groups]) element.addEventListener('mousedown', (e) => {
      e.stopPropagation();
      // Clicking a HUD button must not keep keyboard focus (Space/Enter would press it again).
      if ((e.target as HTMLElement).closest('button')) e.preventDefault();
    });
  }

  // Hotkey from the keyboard: true when an action used it.
  hotkey(letter: string): boolean {
    const action = this.actions.find((a) => a.hotkey === letter);
    if (!action) return false;
    this.execute(action);
    return true;
  }

  private execute(action: SelectionAction | undefined) {
    if (!action || !this.session.canIssueOrders) return;
    const ok = action.execute();
    this.onActionResult?.(ok);
    this.cardKey = '';
  }

  update(now: number): void {
    const session = this.session, view = session.view;
    if (view) {
      this.minerals.textContent = String(view.minerals);
      this.gas.textContent = String(view.catalyst);
      this.supply.textContent = view.supply + ' / ' + view.capacity;
      this.supply.parentElement!.classList.toggle('full', view.supply >= view.capacity);
      this.clock.textContent = formatTime(view.tick);
      const idle = session.idleWorkers().length, army = session.army().length;
      this.idleButton.textContent = tf('Idle workers: {0}', idle);
      this.idleButton.classList.toggle('alert', idle > 0);
      this.armyButton.textContent = tf('Army: {0}', army);
    }
    const a = session.announcement;
    if (a.version !== this.messageVersion) {
      this.messageVersion = a.version; this.messageUntil = now + (a.error ? 4 : 3.2);
      this.message.textContent = a.text; this.message.classList.toggle('error', a.error);
    }
    this.message.style.opacity = a.text && now < this.messageUntil ? '1' : '0';
    const remaining = session.countdownRemaining;
    this.countdown.style.display = remaining > 0 ? 'block' : 'none';
    if (remaining > 0) this.countdown.textContent = String(Math.ceil(remaining));
    this.updateGroups();
    this.updateCard();
    if (now - this.lastInfo > 0.12) { this.lastInfo = now; this.updateInfo(); }
  }

  private updateGroups() {
    const sizes = this.session.groups.map((_, i) => this.session.groupSize(i));
    const key = sizes.join(',');
    if (key === this.groupsKey) return;
    this.groupsKey = key;
    clear(this.groups);
    sizes.forEach((size, index) => {
      if (size === 0) return;
      const label = String((index + 1) % 10);
      this.groups.append(h('button', { class: 'group', title: tf('Recall group {0} · Ctrl+{0} replaces · Shift+{0} adds', label), onclick: () => this.session.group(index, 'recall') },
        h('b', {}, label), String(size)));
    });
  }

  private updateCard() {
    const session = this.session;
    const result = selectionActions(session);
    const view = session.view;
    const key = result.heading + '|' + result.items.map((a) => `${a.id}:${a.active}:${a.blocked}:${a.detail}:${view ? (view.minerals >= a.minerals && view.catalyst >= a.gas) : true}`).join('|');
    this.actions = result.items;
    if (key === this.cardKey) return;
    this.cardKey = key;
    this.heading.textContent = result.items.length > 0 ? result.heading : result.hint;
    this.heading.classList.toggle('hint', result.items.length === 0);
    for (let i = 0; i < this.buttons.length; i++) {
      const button = this.buttons[i];
      const action = result.items.find((a) => a.hotkey === Slots[i]);
      clear(button);
      button.className = 'action' + (action ? ' ' + action.category : ' empty') + (action?.active ? ' active' : '')
        + (action && action.blocked ? ' blocked' : '') + (action && view && (view.minerals < action.minerals || view.catalyst < action.gas) ? ' poor' : '');
      button.disabled = !action;
      if (!action) continue;
      const icon = action.model ? h('img', { src: this.icons.icon(action.model), alt: '' }) : h('span', { class: 'glyph' }, Glyphs[action.id] ?? (action.category === 'research' ? '⬆' : action.category === 'abilities' ? '✦' : '•'));
      fill(button, icon, h('span', { class: 'key' }, action.hotkey), h('span', { class: 'label' }, t(action.label)),
        action.minerals || action.gas ? h('span', { class: 'cost' }, action.minerals + (action.gas ? ' · ' + action.gas : '')) : null);
    }
    if (this.hoverAction) {
      const index = Slots.indexOf(this.hoverAction.hotkey);
      this.hoverAction = result.items.find((a) => a.hotkey === this.hoverAction!.hotkey) ?? null;
      if (index >= 0) this.renderTooltip(this.buttons[index]);
    }
  }

  private renderTooltip(anchor: HTMLElement) {
    const action = this.hoverAction;
    if (!action) { this.tooltip.style.display = 'none'; return; }
    fill(clear(this.tooltip),
      h('div', { class: 'title' }, t(action.title), h('span', { class: 'key' }, action.hotkey)),
      action.minerals || action.gas ? h('div', { class: 'costs' }, h('span', { class: 'minerals' }, String(action.minerals)), action.gas ? h('span', { class: 'gas' }, String(action.gas)) : null) : null,
      action.description ? h('div', { class: 'description' }, action.description) : null,
      action.helpDetail ? h('div', { class: 'detail' }, action.helpDetail) : null,
      action.blocked ? h('div', { class: 'blocked' }, action.blocked) : null);
    const rect = anchor.getBoundingClientRect();
    this.tooltip.style.display = 'block';
    const width = 300;
    this.tooltip.style.left = Math.max(8, Math.min(window.innerWidth - width - 8, rect.left + rect.width / 2 - width / 2)) + 'px';
    this.tooltip.style.bottom = (window.innerHeight - rect.top + 10) + 'px';
  }

  private updateInfo() {
    const session = this.session, view = session.view;
    if (!view) return;
    const selected = session.selected().filter((e) => !e.remembered);
    const signature = [view.tick, [...session.selection].join(','), session.selectedResource, session.inspected].join('|');
    if (signature === this.infoKey) return;
    this.infoKey = signature;
    clear(this.info);
    if (session.selectedResource) {
      const resource = view.resources.find((r) => r.id === session.selectedResource);
      if (resource) {
        const gas = resource.kind === 'catalyst';
        this.info.append(h('div', { class: 'single' },
          h('div', { class: 'portrait glyph ' + (gas ? 'gas' : 'minerals') }, gas ? '◉' : '◆'),
          h('div', { class: 'details' }, h('div', { class: 'name' }, gas ? t('Gas deposit') : t('Mineral deposit')),
            h('div', { class: 'sub' }, tf('{0} remaining', resource.remaining)),
            h('div', { class: 'sub' }, tf('Workers {0}/{1}', resource.assignedWorkers, resource.optimalWorkers || optimalWorkers(resource.kind))))));
      }
      return;
    }
    if (selected.length === 0) {
      const inspected = session.inspected ? session.entity(session.inspected) : undefined;
      if (inspected) { this.info.append(this.single(inspected)); return; }
      this.info.append(h('div', { class: 'empty-info' }, h('div', {}, t('Select units or buildings. Right-click to give orders.')),
        h('div', { class: 'sub' }, t('Shift adds · Ctrl+1–0 saves a group · F1 idle workers · F2 army'))));
      return;
    }
    if (selected.length === 1) { this.info.append(this.single(selected[0])); return; }
    const grid = h('div', { class: 'cards' });
    const sorted = [...selected].sort((a, b) => a.definition.localeCompare(b.definition) || a.id - b.id);
    for (const entity of sorted.slice(0, 40)) {
      const type = session.catalog.get(entity.definition);
      const fraction = Math.max(0, Math.min(1, entity.health / type.health));
      const card = h('button', { class: 'card', title: t(type.name) },
        h('img', { src: this.icons.icon(type), alt: '' }),
        h('span', { class: 'bar' }, h('i', { style: { width: (fraction * 100) + '%', background: fraction > 0.6 ? '#48e06a' : fraction > 0.3 ? '#f2c94c' : '#f05545' } })));
      card.addEventListener('click', (e) => {
        if (e.shiftKey) session.deselect(entity.id);
        else if (e.ctrlKey || e.metaKey) session.selectTypeInSelection(entity.definition, false);
        else session.selectEntities([entity.id], false);
      });
      card.addEventListener('dblclick', () => { const c = session.catalog.center(entity.definition, entity.position); this.centerOn(c.x, c.y); });
      grid.append(card);
    }
    if (sorted.length > 40) grid.append(h('div', { class: 'more' }, '+' + (sorted.length - 40)));
    this.info.append(grid);
    const producers = selected.filter((e) => e.queue.length > 0);
    if (producers.length > 0 && selected.every((e) => session.catalog.get(e.definition).building)) this.info.append(this.queue(producers, false));
  }

  private single(entity: VisibleEntity): HTMLElement {
    const session = this.session, type = session.catalog.get(entity.definition);
    const own = entity.owner === session.localPlayer;
    const fraction = Math.max(0, Math.min(1, entity.health / type.health));
    const faction = session.catalog.faction(type.faction)?.name ?? type.faction;
    const details = h('div', { class: 'details' },
      h('div', { class: 'name' }, t(type.name), own ? null : h('span', { class: 'enemy-tag' }, t('Enemy'))),
      h('div', { class: 'sub' }, (type.building ? t('Structure') : roleName(type.role)) + ' · ' + t(faction)),
      h('div', { class: 'health' }, h('span', { class: 'bar' }, h('i', { style: { width: (fraction * 100) + '%' } })), h('span', {}, Math.ceil(entity.health) + ' / ' + type.health)),
      own && type.energyMax > 0 ? h('div', { class: 'energy' }, h('span', { class: 'bar' }, h('i', { style: { width: Math.min(100, entity.energy / type.energyMax * 100) + '%' } })), h('span', {}, Math.floor(entity.energy) + ' / ' + type.energyMax)) : null,
      h('div', { class: 'status' }, own ? orderText(session, entity) : entity.remembered ? t('Last seen') : ''),
      own && type.role === 'worker' && entity.cargo > 0 ? h('div', { class: 'sub' }, tf('Carrying {0}', entity.cargo)) : null,
      !type.building && type.damage > 0 ? h('div', { class: 'stats' }, tf('Damage {0} · Range {1} · Speed {2}', type.damage, type.range, type.speed)) : null);
    const block = h('div', { class: 'single' }, h('img', { class: 'portrait', src: this.icons.icon(type, own), alt: '' }), details);
    if (own && entity.complete && type.building && (entity.queue.length > 0 || session.catalog.canProduceUnits(entity.definition) || type.role === 'research'))
      return h('div', {}, block, this.queue([entity], true));
    if (own && type.building && session.view) {
      const base = session.view.resources.filter((r) => r.baseId === entity.id || r.extractorId === entity.id);
      if (base.length > 0) {
        const assigned = base.reduce((n, r) => n + r.assignedWorkers, 0), optimal = base.reduce((n, r) => n + r.optimalWorkers, 0);
        details.append(h('div', { class: 'sub' + (assigned > optimal ? ' warn' : '') }, tf(type.role === 'extractor' ? 'Gas workers {0}/{1}' : 'Mineral workers {0}/{1}', assigned, optimal)));
      }
    }
    return block;
  }

  // Up to five queued orders; with one producer selected each slot cancels and refunds its order.
  private queue(producers: VisibleEntity[], cancellable: boolean): HTMLElement {
    const session = this.session;
    const items = producers.flatMap((producer, building) => producer.queue.map((order, depth) => ({ producer, order, depth, building })))
      .sort((a, b) => a.depth - b.depth || a.building - b.building).slice(0, 5);
    const row = h('div', { class: 'queue' }, h('div', { class: 'queue-title' }, t('PRODUCTION QUEUE') + ' · ' + items.length + ' / ' + (producers.length * 5)));
    for (let i = 0; i < 5; i++) {
      const item = items[i];
      if (!item) { row.append(h('div', { class: 'slot empty' })); continue; }
      const { order, producer } = item;
      const research = order.research;
      const type = research ? undefined : session.catalog.find(order.definition);
      const upgrade = research ? session.catalog.upgrade(order.definition) : undefined;
      const progress = order.total > 0 ? 1 - order.remaining / order.total : 0;
      const slot = h('button', { class: 'slot' + (item.depth === 0 ? ' active' : ''), title: t(type?.name ?? upgrade?.name ?? order.definition) + (cancellable ? ' · ' + t('Cancel + refund') : '') },
        type ? h('img', { src: this.icons.icon(type), alt: '' }) : h('span', { class: 'glyph' }, '⬆'),
        h('span', { class: 'bar' }, h('i', { style: { width: (progress * 100) + '%' } })),
        item.depth === 0 ? h('span', { class: 'time' }, Math.ceil(order.remaining / TickRate) + 's') : null);
      if (cancellable) slot.addEventListener('click', () => session.send(CommandKind.Cancel, { queueOrderId: order.id, entityIds: [producer.id] }));
      else slot.disabled = true;
      row.append(slot);
    }
    return row;
  }

  dispose(): void { this.root.remove(); }
}
