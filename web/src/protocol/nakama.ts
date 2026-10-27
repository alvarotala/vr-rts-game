import type { Compatibility } from './matchConnection';

export interface PublicConnection { matchId: string; host: string; port: number; wsPort: number; joinTicket: string; faction: string }
type Status = (message: string) => void;

const ServerKey = 'littlelegion';
const OpaqueId = /^[A-Za-z0-9_-]{24,64}$/;

// Validates the online service URL like NakamaMatchmaker.ValidateEndpoint: plain HTTP
// only for this computer or a private LAN; public services require HTTPS.
export function validateEndpoint(value: string): URL {
  let url: URL;
  try { url = new URL(value.trim()); } catch { throw new Error('Enter a valid online service URL.'); }
  if ((url.protocol !== 'http:' && url.protocol !== 'https:') || url.username || url.password || url.search || url.hash || (url.pathname !== '/' && url.pathname !== ''))
    throw new Error('Enter a valid online service URL.');
  if (url.protocol === 'http:' && !isLocalHost(url.hostname)) throw new Error('Public online services require HTTPS.');
  return url;
}

export function isLocalHost(host: string): boolean {
  if (host === 'localhost' || host === '127.0.0.1' || host === '[::1]') return true;
  const parts = host.split('.').map(Number);
  if (parts.length !== 4 || parts.some((p) => !Number.isInteger(p) || p < 0 || p > 255)) return false;
  return parts[0] === 10 || parts[0] === 127 || (parts[0] === 192 && parts[1] === 168) || (parts[0] === 172 && parts[1] >= 16 && parts[1] <= 31);
}

function identity(origin: string, storage: Storage | null): string {
  const key = 'frontier.online.identity.v1.' + origin;
  let value = storage?.getItem(key) ?? '';
  if (!/^[0-9a-f]{64}$/.test(value)) {
    const bytes = crypto.getRandomValues(new Uint8Array(32));
    value = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
    storage?.setItem(key, value);
  }
  return value;
}

function statusText(state: string): string {
  switch (state) {
    case 'searching': return 'Searching for an opponent…';
    case 'waitingServer': return 'Waiting for an available server…';
    case 'preparing': return 'Preparing your match…';
    case 'ready': return 'Connecting to your match…';
    default: return 'Updating search…';
  }
}

const delay = (ms: number, signal: AbortSignal) => new Promise<void>((resolve, reject) => {
  const timer = setTimeout(resolve, ms);
  signal.addEventListener('abort', () => { clearTimeout(timer); reject(new DOMException('Cancelled', 'AbortError')); }, { once: true });
});

// Same flow as the Unity NakamaMatchmaker: device session, ll_search_open, native
// matchmaker over the realtime socket, ll_search_status polling, ll_search_cancel.
// The search declares websocket=true so the pair is placed on a browser-capable server.
export class NakamaMatchmaker {
  private base = '';
  private token = '';
  private searchId = '';
  private socket: WebSocket | null = null;
  private joined = false;
  constructor(
    private readonly compatibility: Compatibility,
    private readonly storage: Storage | null = typeof localStorage === 'undefined' ? null : localStorage,
    private readonly identityOverride: string | null = null,
  ) {}

  async find(backendUrl: string, faction: string, signal: AbortSignal, status: Status): Promise<PublicConnection> {
    const endpoint = validateEndpoint(backendUrl);
    if (faction !== 'industrial' && faction !== 'biological') throw new Error('Unknown faction.');
    this.base = endpoint.origin;
    let handedOff = false;
    try {
      status('Signing in…');
      const session = await this.http('/v2/account/authenticate/device?create=true', { id: this.identityOverride ?? identity(endpoint.origin, this.storage) },
        'Basic ' + btoa(ServerKey + ':'), signal) as { token?: string };
      if (typeof session.token !== 'string' || session.token.length < 16) throw new Error('The online service returned an invalid session.');
      this.token = session.token;
      const opened = await this.rpc('ll_search_open', { faction, protocol: this.compatibility.protocol, rules: this.compatibility.rules,
        contentHash: this.compatibility.contentHash, websocket: true }, signal) as { searchId?: string };
      if (typeof opened.searchId !== 'string' || !OpaqueId.test(opened.searchId)) throw new Error('The service returned an invalid search.');
      this.searchId = opened.searchId;
      await this.queue(endpoint, signal);
      let previous = '';
      for (;;) {
        signal.throwIfAborted();
        const result = await this.rpc('ll_search_status', { searchId: this.searchId }, signal) as { status?: string; connection?: PublicConnection };
        const state = String(result.status ?? '');
        if (state !== previous) { previous = state; status(statusText(state)); }
        if (state === 'ready') {
          const connection = result.connection;
          if (!connection || !OpaqueId.test(connection.matchId) || !connection.host || connection.host === '0.0.0.0'
            || connection.faction !== faction || typeof connection.joinTicket !== 'string' || connection.joinTicket.length < 24 || connection.joinTicket.length > 256)
            throw new Error('The service returned an invalid match connection.');
          if (!(connection.wsPort > 0 && connection.wsPort < 65536)) throw new Error('The assigned server does not accept browser players yet.');
          handedOff = true;
          return connection;
        }
        if (state === 'cancelled') throw new Error('The search was cancelled. Please search again.');
        if (state === 'failed' || state === 'finished') throw new Error('The match is no longer available. Please search again.');
        if (state !== 'searching' && state !== 'waitingServer' && state !== 'preparing') throw new Error('The service returned an invalid search state.');
        if (state === 'searching' && this.socket?.readyState !== 1) throw new Error('The matchmaking connection was lost. Please search again.');
        await delay(1000, signal);
      }
    } finally {
      if (!handedOff) { await this.cancel(); }
    }
  }

  // After the dedicated server welcomes us, the search is committed: never cancel it.
  confirmJoined(): void { this.joined = true; this.searchId = ''; this.closeSocket(); }

  async cancel(): Promise<void> {
    const id = this.searchId;
    if (!this.joined && id && this.token) {
      try { await this.rpc('ll_search_cancel', { searchId: id }, AbortSignal.timeout(5000)); } catch { /* backend expiry is the fallback */ }
      if (this.searchId === id) this.searchId = '';
    }
    this.closeSocket();
  }

  private closeSocket() {
    const socket = this.socket; this.socket = null;
    if (socket && socket.readyState <= 1) { try { socket.close(); } catch { /* ignore */ } }
  }

  private queue(endpoint: URL, signal: AbortSignal): Promise<void> {
    const ws = (endpoint.protocol === 'https:' ? 'wss://' : 'ws://') + endpoint.host + '/ws?lang=en&status=false&format=json&token=' + encodeURIComponent(this.token);
    const compat = `${this.compatibility.protocol}:${this.compatibility.rules}:${this.compatibility.contentHash}`;
    return new Promise((resolve, reject) => {
      const socket = new WebSocket(ws);
      this.socket = socket;
      const timer = setTimeout(() => reject(new Error('The matchmaking connection timed out.')), 8000);
      const abort = () => { clearTimeout(timer); reject(new DOMException('Cancelled', 'AbortError')); };
      signal.addEventListener('abort', abort, { once: true });
      socket.onopen = () => {
        socket.send(JSON.stringify({ cid: '1', matchmaker_add: { min_count: 2, max_count: 2, query: `+properties.compat:"${compat}"`,
          string_properties: { compat, searchId: this.searchId } } }));
      };
      socket.onmessage = (event) => {
        let message: Record<string, unknown>;
        try { message = JSON.parse(String(event.data)); } catch { return; }
        if (message.cid !== '1') return;
        clearTimeout(timer); signal.removeEventListener('abort', abort);
        if (message.error) reject(new Error('The online service refused the search.'));
        else resolve();
      };
      socket.onclose = () => { clearTimeout(timer); reject(new Error('The matchmaking connection was lost. Please search again.')); };
    });
  }

  private async rpc(name: string, body: unknown, signal: AbortSignal): Promise<unknown> {
    return this.http(`/v2/rpc/${name}?unwrap`, body, 'Bearer ' + this.token, signal);
  }

  private async http(path: string, body: unknown, authorization: string, signal: AbortSignal): Promise<unknown> {
    const response = await fetch(this.base + path, {
      method: 'POST', body: JSON.stringify(body), signal: AbortSignal.any([signal, AbortSignal.timeout(8000)]),
      headers: { 'Content-Type': 'application/json', Authorization: authorization },
    });
    const text = await response.text();
    if (!response.ok) throw new Error('The online service could not complete the search. Check the connection and try again.');
    if (text.length > 16384) throw new Error('The online service returned an invalid response.');
    return text ? JSON.parse(text) : {};
  }
}
