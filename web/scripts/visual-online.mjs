#!/usr/bin/env node
// Headless check of the online path: Direct connection to a dedicated server (the
// harness) whose AI opponent joins through NGO like a Quest player.
import { chromium } from 'playwright';
import { mkdirSync, writeFileSync } from 'node:fs';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const repository = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const out = join(repository, 'TestResults/web');
mkdirSync(out, { recursive: true });
const url = process.argv[2] ?? 'http://localhost:5173/';
const address = process.argv[3] ?? '127.0.0.1:17877';
const language = process.argv[4] ?? 'en';
const browser = await chromium.launch({ headless: true, args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--ignore-gpu-blocklist'] });
const page = await browser.newPage({ viewport: { width: 1600, height: 900 } });
const logs = [];
page.on('console', (m) => logs.push(`[${m.type()}] ${m.text()}`));
page.on('pageerror', (e) => logs.push(`[pageerror] ${e.message}`));
const shot = async (name) => { await page.screenshot({ path: join(out, name + '.png') }); console.log('screenshot', name); };
try {
  await page.addInitScript(([a, l]) => { localStorage.setItem('frontier.web.settings.v1', JSON.stringify({ directAddress: a, faction: 'industrial' })); localStorage.setItem('frontier.language.v1', l); }, [address, language]);
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.click('.menu-list button:nth-child(3)');
  await shot('20-direct-' + language);
  await page.click('.menu-card .row button.primary');
  await page.waitForSelector('.hud', { timeout: 30000 });
  await page.waitForTimeout(1200);
  await shot('21-online-countdown-' + language);
  await page.waitForFunction(() => window.frontier.session?.phase === 'playing', null, { timeout: 20000 });
  const info = await page.evaluate(() => { const s = window.frontier.session; return { mode: s.mode, player: s.localPlayer, faction: s.faction, enemy: s.enemyFaction, map: s.mapId }; });
  console.log('online', JSON.stringify(info));
  // Order all workers to the farthest visible mineral with a right click.
  await page.evaluate(() => { const s = window.frontier.session, v = s.view; s.selectEntities(v.entities.filter((e) => e.owner === v.player && e.definition.endsWith('.worker')).map((e) => e.id), false); });
  const target = await page.evaluate(() => { const f = window.frontier, s = f.session, v = s.view; const r = v.resources.filter((x) => x.kind === 'mineral')[0]; return f.view.project(r.position); });
  await page.mouse.click(target.x, target.y, { button: 'right' });
  await page.waitForTimeout(2500);
  await shot('22-online-playing-' + language);
  // Concede: the server publishes the shared result to both players.
  await page.evaluate(() => window.frontier.session.surrender());
  await page.waitForSelector('.results', { timeout: 20000 });
  await page.waitForTimeout(500);
  await shot('23-online-results-' + language);
  const result = await page.evaluate(() => { const s = window.frontier.session; return { phase: s.phase, winner: s.winner, player: s.localPlayer, report: !!s.report }; });
  console.log('result', JSON.stringify(result));
  await page.click('.results .row button:last-child');
  await page.waitForTimeout(500);
  await shot('24-back-to-menu-' + language);
} catch (error) {
  console.error('VISUAL_ONLINE_ERROR', error.message);
  await shot('error-online').catch(() => {});
  process.exitCode = 1;
} finally {
  writeFileSync(join(out, 'console-online.log'), logs.join('\n'));
  for (const line of logs.filter((l) => /error|pageerror/i.test(l)).slice(0, 20)) console.log(line);
  await browser.close();
}
