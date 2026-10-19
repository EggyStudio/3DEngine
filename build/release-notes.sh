#!/usr/bin/env bash
# Writes the package's release notes into the file named, the commits since the one the newest
# package of this minor on nuget.org was built from, newest first, each commit's sentence a line,
# so a package's page says what changed since the package before it rather than since the minor
# began. The feed's index lists the versions, and each version's nuspec names in its repository
# element the commit it was built from. Where the feed answers nothing, lists no version of the
# minor, or names a commit the checkout does not have, the notes are the commits since
# build/version.txt last changed, as they were, and it says why. It prints the commit the notes
# start from, which the pack run's summary shows. E3D_RELEASE_FEED names another feed laid out as
# nuget.org's flat container is, a folder given as a file:// address, as the test gives one.
#
#   build/release-notes.sh <file>
set -euo pipefail
cd "$(dirname "$0")/.."

out="$1"
feed="${E3D_RELEASE_FEED:-https://api.nuget.org/v3-flatcontainer/3dengine}"
minor="$(tr -d '[:space:]' < build/version.txt)"
mkdir -p "$(dirname "$out")"
: > "$out"

from="" since="" why=""
if ! index="$(curl -fsSL --retry 2 --max-time 30 "$feed/index.json" 2>/dev/null)"; then
  why="the feed answered nothing"
else
  latest="$(printf '%s' "$index" | grep -oE "\"${minor//./\\.}\.[0-9]+\"" | tr -d '"' | sort -t. -k3,3n | tail -1 || true)"
  if [ -z "$latest" ]; then
    why="the feed lists no version of $minor"
  elif ! nuspec="$(curl -fsSL --retry 2 --max-time 30 "$feed/$latest/3dengine.nuspec" 2>/dev/null)"; then
    why="the feed gave no nuspec of $latest"
  else
    commit="$(printf '%s' "$nuspec" | grep -oE 'commit="[0-9a-f]+"' | head -1 | cut -d'"' -f2 || true)"
    if [ -z "$commit" ]; then
      why="the nuspec of $latest names no commit"
    elif ! git cat-file -e "$commit^{commit}" 2>/dev/null; then
      why="$latest was built from $commit, which this checkout does not have"
    else
      from="$commit"
      since="$latest was built"
    fi
  fi
fi
if [ -z "$from" ] && changed="$(git log -1 --format=%H -- build/version.txt 2>/dev/null)" && [ -n "$changed" ]; then
  from="$changed"
  since="build/version.txt last changed, since $why"
fi
if [ -n "$from" ]; then
  git log --format=%b "$from"..HEAD | sed -e 's/[[:space:]]*$//' -e '/^$/d' > "$out" || true
  echo "the release notes start after $from, where $since"
else
  echo "the release notes are empty, since $why and the checkout has no history of build/version.txt"
fi

# A line naming who decided a change is left out and said, since a reader of the package has no
# one to follow and the history cannot be written again (NORM.md, N 4.7). The words are those
# NormTests looks for in the documents.
named='the owner|the reviewing session|REVIEW\.md'
if grep -iqE "$named" "$out"; then
  grep -iE "$named" "$out" | while IFS= read -r line; do
    if [ -n "${GITHUB_ACTIONS:-}" ]; then echo "::warning title=Release notes::Left out for naming who decided, $line"
    else echo "left out of the release notes for naming who decided: $line"; fi
  done
  grep -viE "$named" "$out" > "$out.kept" || true
  mv "$out.kept" "$out"
fi
