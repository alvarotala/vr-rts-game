#!/usr/bin/env node
// Headless visual smoke test: menu, offline match against the AI (WebAssembly core in a
// worker), selection, orders and construction through the real mouse/keyboard input.
// Screenshots and console output go to TestResults/web/.
import { chromium } from 'playwright';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const out = join(repository, 'TestResults/web');
mkdirSync(out, { recursive: true });
const url = process.argv[2] ?? 'http://localhost:5173/';
const map = process.argv[3] ?? 'rift';
const browser = await chromium.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 1600, height: 900 } });
const logs = [];
page.on('console', (m) => logs.push(`[${m.type()}] ${m.text()}`));
page.on('pageerror', (e) => logs.push(`[pageerror] ${e.message}`));
const shot = async (name) => { await page.screenshot({ path: join(out, name + '.png') }); console.log('screenshot', name); };
const state = () => page.evaluate(() => {
  const s = window.frontier.session;
  if (!s) return null;
  const v = s.view;
  return { phase: s.phase, tick: v?.tick ?? -1, minerals: v?.minerals, supply: v?.supply, capacity: v?.capacity, selection: [...s.selection],
    own: v ? v.entities.filter((e) => e.owner === v.player).map((e) => ({ id: e.id, d: e.definition, x: e.position.x, y: e.position.y, c: e.complete, o: e.order })) : [],
    message: s.announcement.text, error: s.announcement.error };
});
try {
  await page.addInitScript((m) => localStorage.setItem('frontier.web.settings.v1', JSON.stringify({ mapId: m, difficulty: 'easy' })), map);
  await page.goto(url, { waitUntil: 'networkidle' });
  await shot('01-menu');
  await page.click('text=Play vs AI');
  await shot('02-setup');
  await page.click('text=Start Match');
  await page.waitForSelector('.hud', { timeout: 60000 });
  await page.waitForTimeout(800);
  await shot('03-countdown');
  await page.waitForFunction(() => window.frontier.session?.phase === 'playing', null, { timeout: 30000 });
  await page.waitForTimeout(1500);
  await shot('04-playing');
  let s = await state();
  console.log('state', JSON.stringify({ phase: s.phase, tick: s.tick, own: s.own.length, minerals: s.minerals }));
  // Drag-select the workers around the headquarters (screen projection from the view).
  const box = await page.evaluate(() => {
    const f = window.frontier, s = f.session, v = s.view, view = f.view;
    const workers = v.entities.filter((e) => e.owner === v.player && e.definition.endsWith('.worker'));
    const points = workers.map((e) => view.project(s.presentation.position(e)));
    const xs = points.map((p) => p.x), ys = points.map((p) => p.y);
    return { left: Math.min(...xs) - 90, right: Math.max(...xs) + 90, top: Math.min(...ys) - 90, bottom: Math.max(...ys) + 90, count: workers.length };
  });
  await page.mouse.move(box.left, box.top); await page.mouse.down(); await page.mouse.move(box.right, box.bottom, { steps: 8 }); await page.mouse.up();
  await page.waitForTimeout(300);
  s = await state();
  console.log('selected after drag', s.selection.length, 'of', box.count, 'workers');
  await shot('05-selected-workers');
  // Build a supply structure with the command card hotkey, placed by left click.
  const target = await page.evaluate(() => {
    const f = window.frontier, s = f.session, v = s.view;
    const supply = s.catalog.role(s.faction, 'supply').id;
    const mask = s.placementMask(supply);
    const hq = v.entities.find((e) => e.owner === v.player && s.catalog.get(e.definition).role === 'hq');
    const c = s.catalog.center(hq.definition, hq.position);
    let best = null, d = 1e9;
    for (let y = 0; y < s.map.height; y++) for (let x = 0; x < s.map.width; x++) {
      if (!mask[y * s.map.width + x]) continue;
      const dd = Math.hypot(x - c.x, y - c.y);
      if (dd > 7 && dd < d) { d = dd; best = { x, y }; }
    }
    return { screen: f.view.project({ x: best.x + 1, y: best.y + 1 }), supply };
  });
  const card = await page.evaluate((id) => {
    const s = window.frontier.session;
    return [...document.querySelectorAll('.command-card .action')].map((b) => b.textContent);
  }, target.supply);
  console.log('command card', JSON.stringify(card));
  // Supply is the third construction button (Q W E…): use its hotkey from the card.
  const key = await page.evaluate((supply) => {
    const s = window.frontier.session;
    const name = s.catalog.get(supply).name;
    const button = [...document.querySelectorAll('.command-card .action')].find((b) => b.textContent.includes(name));
    return button?.querySelector('.key')?.textContent;
  }, target.supply);
  await page.keyboard.press('Key' + key);
  await page.mouse.move(target.screen.x, target.screen.y, { steps: 4 });
  await page.waitForTimeout(300);
  await shot('06-placing-supply');
  await page.mouse.click(target.screen.x, target.screen.y);
  await page.waitForTimeout(1500);
  s = await state();
  console.log('after build', s.message, JSON.stringify(s.own.filter((e) => !e.d.endsWith('.worker'))));
  await shot('07-constructing');
  // Train a worker at the headquarters through its hotkey.
  await page.evaluate(() => { const s = window.frontier.session, v = s.view; const hq = v.entities.find((e) => e.owner === v.player && s.catalog.get(e.definition).role === 'hq'); s.select(hq.id, false); });
  await page.waitForTimeout(300);
  await page.keyboard.press('KeyQ');
  await page.waitForTimeout(1200);
  await shot('08-hq-training');
  // Right-click move of the army/workers and a zoomed-out overview.
  await page.keyboard.press('F1');
  await page.waitForTimeout(200);
  for (let i = 0; i < 6; i++) { await page.mouse.wheel(0, 400); await page.waitForTimeout(80); }
  await page.waitForTimeout(800);
  await shot('09-zoomed-out');
  await page.waitForTimeout(20000);
  s = await state();
  console.log('later', JSON.stringify({ tick: s.tick, minerals: s.minerals, supply: s.supply, capacity: s.capacity, structures: s.own.filter((e) => !e.d.endsWith('.worker')).map((e) => e.d + (e.c ? '' : '*')) }));
  await shot('10-later');
  await page.keyboard.press('Escape');
  await page.waitForTimeout(300);
  await shot('11-pause-menu');
} catch (error) {
  console.error('VISUAL_SMOKE_ERROR', error.message);
  await shot('error').catch(() => {});
  process.exitCode = 1;
} finally {
  writeFileSync(join(out, 'console.log'), logs.join('\n'));
  console.log('console lines', logs.length);
  for (const line of logs.filter((l) => /error|warn/i.test(l)).slice(0, 20)) console.log(line);
  await browser.close();
}
