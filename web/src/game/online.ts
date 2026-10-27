import { contentHash, protocolVersion, rulesVersion } from 'virtual:frontier-content';
import { MatchConnection } from '../protocol/matchConnection';
import type { GameCommand } from '../protocol/types';
import type { MatchMode, MatchTransport, TransportHandlers } from './session';

export const compatibility = { protocol: protocolVersion, rules: rulesVersion, contentHash };
export const compatibilityKey = `${protocolVersion}:${rulesVersion}:${contentHash}`;

// The dedicated server over websocket: the same authoritative match a Quest joins over NGO.
export class OnlineTransport implements MatchTransport {
  private connection: MatchConnection | null = null;
  constructor(readonly mode: MatchMode, private readonly url: string, private readonly faction: string,
    private readonly ticket: { matchId: string; joinTicket: string } | null = null, private readonly onJoined: (() => void) | null = null) {}

  start(handlers: TransportHandlers): void {
    const connection = new MatchConnection(this.url, compatibility, this.faction, {
      started: (welcome) => {
        if (this.ticket && welcome.matchId !== this.ticket.matchId) { connection.close('Could not join the match. Search again.'); return; }
        this.onJoined?.();
        handlers.started({ player: welcome.player, mapId: welcome.mapId, faction0: welcome.faction0, faction1: welcome.faction1 });
      },
      view: (view) => handlers.view(view),
      ack: (ack) => handlers.ack(ack),
      finished: (result) => handlers.finished(result),
      status: (message) => handlers.status(message),
      closed: (reason) => handlers.closed(reason),
    }, this.ticket);
    this.connection = connection;
    connection.connect();
  }
  send(command: GameCommand): boolean { return this.connection?.send(command) ?? false; }
  countdownRemaining(): number { return this.connection?.countdownRemaining ?? 0; }
  setPaused(): void { /* The server never pauses for one player. */ }
  leave(): void { this.connection?.close(); this.connection = null; }
}

export function websocketUrl(host: string, port: number): string {
  const secure = typeof location !== 'undefined' && location.protocol === 'https:';
  const bracketed = host.includes(':') && !host.startsWith('[') ? `[${host}]` : host;
  return `${secure ? 'wss' : 'ws'}://${bracketed}:${port}/`;
}

// Accepts "host", "host:port" or a full ws:// / wss:// URL for direct connection.
export function normalizeDirectAddress(value: string, defaultPort = 7877): string {
  const text = value.trim();
  if (/^wss?:\/\//i.test(text)) { const url = new URL(text); return url.toString(); }
  const match = /^(\[[^\]]+\]|[^:/]+)(?::(\d+))?$/.exec(text);
  if (!match) throw new Error('Enter a server address such as 192.168.1.20:7877.');
  const port = match[2] ? Number(match[2]) : defaultPort;
  if (!(port > 0 && port < 65536)) throw new Error('Enter a server address such as 192.168.1.20:7877.');
  return websocketUrl(match[1].replace(/^\[|\]$/g, ''), port);
}
