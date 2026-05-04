#!/usr/bin/env bash
# Set the GitHub repository description for a module (or the parent repo).
#
# Usage:
#   ./set-module-description.sh <name> "New description"
#   ./set-module-description.sh Engine.Common "Shared core utilities"
#
# Pass "." as <name> to target the current repo (auto-detected via gh).
#
# Owner defaults to the authenticated gh user; override with:
#   OWNER=EggyStudio ./set-module-description.sh ...
set -euo pipefail

if [ "$#" -ne 2 ] || [ -z "${1:-}" ]; then
    echo "Usage: $0 <module-name|.> \"description\"" >&2
    exit 1
fi

name="$1"
desc="$2"

if ! command -v gh >/dev/null 2>&1; then
    echo "✗ GitHub CLI (gh) is not installed." >&2
    exit 1
fi
if ! gh auth status >/dev/null 2>&1; then
    echo "✗ gh is not authenticated. Run: gh auth login" >&2
    exit 1
fi

cd "$(git rev-parse --show-toplevel)"

if [ "$name" = "." ]; then
    echo ">> Updating description of current repo"
    gh repo edit --description "$desc"
else
    owner="${OWNER:-$(gh api user --jq .login)}"
    echo ">> Updating description of $owner/$name"
    gh repo edit "$owner/$name" --description "$desc"
fi

echo ">> Done"
