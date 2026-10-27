import type { MatchTransport, TransportHandlers } from './session';
import { normalizeAck, normalizeReport, normalizeView, type GameCommand } from '../protocol/types';
import { encodeCommand } from '../protocol/types';

export interface OfflineOptions { catalog: string; map: string; mapId: string; faction: string; enemyFaction: string; difficulty: string; speed?: number }

// Offline matches run Frontier.Core (compiled to WebAssembly) in a worker, so AI
// planning spikes never stall rendering. The page sees the same PlayerView contract.
export class OfflineTransport implements MatchTransport {
  readonly mode = 'offline' as const;
  private worker: Worker | null = null;
  private handlers: TransportHandlers | null = null;
  constructor(private readonly options: OfflineOptions) {}

  start(handlers: TransportHandlers): void {
    this.handlers = handlers;
    const worker = new Worker(new URL('./offlineWorker.ts', import.meta.url), { type: 'module' });
    this.worker = worker;
    worker.onmessage = (event: MessageEvent) => this.receive(event.data);
    worker.onerror = (event) => { event.preventDefault(); handlers.closed('The offline simulation stopped: ' + (event.message || 'unknown error')); };
    worker.postMessage({ type: 'start', base: new URL('core/', document.baseURI).href, ...this.options });
  }

  private receive(message: { type: string; [key: string]: unknown }): void {
    const handlers = this.handlers;
    if (!handlers) return;
    switch (message.type) {
      case 'started': handlers.started({ player: 0, mapId: this.options.mapId, faction0: this.options.faction, faction1: this.options.enemyFaction }); break;
      case 'view': handlers.view(normalizeView(JSON.parse(message.view as string))); break;
      case 'ack': handlers.ack(normalizeAck(message.ack)); break;
      case 'notices': for (const notice of message.notices as string[]) handlers.notice(notice); break;
      case 'finished': handlers.finished({ view: normalizeView(JSON.parse(message.view as string)), report: message.report ? normalizeReport(JSON.parse(message.report as string)) : null }); break;
      case 'error': handlers.closed(String(message.message)); this.leave(); break;
    }
  }

  send(command: GameCommand): boolean {
    if (!this.worker) return false;
    this.worker.postMessage({ type: 'command', command: encodeCommand(command), sequence: command.sequence });
    return true;
  }
  countdownRemaining(): number { return 0; }
  setPaused(paused: boolean): void { this.worker?.postMessage({ type: 'pause', paused }); }
  leave(): void { this.worker?.terminate(); this.worker = null; this.handlers = null; }
}
