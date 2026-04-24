#!/usr/bin/env bash
# Discard all local changes and untracked files in every submodule.
# Usage: ./clean-modules.sh [--force]
set -euo pipefail

if [ "${1:-}" != "--force" ]; then
    echo "This will permanently delete uncommitted changes in ALL submodules."
    echo "Re-run with --force to confirm."
    exit 1
fi

cd "$(git rev-parse --show-toplevel)"

git submodule foreach --recursive '
    git reset --hard
    git clean -fdx
'

