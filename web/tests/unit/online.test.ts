import { afterEach, describe, expect, it, vi } from 'vitest';
import { normalizeDirectAddress, websocketUrl } from '../../src/game/online';

afterEach(() => vi.unstubAllGlobals());

describe('game WebSocket addresses', () => {
  it.each([
    ['http:', 'https://matchmaking.invalid', 'wss'],
    ['https:', 'https://matchmaking.invalid', 'wss'],
    ['http:', 'http://127.0.0.1:7350', 'ws'],
    ['https:', 'http://127.0.0.1:7350', 'wss'],
    ['http:', undefined, 'ws'],
    ['https:', undefined, 'wss'],
  ])('uses %s page and %s backend TLS policy', (protocol, backend, scheme) => {
    vi.stubGlobal('location', { protocol });
    expect(websocketUrl('game.invalid', 8443, backend)).toBe(`${scheme}://game.invalid:8443/`);
  });

  it('keeps direct IPv6 addresses and explicitly configured local WebSocket URLs', () => {
    vi.stubGlobal('location', { protocol: 'http:' });
    expect(normalizeDirectAddress('[::1]:7877')).toBe('ws://[::1]:7877/');
    expect(normalizeDirectAddress('ws://localhost:7878/')).toBe('ws://localhost:7878/');
  });

  it('uses public TLS without a browser location, including bracketed IPv6 hosts', () => {
    vi.stubGlobal('location', undefined);
    expect(websocketUrl('[::1]', 8443, 'https://matchmaking.invalid')).toBe('wss://[::1]:8443/');
  });
});
