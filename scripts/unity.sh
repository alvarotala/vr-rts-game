#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
# Web builds use a generated project without the Quest/XR dependencies.
if [[ "${1:-}" == "webgl" ]]; then shift; exec python3 scripts/build-webgl.py "$@"; fi
editor="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.3.26f1/Unity.app/Contents/MacOS/Unity}"
[[ -x "$editor" ]] || { echo "Unity 6000.3.26f1 no está instalado. Configurá UNITY_EDITOR si usás otra ruta." >&2; exit 1; }
mkdir -p TestResults
case "${1:-configure}" in
  configure) method=Configure; target=StandaloneOSX ;;
  quest) method=BuildQuest; target=Android ;;
  desktop) method=BuildDesktop; target=StandaloneOSX ;;
  validate) method=ValidateContent; target=StandaloneOSX ;;
  *) echo "Uso: $0 configure|quest|desktop|webgl|validate" >&2; exit 1 ;;
esac
"$editor" -batchmode -nographics -quit -projectPath "$PWD" -buildTarget "$target" -executeMethod "Frontier.Editor.ProjectBuilder.$method" -logFile "$PWD/TestResults/unity-$method.log"
