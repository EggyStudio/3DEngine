#!/usr/bin/env bash
# Pull latest from the tracked branch in every submodule (no commit).
# Usage: ./pull-modules.sh
set -euo pipefail

git submodule foreach --recursive '
    branch=$(git config -f $toplevel/.gitmodules submodule.$name.branch || echo main)
    git fetch origin "$branch"
    git checkout "$branch"
    git pull --ff-only origin "$branch"
'

