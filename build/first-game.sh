#!/usr/bin/env bash
# Builds every step of docs/first-game.md's game, each a whole program under games/FirstGame/steps,
# in a copy of the game's project against the package in build/package, and runs it offscreen for
# thirty frames, so no step the page shows can stop compiling or starting. With --shots it also
# opens each in a hidden window through ./e3d, walks the player as the step's picture shows, and
# writes the picture into .github/assets/first-game.
#
#   build/first-game.sh [--shots]
set -euo pipefail
cd "$(dirname "$0")/.."
root="$PWD"
shots="${1:-}"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
mkdir -p .github/assets/first-game

# A path as dotnet reads it on every system, Windows' own form where Git's bash runs, since a path
# written into a file or a variable is not turned into one there as an argument is.
native() { if command -v cygpath > /dev/null 2>&1; then cygpath -m "$1"; else printf '%s' "$1"; fi; }

for step in games/FirstGame/steps/*.cs; do
  n="$(basename "$step" .cs)"
  project="$work/Coins"
  rm -rf "$project"
  mkdir -p "$project"
  cp games/FirstGame/Coins.csproj "$project/"
  cp -r games/FirstGame/resources "$project/"
  sed "s#../../build/package#$(native "$root")/build/package#" games/FirstGame/nuget.config > "$project/nuget.config"
  cp "$step" "$project/Program.cs"
  echo "step $n"
  dotnet build "$project" -v q --nologo | grep -E "error|rror\(s\)" || true
  program="$project/bin/Debug/net10.0/Coins"
  (cd "$project/bin/Debug/net10.0" && ./Coins --offscreen --frames 30 > run.log 2>&1)
  if grep -E '\[(ERROR|FATAL)' "$project/bin/Debug/net10.0/run.log"; then
    echo "step $n logged an error" >&2
    exit 1
  fi

  if [ "$shots" = "--shots" ]; then
    ./e3d open "$program" --hidden --quiet
    ./e3d command frames.wait 30 --quiet
    # A step where the player walks is shown after a walk to the right, toward the first coin.
    case "$n" in 01|02) ;; *) ./e3d command input.key D 45 --quiet ;; esac
    ./e3d command frames.wait 10 --quiet
    ./e3d shot "$work/$n.png" --quiet
    ./e3d stop --quiet
    build/webp.sh "$work/$n.png" ".github/assets/first-game/$n.webp" lossy
  fi
done
echo "every step of the first game built and ran"
