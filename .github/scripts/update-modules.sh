#!/usr/bin/env bash
# Update all submodules to the latest commit on their tracked branch.
# Usage: ./update-modules.sh [--push]
set -euo pipefail

push=0
for arg in "$@"; do
    case "$arg" in
        --push) push=1 ;;
        *) echo "Unknown argument: $arg" >&2; exit 1 ;;
    esac
done

git submodule sync --recursive
git submodule update --init --recursive --remote --merge

if ! git diff --quiet --ignore-submodules=none -- ; then
    git add .
    git commit -m "Update all submodules to latest"
    if [ "$push" -eq 1 ]; then
        git push
    fi
else
    echo "All submodules already up to date."
fi

