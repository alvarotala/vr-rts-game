import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { NakamaMatchmaker, validateEndpoint, type PublicConnection } from '../../src/protocol/nakama';

const endpoint = 'https://matchmaking.invalid';
const searchId = 'synthetic_search_identifier_0001';
const sessionToken = 'synthetic_session_token_for_tests';
const identity = 'synthetic_device_identity_for_tests';
const compatibility = { protocol: 7, rules: 11, contentHash: 'TEST_CONTENT_HASH' };
const compat = `${compatibility.protocol}:${compatibility.rules}:${compatibility.contentHash}`;
const connection: PublicConnection = {
  matchId: 'synthetic_match_identifier_0001', host: 'game.invalid', port: 7777, wsPort: 7778,
  joinTicket: 'synthetic_join_ticket_for_tests', faction: 'industrial',
};

function searching(queueRevision = 0) {
  return { status: 'searching', rating: 1200, searchRadius: (queueRevision + 1) * 100, queueRevision };
}
function opened() { return { searchId, rating: 1200, searchRadius: 100, queueRevision: 0 }; }
function ready(value: unknown = connection) { return { status: 'ready', connection: value }; }
function deferred<T>() {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((done) => { resolve = done; });
  return { promise, resolve };
}

interface QueueMessage {
  cid: string;
  matchmaker_add?: { min_count: number; max_count: number; query: string; string_properties: { compat: string; searchId: string } };
  matchmaker_remove?: { ticket: string };
}

class FakeWebSocket extends EventTarget {
  static instances: FakeWebSocket[] = [];
  static autoOpen = true;
  static onSend: (socket: FakeWebSocket, request: QueueMessage) => void;
  readonly sent: QueueMessage[] = [];
  readonly listeners = new Map<string, Set<EventListenerOrEventListenerObject>>();
  readyState = 0;
  closeCount = 0;

  constructor(readonly url: string) {
    super();
    FakeWebSocket.instances.push(this);
    if (FakeWebSocket.autoOpen) queueMicrotask(() => this.open());
  }
  override addEventListener(type: string, listener: EventListenerOrEventListenerObject | null, options?: boolean | AddEventListenerOptions) {
    if (listener) {
      const listeners = this.listeners.get(type) ?? new Set<EventListenerOrEventListenerObject>();
      listeners.add(listener); this.listeners.set(type, listeners);
    }
    super.addEventListener(type, listener, options);
  }
  override removeEventListener(type: string, listener: EventListenerOrEventListenerObject | null, options?: boolean | EventListenerOptions) {
    if (listener) this.listeners.get(type)?.delete(listener);
    super.removeEventListener(type, listener, options);
  }
  open() { if (this.readyState === 0) { this.readyState = 1; this.dispatchEvent(new Event('open')); } }
  send(text: string) {
    if (this.readyState !== 1) throw new Error('Fake socket is closed.');
    const request = JSON.parse(text) as QueueMessage;
    this.sent.push(request); FakeWebSocket.onSend(this, request);
  }
  emit(value: unknown) { this.raw(JSON.stringify(value)); }
  raw(value: string) { this.dispatchEvent(new MessageEvent('message', { data: value })); }
  reply(request: QueueMessage, value: Record<string, unknown> = {}) { this.emit({ cid: request.cid, ...value }); }
  disconnect() { this.readyState = 3; this.dispatchEvent(new Event('close')); }
  close() { this.closeCount++; this.disconnect(); }
  get listenerCount() { return [...this.listeners.values()].reduce((sum, values) => sum + values.size, 0); }
}

interface HttpCall { path: string; body: Record<string, unknown>; signal: AbortSignal | null | undefined }
let calls: HttpCall[];
let states: unknown[];
let openResult: unknown;
let authResult: unknown;
let cancelResult: unknown;
let ticketCount: number;
let controllers: AbortController[];

function acknowledge(socket: FakeWebSocket, request: QueueMessage) {
  socket.reply(request, request.matchmaker_add ? { matchmaker_ticket: { ticket: `ticket-${++ticketCount}` } } : {});
}
function requests(name: string) { return calls.filter((call) => call.path === `/v2/rpc/${name}`); }
function socket() { expect(FakeWebSocket.instances).toHaveLength(1); return FakeWebSocket.instances[0]; }
function clean(value: FakeWebSocket) { expect(value.listenerCount).toBe(0); expect(vi.getTimerCount()).toBe(0); }
const flush = () => vi.advanceTimersByTimeAsync(0);
const poll = () => vi.advanceTimersByTimeAsync(1000);

function start(onStatus: (message: string) => void = vi.fn()) {
  const abort = new AbortController(); controllers.push(abort);
  const finder = new NakamaMatchmaker(compatibility, null, identity);
  const result = finder.find(endpoint, 'industrial', abort.signal, onStatus);
  // Attach immediately: a fake socket can fail before the test awaits the result.
  void result.catch(() => {});
  return { finder, abort, result };
}

describe('Nakama public matchmaking', { timeout: 5000 }, () => {
  beforeEach(() => {
    vi.useFakeTimers();
    calls = []; states = [ready()]; controllers = [];
    openResult = opened(); authResult = { token: sessionToken }; cancelResult = { ok: true }; ticketCount = 0;
    FakeWebSocket.instances = []; FakeWebSocket.autoOpen = true; FakeWebSocket.onSend = acknowledge;
    vi.stubGlobal('WebSocket', FakeWebSocket);
    vi.stubGlobal('fetch', vi.fn(async (input: string | URL | Request, init?: RequestInit) => {
      const url = new URL(String(input));
      if (url.origin !== endpoint) throw new Error('Unexpected fake endpoint.');
      init?.signal?.throwIfAborted();
      const call = { path: url.pathname, body: JSON.parse(String(init?.body)) as Record<string, unknown>, signal: init?.signal };
      calls.push(call);
      let value: unknown;
      switch (url.pathname) {
        case '/v2/account/authenticate/device': value = authResult; break;
        case '/v2/rpc/ll_search_open': value = openResult; break;
        case '/v2/rpc/ll_search_status':
          if (!states.length) throw new Error('No fake search state remains.');
          value = states.shift(); break;
        case '/v2/rpc/ll_search_cancel': value = cancelResult; break;
        default: throw new Error('Unexpected fake request.');
      }
      value = await value;
      return value instanceof Response ? value : new Response(JSON.stringify(value), { status: 200 });
    }));
  });

  afterEach(async () => {
    controllers.forEach((controller) => controller.abort());
    await flush();
    FakeWebSocket.instances.forEach((value) => { if (value.readyState < 2) value.close(); });
    vi.clearAllTimers(); vi.useRealTimers(); vi.unstubAllGlobals();
  });

  it('widens all four ranges using one search and socket, replacing the correct native ticket', async () => {
    states = [searching(0), searching(1), searching(2), searching(3), ready()];
    const status = vi.fn(); const run = start(status);
    await flush();
    expect(socket().sent).toHaveLength(1);
    for (let revision = 1; revision <= 3; revision++) {
      await poll();
      expect(socket().sent).toHaveLength(1 + revision * 2);
    }
    await poll();
    await expect(run.result).resolves.toEqual(connection);
    const value = socket();
    expect(value.sent.filter((message) => message.matchmaker_remove).map((message) => message.matchmaker_remove?.ticket))
      .toEqual(['ticket-1', 'ticket-2', 'ticket-3']);
    for (const message of value.sent.filter((message) => message.matchmaker_add)) {
      expect(message.matchmaker_add).toEqual({ min_count: 2, max_count: 2, query: `+properties.compat:"${compat}"`,
        string_properties: { compat, searchId } });
    }
    expect(new Set(value.sent.map((message) => message.cid)).size).toBe(7);
    expect(value.sent.every((message) => typeof message.cid === 'string' && message.cid.length > 0)).toBe(true);
    expect(requests('ll_search_open')).toHaveLength(1);
    expect(requests('ll_search_open')[0].body).toEqual({ faction: 'industrial', ...compatibility, websocket: true });
    expect(calls[0].body).toEqual({ id: identity });
    expect(requests('ll_search_status').map((call) => call.body)).toEqual(Array(5).fill({ searchId }));
    expect(new URL(value.url).protocol).toBe('wss:');
    expect(new URL(value.url).searchParams.get('token')).toBe(sessionToken);
    expect(status.mock.calls.map(([message]) => message)).toEqual(['Signing in…',
      ...[100, 200, 300, 400].map((radius) => `Searching for a similar opponent (rating 1200, range ±${radius})…`),
      'Connecting to your match…']);
    expect(requests('ll_search_cancel')).toHaveLength(0);
    clean(value);
  });

  it('does not requeue the same revision or validate ranges after a match has been assigned', async () => {
    states = [searching(), searching(), { status: 'waitingServer', queueRevision: 99 }, { status: 'preparing' }, ready()];
    const status = vi.fn(); const run = start(status);
    await flush();
    for (let index = 0; index < 4; index++) await poll();
    await expect(run.result).resolves.toEqual(connection);
    expect(socket().sent).toHaveLength(1);
    expect(status.mock.calls.filter(([message]) => message.includes('rating 1200'))).toHaveLength(1);
    expect(requests('ll_search_cancel')).toHaveLength(0);
    clean(socket());
  });

  it.each(['remove', 'add', 'both'])('re-polls an assigned match after a %s failure without cancelling it', async (failure) => {
    states = [searching(1), { status: 'waitingServer' }, { status: 'preparing' }, ready()];
    FakeWebSocket.onSend = (value, request) => {
      if (request.matchmaker_remove) value.emit({ matchmaker_matched: { match_id: 'notification_is_not_a_connection' } });
      if (request.matchmaker_remove && failure !== 'add' || request.matchmaker_add && value.sent.length > 1 && failure !== 'remove')
        value.reply(request, { error: { code: 3, message: 'Matching already won.' } });
      else acknowledge(value, request);
    };
    const run = start(); await flush();
    expect(socket().sent).toHaveLength(3);
    for (let index = 0; index < 3; index++) await poll();
    await expect(run.result).resolves.toEqual(connection);
    expect(requests('ll_search_status')).toHaveLength(4);
    expect(requests('ll_search_cancel')).toHaveLength(0);
    clean(socket());
  });

  it('retries a failed addition until its revision is actually queued', async () => {
    states = [searching(1), searching(1), searching(1), ready()];
    FakeWebSocket.onSend = (value, request) => {
      if (request.matchmaker_add && value.sent.length === 3) value.reply(request, { error: { code: 3 } });
      else acknowledge(value, request);
    };
    const run = start(); await flush(); await poll(); await poll(); await poll();
    await expect(run.result).resolves.toEqual(connection);
    expect(socket().sent.filter((message) => message.matchmaker_add)).toHaveLength(3);
    expect(socket().sent.filter((message) => message.matchmaker_remove).map((message) => message.matchmaker_remove?.ticket))
      .toEqual(['ticket-1', 'ticket-1']);
    expect(requests('ll_search_cancel')).toHaveLength(0);
    clean(socket());
  });

  it.each(['remove', 'add'])('re-polls after a %s timeout and still accepts an assigned match', async (phase) => {
    states = [searching(1), ready()];
    FakeWebSocket.onSend = (value, request) => {
      if (phase === 'remove' && request.matchmaker_remove || phase === 'add' && request.matchmaker_add && value.sent.length > 1) return;
      acknowledge(value, request);
    };
    const run = start(); await flush();
    expect(socket().listenerCount).toBe(3);
    await vi.advanceTimersByTimeAsync(8000);
    expect(socket().listenerCount).toBe(0);
    await poll();
    await expect(run.result).resolves.toEqual(connection);
    expect(requests('ll_search_cancel')).toHaveLength(0);
    clean(socket());
  });

  it.each([
    ['remove', 'ready'], ['add', 'ready'], ['remove', 'searching'], ['add', 'searching'],
  ])('checks search status after socket loss during %s, then handles %s', async (phase, next) => {
    states = [searching(1), next === 'ready' ? ready() : searching(1)];
    FakeWebSocket.onSend = (value, request) => {
      if (phase === 'remove' && request.matchmaker_remove || phase === 'add' && request.matchmaker_add && value.sent.length > 1)
        value.disconnect();
      else acknowledge(value, request);
    };
    const run = start(); await flush(); await poll();
    if (next === 'ready') {
      await expect(run.result).resolves.toEqual(connection);
      expect(requests('ll_search_cancel')).toHaveLength(0);
    } else {
      await expect(run.result).rejects.toThrow('The matchmaking connection was lost.');
      expect(requests('ll_search_cancel')).toHaveLength(1);
    }
    expect(requests('ll_search_status')).toHaveLength(2);
    clean(socket());
  });

  it('ignores malformed, unsolicited and stale CID responses while refreshing tickets', async () => {
    states = [searching(1), searching(2), ready()];
    FakeWebSocket.onSend = () => {};
    const run = start(); await flush(); const value = socket(); const first = value.sent[0];
    value.raw('{'); value.emit(null); value.emit([]); value.emit({ matchmaker_matched: { match_id: connection.matchId } });
    value.emit({ cid: 'unrelated', matchmaker_ticket: { ticket: 'wrong-ticket' } });
    await flush();
    expect(requests('ll_search_status')).toHaveLength(0);
    value.reply(first, { matchmaker_ticket: { ticket: 'ticket-initial' } });
    await flush(); const removal = value.sent[1];
    value.reply(first, { matchmaker_ticket: { ticket: 'stale-ticket' } }); await flush();
    expect(value.sent).toHaveLength(2);
    value.reply(removal); await flush(); const addition = value.sent[2];
    value.reply(removal, { matchmaker_ticket: { ticket: 'also-stale' } }); await flush();
    expect(requests('ll_search_status')).toHaveLength(1);
    expect(value.listenerCount).toBe(3);
    value.reply(addition, { matchmaker_ticket: { ticket: 'ticket-refreshed' } });
    FakeWebSocket.onSend = acknowledge;
    await flush(); await poll(); await poll();
    await expect(run.result).resolves.toEqual(connection);
    expect(value.sent.filter((message) => message.matchmaker_remove).map((message) => message.matchmaker_remove?.ticket))
      .toEqual(['ticket-initial', 'ticket-refreshed']);
    expect(new Set(value.sent.map((message) => message.cid)).size).toBe(value.sent.length);
    clean(value);
  });

  it.each([
    ['remove', 'signal'], ['add', 'signal'], ['remove', 'cancel'], ['add', 'cancel'],
  ])('cancels a pending %s through %s, deduplicates cleanup and ignores late replies', async (phase, method) => {
    states = [searching(1)];
    const cancelled = deferred<unknown>(); cancelResult = cancelled.promise;
    FakeWebSocket.onSend = (value, request) => {
      if (phase === 'remove' && request.matchmaker_remove || phase === 'add' && request.matchmaker_add && value.sent.length > 1) return;
      acknowledge(value, request);
    };
    const run = start(); await flush(); const value = socket(); const count = value.sent.length;
    expect(value.listenerCount).toBe(3);
    if (method === 'signal') run.abort.abort();
    const cancellations = method === 'cancel' ? [run.finder.cancel(), run.finder.cancel()] : [];
    await flush();
    expect(requests('ll_search_cancel')).toHaveLength(1);
    expect(requests('ll_search_cancel')[0].body).toEqual({ searchId });
    expect(value.closeCount).toBe(1);
    clean(value);
    value.reply(value.sent.at(-1)!, { matchmaker_ticket: { ticket: 'late-ticket' } });
    await flush(); expect(value.sent).toHaveLength(count);
    cancelled.resolve({ ok: true }); await flush();
    await expect(run.result).rejects.toMatchObject({ name: 'AbortError' });
    await Promise.all(cancellations);
    await run.finder.cancel();
    expect(requests('ll_search_cancel')).toHaveLength(1);
    clean(value);
  });

  it.each(['signal', 'cancel'])('cleans up a late search_open response after %s cancellation', async (method) => {
    const opening = deferred<unknown>(); openResult = opening.promise;
    const run = start(); await flush();
    expect(requests('ll_search_open')).toHaveLength(1);
    if (method === 'signal') run.abort.abort(); else await run.finder.cancel();
    expect(requests('ll_search_open')[0].signal?.aborted).toBe(false);
    opening.resolve(opened()); await flush();
    await expect(run.result).rejects.toMatchObject({ name: 'AbortError' });
    expect(requests('ll_search_cancel').map((call) => call.body)).toEqual([{ searchId }]);
    expect(FakeWebSocket.instances).toHaveLength(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('does not open a search if cancellation wins while sign-in is still completing', async () => {
    const signingIn = deferred<unknown>(); authResult = signingIn.promise;
    const run = start(); await flush(); run.abort.abort();
    signingIn.resolve({ token: sessionToken }); await flush();
    await expect(run.result).rejects.toMatchObject({ name: 'AbortError' });
    expect(requests('ll_search_open')).toHaveLength(0);
    expect(requests('ll_search_cancel')).toHaveLength(0);
    expect(FakeWebSocket.instances).toHaveLength(0);
    expect(vi.getTimerCount()).toBe(0);
  });

  it('does not hand off a ready response that arrives after cancellation', async () => {
    const pending = deferred<unknown>(); states = [pending.promise];
    const run = start(); await flush(); run.abort.abort();
    // The fake completes despite abort to exercise the explicit post-request guard.
    pending.resolve(ready()); await flush();
    await expect(run.result).rejects.toMatchObject({ name: 'AbortError' });
    expect(requests('ll_search_cancel')).toHaveLength(1);
    expect(socket().closeCount).toBe(1);
    clean(socket());
  });

  it('never cancels a search after the dedicated server confirms joining', async () => {
    const run = start(); await flush(); await expect(run.result).resolves.toEqual(connection);
    run.finder.confirmJoined(); await run.finder.cancel(); await run.finder.cancel();
    expect(requests('ll_search_cancel')).toHaveLength(0);
    expect(socket().closeCount).toBe(1);
    clean(socket());
  });

  it('closes and cancels an initial socket connection that times out', async () => {
    FakeWebSocket.autoOpen = false;
    const run = start(); await flush(); await vi.advanceTimersByTimeAsync(8000);
    await expect(run.result).rejects.toThrow('The matchmaking connection timed out.');
    expect(requests('ll_search_cancel')).toHaveLength(1);
    expect(socket().closeCount).toBe(1);
    expect(socket().sent).toHaveLength(0);
    clean(socket());
  });

  it.each([undefined, {}, { ticket: '' }, { ticket: 42 }])('rejects an invalid initial queue ticket: %j', async (ticket) => {
    FakeWebSocket.onSend = (value, request) => value.reply(request, { matchmaker_ticket: ticket });
    const run = start(); await flush();
    await expect(run.result).rejects.toThrow('The online service returned an invalid matchmaking ticket.');
    expect(requests('ll_search_status')).toHaveLength(0);
    expect(requests('ll_search_cancel')).toHaveLength(1);
    clean(socket());
  });

  const invalidWindows = [
    { rating: -1 }, { rating: 1.5 }, { rating: '1200' }, { rating: undefined },
    { searchRadius: 99 }, { searchRadius: 150 }, { searchRadius: 500 },
    { queueRevision: 1 }, { queueRevision: '0' }, { queueRevision: undefined },
  ];
  describe.each(['open', 'status'])('rating validation at %s', (stage) => {
    it.each(invalidWindows)('rejects %j and cancels the known search', async (invalid) => {
      if (stage === 'open') openResult = { ...opened(), ...invalid };
      else states = [{ ...searching(), ...invalid }];
      const run = start(); await flush();
      await expect(run.result).rejects.toThrow('The service returned an invalid rating range.');
      expect(requests('ll_search_cancel').map((call) => call.body)).toEqual([{ searchId }]);
      if (stage === 'open') expect(FakeWebSocket.instances).toHaveLength(0);
      else { expect(socket().sent).toHaveLength(1); clean(socket()); }
      expect(vi.getTimerCount()).toBe(0);
    });
  });

  it.each([
    { matchId: 'short' }, { host: '' }, { host: '0.0.0.0' }, { faction: 'biological' },
    { joinTicket: '' }, { joinTicket: 'x'.repeat(257) },
  ])('preserves match connection validation for %j', async (invalid) => {
    states = [ready({ ...connection, ...invalid })];
    const run = start(); await flush();
    await expect(run.result).rejects.toThrow('The service returned an invalid match connection.');
    expect(requests('ll_search_cancel')).toHaveLength(1);
    clean(socket());
  });

  it.each([undefined, 0, 65536])('requires a browser-capable assigned server (wsPort=%s)', async (wsPort) => {
    states = [ready({ ...connection, wsPort })];
    const run = start(); await flush();
    await expect(run.result).rejects.toThrow('The assigned server does not accept browser players yet.');
    expect(requests('ll_search_cancel')).toHaveLength(1);
    clean(socket());
  });

  it.each(['cancelled', 'failed', 'finished', 'unknown'])('cleans up the terminal or invalid state %s', async (status) => {
    states = [{ status }];
    const run = start(); await flush();
    await expect(run.result).rejects.toThrow(status === 'cancelled' ? 'The search was cancelled.'
      : status === 'unknown' ? 'The service returned an invalid search state.' : 'The match is no longer available.');
    expect(requests('ll_search_cancel')).toHaveLength(1);
    clean(socket());
  });

  it('preserves response-size validation and still cleans up the search', async () => {
    states = [new Response(' '.repeat(16385))];
    const run = start(); await flush();
    await expect(run.result).rejects.toThrow('The online service returned an invalid response.');
    expect(requests('ll_search_cancel')).toHaveLength(1);
    clean(socket());
  });

  it('requires a valid search ID before connecting the socket', async () => {
    openResult = { ...opened(), searchId: 'invalid' };
    const run = start(); await flush();
    await expect(run.result).rejects.toThrow('The service returned an invalid search.');
    expect(FakeWebSocket.instances).toHaveLength(0);
    expect(requests('ll_search_cancel')).toHaveLength(0);
  });

  it('rejects reuse without starting another request', async () => {
    const run = start(); await flush(); await run.result;
    await expect(run.finder.find(endpoint, 'industrial', new AbortController().signal, vi.fn())).rejects.toThrow('A search is already active.');
    expect(requests('ll_search_open')).toHaveLength(1);
    run.finder.confirmJoined();
    clean(socket());
  });

  it.each(['http://public.invalid', 'https://matchmaking.invalid/path', 'https://matchmaking.invalid?query=1', 'https://matchmaking.invalid#fragment',
    'https://user:password@matchmaking.invalid', 'ws://matchmaking.invalid'])('preserves endpoint rejection for %s', (value) => {
    expect(() => validateEndpoint(value)).toThrow();
    expect(calls).toHaveLength(0);
  });

  it.each(['http://localhost', 'http://127.0.0.1:7350', 'http://[::1]:7350', 'http://10.0.0.1', 'http://172.16.0.1', 'http://192.168.1.1', endpoint])
  ('preserves permitted service endpoint %s', (value) => {
    expect(validateEndpoint(value).origin).toBe(new URL(value).origin);
  });
});
