#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Make sure Godot finds the same dotnet SDK as your shell.
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:$PATH"

cd "$PROJECT_DIR"

# godot-mono requis (projet C#) ; surcharge possible via GODOT_BIN.
GODOT_BIN="${GODOT_BIN:-$(command -v godot-mono || true)}"
if [[ -z "$GODOT_BIN" ]]; then
  echo "Erreur: godot-mono introuvable. Définis GODOT_BIN=/chemin/vers/godot (mono)" >&2
  exit 1
fi

exec "$GODOT_BIN" "$@"
