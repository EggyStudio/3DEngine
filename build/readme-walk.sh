#!/usr/bin/env bash
# Follows the documents as someone holding only the package would. A new console project gets
# BUILDING.md's nuget.config with the package folder's path in it and its `dotnet add package` line,
# and the README's first program, built and run offscreen for thirty frames. Each step is read from
# the page that shows it, so the walk fails when a page goes wrong.
#
#   build/readme-walk.sh <package folder> [work folder]
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
readme="$root/README.md"
building="$root/.github/BUILDING.md"
package="$(cd "$1" && pwd)"
work="${2:-$(mktemp -d)}"

# The first fenced block of a language in a page.
block() { awk -v fence="\`\`\`$2" '$0 == fence { inside = 1; next } /^```/ { if (inside) exit } inside' "$1"; }

mkdir -p "$work" && cd "$work"
rm -rf Hello
dotnet new console -n Hello -o Hello
cd Hello
block "$building" xml | sed "s#path/to/3DEngine/build/package#$package#" > nuget.config
# The line for a package built from a checkout, which names its version.
eval "$(grep -m1 '^dotnet add package 3DEngine --version' "$building")"
block "$readme" csharp > Program.cs
dotnet build
dotnet run --no-build -- --offscreen --frames 30 | tee run.log
if grep -E '\[(ERROR|FATAL)' run.log; then
  echo "the README's program logged an error" >&2
  exit 1
fi
echo "the README's program built and ran"
