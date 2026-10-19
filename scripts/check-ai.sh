#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

# Compare current strategy against a preserved local source artifact using the
# current simulation/catalog on both sides. Example first pass (six matches):
# bash scripts/check-ai.sh --difficulty=all --limit=6 --output=TestResults/ai-opening.json
# Full Hard matrix: --difficulty=Hard --limit=16. Timeouts are results, not wins.
baseline="${FRONTIER_AI_BASELINE:-$PWD/TestResults/ai-before/LegacyStrategist.cs}"
arguments=()
for argument in "$@"; do
  if [[ "$argument" == --help ]]; then
    cat <<'HELP'
Usage: bash scripts/check-ai.sh [options]
  --baseline=PATH          Preserved source defining Frontier.Core.LegacyStrategist
  --difficulty=all|Hard    Current AI difficulty (Easy, Normal, Hard; default Hard)
  --opponent=legacy|current  Comparison strategy (default legacy)
  --legacy-difficulty=X   Comparison difficulty (default same)
  --map=all|training|rift --pair=all|mirrors|industrial-biological
  --side=both|0|1         Current AI's player slot
  --start=0 --limit=48    Select a reproducible slice of the matchup matrix
  --seconds=1800          Simulated timeout per match
  --wall-seconds=180      Wall-clock timeout per match
  --development-only     Synthetic peaceful economy fixture, no competitive wins
  --development          Also run peaceful economy cases after competitive matches
  --development-seconds=720  Duration of the peaceful fixture
  --output=PATH          JSON evidence file, updated after each finished match
First six map/difficulty cases: --difficulty=all --limit=6
Full Hard faction/side matrix: --difficulty=Hard --limit=16
Current Hard vs current Normal: --opponent=current --legacy-difficulty=Normal --pair=mirrors --limit=8
JSON legacy-named fields identify the comparator slot; opponentStrategy records its code.
Timeouts are recorded as timeouts; no material advantage is counted as a win.
HELP
    exit 0
  fi
  if [[ "$argument" == --baseline=* ]]; then baseline="${argument#--baseline=}";
  else arguments+=("$argument"); fi
done
if [[ ! -f "$baseline" ]]; then
  echo "Missing legacy AI snapshot: $baseline. Pass --baseline=/path/to/LegacyStrategist.cs." >&2
  exit 1
fi
if [[ -n "${DOTNET:-}" ]]; then runtime="$DOTNET";
elif command -v dotnet >/dev/null 2>&1; then runtime="$(command -v dotnet)";
else runtime="/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet"; fi
if [[ ! -x "$runtime" ]]; then echo "Install .NET 8 SDK or set DOTNET to its executable." >&2; exit 1; fi
export DOTNET_CLI_HOME="$PWD/.cache/dotnet"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
harness_dir="$(mktemp -d "${TMPDIR:-/tmp}/frontier-ai-benchmark.XXXXXX")"
trap 'rm -rf "$harness_dir"' EXIT
mkdir -p TestResults/ai-snapshots
source_snapshot="$(mktemp -d "$PWD/TestResults/ai-snapshots/run.XXXXXX")"
mkdir -p "$source_snapshot/Core" "$source_snapshot/Resources/Maps"
cp "$baseline" "$source_snapshot/LegacyStrategist.cs"
cp Assets/Frontier/Core/*.cs "$source_snapshot/Core/"
cp Assets/Frontier/Resources/catalog.json "$source_snapshot/Resources/"
cp Assets/Frontier/Resources/Maps/*.json "$source_snapshot/Resources/Maps/"
cp tests/CoreChecks/AiMatchChecks.cs "$source_snapshot/AiMatchChecks.cs"
python3 - "$PWD" "$harness_dir" "$source_snapshot" <<'PY'
from pathlib import Path
from xml.sax.saxutils import escape
import sys
root, target, snapshot = map(Path, sys.argv[1:])
sources = [snapshot / 'Core/*.cs', snapshot / 'AiMatchChecks.cs', snapshot / 'LegacyStrategist.cs', target / 'Program.cs']
project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>disable</Nullable><EnableDefaultCompileItems>false</EnableDefaultCompileItems><EnableNETAnalyzers>false</EnableNETAnalyzers></PropertyGroup><ItemGroup>'
project += ''.join('<Compile Include="' + escape(str(p), {'"':'&quot;'}) + '"/>' for p in sources)
project += '</ItemGroup></Project>\n'
(target / 'Benchmark.csproj').write_text(project)
(target / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>\n')
(target / 'Program.cs').write_text('''using System.Text.Json;
using Frontier.Core;
var root=Directory.GetCurrentDirectory();
var json=new JsonSerializerOptions{IncludeFields=true};
var data=args.First(a=>a.StartsWith("--data-dir=")).Substring(11);
var catalog=new Catalog(JsonSerializer.Deserialize<CatalogDefinition>(File.ReadAllText(Path.Combine(data,"catalog.json")),json)!);
var maps=Directory.GetFiles(Path.Combine(data,"Maps"),"*.json").Select(path=>JsonSerializer.Deserialize<MapDefinition>(File.ReadAllText(path),json)!).ToArray();
AiMatchChecks.Run(catalog,maps,root,args,(player,level,catalog,map)=>{var bot=new LegacyStrategist(player,level,catalog,map);return bot.Decide;});
''')
PY
"$runtime" restore "$harness_dir/Benchmark.csproj" --configfile "$harness_dir/NuGet.Config" --verbosity quiet
"$runtime" run --no-restore --project "$harness_dir/Benchmark.csproj" --configuration Release -- "${arguments[@]}" \
  "--baseline-source=$baseline" "--baseline-copy=$source_snapshot/LegacyStrategist.cs" "--current-source-dir=$source_snapshot/Core" \
  "--data-dir=$source_snapshot/Resources" "--benchmark-source=$source_snapshot/AiMatchChecks.cs" "--source-snapshot=$source_snapshot"
