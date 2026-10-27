#!/usr/bin/env node
// Builds the .NET server harness and writes the C#-produced protocol fixture used by
// the web client's parity tests (compatibility hash, snapshot chunks, final result).
import { execFileSync } from 'node:child_process';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const web = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const repository = resolve(web, '..');
const candidates = [process.env.DOTNET, 'dotnet', join(repository, '.cache/dotnet-sdk/dotnet')].filter(Boolean);
let dotnet = null;
for (const candidate of candidates) { try { execFileSync(candidate, ['--version'], { stdio: 'ignore' }); dotnet = candidate; break; } catch { /* next */ } }
if (!dotnet) { console.error('The parity tests need a .NET 8 SDK (set DOTNET).'); process.exit(1); }
const env = { ...process.env, DOTNET_CLI_TELEMETRY_OPTOUT: '1', DOTNET_NOLOGO: '1', DOTNET_SKIP_FIRST_TIME_EXPERIENCE: '1', DOTNET_CLI_HOME: process.env.DOTNET_CLI_HOME ?? join(repository, '.cache/dotnet') };
const project = join(repository, 'tests/ServerHarness/ServerHarness.csproj');
execFileSync(dotnet, ['build', project, '-c', 'Release', '-nologo', '-v', 'quiet'], { stdio: ['ignore', 'ignore', 'inherit'], env });
execFileSync(dotnet, ['run', '--no-build', '--project', project, '-c', 'Release', '--', 'fixture', join(repository, '.cache/web-fixtures/protocol.json')], { stdio: 'inherit', env });
