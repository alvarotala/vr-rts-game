import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { loadSettings, saveSettings } from '../../src/settings';

const legacyKey = 'frontier.web.settings.v1';
const currentKey = 'frontier.web.settings.v2';
let stored: Map<string, string>;

beforeEach(() => {
  stored = new Map();
  vi.stubGlobal('localStorage', {
    getItem: (key: string) => stored.get(key) ?? null,
    setItem: (key: string, value: string) => stored.set(key, value),
  });
});
afterEach(() => vi.unstubAllGlobals());

describe('desktop online service preferences', () => {
  it('uses production for a new installation', () => {
    expect(loadSettings().backendUrl).toBe('https://server-us-1.littlelegion.app');
  });

  it('migrates the legacy service while retaining LAN, controls, audio and identities', () => {
    const legacy = JSON.stringify({ backendUrl: 'http://192.168.1.20:7350', directAddress: '192.168.1.20:7877',
      musicVolume: 0.2, edgeScroll: false, faction: 'biological' });
    stored.set(legacyKey, legacy);
    stored.set('frontier.online.identity.v1.http://192.168.1.20:7350', 'existing-identity');
    const settings = loadSettings();
    expect(settings).toMatchObject({ backendUrl: 'https://server-us-1.littlelegion.app', directAddress: '192.168.1.20:7877',
      musicVolume: 0.2, edgeScroll: false, faction: 'biological' });
    saveSettings(settings);
    expect(stored.get(legacyKey)).toBe(legacy);
    expect(stored.get('frontier.online.identity.v1.http://192.168.1.20:7350')).toBe('existing-identity');
    expect(JSON.parse(stored.get(currentKey)!)).toMatchObject(settings);
  });

  it('retains a service explicitly selected after migration', () => {
    stored.set(legacyKey, JSON.stringify({ backendUrl: 'http://192.168.1.20:7350' }));
    const settings = loadSettings();
    settings.backendUrl = 'http://127.0.0.1:7350';
    saveSettings(settings);
    expect(loadSettings().backendUrl).toBe('http://127.0.0.1:7350');
  });

  it('falls back to production when saved preferences are corrupt or storage is unavailable', () => {
    stored.set(currentKey, '{invalid');
    expect(loadSettings().backendUrl).toBe('https://server-us-1.littlelegion.app');
    vi.stubGlobal('localStorage', { getItem() { throw new Error('Storage unavailable'); }, setItem() { throw new Error('Storage unavailable'); } });
    const settings = loadSettings();
    expect(settings.backendUrl).toBe('https://server-us-1.littlelegion.app');
    expect(() => saveSettings(settings)).not.toThrow();
  });
});
