#!/usr/bin/env bash
set -euo pipefail

# Lance Godot en s'assurant que dotnet est visible (cas typique: Godot lancé via UI/launcher
# sans charger la config shell, donc ~/.dotnet pas dans le PATH).

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# 1) Rendre dotnet trouvable
DOTNET_ROOT_DEFAULT="$HOME/.dotnet"
DOTNET_ROOT="${DOTNET_ROOT:-$DOTNET_ROOT_DEFAULT}"
if [[ -x "$DOTNET_ROOT/dotnet" ]]; then
  export DOTNET_ROOT
  export PATH="$DOTNET_ROOT:$PATH"
fi

# 2) Trouver Godot (mono) 
GODOT_BIN="${GODOT_BIN:-}"
if [[ -z "$GODOT_BIN" ]]; then
  if command -v godot >/dev/null 2>&1; then
    GODOT_BIN="$(command -v godot)"
  elif [[ -x "$HOME/bin/godot" ]]; then
    GODOT_BIN="$HOME/bin/godot"
  else
    echo "Erreur: Godot introuvable. Définis GODOT_BIN=/chemin/vers/godot (mono)" >&2
    exit 1
  fi
fi

exec "$GODOT_BIN" --path "$PROJECT_DIR"
