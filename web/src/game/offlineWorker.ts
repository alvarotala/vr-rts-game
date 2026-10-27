/// <reference lib="webworker" />
// Hosts the .NET runtime with FrontierCore.Web (Frontier.Core + the Strategist AI).
// Runs the 20 Hz simulation clock with the same catch-up limit as GameSession
// (at most 5 ticks per update) and posts the player's filtered view after each step.
interface OfflineApi {
  Start(catalog: string, map: string, faction: string, enemy: string, difficulty: string): string;
  Submit(command: string): string;
  Step(ticks: number): number;
  SetPaused(paused: boolean): void;
  View(player: number): string;
  Report(): string;
  TakeNotices(): string;
  Stop(): void;
}

const scope = self as unknown as DedicatedWorkerGlobalScope;
let api: OfflineApi | null = null;
let paused = true;
let accumulator = 0;
let last = 0;
let lastTick = -1;
let finished = false;
let speed = 1;
const queue: { command: string; sequence: number }[] = [];

async function boot(message: { base: string; catalog: string; map: string; faction: string; enemyFaction: string; difficulty: string; speed?: number }) {
  speed = Math.max(1, Math.min(16, message.speed ?? 1));
  try {
    const module = await import(/* @vite-ignore */ message.base + '_framework/dotnet.js');
    const runtime = await module.dotnet.create();
    const exports = await runtime.getAssemblyExports(runtime.getConfig().mainAssemblyName);
    api = exports.Frontier.Web.OfflineMatch as OfflineApi;
    const error = api.Start(message.catalog, message.map, message.faction, message.enemyFaction, message.difficulty);
    if (error) throw new Error(error);
    api.SetPaused(true);
    scope.postMessage({ type: 'started' });
    scope.postMessage({ type: 'view', view: api.View(0) });
    last = performance.now();
    setInterval(tick, 16);
  } catch (error) {
    scope.postMessage({ type: 'error', message: 'Could not start the offline simulation: ' + (error instanceof Error ? error.message : String(error)) });
  }
}

function tick() {
  if (!api || finished) return;
  const now = performance.now();
  const elapsed = Math.min(250, now - last) / 1000;
  last = now;
  // Orders are processed before the next step, exactly once each.
  let submitted = false;
  while (queue.length > 0) {
    const item = queue.shift()!;
    const outcome = JSON.parse(api.Submit(item.command)) as { accepted: boolean; message: string };
    scope.postMessage({ type: 'ack', ack: { sequence: item.sequence, tick: lastTick, accepted: outcome.accepted, message: outcome.message ?? '' } });
    submitted = true;
  }
  if (submitted && publish()) return;
  if (paused) { accumulator = 0; return; }
  accumulator += elapsed * speed;
  let steps = 0;
  while (accumulator >= 0.05 && steps < 5 * speed) { steps++; accumulator -= 0.05; }
  if (steps === 0) return;
  if (accumulator > 0.05) accumulator = 0;
  lastTick = api.Step(steps);
  publish();
}

// Posts the current view; returns true once the match has ended.
function publish(): boolean {
  if (!api) return false;
  const view = api.View(0);
  const notices = api.TakeNotices();
  if (notices) scope.postMessage({ type: 'notices', notices: JSON.parse(notices) });
  const parsed = JSON.parse(view) as { winner: number };
  if (parsed.winner !== -1) {
    finished = true;
    scope.postMessage({ type: 'finished', view, report: api.Report() });
    return true;
  }
  scope.postMessage({ type: 'view', view });
  return false;
}

scope.onmessage = (event: MessageEvent) => {
  const message = event.data as { type: string; [key: string]: unknown };
  if (message.type === 'start') void boot(message as never);
  else if (message.type === 'command') {
    queue.push({ command: message.command as string, sequence: message.sequence as number });
    // A surrender ends the match immediately, even while paused.
    if (api && paused) tick();
  }
  else if (message.type === 'pause') { paused = !!message.paused; api?.SetPaused(paused); last = performance.now(); }
};
