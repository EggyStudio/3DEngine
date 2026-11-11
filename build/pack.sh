#!/usr/bin/env bash
# Builds the engine in Release, compiles its shaders into a cache with `e3d shaders`, and packs
# the library, its areas, the shaders and that cache into build/package, with the `dotnet new`
# templates beside it. A game built from the package then loads the built-in shaders with no
# slangc of its own.
set -euo pipefail
cd "$(dirname "$0")/.."

# A release passes its version, which build/version.sh works out, as the pack workflow does. A pack
# without one is a version of its own, stamped with the time, since NuGet keeps the first package of
# a version it sees and never reads it again, and a game asks for the newest with a floating
# version such as 0.1.0-*.
version="${1:-0.1.0-preview.$(date -u +%Y%m%d%H%M%S)}"
# Built at the package's version, since the packs below take the assemblies as built, and the
# engine keeps what its user's cache holds under the version its assemblies carry, so a package
# built at the default one shared one folder of pipelines with every package before it.
dotnet build 3DEngine.slnx -c Release -p:Version="$version"
rm -rf build/shader-cache
./e3d shaders 3DEngine/Shaders build/shader-cache
# The release notes, the commits since the last package of this minor on nuget.org was built
# (build/release-notes.sh), whose first line, the commit they start after, the run's summary shows.
notes="$PWD/build/artifacts/release-notes.txt"
said="$(build/release-notes.sh "$notes")"
echo "$said"
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  echo "$said" | head -1 | sed 's/^t/T/; s/$/./' >> "$GITHUB_STEP_SUMMARY"
fi
if [ ! -s "$notes" ]; then
  echo "No commits since the package before $version." > "$notes"
fi
# The engine's package, the flat API's, and a package for each area it depends on, all of the one
# version, so a game's one reference to 3DEngine brings every area at that version.
for project in 3DEngine/3DEngine.csproj $(find 3DEngine -mindepth 2 -name '*.csproj' -not -path '*/obj/*' | sort); do
  dotnet pack "$project" -c Release --no-build -o build/package -p:Version="$version" -p:ReleaseNotesFile="$notes"
done
# The version the packed assemblies carry, the one the engine names its cache folders by, read from
# the core area's package, which every other area depends on.
core="build/package/3DEngine.Core.$version.nupkg"
carried="$(unzip -p "$core" 'lib/*/3DEngine.Core.dll' | grep -aoE "$(printf '%s' "$version" | sed 's/[.]/[.]/g')(\+[0-9a-f]+)?" | head -1 || true)"
if [ "${carried%%+*}" != "$version" ]; then
  echo "the packed 3DEngine.Core.dll does not carry the version $version, so the package's caches would share another version's folders" >&2
  exit 1
fi
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
echo "packed 3DEngine, its areas and 3DEngine.Templates $version into build/package, with $(wc -l < "$notes") lines of release notes"
