import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { spawn, execFileSync, type ChildProcess } from 'node:child_process';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { catalogText, mapTexts } from 'virtual:frontier-content';
import { GameSession } from '../../src/game/session';
import { OnlineTransport } from '../../src/game/online';
import { CommandKind } from '../../src/protocol/types';
import { setLanguage } from '../../src/i18n/i18n';

// The browser client stack (GameSession + websocket transport) against the real match
// server code (FrontierServerHarness), with an AI opponent joining through NGO like a Quest.
const repository = resolve(fileURLToPath(new URL('.', import.meta.url)), '../../..');
const project = join(repository, 'tests/ServerHarness/ServerHarness.csproj');
const dotnet = [process.env.DOTNET, 'dotnet', join(repository, '.cache/dotnet-sdk/dotnet')].filter(Boolean).find((candidate) => {
  try { execFileSync(candidate!, ['--version'], { stdio: 'ignore' }); return true; } catch { return false; }
})!;
let server: ChildProcess;
let log = '';

const waitFor = (condition: () => boolean, seconds: number, label: string) => new Promise<void>((resolveWait, reject) => {
  const started = Date.now();
  const timer = setInterval(() => {
    if (condition()) { clearInterval(timer); resolveWait(); }
    else if (Date.now() - started > seconds * 1000) { clearInterval(timer); reject(new Error('Timed out: ' + label + '\n' + log.slice(-3000))); }
  }, 20);
});

// FRONTIER_E2E_SERVER=ws://host:7877/ runs two browser clients against an external
// server instead (for example the Unity Linux server in Docker).
const external = process.env.FRONTIER_E2E_SERVER;

beforeAll(async () => {
  setLanguage('en', false);
  if (external) return;
  server = spawn(dotnet, ['run', '--no-build', '--project', project, '-c', 'Release', '--', 'serve', '--port', '18777', '--ws-port', '18877',
    '--map', 'training', '--listen', '127.0.0.1', '--speed', '8', '--bot', 'biological', '--bot-transport', 'ngo', '--bot-difficulty', 'easy'],
    { env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1' } });
  server.stdout!.on('data', (data) => { log += data.toString(); });
  server.stderr!.on('data', (data) => { log += data.toString(); });
  await waitFor(() => log.includes('FRONTIER_DEV_SERVER ready'), 60, 'server start');
});

afterAll(() => { server?.kill('SIGINT'); });

describe.skipIf(!!external)('browser client against the dedicated server', () => {
  it('plays a match against a Quest-path opponent and receives the shared result', async () => {
    let clock = () => performance.now() / 1000;
    const session = new GameSession(catalogText, mapTexts, new OnlineTransport('direct', 'ws://127.0.0.1:18877/', 'industrial'), 'rift', () => clock());
    const acks: { kind: CommandKind; accepted: boolean }[] = [];
    session.onCommandResult = (accepted, kind) => acks.push({ kind, accepted });
    let views = 0;
    session.subscribe(() => { views++; });
    const pump = setInterval(() => session.update(), 16);
    try {
      session.start();
      await waitFor(() => session.phase === 'countdown' || session.phase === 'playing', 30, 'welcome');
      expect(session.mapId).toBe('training');
      expect(session.faction).toBe('industrial');
      expect(session.enemyFaction).toBe('biological');
      await waitFor(() => session.phase === 'playing', 10, 'countdown');
      const view = session.view!;
      expect(view.player).toBe(session.localPlayer);
      expect(view.visibility.length).toBe(session.map.width * session.map.height);

      // Train a worker at the headquarters.
      const hq = view.entities.find((e) => e.owner === view.player && session.catalog.get(e.definition).role === 'hq')!;
      session.select(hq.id, false);
      expect(session.send(CommandKind.Train, { definition: 'industrial.worker' })).toBe(true);
      await waitFor(() => acks.some((a) => a.kind === CommandKind.Train), 10, 'train ack');
      expect(acks.find((a) => a.kind === CommandKind.Train)!.accepted).toBe(true);

      // A worker places a supply structure on the first valid spot near the base.
      const worker = session.view!.entities.find((e) => e.owner === view.player && e.definition === 'industrial.worker')!;
      session.select(worker.id, false);
      session.chooseBuild('industrial.supply');
      const mask = session.placementMask('industrial.supply');
      const center = session.catalog.center(hq.definition, hq.position);
      let spot = { x: -1, y: -1 }, best = Infinity;
      for (let y = 0; y < session.map.height; y++) for (let x = 0; x < session.map.width; x++) {
        if (!mask[y * session.map.width + x]) continue;
        const d = Math.hypot(x - center.x, y - center.y);
        if (d > 7 && d < best) { best = d; spot = { x, y }; }
      }
      expect(spot.x).toBeGreaterThanOrEqual(0);
      expect(session.context({ x: spot.x + 0.5, y: spot.y + 0.5 }, 0, 0)).toBeGreaterThan(0);
      await waitFor(() => acks.some((a) => a.kind === CommandKind.Build), 10, 'build ack');
      expect(acks.find((a) => a.kind === CommandKind.Build)!.accepted).toBe(true);
      await waitFor(() => !!session.view!.entities.find((e) => e.definition === 'industrial.supply' && e.owner === view.player && e.complete), 60, 'supply completed');

      // Views keep arriving at the server's publication cadence.
      const before = views, start = Date.now();
      await new Promise((r) => setTimeout(r, 2000));
      const rate = (views - before) / ((Date.now() - start) / 1000);
      expect(rate).toBeGreaterThan(5);

      // Conceding ends the match for both transports with the shared report.
      expect(session.surrender()).toBe(true);
      await waitFor(() => session.phase === 'finished', 20, 'result');
      expect(session.winner).toBe(1 - session.localPlayer);
      expect(session.report).not.toBeNull();
      expect(session.report!.winner).toBe(session.winner);
      expect(session.report!.samples.at(-1)!.players[session.localPlayer].StructuresCompleted).toBeGreaterThanOrEqual(1);
      expect(session.report!.samples.at(-1)!.players[session.localPlayer].WorkersProduced).toBeGreaterThanOrEqual(1);
      session.leave();
      await waitFor(() => log.includes('Waiting for two players.') && (log.match(/Player \d disconnected/g) ?? []).length >= 2, 20, 'server reset');
      void clock; clock = () => performance.now() / 1000;
    } finally { clearInterval(pump); }
  });
});

describe.skipIf(!external)('two browser clients against an external dedicated server', () => {
  it('both clients play one match and receive the same result', async () => {
    const make = (faction: string) => new GameSession(catalogText, mapTexts, new OnlineTransport('direct', external!, faction), 'rift');
    const first = make('industrial'), second = make('biological');
    const pump = setInterval(() => { first.update(); second.update(); }, 16);
    try {
      first.start(); second.start();
      await waitFor(() => first.phase === 'playing' && second.phase === 'playing', 40, 'both playing');
      expect(first.localPlayer).not.toBe(second.localPlayer);
      expect(first.mapId).toBe(second.mapId);
      const hq = first.view!.entities.find((e) => e.owner === first.localPlayer && first.catalog.get(e.definition).role === 'hq')!;
      first.select(hq.id, false);
      expect(first.send(CommandKind.Train, { definition: first.catalog.role(first.faction, 'worker').id })).toBe(true);
      await new Promise((r) => setTimeout(r, 3000));
      expect(second.surrender()).toBe(true);
      await waitFor(() => first.phase === 'finished' && second.phase === 'finished', 30, 'results');
      expect(first.winner).toBe(first.localPlayer);
      expect(second.winner).toBe(first.localPlayer);
      expect(first.report?.tick).toBe(second.report?.tick);
      first.leave(); second.leave();
    } finally { clearInterval(pump); }
  });
});
