#!/usr/bin/env bash
# Builds the engine in Release, compiles its shaders into a cache with `e3d shaders`, and packs
# the library, the shaders and that cache into build/package, with the `dotnet new` templates
# beside it. A game built from the package then loads the built-in shaders with no slangc of its
# own.
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
# history has none, and nor does a version raised by the last commit, which say so in their place.
notes="$PWD/build/artifacts/release-notes.txt"
mkdir -p build/artifacts
: > "$notes"
if changed="$(git log -1 --format=%H -- build/version.txt 2>/dev/null)" && [ -n "$changed" ]; then
  git log --format=%b "$changed"..HEAD | sed -e 's/[[:space:]]*$//' -e '/^$/d' > "$notes" || true
fi
if [ ! -s "$notes" ]; then
  echo "The first package of $version, with no commits since its version was raised." > "$notes"
fi
dotnet pack 3DEngine/3DEngine.csproj -c Release --no-build -o build/package -p:Version="$version" -p:ReleaseNotesFile="$notes"
# The templates, of the same version, from a copy with that version written in as the one a new
# project asks for, so a project made from them builds against the engine packed with them.
rm -rf build/templates
cp -r templates build/templates
# Written to a file beside each and moved over it, since macOS's sed takes what follows -i as the
# ending of a backup and GNU's does not.
for template in build/templates/content/*/.template.config/template.json; do
  sed "s/PACKED_VERSION/$version/" "$template" > "$template.packed"
  mv "$template.packed" "$template"
done
dotnet pack build/templates/3DEngine.Templates.csproj -c Release -o build/package -p:Version="$version"
echo "packed 3DEngine and 3DEngine.Templates $version into build/package, with $(wc -l < "$notes") lines of release notes"
