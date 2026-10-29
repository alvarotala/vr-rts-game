import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import vm from 'node:vm';

// Exercise the authored shell with Unity replaced at its loader boundary. The
// player itself and browser audio policy still require device smoke tests.
const html = readFileSync(new URL('../../webgl/Template/index.html', import.meta.url), 'utf8');
const script = html.match(/<script>([\s\S]*?)<\/script>/)[1]
  .replace(/^#if[^\n]*\n[\s\S]*?^#endif\n/gm, '');

function shell({ touch = true, language = 'es', fullscreen = false, rejectFullscreen = false, failUnity = false } = {}) {
  const elements = new Map();
  for (const match of html.matchAll(/<[^>]*\bid="([^"]+)"[^>]*>/g)) {
    elements.set(match[1], {
      hidden: /\bhidden\b/.test(match[0]), disabled: false, listeners: {}, attributes: {},
      setAttribute(name, value) { this.attributes[name] = value; },
      addEventListener(name, listener) { this.listeners[name] = listener; },
      focus() { this.focused = true; },
    });
  }
  const scripts = [], windowListeners = {}, documentListeners = {}, unityCalls = [];
  let reloads = 0;
  const document = {
    getElementById: id => elements.get(id), createElement: () => ({}),
    body: { appendChild: child => scripts.push(child) },
    documentElement: {}, fullscreenEnabled: fullscreen,
    addEventListener: (name, handler) => { documentListeners[name] = handler; },
  };
  if (fullscreen) document.documentElement.requestFullscreen = () => {
    if (rejectFullscreen) return Promise.reject(new Error('Denied by browser'));
    document.fullscreenElement = document.documentElement;
    documentListeners.fullscreenchange();
    return Promise.resolve();
  };
  document.exitFullscreen = () => {
    document.fullscreenElement = null;
    documentListeners.fullscreenchange();
    return Promise.resolve();
  };
  const window = {
    devicePixelRatio: 3, innerWidth: 390, innerHeight: 844,
    matchMedia: () => ({ matches: touch }),
    addEventListener: (name, handler) => { windowListeners[name] = handler; },
    location: { reload: () => { reloads++; } },
  };
  const context = vm.createContext({
    document, window, Promise, navigator: { language, maxTouchPoints: touch ? 5 : 0 },
    createUnityInstance: (canvas, config, progress) => {
      unityCalls.push({ canvas, config }); progress(.5);
      return failUnity ? Promise.reject(new Error('Out of memory')) : Promise.resolve({});
    },
  });
  vm.runInContext(script, context);
  return {
    elements, scripts, window, document, unityCalls, context,
    click: id => elements.get(id).onclick(),
    resize(width, height) { window.innerWidth = width; window.innerHeight = height; windowListeners.resize(); },
    get reloads() { return reloads; },
    async ready() { this.click('start'); scripts[0].onload(); await Promise.resolve(); await Promise.resolve(); },
  };
}

test('touch player loads once on request and waits for a gesture after Unity initialization', async () => {
  const app = shell();
  assert.equal(app.scripts.length, 0);
  app.click('start');
  app.click('start');
  assert.equal(app.scripts.length, 1);
  assert.equal(app.elements.get('start').disabled, true);
  app.scripts[0].onload();
  await Promise.resolve();
  assert.equal(app.unityCalls.length, 1);
  assert.equal(app.unityCalls[0].config.devicePixelRatio, 1);
  assert.equal(app.elements.get('loading').hidden, false);
  assert.equal(app.elements.get('start').textContent, 'Jugar');
  app.click('start');
  assert.equal(app.elements.get('loading').hidden, true);
  assert.equal(app.elements.get('game').focused, true);
  assert.equal(app.elements.get('fullscreen').hidden, true);
});

test('portrait advice is dismissible and rotation recovers without reloading Unity', async () => {
  const app = shell(); await app.ready(); app.click('start');
  assert.equal(app.elements.get('rotate').hidden, false);
  app.click('portrait');
  assert.equal(app.elements.get('rotate').hidden, true);
  app.resize(390, 800);
  assert.equal(app.elements.get('rotate').hidden, true);
  app.resize(844, 390);
  assert.equal(app.elements.get('rotate').hidden, true);
  app.resize(390, 844);
  assert.equal(app.elements.get('rotate').hidden, false);
  app.resize(844, 390);
  assert.equal(app.elements.get('rotate').hidden, true);
  assert.equal(app.unityCalls.length, 1);
});

test('desktop keeps its resolution cap and never shows touch orientation advice', async () => {
  const app = shell({ touch: false, language: 'en-US', fullscreen: true });
  assert.equal(app.elements.get('fullscreen').hidden, false);
  await app.ready();
  assert.equal(app.document.documentElement.lang, 'en');
  assert.equal(app.unityCalls[0].config.devicePixelRatio, 2);
  assert.equal(app.elements.get('rotate').hidden, true);
  assert.equal(app.elements.get('fullscreen').hidden, false);
  app.click('fullscreen');
  assert.equal(app.elements.get('fullscreen').textContent, 'Exit full screen');
  app.click('fullscreen');
  assert.equal(app.document.fullscreenElement, null);
  app.click('start');
  assert.equal(app.elements.get('fullscreen').hidden, true);
  assert.equal(app.elements.get('rotate').hidden, true);
});

test('fullscreen rejection leaves the player usable', async () => {
  const app = shell({ fullscreen: true, rejectFullscreen: true });
  await app.ready(); app.click('fullscreen'); await Promise.resolve();
  assert.equal(app.elements.get('notice').hidden, false);
  app.click('start');
  assert.equal(app.elements.get('loading').hidden, true);
  assert.equal(app.elements.get('fullscreen').hidden, true);
  assert.equal(app.context.phase, 'playing');
});

test('loader and player failures present an explicit reload without another instance', async () => {
  const failedLoader = shell(); failedLoader.click('start'); failedLoader.scripts[0].onerror();
  assert.equal(failedLoader.elements.get('start').textContent, 'Volver a cargar');
  failedLoader.click('start');
  assert.equal(failedLoader.reloads, 1);
  assert.equal(failedLoader.unityCalls.length, 0);
  const failedPlayer = shell({ failUnity: true }); await failedPlayer.ready();
  assert.equal(failedPlayer.elements.get('start').disabled, false);
  assert.equal(failedPlayer.elements.get('progress').hidden, true);
  assert.equal(failedPlayer.elements.get('loading').hidden, false);
  failedPlayer.click('start');
  assert.equal(failedPlayer.reloads, 1);
  assert.equal(failedPlayer.unityCalls.length, 1);
});

test('a context loss cannot be overwritten by late successful initialization', async () => {
  const app = shell(); app.click('start'); app.scripts[0].onload();
  app.elements.get('game').listeners.webglcontextlost();
  await Promise.resolve();
  assert.equal(app.context.phase, 'failed');
  assert.equal(app.elements.get('start').textContent, 'Volver a cargar');
});
