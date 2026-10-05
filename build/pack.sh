#!/usr/bin/env bash
# Builds the engine in Release, compiles its shaders into a cache with `e3d shaders`, and packs
# the library, the shaders and that cache into build/package. A game built from the package then
# loads the built-in shaders with no slangc of its own.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet build 3DEngine.slnx -c Release
rm -rf build/shader-cache
./e3d shaders 3DEngine/Shaders build/shader-cache
# A release passes its version, which build/version.sh works out, as the pack workflow does. A pack
# without one is a version of its own, stamped with the time, since NuGet keeps the first package of
# a version it sees and never reads it again, and a game asks for the newest with a floating
# version such as 0.1.0-*.
version="${1:-0.1.0-preview.$(date -u +%Y%m%d%H%M%S)}"
# The release notes are the commits since build/version.txt last changed, newest first, each
# commit's sentence a line, as the history already says them to be read. A checkout without the
# history has none, and nor does a version raised by the last commit.
notes="$PWD/build/artifacts/release-notes.txt"
mkdir -p build/artifacts
: > "$notes"
if changed="$(git log -1 --format=%H -- build/version.txt 2>/dev/null)" && [ -n "$changed" ]; then
  git log --format=%b "$changed"..HEAD | sed -e 's/[[:space:]]*$//' -e '/^$/d' > "$notes" || true
fi
dotnet pack 3DEngine/3DEngine.csproj -c Release --no-build -o build/package -p:Version="$version" -p:ReleaseNotesFile="$notes"
echo "packed 3DEngine $version into build/package, with $(wc -l < "$notes") lines of release notes"
