#!/usr/bin/env bash
# Initialize and fetch all submodules (use after a fresh clone).
# Usage: ./init-modules.sh
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

git submodule sync --recursive
git submodule update --init --recursive

