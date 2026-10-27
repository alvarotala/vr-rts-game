import { spanishCatalog } from 'virtual:frontier-content';
import { webSpanish } from './webSpanish';

// Port of Frontier.Runtime.Localization.Text: English source strings are keys; whole-text
// templates ({0}, {1}...) adapt composed strings. The game's Spanish catalog is reused, so
// unit names, actions and server/simulation messages read the same as on Quest.
export type Language = 'en' | 'es';
export type LanguagePreference = Language | 'system';
interface Entry { key: string; value: string; template?: boolean }
interface Template { matcher: RegExp; value: string; args: number[]; count: number; prefix: string; suffix: string; weight: number; literals: string[] }

const PreferenceKey = 'frontier.language.v1';
const exact = new Map<string, string>();
const names = new Map<string, string>();
const formats = new Map<string, string>();
const cache = new Map<string, string>();
let templates: Template[] = [];
let code: Language = 'en';
let preference: LanguagePreference = 'system';
const listeners = new Set<() => void>();

const escape = (text: string) => text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

function compile(entries: Entry[]) {
  exact.clear(); names.clear(); formats.clear(); cache.clear();
  const compiled: Template[] = [];
  for (const entry of entries) {
    formats.set(entry.key, entry.value);
    if (!entry.template) { exact.set(entry.key, entry.value); names.set(entry.key.toLowerCase(), entry.value); continue; }
    const matches = [...entry.key.matchAll(/\{(\d+)\}/g)];
    let pattern = escape(entry.key);
    for (const match of matches) pattern = pattern.replace(escape(match[0]), '(.*?)');
    const args = matches.map((m) => Number(m[1]));
    const parts = entry.key.split(/\{\d+\}/);
    const flags = entry.key === '{0}\n{1}' ? 's' : '';
    compiled.push({ matcher: new RegExp('^' + pattern + '$', flags), value: entry.value, args, count: Math.max(...args) + 1,
      prefix: parts[0], suffix: parts[parts.length - 1], weight: parts.reduce((n, p) => n + p.length, 0), literals: parts.filter((p) => p.length > 0) });
  }
  templates = compiled.sort((a, b) => b.weight - a.weight);
}

function format(template: string, args: unknown[]): string {
  return template.replace(/\{(\d+)\}/g, (_, i) => String(args[Number(i)] ?? ''));
}

function translate(source: string, depth: number): string {
  const direct = exact.get(source);
  if (direct !== undefined) return direct;
  if (source.length === 0 || source.length > 4096 || depth >= 12) return source;
  const trimmed = source.trim();
  const named = trimmed.length > 0 ? names.get(trimmed.toLowerCase()) : undefined;
  if (named !== undefined) {
    let value = named;
    if (trimmed === trimmed.toUpperCase()) value = value.toUpperCase();
    else if (trimmed === trimmed.toLowerCase()) value = value.toLowerCase();
    const start = source.indexOf(trimmed);
    return source.slice(0, start) + value + source.slice(start + trimmed.length);
  }
  for (const template of templates) {
    if (!source.startsWith(template.prefix) || !source.endsWith(template.suffix)) continue;
    let offset = 0, possible = true;
    for (const literal of template.literals) { const index = source.indexOf(literal, offset); if (index < 0) { possible = false; break; } offset = index + literal.length; }
    if (!possible) continue;
    const match = template.matcher.exec(source);
    if (!match) continue;
    const args: unknown[] = new Array(template.count);
    let smaller = true;
    for (let i = 0; i < template.args.length; i++) {
      const part = match[i + 1];
      if (part.length >= source.length) { smaller = false; break; }
      args[template.args[i]] = translate(part, depth + 1);
    }
    if (smaller) return format(template.value, args);
  }
  return source;
}

// Translate an English source string (exact, case-adapted name, or registered template).
export function t(source: string): string {
  if (!source || code === 'en') return source ?? '';
  const cached = cache.get(source);
  if (cached !== undefined) return cached;
  const value = translate(source, 0);
  if (cache.size >= 4096) cache.clear();
  cache.set(source, value);
  return value;
}

// Explicit key with arguments: "{0} construction requested." → localized format.
export function tf(key: string, ...args: unknown[]): string {
  const template = code === 'en' ? key : formats.get(key) ?? key;
  return format(template, args.map((a) => (typeof a === 'string' ? t(a) : a)));
}

export function systemLanguage(): Language {
  const languages = typeof navigator === 'undefined' ? [] : navigator.languages ?? [navigator.language];
  return languages.some((l) => l?.toLowerCase().startsWith('es')) ? 'es' : 'en';
}

export function setLanguage(value: LanguagePreference, persist = true): void {
  preference = value;
  code = value === 'system' ? systemLanguage() : value;
  if (persist && typeof localStorage !== 'undefined') {
    if (value === 'system') localStorage.removeItem(PreferenceKey); else localStorage.setItem(PreferenceKey, value);
  }
  compile(code === 'es' ? [...spanishCatalog.entries, ...webSpanish] : []);
  if (typeof document !== 'undefined') document.documentElement.lang = code;
  for (const listener of listeners) listener();
}

export const language = () => code;
// True when the active catalog defines this key (even with an identical translation).
export const hasTranslation = (key: string) => code === 'en' || formats.has(key);
export const languagePreference = () => preference;
export function onLanguageChanged(listener: () => void): () => void { listeners.add(listener); return () => listeners.delete(listener); }

export function initLanguage(): void {
  const saved = typeof localStorage === 'undefined' ? null : localStorage.getItem(PreferenceKey);
  setLanguage(saved === 'en' || saved === 'es' ? saved : 'system', false);
}
