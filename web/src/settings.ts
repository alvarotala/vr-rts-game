export interface Settings {
  musicVolume: number; effectsVolume: number; edgeScroll: boolean; scrollSpeed: number; shadows: boolean;
  backendUrl: string; directAddress: string; faction: string; enemyFaction: string; difficulty: string; mapId: string;
}

const Key = 'frontier.web.settings.v2';
const LegacyKey = 'frontier.web.settings.v1';
const host = typeof location !== 'undefined' && location.hostname ? location.hostname : '127.0.0.1';
export const defaults: Settings = {
  musicVolume: 0.35, effectsVolume: 0.65, edgeScroll: true, scrollSpeed: 1, shadows: true,
  backendUrl: 'https://server-us-1.littlelegion.app', directAddress: `${host === 'localhost' ? '127.0.0.1' : host}:7877`,
  faction: 'industrial', enemyFaction: 'biological', difficulty: 'normal', mapId: 'rift',
};

export function loadSettings(): Settings {
  try {
    const current = localStorage.getItem(Key);
    const saved = JSON.parse(current ?? localStorage.getItem(LegacyKey) ?? '{}') as Partial<Settings>;
    // Retain desktop preferences while retiring the old LAN service default.
    // Explicit service choices saved by this version still override production.
    if (current === null) delete saved.backendUrl;
    return { ...defaults, ...saved };
  } catch { return { ...defaults }; }
}
export function saveSettings(settings: Settings): void { try { localStorage.setItem(Key, JSON.stringify(settings)); } catch { /* private mode */ } }
