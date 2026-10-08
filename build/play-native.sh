#!/usr/bin/env bash
# Publishes a game from the package as native code, as a player gets it, and draws frames of it
# offscreen under the validation layer, failing when it does not run to the end or the layer
# reports an error. A native publish keeps only what the compiler sees used, so a type the library
# reaches by reflection, which a generator could have registered, fails here first (NORM.md, N 2.5).
#
#   build/play-native.sh <game> [frames]
#
# The package is the newest in build/package, which build/pack.sh makes. The game is published for
# the machine it runs on, with the toolchain the machine has, as a player's copy is, and what fails
# is said as an error annotation naming the game and the system, so the page says why.
set -euo pipefail

game="$1"
frames="${2:-300}"
cd "$(dirname "$0")/.."
mkdir -p captures
# The system by the name a reader knows it by, where Git's bash on Windows calls itself MINGW64.
case "$(uname -s)" in
  MINGW*|MSYS*|CYGWIN*) system=Windows ;;
  Darwin) system=macOS ;;
  *) system=$(uname -s) ;;
esac
fail() {
  echo "::error title=$game published native on $system::$game published native: $1"
  echo "$game published native: $1" >&2
  exit 1
}

case "$(uname -s)-$(uname -m)" in
  Linux-x86_64) rid=linux-x64 ;;
  Linux-aarch64) rid=linux-arm64 ;;
  Darwin-arm64) rid=osx-arm64 ;;
  Darwin-x86_64) rid=osx-x64 ;;
  *) rid=win-x64 ;;
esac

out="captures/$game-native"
published="captures/$game-native-publish.log"
if ! { dotnet restore "games/$game" --force-evaluate -r "$rid" -p:PublishAot=true &&
       dotnet publish "games/$game" --no-restore -c Release -r "$rid" -p:PublishAot=true -o "$out"; } > "$published" 2>&1; then
  cat "$published"
  # The compiler's or the linker's own error lines, which name what it lacked.
  said=$(grep -E ' error |error [A-Z]+[0-9]+' "$published" | tail -n 3 | tr -s '\r\n' '  ' | cut -c1-600)
  fail "did not publish for $rid${said:+, $said}"
fi
tail -n 3 "$published"

program="$out/$game"
[ -f "$program.exe" ] && program="$program.exe"
log="captures/$game-native.log"
# Run from its folder, where it finds its resources as a player's copy does.
if ! (cd "$out" && ENGINE_VULKAN_VALIDATION=1 "./$(basename "$program")" --offscreen --frames "$frames") > "$log" 2>&1; then
  tail -40 "$log"
  ending=$(tail -n 3 "$log" | tr -s '\r\n' '  ' | sed 's/ *$//' | cut -c1-600)
  fail "did not run to frame $frames${ending:+, its log ends with $ending}"
fi
grep -q "Validation layers: ENABLED" "$log" || fail "drew without the validation layer"
if grep -F 'Validation Error' "$log"; then fail "the validation layer reported an error"; fi
echo "$game published native drew $frames frames offscreen on $system with no validation error"
