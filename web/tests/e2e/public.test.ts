import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { spawn, execFileSync, type ChildProcess } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { catalogText, mapTexts } from 'virtual:frontier-content';
import { GameSession } from '../../src/game/session';
import { OnlineTransport, compatibility, websocketUrl } from '../../src/game/online';
import { NakamaMatchmaker } from '../../src/protocol/nakama';
import { CommandKind } from '../../src/protocol/types';
import { setLanguage } from '../../src/i18n/i18n';

// Public 1v1 through the real Nakama module: a Quest-like player (NGO) and the browser
// client (websocket) are matched, allocated to the same dedicated server by its real
// allocation agent, and finish one match with a shared result. Needs the local backend
// (python3 scripts/online-stack.py init/backend) and its private configuration.
const repository = resolve(fileURLToPath(new URL('.', import.meta.url)), '../../..');
const envFile = join(repository, '.cache/online/env');
const backend = process.env.FRONTIER_BACKEND ?? 'http://127.0.0.1:7350';
const env = existsSync(envFile) ? Object.fromEntries(readFileSync(envFile, 'utf8').split('\n').filter((l) => l.includes('=')).map((l) => [l.slice(0, l.indexOf('=')), l.slice(l.indexOf('=') + 1)])) : {};
let reachable = false;
try { reachable = (await fetch(backend + '/healthcheck', { signal: AbortSignal.timeout(2000) })).ok; } catch { reachable = false; }
const project = join(repository, 'tests/ServerHarness/ServerHarness.csproj');
const dotnet = [process.env.DOTNET, 'dotnet', join(repository, '.cache/dotnet-sdk/dotnet')].filter(Boolean).find((candidate) => {
  try { execFileSync(candidate!, ['--version'], { stdio: 'ignore' }); return true; } catch { return false; }
})!;
let server: ChildProcess;
let log = '';
const waitFor = (condition: () => boolean, seconds: number, label: string) => new Promise<void>((done, fail) => {
  const started = Date.now();
  const timer = setInterval(() => {
    if (condition()) { clearInterval(timer); done(); }
    else if (Date.now() - started > seconds * 1000) { clearInterval(timer); fail(new Error('Timed out: ' + label + '\n' + log.slice(-3000))); }
  }, 25);
});

describe.skipIf(!reachable || !env.LL_RUNTIME_HTTP_KEY)('public matchmaking between a Quest-path player and the browser client', () => {
  beforeAll(async () => {
    setLanguage('en', false);
    server = spawn(dotnet, ['run', '--no-build', '--project', project, '-c', 'Release', '--', 'serve', '--allocated', '--port', '17778', '--ws-port', '17878',
      '--map', 'rift', '--listen', '127.0.0.1', '--speed', '4', '--public-bot', 'industrial', '--bot-difficulty', 'easy'], {
      env: { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1', FRONTIER_BACKEND_URL: backend, FRONTIER_SERVER_KEY: env.LL_RUNTIME_HTTP_KEY,
        FRONTIER_SERVER_ID: 'local-1', FRONTIER_ADVERTISE_HOST: '127.0.0.1' },
    });
    server.stdout!.on('data', (d) => { log += d.toString(); });
    server.stderr!.on('data', (d) => { log += d.toString(); });
    await waitFor(() => log.includes('FRONTIER_ALLOCATION Registered dedicated server.') && log.includes('public AI searching'), 60, 'registration');
  });
  afterAll(() => { server?.kill('SIGINT'); });

  it('matches, allocates and plays one shared match', async () => {
    const statuses: string[] = [];
    const finder = new NakamaMatchmaker(compatibility, null, 'e2e-desktop-' + crypto.randomUUID().replace(/-/g, ''));
    const connection = await finder.find(backend, 'biological', AbortSignal.timeout(90_000), (status) => statuses.push(status));
    expect(statuses).toContain('Signing in…');
    expect(connection.port).toBe(17778);
    expect(connection.wsPort).toBe(17878);
    expect(connection.faction).toBe('biological');
    const session = new GameSession(catalogText, mapTexts, new OnlineTransport('public', websocketUrl(connection.host, connection.wsPort), connection.faction,
      { matchId: connection.matchId, joinTicket: connection.joinTicket }, () => finder.confirmJoined()), 'training');
    const acks: { kind: CommandKind; accepted: boolean }[] = [];
    session.onCommandResult = (accepted, kind) => acks.push({ kind, accepted });
    const pump = setInterval(() => session.update(), 16);
    try {
      session.start();
      await waitFor(() => session.phase === 'playing', 40, 'allocated match start');
      expect(session.mapId).toBe('rift');
      expect(session.faction).toBe('biological');
      expect(session.enemyFaction).toBe('industrial');
      expect(log).toContain('public AI allocated match=' + connection.matchId);
      const view = session.view!;
      const hq = view.entities.find((e) => e.owner === view.player && session.catalog.get(e.definition).role === 'hq')!;
      session.select(hq.id, false);
      expect(session.send(CommandKind.Train, { definition: 'biological.worker' })).toBe(true);
      await waitFor(() => acks.some((a) => a.kind === CommandKind.Train), 10, 'train ack');
      expect(acks[0].accepted).toBe(true);
      await new Promise((r) => setTimeout(r, 1500));
      expect(session.surrender()).toBe(true);
      await waitFor(() => session.phase === 'finished', 20, 'result');
      expect(session.winner).toBe(1 - session.localPlayer);
      expect(session.report?.winner).toBe(session.winner);
      await waitFor(() => log.includes('public AI finished: winner=' + session.winner), 20, 'Quest-path result');
      session.leave();
      await waitFor(() => log.includes('Waiting for a match assignment.'), 60, 'slot released');
    } finally { clearInterval(pump); }
  });
});
