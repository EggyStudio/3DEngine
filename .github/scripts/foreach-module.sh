#!/usr/bin/env bash
# Run an arbitrary git command in every submodule.
# Usage: ./foreach-module.sh <git-args...>
# Example: ./foreach-module.sh status -s
set -euo pipefail

if [ "$#" -eq 0 ]; then
    echo "Usage: $0 <git-args...>" >&2
    exit 1
fi

cd "$(git rev-parse --show-toplevel)"

git submodule foreach --recursive "git $* || true"
