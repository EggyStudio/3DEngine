#!/usr/bin/env bash
# Follows the README as someone holding only the package would. A new console project gets the
# README's nuget.config with the package folder's path in it, its `dotnet add package` line and its
# first program, built and run offscreen for thirty frames. Each step is read from the README, so the
# walk fails when the page goes wrong.
#
#   build/readme-walk.sh <package folder> [work folder]
set -euo pipefail
readme="$(cd "$(dirname "$0")/.." && pwd)/README.md"
package="$(cd "$1" && pwd)"
work="${2:-$(mktemp -d)}"

# The first fenced block of a language in the README.
block() { awk -v fence="\`\`\`$1" '$0 == fence { inside = 1; next } /^```/ { if (inside) exit } inside' "$readme"; }

mkdir -p "$work" && cd "$work"
rm -rf Hello
dotnet new console -n Hello -o Hello
cd Hello
block xml | sed "s#path/to/3DEngine/build/package#$package#" > nuget.config
# The line for a package built from a checkout, which names its version.
eval "$(grep -m1 '^dotnet add package 3DEngine --version' "$readme")"
block csharp > Program.cs
dotnet build
dotnet run --no-build -- --offscreen --frames 30 | tee run.log
if grep -E '\[(ERROR|FATAL)' run.log; then
  echo "the README's program logged an error" >&2
  exit 1
fi
echo "the README's program built and ran"
