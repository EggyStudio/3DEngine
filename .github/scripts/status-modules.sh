#!/usr/bin/env bash
# Show status (current branch, latest commit, dirty state) for every submodule.
# Usage: ./status-modules.sh
set -euo pipefail

git submodule foreach --quiet --recursive '
    branch=$(git rev-parse --abbrev-ref HEAD)
    commit=$(git log -1 --pretty=format:"%h %s")
    if [ -n "$(git status --porcelain)" ]; then
        dirty=" [dirty]"
    else
        dirty=""
    fi
    printf "%-50s %-15s %s%s\n" "$name" "$branch" "$commit" "$dirty"
'

