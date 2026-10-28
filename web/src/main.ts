import './style.css';
import { catalogText, mapTexts, contentHash } from 'virtual:frontier-content';
import { initLanguage, onLanguageChanged, t, tf } from './i18n/i18n';
import { Catalog } from './game/catalog';
import { parseMap } from './game/terrain';
import { GameSession, type MatchTransport } from './game/session';
import { OfflineTransport } from './game/offline';
import { OnlineTransport, compatibility, normalizeDirectAddress, websocketUrl } from './game/online';
import { NakamaMatchmaker } from './protocol/nakama';
import { BattleView } from './render/view';
import { InputController } from './input';
import { Hud } from './ui/hud';
import { Minimap } from './ui/minimap';
import { IconRenderer } from './ui/icons';
import { Screens, resultsPanel } from './ui/screens';
import { h, clear, fill } from './ui/dom';
import { AudioSystem } from './audio';
import { loadSettings, saveSettings } from './settings';
import { CommandKind } from './protocol/types';

initLanguage();
const settings = loadSettings();
const catalog = new Catalog(catalogText);
const maps = Object.keys(mapTexts).map((id) => ({ id, title: parseMap(mapTexts[id]).title }))
  .sort((a, b) => ['training', 'rift', 'desert'].indexOf(a.id) - ['training', 'rift', 'desert'].indexOf(b.id));
const audio = new AudioSystem();
audio.musicVolume = settings.musicVolume; audio.effectsVolume = settings.effectsVolume;
const app = document.getElementById('app')!;
app.addEventListener('contextmenu', (event) => event.preventDefault());
const version = `v${compatibility.protocol}.${compatibility.rules} · ${contentHash.slice(0, 8)}`;

interface Match {
  session: GameSession; view: BattleView | null; input: InputController | null; hud: Hud | null; minimap: Minimap | null;
  canvas: HTMLCanvasElement; overlay: HTMLDivElement; frame: number; last: number; menuOpen: boolean; results: boolean; modeText: string;
  rematch: (() => void) | null; settled: boolean; started: boolean;
}

let match: Match | null = null;
let matchmaker: NakamaMatchmaker | null = null;
let searchAbort: AbortController | null = null;
const icons = new IconRenderer();

const screens = new Screens(settings, catalog, maps, {
  offline: () => startOffline(),
  findMatch: () => void findMatch(),
  direct: () => startDirect(),
  cancel: () => cancelSearch(),
  settingsChanged: () => { saveSettings(settings); audio.setMusicVolume(settings.musicVolume); audio.effectsVolume = settings.effectsVolume; applyLiveSettings(); },
  click: () => audio.play('click'),
}, version);
app.append(screens.root);
screens.main();
onLanguageChanged(() => { if (!match) screens.main(); });

function applyLiveSettings() {
  if (!match) return;
  if (match.view) match.view.shadows = settings.shadows;
  if (match.input) { match.input.edgeScroll = settings.edgeScroll; match.input.scrollSpeed = settings.scrollSpeed; }
}

function startOffline() {
  // ?speed=N accelerates offline matches for development and automated visual checks.
  const speed = Math.max(1, Math.min(16, Number(new URLSearchParams(location.search).get('speed')) || 1));
  const transport = new OfflineTransport({ catalog: catalogText, map: mapTexts[settings.mapId], mapId: settings.mapId,
    faction: settings.faction, enemyFaction: settings.enemyFaction, difficulty: settings.difficulty, speed });
  const difficulty = settings.difficulty === 'hard' ? t('Hard') : settings.difficulty === 'easy' ? t('Easy') : t('Normal');
  begin(transport, settings.mapId, tf('Offline vs AI · {0}', difficulty), () => startOffline(), t('Loading the simulation…'));
}

function startDirect() {
  let url: string;
  try { url = normalizeDirectAddress(settings.directAddress); }
  catch (error) { screens.setStatus(t((error as Error).message), true); return; }
  begin(new OnlineTransport('direct', url, settings.faction), 'rift', t('Online · direct server'), null, tf('Connecting to {0}…', url));
}

async function findMatch() {
  cancelSearch();
  const abort = new AbortController();
  searchAbort = abort;
  const finder = new NakamaMatchmaker(compatibility);
  const backendUrl = settings.backendUrl;
  matchmaker = finder;
  screens.setStatus(t('Signing in for online play…'));
  try {
    const connection = await finder.find(backendUrl, settings.faction, abort.signal, (status) => screens.setStatus(t(status)));
    if (abort.signal.aborted) return;
    searchAbort = null;
    const transport = new OnlineTransport('public', websocketUrl(connection.host, connection.wsPort, backendUrl), connection.faction,
      { matchId: connection.matchId, joinTicket: connection.joinTicket }, () => finder.confirmJoined());
    begin(transport, 'rift', t('Online · public match'), null, t('Opponent found. Connecting to your match…'), () => void finder.cancel());
  } catch (error) {
    if (abort.signal.aborted) return;
    searchAbort = null;
    screens.setStatus(t(error instanceof Error && error.name !== 'AbortError' ? error.message : 'Could not find a match. Check the connection and try again.'), true);
  }
}

function cancelSearch() {
  searchAbort?.abort(); searchAbort = null;
  const finder = matchmaker; matchmaker = null;
  void finder?.cancel();
}

function begin(transport: MatchTransport, mapId: string, modeText: string, rematch: (() => void) | null, loadingText: string, onAbort?: () => void) {
  endMatch();
  const session = new GameSession(catalogText, mapTexts, transport, mapId);
  const canvas = h('canvas', { class: 'battlefield' });
  const overlay = h('div', { class: 'match-overlay' });
  app.prepend(canvas);
  app.append(overlay);
  const current: Match = { session, view: null, input: null, hud: null, minimap: null, canvas, overlay, frame: 0, last: performance.now(), menuOpen: false,
    results: false, modeText, rematch, settled: false, started: false };
  match = current;
  screens.loading(loadingText, () => { onAbort?.(); leave(); });
  session.onFinished = () => {
    if (match !== current) return;
    if (!current.started) { screens.hide(); leave(); screens.main(); screens.setStatus(''); showError(session.closedReason || t('Could not connect.')); return; }
    current.settled = true;
  };
  session.onCommandResult = (accepted) => { if (!accepted) audio.play('error'); };
  session.subscribe(() => { if (match === current && !current.view && session.view && (session.phase === 'countdown' || session.phase === 'playing')) startBattle(current); });
  session.start();
  current.frame = requestAnimationFrame((time) => loop(current, time));
}

function showError(message: string) {
  const box = h('div', { class: 'toast error' }, message);
  app.append(box);
  setTimeout(() => box.remove(), 6000);
}

function startBattle(current: Match) {
  const session = current.session;
  current.started = true;
  screens.hide();
  const view = new BattleView(current.canvas, session);
  view.shadows = settings.shadows;
  const input = new InputController(view, session, current.canvas);
  input.edgeScroll = settings.edgeScroll; input.scrollSpeed = settings.scrollSpeed;
  const minimap = new Minimap(session, view);
  minimap.onOrder = (point, append) => { if (session.context(point, 0, 0, append) > 0) audio.play('order'); };
  const hud = new Hud(session, icons, minimap, current.modeText, (x, y) => view.rts.centerOn(x, y));
  hud.onMenu = () => openMenu(current);
  hud.onActionResult = (ok) => audio.play(ok ? 'order' : 'error');
  input.onMenu = () => openMenu(current);
  input.onAction = (letter) => hud.hotkey(letter);
  input.onFeedback = (kind) => audio.play(kind === 'order' ? 'order' : kind === 'select' ? 'select' : 'error');
  view.effects.onImpact = (_own, heavy) => audio.play(heavy ? 'heavy' : 'shot');
  view.effects.onDeath = (_own, building) => audio.play(building ? 'explosion' : 'death');
  current.overlay.append(hud.root);
  Object.assign(current, { view, input, hud, minimap });
  input.centerOnBase();
  audio.playMusic(session.mapId);
  window.addEventListener('resize', onResize);
}

function onResize() { match?.view?.resize(); }

function loop(current: Match, time: number) {
  if (match !== current) return;
  const dt = Math.min(0.1, Math.max(0, (time - current.last) / 1000));
  current.last = time;
  const session = current.session;
  session.update();
  if (current.view && current.input && current.hud && current.minimap) {
    current.input.update(dt);
    current.view.frame(dt);
    current.hud.update(time / 1000);
    current.minimap.draw(time / 1000);
    if (current.settled && !current.results) showResults(current);
  }
  current.frame = requestAnimationFrame((next) => loop(current, next));
}

function openMenu(current: Match) {
  if (current.menuOpen || current.results) return;
  current.menuOpen = true;
  current.session.setPaused(true);
  if (current.input) current.input.enabled = false;
  const offline = current.session.mode === 'offline';
  const panel = h('div', { class: 'menu-card pause' });
  const close = () => { current.menuOpen = false; current.session.setPaused(false); if (current.input) current.input.enabled = true; layer.remove(); };
  const layer = h('div', { class: 'modal' }, panel);
  const showMain = () => {
    fill(clear(panel), h('h2', {}, offline ? t('Paused') : t('Menu')),
      offline ? null : h('p', { class: 'note' }, t('Online matches keep running while this menu is open.')),
      h('div', { class: 'menu-list' },
        h('button', { class: 'menu-button-large primary', onclick: close }, offline ? t('Resume match') : t('Back to the match')),
        h('button', { class: 'menu-button-large', onclick: () => { const box = new Screens(settings, catalog, maps, { offline() {}, findMatch() {}, direct() {}, cancel() {}, settingsChanged: () => { saveSettings(settings); audio.setMusicVolume(settings.musicVolume); audio.effectsVolume = settings.effectsVolume; applyLiveSettings(); }, click: () => audio.play('click') }, version);
          box.settingsPage(showMain); clear(panel).append(...Array.from(box.root.querySelector('.menu-card')!.childNodes)); } }, t('Settings')),
        h('button', { class: 'menu-button-large', onclick: () => { const box = new Screens(settings, catalog, maps, { offline() {}, findMatch() {}, direct() {}, cancel() {}, settingsChanged() {}, click() {} }, version);
          box.controls(showMain); clear(panel).append(...Array.from(box.root.querySelector('.menu-card')!.childNodes)); } }, t('Controls')),
        h('button', { class: 'menu-button-large danger', onclick: () => confirmLeave() }, t('Leave match'))));
  };
  const confirmLeave = () => {
    clear(panel).append(h('h2', {}, t('Leave match?')), h('p', { class: 'note' }, t('Leaving concedes the match to your opponent.')),
      h('div', { class: 'row' }, h('button', { class: 'menu-button-large', onclick: showMain }, t('Keep playing')),
        h('button', { class: 'menu-button-large danger', onclick: () => { layer.remove(); current.menuOpen = false; surrenderAndLeave(current); } }, t('Leave match'))));
  };
  showMain();
  current.overlay.append(layer);
}

function surrenderAndLeave(current: Match) {
  const session = current.session;
  if (session.phase === 'playing' || session.phase === 'countdown') {
    session.surrender();
    // Give the authority a moment to record the concession before closing the transport.
    setTimeout(() => { if (match === current && !current.results) leave(); }, 400);
  } else leave();
}

function showResults(current: Match) {
  current.results = true;
  if (current.input) current.input.enabled = false;
  const session = current.session;
  const winner = session.winner;
  const victory = winner < 0 || winner === 2 ? null : winner === session.localPlayer;
  const title = session.phase === 'closed' ? t('Match ended') : winner === 2 ? t('DRAW') : victory ? t('VICTORY') : t('DEFEAT');
  if (victory !== null) audio.play(victory ? 'victory' : 'defeat');
  const actions: { label: string; run: () => void; primary?: boolean }[] = [{ label: t('Main menu'), run: () => leave(), primary: !current.rematch }];
  if (current.rematch) actions.push({ label: t('Rematch'), run: () => current.rematch!(), primary: true });
  actions.unshift({ label: t('View battlefield'), run: () => {
    layer.style.display = 'none';
    if (current.input) current.input.enabled = true;
    const back = h('button', { class: 'menu-button-large floating', onclick: () => { layer.style.display = 'flex'; back.remove(); if (current.input) current.input.enabled = false; } }, t('Show results'));
    current.overlay.append(back);
  } });
  const panel = resultsPanel(title, victory, session.report, session.localPlayer, session.reportUnavailable || session.phase === 'closed', catalog, actions);
  if (session.phase === 'closed' && session.closedReason) panel.insertBefore(h('p', { class: 'note' }, t(session.closedReason)), panel.children[1] ?? null);
  const layer = h('div', { class: 'modal' }, panel);
  current.overlay.append(layer);
}

function endMatch() {
  const current = match;
  if (!current) return;
  match = null;
  cancelAnimationFrame(current.frame);
  current.session.leave();
  current.input?.dispose(); current.hud?.dispose(); current.minimap?.dispose(); current.view?.dispose();
  current.canvas.remove(); current.overlay.remove();
  window.removeEventListener('resize', onResize);
  audio.stopMusic();
}

function leave() {
  endMatch();
  screens.main();
}

// Development hooks for automated checks (no effect on play).
(window as unknown as { frontier: unknown }).frontier = {
  get session() { return match?.session ?? null; },
  get view() { return match?.view ?? null; },
  CommandKind,
};
