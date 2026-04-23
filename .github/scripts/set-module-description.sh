#!/usr/bin/env bash
# Set the GitHub repository description for a module (or the parent repo).
#
# Usage:
#   ./set-module-description.sh <name> "New description"
#   ./set-module-description.sh 3DEngine.Common "Shared core utilities"
#
# Pass "." as <name> to target the current repo (auto-detected via gh).
set -euo pipefail

if [ "$#" -ne 2 ]; then
    echo "Usage: $0 <module-name|.> \"description\"" >&2
    exit 1
fi

name="$1"
desc="$2"
org="EggyStudio"

if [ "$name" = "." ]; then
    echo ">> Updating description of current repo"
    gh repo edit --description "$desc"
else
    echo ">> Updating description of $org/$name"
    gh repo edit "$org/$name" --description "$desc"
fi

echo ">> Done"

