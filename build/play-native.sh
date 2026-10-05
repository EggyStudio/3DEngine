#!/usr/bin/env bash
# Publishes a game from the package as native code, as a player gets it, and draws frames of it
# offscreen under the validation layer, failing when it does not run to the end or the layer
# reports an error. A native publish keeps only what the compiler sees used, so a type the library
# reaches by reflection, which a generator could have registered, fails here first (NORM.md, N 2.5).
#
#   build/play-native.sh <game> [frames]
#
# The package is the newest in build/package, which build/pack.sh makes. The game is published for
# the machine it runs on.
set -euo pipefail

game="$1"
frames="${2:-300}"
cd "$(dirname "$0")/.."
mkdir -p captures

case "$(uname -s)-$(uname -m)" in
  Linux-x86_64) rid=linux-x64 ;;
  Linux-aarch64) rid=linux-arm64 ;;
  Darwin-arm64) rid=osx-arm64 ;;
  Darwin-x86_64) rid=osx-x64 ;;
  *) rid=win-x64 ;;
esac

out="captures/$game-native"
dotnet restore "games/$game" --force-evaluate -r "$rid" -p:PublishAot=true
dotnet publish "games/$game" --no-restore -c Release -r "$rid" -p:PublishAot=true -o "$out"

program="$out/$game"
[ -f "$program.exe" ] && program="$program.exe"
log="captures/$game-native.log"
# Run from its folder, where it finds its resources as a player's copy does.
if ! (cd "$out" && ENGINE_VULKAN_VALIDATION=1 "./$(basename "$program")" --offscreen --frames "$frames") > "$log" 2>&1; then
  tail -40 "$log"
  echo "$game published native did not run to frame $frames" >&2
  exit 1
fi
if ! grep -q "Validation layers: ENABLED" "$log"; then
  echo "$game published native drew without the validation layer" >&2
  exit 1
fi
if grep -F 'Validation Error' "$log"; then
  echo "$game published native: the validation layer reported an error" >&2
  exit 1
fi
echo "$game published native drew $frames frames offscreen with no validation error"
