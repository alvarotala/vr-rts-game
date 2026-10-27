#!/usr/bin/env node
// Publishes web/core-wasm (Frontier.Core + JS bridge) to web/public/core/_framework.
// Uses DOTNET, then `dotnet` on PATH, then the repository-local SDK in .cache/dotnet-sdk.
import { execFileSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readdirSync, rmSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const web = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repository = resolve(web, '..');
const candidates = [process.env.DOTNET, 'dotnet', join(repository, '.cache/dotnet-sdk/dotnet')].filter(Boolean);
let dotnet = null;
for (const candidate of candidates) {
  try { execFileSync(candidate, ['--version'], { stdio: 'ignore' }); dotnet = candidate; break; } catch { /* next */ }
}
if (!dotnet) {
  console.error('A .NET 8 SDK is required to build the offline simulation. Install it or set DOTNET.');
  process.exit(1);
}
const output = join(repository, '.cache/core-wasm-publish');
rmSync(output, { recursive: true, force: true });
const env = { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1',
  DOTNET_CLI_HOME: process.env.DOTNET_CLI_HOME ?? join(repository, '.cache/dotnet') };
execFileSync(dotnet, ['publish', join(web, 'core-wasm/FrontierCore.Web.csproj'), '-c', 'Release', '-o', output, '-nologo'], { stdio: 'inherit', env });
const framework = join(output, 'wwwroot/_framework');
if (!existsSync(join(framework, 'dotnet.js'))) { console.error('Publish did not produce _framework/dotnet.js'); process.exit(1); }
const target = join(web, 'public/core/_framework');
rmSync(join(web, 'public/core'), { recursive: true, force: true });
mkdirSync(target, { recursive: true });
let copied = 0;
for (const file of readdirSync(framework)) {
  // Static hosts compress on the fly; precompressed duplicates and symbols are not needed.
  if (file.endsWith('.br') || file.endsWith('.gz') || file.endsWith('.pdb')) continue;
  cpSync(join(framework, file), join(target, file));
  copied++;
}
console.log(`FRONTIER_CORE_WASM ${copied} files → ${target}`);
