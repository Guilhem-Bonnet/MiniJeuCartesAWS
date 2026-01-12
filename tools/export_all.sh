#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_BIN="$ROOT_DIR/.tools/godot_mono/Godot_v4.5.1-stable_mono_linux_x86_64/Godot_v4.5.1-stable_mono_linux.x86_64"

if [[ ! -x "$GODOT_BIN" ]]; then
  echo "Godot Mono introuvable ou non exécutable: $GODOT_BIN" >&2
  exit 1
fi

mkdir -p "$ROOT_DIR/../export_builds/windows" "$ROOT_DIR/../export_builds/linux" "$ROOT_DIR/../export_builds/macos"

# Important: évite que ~/.dotnet (sans SDK) prenne le dessus.
export DOTNET_ROOT=/usr/lib/dotnet
export DOTNET_MULTILEVEL_LOOKUP=0
export PATH="/usr/bin:/bin:/usr/sbin:/sbin"

"$GODOT_BIN" --headless --path "$ROOT_DIR" --export-release "Linux (x86_64)"
"$GODOT_BIN" --headless --path "$ROOT_DIR" --export-release "Windows (x86_64)"
"$GODOT_BIN" --headless --path "$ROOT_DIR" --export-release "macOS (universal)"

echo "Exports terminés dans ../export_builds"