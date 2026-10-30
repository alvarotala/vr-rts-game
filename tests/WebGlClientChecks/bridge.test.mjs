import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';

function bridge(path) {
  const messages = [], sockets = [], timers = new Map();
  let nextTimer = 0;
  class Socket {
    static CONNECTING = 0; static OPEN = 1;
    readyState = 0; bufferedAmount = 0; sent = []; closed = false;
    constructor(url) { this.url = url; sockets.push(this); }
    send(data) { this.sent.push(typeof data === 'string' ? data : Uint8Array.from(data)); }
    close() { this.closed = true; }
  }
  const context = vm.createContext({
    LibraryManager: { library: {} }, mergeInto: (target, value) => Object.assign(target, value), autoAddDeps() {},
    WebSocket: Socket, Map, ArrayBuffer, Uint8Array,
    UTF8ToString: value => value, HEAPU8: new Uint8Array(8192),
    SendMessage: (...args) => messages.push(args),
    btoa: value => Buffer.from(value, 'binary').toString('base64'),
    setTimeout: callback => { timers.set(++nextTimer, callback); return nextTimer; },
    clearTimeout: reference => timers.delete(reference),
  });
  vm.runInContext(readFileSync(new URL(path, import.meta.url), 'utf8'), context);
  const library = context.LibraryManager.library;
  for (const [name, value] of Object.entries(library)) if (name.startsWith('$')) context[name.slice(1)] = value;
  return { library, context, messages, sockets, timers };
}
const game = '../../Assets/Frontier/Networking/Plugins/FrontierBrowserSocket.jslib';
const nakama = '../../Assets/Nakama/Runtime/Plugins/UnityWebGLSocketBridge.jslib';

test('game bridge preserves binary bytes and enforces backpressure and message limits', () => {
  const { library: api, context, messages, sockets } = bridge(game);
  assert.equal(api.FrontierBrowserCreate(1, 'receiver', 'wss://game.invalid:443/', 4097, 65536), 1);
  const ws = sockets[0]; ws.readyState = 1; ws.onopen();
  assert.equal(ws.binaryType, 'arraybuffer');
  assert.equal(messages.at(-1)[1], 'BrowserSocketOpened');
  const incoming = Uint8Array.from([0x16, 0, 255, 127]);
  ws.onmessage({ data: incoming.buffer });
  assert.deepEqual([...Buffer.from(messages.at(-1)[2], 'base64')], [...incoming]);
  context.HEAPU8.set(incoming, 32);
  assert.equal(api.FrontierBrowserSend(1, 32, 4), 1);
  assert.deepEqual([...ws.sent[0]], [...incoming]);
  ws.bufferedAmount = 65535;
  assert.equal(api.FrontierBrowserSend(1, 32, 4), 0);
  assert.equal(api.FrontierBrowserSend(1, 32, 4098), 0);
  ws.onmessage({ data: new ArrayBuffer(4098) });
  assert.equal(messages.at(-1)[1], 'BrowserSocketFailed');
  ws.onmessage({ data: 'not binary' });
  assert.equal(messages.at(-1)[1], 'BrowserSocketFailed');
  const queuedMessage = ws.onmessage;
  api.FrontierBrowserClose(1);
  const count = messages.length;
  queuedMessage({ data: incoming.buffer });
  assert.equal(messages.length, count);
  assert.equal(ws.closed, true);
  assert.equal(api.FrontierBrowserSend(1, 32, 4), 0);
});

test('Nakama connection timeout is driven by JS and releases the failed socket', () => {
  const { library: api, messages, sockets, timers, context } = bridge(nakama);
  api.NKCreateSocket(1, 'wss://service.invalid/ws', 8000);
  assert.equal(timers.size, 1);
  [...timers.values()][0]();
  assert.equal(sockets[0].closed, true);
  assert.equal(context.SOCKET_REF_MAP.size, 0);
  assert.match(messages.at(-1)[2], /timed out/);
});

test('Nakama open cancels timeout; cancel detaches callbacks and does not emit spurious errors', () => {
  const { library: api, messages, sockets, timers, context } = bridge(nakama);
  api.NKCreateSocket(3, 'wss://service.invalid/ws', 8000);
  const ws = sockets[0]; ws.readyState = 1; ws.onopen();
  assert.equal(timers.size, 0);
  assert.match(messages.at(-1)[1], /OnOpen/);
  api.NKSendData(3, '{"test":1}');
  assert.deepEqual(ws.sent, ['{"test":1}']);
  api.NKCloseSocket(3);
  const count = messages.length;
  api.NKCloseSocket(3);
  assert.equal(messages.length, count);
  assert.equal(ws.closed, true);
  assert.equal(ws.onopen, null);
  assert.equal(context.SOCKET_REF_MAP.size, 0);
});

test('Nakama custom close codes are forwarded and invalid payloads terminate the socket', () => {
  const { library: api, messages, sockets, context } = bridge(nakama);
  api.NKCreateSocket(1, 'wss://service.invalid/ws', 8000);
  sockets[0].onclose({ code: 4001, reason: 'Retry' });
  assert.equal(messages.at(-1)[2], '1_4001_Retry');
  api.NKCreateSocket(2, 'wss://service.invalid/ws', 8000);
  sockets[1].onmessage({ data: 'x'.repeat(65537) });
  assert.equal(sockets[1].closed, true);
  assert.equal(context.SOCKET_REF_MAP.size, 0);
  assert.match(messages.at(-1)[1], /OnError/);
});
