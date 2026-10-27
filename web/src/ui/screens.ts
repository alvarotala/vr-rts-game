import { h, clear, formatTime } from './dom';
import { t, tf, languagePreference, setLanguage, type LanguagePreference } from '../i18n/i18n';
import type { Settings } from '../settings';
import type { MatchReport, PlayerStats } from '../protocol/types';
import type { Catalog } from '../game/catalog';

export interface MapChoice { id: string; title: string }
export interface Choice { value: string; label: string }

function select(label: string, value: string, choices: Choice[], change: (value: string) => void): HTMLElement {
  const element = h('select', { onchange: (e: Event) => change((e.target as HTMLSelectElement).value) },
    ...choices.map((c) => h('option', { value: c.value, selected: c.value === value }, c.label)));
  return h('label', { class: 'field' }, h('span', {}, label), element);
}
function input(label: string, value: string, change: (value: string) => void, placeholder = ''): HTMLElement {
  return h('label', { class: 'field' }, h('span', {}, label), h('input', { value, placeholder, spellcheck: false, oninput: (e: Event) => change((e.target as HTMLInputElement).value) }));
}
function range(label: string, value: number, change: (value: number) => void, min = 0, max = 1, step = 0.05): HTMLElement {
  return h('label', { class: 'field' }, h('span', {}, label), h('input', { type: 'range', min, max, step, value, oninput: (e: Event) => change(Number((e.target as HTMLInputElement).value)) }));
}
function toggle(label: string, value: boolean, change: (value: boolean) => void): HTMLElement {
  return h('label', { class: 'field toggle' }, h('input', { type: 'checkbox', checked: value, onchange: (e: Event) => change((e.target as HTMLInputElement).checked) }), h('span', {}, label));
}

export interface MenuCallbacks {
  offline(): void; findMatch(): void; direct(): void; cancel(): void; settingsChanged(): void; click(): void;
}

// Full-screen menus outside a match.
export class Screens {
  readonly root = h('div', { class: 'screens' });
  private status = h('div', { class: 'status' });
  constructor(private readonly settings: Settings, private readonly catalog: Catalog, private readonly maps: MapChoice[],
    private readonly callbacks: MenuCallbacks, private readonly version: string) {}

  private factions(): Choice[] { return this.catalog.definition.factions.map((f) => ({ value: f.id, label: t(f.name) })); }
  private page(title: string, ...content: (HTMLElement | null)[]): HTMLElement {
    const card = h('div', { class: 'menu-card' }, h('h2', {}, title), ...content.filter((c): c is HTMLElement => c !== null));
    clear(this.root).append(h('div', { class: 'backdrop' }), card);
    this.root.style.display = 'flex';
    return card;
  }
  private button(label: string, action: () => void, kind = ''): HTMLButtonElement {
    return h('button', { class: 'menu-button-large ' + kind, onclick: () => { this.callbacks.click(); action(); } }, label);
  }
  hide(): void { this.root.style.display = 'none'; }

  main(): void {
    clear(this.root).append(h('div', { class: 'backdrop' }), h('div', { class: 'title-block' },
      h('h1', {}, 'Little Legion'),
      h('div', { class: 'tagline' }, t('Small armies. Big battles.')),
      h('div', { class: 'menu-list' },
        this.button(t('Play vs AI'), () => this.offline(), 'primary'),
        this.button(t('Find online match'), () => this.findMatch()),
        this.button(t('Direct connection'), () => this.direct()),
        this.button(t('Settings'), () => this.settingsPage()),
        this.button(t('Controls'), () => this.controls())),
      h('div', { class: 'footer' }, t('Desktop · plays with Quest players on the same servers'), h('span', { class: 'version' }, this.version))));
    this.root.style.display = 'flex';
  }

  offline(): void {
    const s = this.settings;
    const difficulties: Choice[] = [{ value: 'easy', label: t('Easy') }, { value: 'normal', label: t('Normal') }, { value: 'hard', label: t('Hard') }];
    this.page(t('Play vs AI'),
      select(t('Your faction'), s.faction, this.factions(), (v) => { s.faction = v; this.callbacks.settingsChanged(); }),
      select(t('Opponent'), s.enemyFaction, this.factions(), (v) => { s.enemyFaction = v; this.callbacks.settingsChanged(); }),
      select(t('Difficulty'), s.difficulty, difficulties, (v) => { s.difficulty = v; this.callbacks.settingsChanged(); }),
      select(t('Map'), s.mapId, this.maps.map((m) => ({ value: m.id, label: t(m.title) })), (v) => { s.mapId = v; this.callbacks.settingsChanged(); }),
      h('div', { class: 'row' }, this.button(t('Back'), () => this.main()), this.button(t('Start Match'), () => this.callbacks.offline(), 'primary')));
  }

  findMatch(): void {
    const s = this.settings;
    this.status = h('div', { class: 'status' });
    this.page(t('Find online match'),
      h('p', { class: 'note' }, t('Public 1v1 against Quest or desktop players through the self-hosted matchmaking service.')),
      input(t('Online service'), s.backendUrl, (v) => { s.backendUrl = v.trim(); this.callbacks.settingsChanged(); }, 'https://play.example.com'),
      select(t('Your faction'), s.faction, this.factions(), (v) => { s.faction = v; this.callbacks.settingsChanged(); }),
      this.status,
      h('div', { class: 'row' }, this.button(t('Back'), () => { this.callbacks.cancel(); this.main(); }), this.button(t('Search'), () => this.callbacks.findMatch(), 'primary')));
  }

  direct(): void {
    const s = this.settings;
    this.status = h('div', { class: 'status' });
    this.page(t('Direct connection'),
      h('p', { class: 'note' }, t('Join a dedicated server on your network. Quest players connect to the same server over UDP 7777; browsers use its websocket port (7877 by default).')),
      input(t('Server address'), s.directAddress, (v) => { s.directAddress = v.trim(); this.callbacks.settingsChanged(); }, '192.168.1.20:7877'),
      select(t('Your faction'), s.faction, this.factions(), (v) => { s.faction = v; this.callbacks.settingsChanged(); }),
      this.status,
      h('div', { class: 'row' }, this.button(t('Back'), () => { this.callbacks.cancel(); this.main(); }), this.button(t('Connect'), () => this.callbacks.direct(), 'primary')));
  }

  setStatus(text: string, error = false): void { this.status.textContent = text; this.status.classList.toggle('error', error); }

  loading(text: string, cancel?: () => void): void {
    this.status = h('div', { class: 'status' }, text);
    this.page(t('Little Legion'), h('div', { class: 'spinner' }), this.status, cancel ? h('div', { class: 'row' }, this.button(t('Cancel'), cancel)) : null);
  }

  settingsPage(back: () => void = () => this.main()): void {
    const s = this.settings;
    const languages: Choice[] = [{ value: 'system', label: t('System language') }, { value: 'en', label: 'English' }, { value: 'es', label: 'Español' }];
    this.page(t('Settings'),
      select(t('Language'), languagePreference(), languages, (v) => { setLanguage(v as LanguagePreference); this.settingsPage(back); }),
      range(t('Music'), s.musicVolume, (v) => { s.musicVolume = v; this.callbacks.settingsChanged(); }),
      range(t('Effects'), s.effectsVolume, (v) => { s.effectsVolume = v; this.callbacks.settingsChanged(); }),
      range(t('Camera speed'), s.scrollSpeed, (v) => { s.scrollSpeed = v; this.callbacks.settingsChanged(); }, 0.4, 2.5, 0.1),
      toggle(t('Scroll at screen edges'), s.edgeScroll, (v) => { s.edgeScroll = v; this.callbacks.settingsChanged(); }),
      toggle(t('Shadows'), s.shadows, (v) => { s.shadows = v; this.callbacks.settingsChanged(); }),
      h('div', { class: 'row' }, this.button(t('Back'), back)));
  }

  controls(back: () => void = () => this.main()): void {
    const rows: [string, string][] = [
      [t('Select · drag to select an area'), t('Left click')], [t('Add or remove from selection'), 'Shift + ' + t('Left click')],
      [t('Select the same type nearby'), t('Double click')], [t('Select the same type on screen'), 'Ctrl + ' + t('Left click')],
      [t('Contextual order: move, attack, gather, repair, rally'), t('Right click')], [t('Queue the order'), 'Shift + ' + t('Right click')],
      [t('Attack Move / attack the target'), t('Double right click') + ' · A'], [t('Stop · Hold Position'), 'S · D'],
      [t('Command card'), 'Q W E R T · Z X C V B'], [t('Rally point · cancel'), 'F · G'],
      [t('Recall / save / add to group'), '1–0 · Ctrl+1–0 · Shift+1–0'], [t('Idle workers · army'), 'F1 · F2'],
      [t('Move the camera'), t('Arrows · screen edges · middle drag · minimap')], [t('Zoom'), t('Mouse wheel')],
      [t('Center on selection · on base'), t('Space · Home')], [t('Show all health bars'), 'Alt'], [t('Cancel · menu'), 'Esc'],
    ];
    this.page(t('Controls'), h('table', { class: 'controls' }, ...rows.map(([a, b]) => h('tr', {}, h('td', {}, a), h('td', { class: 'keys' }, b)))),
      h('div', { class: 'row' }, this.button(t('Back'), back)));
  }
}

// Post-match overlay: outcome, duration and the shared report comparison.
export function resultsPanel(title: string, victory: boolean | null, report: MatchReport | null, player: number, unavailable: boolean, catalog: Catalog,
  actions: { label: string; run: () => void; primary?: boolean }[]): HTMLElement {
  const panel = h('div', { class: 'results menu-card' }, h('h2', { class: victory === null ? '' : victory ? 'victory' : 'defeat' }, title));
  if (report && report.samples.length > 0) {
    const last = report.samples[report.samples.length - 1];
    const me = last.players[player], them = last.players[1 - player];
    panel.append(h('div', { class: 'duration' }, tf('Duration {0}', formatTime(report.tick))));
    const row = (label: string, value: (p: PlayerStats) => string | number) => h('tr', {}, h('td', {}, label), h('td', {}, String(value(me))), h('td', {}, String(value(them))));
    const faction = (p: PlayerStats) => t(catalog.faction(p.Faction)?.name ?? p.Faction);
    panel.append(h('table', { class: 'report' },
      h('tr', {}, h('th', {}), h('th', {}, t('You')), h('th', {}, t('Opponent'))),
      row(t('Faction'), faction),
      row(t('Workers trained'), (p) => p.WorkersProduced), row(t('Army trained'), (p) => p.ArmyProduced),
      row(t('Structures completed'), (p) => p.StructuresCompleted), row(t('Units lost'), (p) => p.WorkersLost + p.ArmyLost),
      row(t('Structures lost'), (p) => p.StructuresLost), row(t('Minerals collected'), (p) => p.MineralsCollected),
      row(t('Gas collected'), (p) => p.CatalystCollected), row(t('Peak army'), (p) => p.PeakArmy),
      row(t('Average APM'), (p) => Math.round(p.AverageApm))));
    panel.append(graph(report, player));
  } else if (unavailable) panel.append(h('p', { class: 'note' }, t('Match finished. The detailed report is unavailable.')));
  panel.append(h('div', { class: 'row' }, ...actions.map((a) => h('button', { class: 'menu-button-large' + (a.primary ? ' primary' : ''), onclick: a.run }, a.label))));
  return panel;
}

function graph(report: MatchReport, player: number): HTMLElement {
  const canvas = h('canvas', { class: 'graph', width: 520, height: 120 });
  const c = canvas.getContext('2d');
  if (!c) return canvas;
  const samples = report.samples;
  const max = Math.max(4, ...samples.flatMap((s) => s.players.map((p) => p.Army + p.Workers)));
  const plot = (index: number, color: string) => {
    c.strokeStyle = color; c.lineWidth = 2; c.beginPath();
    samples.forEach((s, i) => {
      const x = report.tick > 0 ? (s.tick / report.tick) * 510 + 5 : 5;
      const p = s.players[index];
      const y = 115 - ((p.Army + p.Workers) / max) * 105;
      if (i === 0) c.moveTo(x, y); else c.lineTo(x, y);
    });
    c.stroke();
  };
  c.fillStyle = 'rgba(255,255,255,0.05)'; c.fillRect(0, 0, 520, 120);
  plot(1 - player, '#ff6a4d'); plot(player, '#33ccff');
  return h('div', { class: 'graph-block' }, h('div', { class: 'legend' }, t('Population over time')), canvas);
}
