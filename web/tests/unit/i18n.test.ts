import { describe, expect, it } from 'vitest';
import { readFileSync, readdirSync, statSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { hasTranslation, setLanguage, t, tf } from '../../src/i18n/i18n';

const source = fileURLToPath(new URL('../../src', import.meta.url));
const files = (dir: string): string[] => readdirSync(dir).flatMap((name) => { const path = join(dir, name); return statSync(path).isDirectory() ? files(path) : path.endsWith('.ts') ? [path] : []; });
// Literal keys passed to t('…') / tf('…') anywhere in the client.
// Only complete literals: t('…') or tf('…', …); composed strings are covered by catalog templates.
const keys = [...new Set(files(source).flatMap((file) => [...readFileSync(file, 'utf8').matchAll(/\bt[f]?\(\s*'((?:[^'\\]|\\.)*)'\s*[),]/g)].map((m) => m[1].replace(/\\'/g, "'").replace(/\\n/g, '\n'))))];
const untranslatable = new Set(['Little Legion', 'English']);

describe('Spanish interface', () => {
  it('reuses the game catalog for shared vocabulary and server messages', () => {
    setLanguage('es', false);
    expect(t('Not enough resources.')).toBe('Recursos insuficientes.');
    expect(t('Select a worker.')).toBe('Selecciona un trabajador.');
    expect(tf('\nRequires: {0}', 'Barracks')).toContain('Requiere');
  });
  it('translates every string the desktop client shows', () => {
    setLanguage('es', false);
    const missing = keys.filter((key) => {
      if (untranslatable.has(key) || !/[a-z]/i.test(key)) return false;
      const render = () => (key.includes('{') ? tf(key, 'X', 'Y', 'Z') : t(key));
      setLanguage('en', false); const english = render();
      setLanguage('es', false); return render() === english && !hasTranslation(key);
    });
    if (missing.length > 0 && process.env.FRONTIER_I18N_DUMP) writeFileSync(process.env.FRONTIER_I18N_DUMP, JSON.stringify(missing, null, 1));
    expect(missing).toEqual([]);
  });
});
