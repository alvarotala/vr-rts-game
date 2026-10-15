#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ -n "${DOTNET:-}" ]]; then runtime="$DOTNET";
elif command -v dotnet >/dev/null 2>&1; then runtime="$(command -v dotnet)";
else runtime="/Applications/Unity/Hub/Editor/6000.5.4f1/Unity.app/Contents/Resources/Scripting/DotNetSdk/dotnet"; fi
if [[ ! -x "$runtime" ]]; then echo "Install .NET 8 SDK or set DOTNET to its executable." >&2; exit 1; fi
export DOTNET_CLI_HOME="$PWD/.cache/dotnet"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
"$runtime" run --project tests/CoreChecks/CoreChecks.csproj --configuration Release -- "$@"
