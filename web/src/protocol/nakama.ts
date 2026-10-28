import type { Compatibility } from './matchConnection';

export interface PublicConnection { matchId: string; host: string; port: number; wsPort: number; joinTicket: string; faction: string }
type Status = (message: string) => void;
interface SearchWindow { rating: number; searchRadius: number; queueRevision: number }

const ServerKey = 'littlelegion';
const OpaqueId = /^[A-Za-z0-9_-]{24,64}$/;

function validateSearchWindow(value: SearchWindow): void {
  if (!Number.isInteger(value.rating) || value.rating < 0 || !Number.isInteger(value.searchRadius)
    || value.searchRadius < 100 || value.searchRadius > 400 || value.searchRadius % 100 !== 0
    || value.queueRevision !== value.searchRadius / 100 - 1)
    throw new Error('The service returned an invalid rating range.');
}

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
  signal.throwIfAborted();
  const abort = () => { clearTimeout(timer); reject(signal.reason); };
  const timer = setTimeout(() => { signal.removeEventListener('abort', abort); resolve(); }, ms);
  signal.addEventListener('abort', abort, { once: true });
});

// Same flow as the Unity NakamaMatchmaker: device session, ll_search_open, native
// matchmaker over the realtime socket, ll_search_status polling, ll_search_cancel.
// The search declares websocket=true so the pair is placed on a browser-capable server.
export class NakamaMatchmaker {
  private base = '';
  private token = '';
  private searchId = '';
  private socket: WebSocket | null = null;
  private readonly cancellation = new AbortController();
  private cancelling: Promise<void> | null = null;
  private used = false;
  private cid = 0;
  private joined = false;
  constructor(
    private readonly compatibility: Compatibility,
    private readonly storage: Storage | null = typeof localStorage === 'undefined' ? null : localStorage,
    private readonly identityOverride: string | null = null,
  ) {}

  async find(backendUrl: string, faction: string, signal: AbortSignal, status: Status): Promise<PublicConnection> {
    if (this.used) throw new Error('A search is already active.');
    this.used = true;
    const endpoint = validateEndpoint(backendUrl);
    if (faction !== 'industrial' && faction !== 'biological') throw new Error('Unknown faction.');
    this.base = endpoint.origin;
    signal = AbortSignal.any([signal, this.cancellation.signal]);
    let handedOff = false;
    try {
      signal.throwIfAborted();
      status('Signing in…');
      // Let bounded creation calls finish so a late search ID can still be cancelled.
      const session = await this.http('/v2/account/authenticate/device?create=true', { id: this.identityOverride ?? identity(endpoint.origin, this.storage) },
        'Basic ' + btoa(ServerKey + ':'), AbortSignal.timeout(8000)) as { token?: string };
      if (typeof session.token !== 'string' || session.token.length < 16) throw new Error('The online service returned an invalid session.');
      this.token = session.token;
      signal.throwIfAborted();
      const opened = await this.rpc('ll_search_open', { faction, protocol: this.compatibility.protocol, rules: this.compatibility.rules,
        contentHash: this.compatibility.contentHash, websocket: true }, AbortSignal.timeout(8000)) as SearchWindow & { searchId?: string };
      if (typeof opened.searchId !== 'string' || !OpaqueId.test(opened.searchId)) throw new Error('The service returned an invalid search.');
      this.searchId = opened.searchId;
      validateSearchWindow(opened);
      signal.throwIfAborted();
      const socket = await this.connect(endpoint, signal);
      let queueTicket = await this.enqueue(socket, signal);
      let queuedRevision = opened.queueRevision;
      let previous = '';
      for (;;) {
        signal.throwIfAborted();
        const result = await this.rpc('ll_search_status', { searchId: this.searchId }, signal) as SearchWindow & { status?: string; connection?: PublicConnection };
        signal.throwIfAborted();
        const state = String(result.status ?? '');
        if (state === 'searching') validateSearchWindow(result);
        if (state !== previous || state === 'searching' && result.queueRevision !== queuedRevision) {
          previous = state;
          status(state === 'searching'
            ? `Searching for a similar opponent (rating ${result.rating}, range ±${result.searchRadius})…`
            : statusText(state));
        }
        signal.throwIfAborted();
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
        if (state === 'searching' && result.queueRevision !== queuedRevision) {
          // Keep the search age and socket session. Matching can win either operation:
          // only search_status decides whether the search failed or has a match ready.
          try {
            try { await this.socketRequest(socket, { matchmaker_remove: { ticket: queueTicket } }, signal); }
            catch { signal.throwIfAborted(); }
            queueTicket = await this.enqueue(socket, signal);
            queuedRevision = result.queueRevision;
          } catch { signal.throwIfAborted(); }
        }
        await delay(1000, signal);
      }
    } finally {
      if (!handedOff) { await this.cancel(); }
    }
  }

  // After the dedicated server welcomes us, the search is committed: never cancel it.
  confirmJoined(): void { this.joined = true; this.searchId = ''; this.closeSocket(); }

  async cancel(): Promise<void> {
    this.cancellation.abort();
    this.closeSocket();
    const id = this.searchId;
    if (!this.joined && id && this.token) {
      this.cancelling ??= this.rpc('ll_search_cancel', { searchId: id }, AbortSignal.timeout(5000))
        .then(() => {}, () => { /* backend expiry is the fallback */ })
        .finally(() => { if (this.searchId === id) this.searchId = ''; this.cancelling = null; });
      await this.cancelling;
    }
  }

  private closeSocket() {
    const socket = this.socket; this.socket = null;
    if (socket && socket.readyState <= 1) { try { socket.close(); } catch { /* ignore */ } }
  }

  private connect(endpoint: URL, signal: AbortSignal): Promise<WebSocket> {
    signal.throwIfAborted();
    const ws = (endpoint.protocol === 'https:' ? 'wss://' : 'ws://') + endpoint.host + '/ws?lang=en&status=false&format=json&token=' + encodeURIComponent(this.token);
    return new Promise((resolve, reject) => {
      const socket = new WebSocket(ws);
      this.socket = socket;
      const cleanup = () => {
        clearTimeout(timer); signal.removeEventListener('abort', abort);
        socket.removeEventListener('open', open); socket.removeEventListener('close', lost); socket.removeEventListener('error', lost);
      };
      const fail = (error: unknown) => { cleanup(); reject(error); };
      const open = () => { cleanup(); resolve(socket); };
      const lost = () => fail(new Error('The matchmaking connection was lost. Please search again.'));
      const abort = () => fail(signal.reason);
      const timer = setTimeout(() => fail(new Error('The matchmaking connection timed out.')), 8000);
      signal.addEventListener('abort', abort, { once: true });
      socket.addEventListener('open', open); socket.addEventListener('close', lost); socket.addEventListener('error', lost);
    });
  }

  private async enqueue(socket: WebSocket, signal: AbortSignal): Promise<string> {
    const compat = `${this.compatibility.protocol}:${this.compatibility.rules}:${this.compatibility.contentHash}`;
    const response = await this.socketRequest(socket, { matchmaker_add: { min_count: 2, max_count: 2,
      query: `+properties.compat:"${compat}"`, string_properties: { compat, searchId: this.searchId } } }, signal);
    const ticket = (response.matchmaker_ticket as { ticket?: unknown } | undefined)?.ticket;
    if (typeof ticket !== 'string' || !ticket) throw new Error('The online service returned an invalid matchmaking ticket.');
    return ticket;
  }

  private socketRequest(socket: WebSocket, body: Record<string, unknown>, signal: AbortSignal): Promise<Record<string, unknown>> {
    signal.throwIfAborted();
    const cid = String(++this.cid);
    return new Promise((resolve, reject) => {
      const cleanup = () => {
        clearTimeout(timer); signal.removeEventListener('abort', abort);
        socket.removeEventListener('message', message); socket.removeEventListener('close', lost); socket.removeEventListener('error', lost);
      };
      const fail = (error: unknown) => { cleanup(); reject(error); };
      const lost = () => fail(new Error('The matchmaking connection was lost. Please search again.'));
      const abort = () => fail(signal.reason);
      const message = (event: MessageEvent) => {
        let response: Record<string, unknown> | null;
        try { response = JSON.parse(String(event.data)); } catch { return; }
        if (response?.cid !== cid) return;
        if (response.error) fail(new Error('The online service refused the search.'));
        else { cleanup(); resolve(response); }
      };
      const timer = setTimeout(() => fail(new Error('The matchmaking connection timed out.')), 8000);
      signal.addEventListener('abort', abort, { once: true });
      socket.addEventListener('message', message); socket.addEventListener('close', lost); socket.addEventListener('error', lost);
      if (socket.readyState !== 1) { lost(); return; }
      try { socket.send(JSON.stringify({ cid, ...body })); } catch (error) { fail(error); }
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
    if (!response.ok) {
      // Older deployments reject the browser-only websocket field in their strict decoder.
      // Keep declaring it: omitting it can allocate a server that only accepts UDP clients.
      if (path === '/v2/rpc/ll_search_open?unwrap' && response.status === 400 && text.length <= 16384) {
        let failure: { code?: unknown; message?: unknown } | null = null;
        try { failure = JSON.parse(text); } catch { /* Keep the generic error for non-JSON responses. */ }
        if (failure?.code === 3 && failure.message === 'Invalid request')
          throw new Error('This server rejected browser matchmaking. Ask its administrator to update browser support.');
      }
      throw new Error('The online service could not complete the search. Check the connection and try again.');
    }
    if (text.length > 16384) throw new Error('The online service returned an invalid response.');
    return text ? JSON.parse(text) : {};
  }
}
