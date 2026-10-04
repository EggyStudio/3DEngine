#!/usr/bin/env bash
# Prints the package's version. The major and minor are build/version.txt's, set by hand, and the
# patch is how many commits came after the one that last changed that file, so each commit raises
# it by one and raising the minor (0.1 to 0.2) starts it again at 0. It needs the whole history,
# which a shallow clone does not have.
set -euo pipefail
cd "$(dirname "$0")/.."

base="$(tr -d '[:space:]' < build/version.txt)"
if ! [[ "$base" =~ ^[0-9]+\.[0-9]+$ ]]; then
  echo "build/version.txt holds '$base', where a major and a minor such as 0.1 belong" >&2
  exit 1
fi

changed="$(git log -1 --format=%H -- build/version.txt)"
if [ -z "$changed" ]; then
  patch=0
else
  patch="$(git rev-list --count "$changed"..HEAD)"
fi
echo "$base.$patch"
