#!/usr/bin/env bash
# Follows the documents as someone holding only the package would. The README's commands make a
# game from each template, installed from the package folder rather than nuget.org, and a console
# project gets BUILDING.md's nuget.config with the folder's path in it, its `dotnet add package`
# line and the README's first program. Each is built and run offscreen for thirty frames. Every
# step is read from the page that shows it, so the walk fails when a page goes wrong.
#
#   build/readme-walk.sh <package folder> [work folder]
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
readme="$root/README.md"
building="$root/.github/BUILDING.md"
package="$(cd "$1" && pwd)"
work="${2:-$(mktemp -d)}"

# The first fenced block of a language in a page.
# A path as dotnet reads it on every system, Windows' own form where Git's bash runs, since a path
# written into a file or a variable is not turned into one there as an argument is.
native() { if command -v cygpath > /dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

block() { awk -v fence="\`\`\`$2" '$0 == fence { inside = 1; next } /^```/ { if (inside) exit } inside' "$1"; }

# Thirty frames with no window, failing on an error in the log.
run() {
  dotnet build
  dotnet run --no-build -- --offscreen --frames 30 | tee run.log
  if grep -E '\[(ERROR|FATAL)' run.log; then
    echo "$1 logged an error" >&2
    exit 1
  fi
}

mkdir -p "$work" && cd "$work"
rm -rf Hello HelloEcs Plain dotnet-home

# The templates go into a list of the walk's own rather than the user's.
export DOTNET_CLI_HOME="$(native "$work/dotnet-home")"
templates="$(ls -t "$package"/3DEngine.Templates.*.nupkg | head -1)"

# The README's three commands, with the templates from the folder, the project told where the
# engine is, and the run offscreen.
commands="$(block "$readme" bash)"
grep -qx 'dotnet new install 3DEngine.Templates' <<< "$commands"
grep -q '^dotnet new 3dengine ' <<< "$commands"
grep -qx 'dotnet run' <<< "$commands"
dotnet new install "$templates"
eval "$(grep '^dotnet new 3dengine ' <<< "$commands" | sed "s#dotnet new 3dengine \([^&]*\)#dotnet new 3dengine \1 --package-folder '$(native "$package")' #")"
run "the flat template's program"
cd "$work"
dotnet new 3dengine-ecs -n HelloEcs --package-folder "$(native "$package")"
cd HelloEcs
run "the behaviors template's program"
grep -q "Hot-reload\|RuntimeBehaviorCompiler" run.log

# The steps without the templates.
cd "$work"
dotnet new console -n Plain -o Plain
cd Plain
block "$building" xml | sed "s#path/to/3DEngine/build/package#$(native "$package")#" > nuget.config
# The line for a package built from a checkout, which names its version.
eval "$(grep -m1 '^dotnet add package 3DEngine --version' "$building")"
block "$readme" csharp > Program.cs
run "the README's program"
echo "both templates and the README's program built and ran"
