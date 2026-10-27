#!/usr/bin/env node
// Headless combat check: accelerated offline match (?speed) where the hard AI attacks.
import { chromium } from 'playwright';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const out = join(repository, 'TestResults/web');
mkdirSync(out, { recursive: true });
const base = process.argv[2] ?? 'http://localhost:5173/';
const browser = await chromium.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 1600, height: 900 } });
const logs = [];
page.on('console', (m) => logs.push(`[${m.type()}] ${m.text()}`));
page.on('pageerror', (e) => logs.push(`[pageerror] ${e.message}`));
const shot = async (name) => { await page.screenshot({ path: join(out, name + '.png') }); console.log('screenshot', name); };
try {
  await page.addInitScript(() => localStorage.setItem('frontier.web.settings.v1', JSON.stringify({ mapId: 'training', difficulty: 'hard', faction: 'biological', enemyFaction: 'industrial' })));
  await page.goto(base + '?speed=8', { waitUntil: 'networkidle' });
  await page.click('.menu-list button.primary');
  await page.click('text=Start Match');
  await page.waitForFunction(() => window.frontier.session?.phase === 'playing', null, { timeout: 60000 });
  await shot('30-biological-start');
  // Keep the camera on our base and wait for the first shots.
  await page.waitForFunction(() => (window.frontier.session.view?.attacks.length ?? 0) > 0, null, { timeout: 180000, polling: 100 });
  const contact = await page.evaluate(() => window.frontier.session.view.tick);
  console.log('first combat at tick', contact);
  for (let i = 0; i < 4; i++) { await page.waitForTimeout(700); await shot('31-combat-' + i); }
  await page.waitForSelector('.results', { timeout: 240000 });
  await page.waitForTimeout(600);
  await shot('32-results');
  const result = await page.evaluate(() => { const s = window.frontier.session; return { winner: s.winner, player: s.localPlayer, tick: s.view.tick, report: !!s.report }; });
  console.log('result', JSON.stringify(result));
} catch (error) {
  console.error('VISUAL_COMBAT_ERROR', error.message);
  await shot('error-combat').catch(() => {});
  process.exitCode = 1;
} finally {
  writeFileSync(join(out, 'console-combat.log'), logs.join('\n'));
  for (const line of logs.filter((l) => /error|pageerror/i.test(l)).slice(0, 20)) console.log(line);
  await browser.close();
}
