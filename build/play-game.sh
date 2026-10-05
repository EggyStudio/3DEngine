#!/usr/bin/env bash
# Builds a game from the package, as a game of a user's own is built, and draws frames of it
# offscreen under the validation layer, failing when it does not run to the end or the layer
# reports an error. The workflow runs it on Windows and macOS, where ./e3d's session is not used.
#
#   build/play-game.sh <game> [frames]
set -euo pipefail

game="$1"
frames="${2:-300}"
cd "$(dirname "$0")/.."
mkdir -p captures

build/pack.sh > "captures/pack.log" 2>&1 || { cat captures/pack.log; exit 1; }
dotnet restore "games/$game" --force-evaluate
dotnet build "games/$game" --no-restore

program="games/$game/bin/Debug/net10.0/$game"
[ -f "$program.exe" ] && program="$program.exe"
log="captures/$game.log"
# Run from its folder, where it finds its resources as a player's copy does.
if ! (cd "$(dirname "$program")" && ENGINE_VULKAN_VALIDATION=1 "./$(basename "$program")" --offscreen --frames "$frames") > "$log" 2>&1; then
  tail -40 "$log"
  echo "$game did not run to frame $frames" >&2
  exit 1
fi
if ! grep -q "Validation layers: ENABLED" "$log"; then
  echo "$game drew without the validation layer" >&2
  exit 1
fi
if grep -F 'Validation Error' "$log"; then
  echo "$game: the validation layer reported an error" >&2
  exit 1
fi
echo "$game drew $frames frames offscreen with no validation error"
