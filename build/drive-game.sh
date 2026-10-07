#!/usr/bin/env bash
# Plays the rhythm game from the package through ./e3d, offscreen and under the validation layer, as
# the build workflow plays every game on Linux, so a game's input, its sound and the session ./e3d
# drives are tried on the system it runs on. Tempo's autopilot plays its song through by the music's
# own clock, on the system's audio device or SDL's dummy driver, and the run fails on a note missed.
# The workflow runs it on Windows and macOS after build/play-game.sh, which packs the engine.
#
#   build/drive-game.sh
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p captures

dotnet restore games/Tempo --force-evaluate
dotnet build games/Tempo --no-restore
rm -f games/Tempo/bin/Debug/net10.0/tempo-best.txt games/Tempo/bin/Debug/net10.0/tempo-offset.txt

# The path without the .exe a Windows build gives it, which ./e3d finds.
ENGINE_VULKAN_VALIDATION=1 ./e3d open games/Tempo/bin/Debug/net10.0/Tempo --offscreen --quiet
trap './e3d stop --quiet > /dev/null 2>&1 || true' EXIT
./e3d command frames.wait 30 --quiet
./e3d command tempo.autopilot true --quiet
./e3d command input.key Enter 2 --quiet

# The song is a minute of the music's time, which a slow device reaches later.
status=""
for i in $(seq 1 60); do
  status=$(./e3d command tempo.status)
  case "$status" in
    Results*) break ;;
    *) ./e3d command frames.wait 60 --quiet --timeout 600 ;;
  esac
done
echo "$status"
./e3d command input.key Enter 2 --quiet
./e3d command frames.wait 10 --quiet
./e3d shot captures/tempo-driven.png --quiet
./e3d stop --quiet
cp build/sessions/Tempo.log captures/tempo-driven.log

case "$status" in
  Results*"miss 0 "*) ;;
  *) echo "Tempo: the autopilot did not play every note of the song within its windows, $status" >&2; exit 1 ;;
esac
if ! grep -q "Validation layers: ENABLED" captures/tempo-driven.log; then
  echo "Tempo drew without the validation layer" >&2
  exit 1
fi
if grep -F 'Validation Error' captures/tempo-driven.log; then
  echo "Tempo: the validation layer reported an error" >&2
  exit 1
fi
echo "Tempo played its song through, driven by ./e3d, with no note missed and no validation error"
