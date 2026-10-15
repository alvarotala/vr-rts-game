#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
binary="$PWD/Builds/Desktop/FrontierCommand.app/Contents/MacOS/Frontier Command"
[[ -x "$binary" ]] || { echo "Primero ejecutá bash scripts/unity.sh desktop." >&2; exit 1; }
mkdir -p TestResults/Desktop
"$binary" --frontier-smoke "$PWD/TestResults/Desktop" \
  -screen-width 1440 -screen-height 1000 -screen-fullscreen 0 \
  -logFile "$PWD/TestResults/desktop-player.log"
# Deserialization errors can precede every managed runtime callback.
if rg -n 'serialization layout|referenced script.*missing|Exception:|Failed to load|Assertion failed' TestResults/desktop-player.log; then
  echo "Falló la comprobación del registro completo del reproductor." >&2
  exit 1
fi
cat TestResults/Desktop/smoke-result.json
