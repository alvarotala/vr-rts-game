import { defineConfig, type Plugin } from 'vite';
import { createHash } from 'node:crypto';
import { readFileSync, readdirSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const repository = resolve(here, '..');
const resources = join(repository, 'Assets/Frontier/Resources');
const virtualId = 'virtual:frontier-content';

// Unity's TextAsset.text: UTF-8 without a byte order mark.
const text = (path: string) => readFileSync(path, 'utf8').replace(/^﻿/, '');

function sourceNumber(file: string, pattern: RegExp): number {
  const match = text(join(repository, file)).match(pattern);
  if (!match) throw new Error(`Cannot read ${pattern} from ${file}`);
  return Number(match[1]);
}

// Same bytes and order as MatchProtocol.ContentHash: catalog, then "\n<name>\n<map>"
// for every map in ordinal name order, SHA-256 as upper-case hex.
export function frontierContent() {
  const catalog = text(join(resources, 'catalog.json'));
  const mapNames = readdirSync(join(resources, 'Maps')).filter((f: string) => f.endsWith('.json'))
    .map((f: string) => f.slice(0, -5)).sort((a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0));
  const maps: Record<string, string> = {};
  let hashed = catalog;
  for (const name of mapNames) {
    maps[name] = text(join(resources, 'Maps', `${name}.json`));
    hashed += `\n${name}\n${maps[name]}`;
  }
  const contentHash = createHash('sha256').update(hashed, 'utf8').digest('hex').toUpperCase();
  const protocolVersion = sourceNumber('Assets/Frontier/Networking/MatchProtocol.cs', /public const int Version\s*=\s*(\d+)/);
  const rulesVersion = sourceNumber('Assets/Frontier/Core/MatchRecording.cs', /ReplayRulesVersion\s*=\s*(\d+)/);
  const spanish = JSON.parse(text(join(resources, 'Localization/es.json')));
  return { catalog, maps, contentHash, protocolVersion, rulesVersion, spanish };
}

function frontierContentPlugin(): Plugin {
  return {
    name: 'frontier-content',
    resolveId(id) { return id === virtualId ? '\0' + virtualId : null; },
    load(id) {
      if (id !== '\0' + virtualId) return null;
      const content = frontierContent();
      this.addWatchFile(join(resources, 'catalog.json'));
      for (const name of Object.keys(content.maps)) this.addWatchFile(join(resources, 'Maps', `${name}.json`));
      const music: Record<string, string> = { training: 'neon-loop.ogg', rift: 'forest-watch.ogg', desert: 'desser-alert.ogg' };
      const imports = Object.values(music).map((file, i) => `import music${i} from ${JSON.stringify(join(resources, 'Music', file) + '?url')};`);
      return [
        ...imports,
        `export const catalogText = ${JSON.stringify(content.catalog)};`,
        `export const mapTexts = ${JSON.stringify(content.maps)};`,
        `export const contentHash = ${JSON.stringify(content.contentHash)};`,
        `export const protocolVersion = ${content.protocolVersion};`,
        `export const rulesVersion = ${content.rulesVersion};`,
        `export const spanishCatalog = ${JSON.stringify(content.spanish)};`,
        `export const musicUrls = {${Object.keys(music).map((map, i) => `${JSON.stringify(map)}: music${i}`).join(', ')}};`,
      ].join('\n');
    },
  };
}

export default defineConfig({
  root: here,
  base: './',
  plugins: [frontierContentPlugin()],
  server: { host: true, port: 5173, fs: { allow: [repository] } },
  preview: { host: true, port: 4173 },
  worker: { format: 'es' },
  build: { target: 'es2022', outDir: 'dist', emptyOutDir: true, chunkSizeWarningLimit: 1600 },
  test: { environment: 'node', include: process.env.FRONTIER_E2E ? ['tests/e2e/**/*.test.ts'] : ['tests/unit/**/*.test.ts'], testTimeout: 240000, hookTimeout: 240000 },
} as never);
