#!/usr/bin/env bash
# Plays a game for a while through ./e3d, as a player left at it would, restarting its level,
# spawning and clearing what it spawns and starting sounds throughout, and reads what the program
# holds (`memory.collect`) every ten seconds into build/soak/<name>.csv. build/soak-check.py then
# fails when anything climbs without leveling off, a leak.
#
#   build/soak.sh <pusher|hopper|summit|swarm|rally|manor|tactics|tempo> <program> <seconds> [--offscreen|--hidden]
#
# The program is the game's executable, built from the package as CI builds it.
set -Eeuo pipefail

name="$1"
program="$2"
seconds="$3"
mode="${4:---offscreen}"
cd "$(dirname "$0")/.."

mkdir -p build/soak
out="build/soak/$name.csv"
: > "$out"
# The first command that ends the soak early, with its exit code and the program's name, which
# build/soak-check.py names with the program's last warnings. -E keeps the trap in functions and
# the reader's subshell.
rm -f "build/soak/$name.failed"
trap '[ -e "build/soak/$name.failed" ] || echo "$? $(basename "$program") $BASH_COMMAND" > "build/soak/$name.failed"' ERR

# Every call names the session, so several games are played at once.
session=(--name "$(basename "$program")")
./e3d open "$program" "$mode" --quiet
trap './e3d stop "${session[@]}" --quiet || true' EXIT
wait_frames() { ./e3d command frames.wait "$1" "${session[@]}" --quiet --timeout 600; }
key() { ./e3d command input.key "$1" "$2" "${session[@]}" --quiet --timeout 600; }
cmd() { ./e3d command "$@" "${session[@]}" --quiet --timeout 600; }

# Drawn at 320 by 180, since a soak reads the memory a program holds and not its picture, and a
# small frame lets a device drawing on its CPU, as a runner with lavapipe is, play far more of each
# game in the soak's time. At the window's own size eight games on four cores drew a frame or two a
# second, and Manor's walk had not passed its first rooms in two minutes, still streaming them in.
cmd window.size 320 180

# How each game starts.
wait_frames 60
case "$name" in
  pusher|summit) key Enter 2 ;;
  swarm) cmd swarm.invulnerable true; key Enter 2 ;;
  rally) cmd rally.autopilot true; key Enter 2 ;;
  manor) cmd input.button 0 RightFaceDown 2; cmd manor.autopilot true ;;
  tactics) cmd tactics.new 1; cmd tactics.autopilot true ;;
  tempo) cmd tempo.autopilot true; key Enter 2 ;;
esac

# One turn of play, each a few seconds, with a restart every few turns. Swarm fights the same
# wave each turn, spawned and despawned again, so what it holds at its most is the same in every
# turn however few a slow device gets through.
turn() {
  local i="$1"
  case "$name" in
    pusher)
      key W 40; key D 40; key S 40; key A 40
      if (( i % 4 == 3 )); then key R 2; fi ;;
    hopper)
      key Right 60; key Space 2; key Right 40; key Left 30
      if (( i % 5 == 4 )); then key R 2; fi ;;
    summit)
      key W 60; key Space 2; key D 30; key A 30
      if (( i % 4 == 3 )); then cmd state.set Screen Won; key R 2; fi ;;
    swarm)
      cmd swarm.wave 3; wait_frames 240; key W 30; key D 30
      if (( i % 5 == 4 )); then cmd state.set Screen Over; wait_frames 30; key Enter 2; fi ;;
    rally)
      # The autopilot races, and each Enter takes a finished race back to the menu and the menu
      # to a new race, while a reset now and then puts the car back at its last gate.
      wait_frames 120
      if (( i % 4 == 3 )); then cmd rally.reset; fi
      key Enter 2 ;;
    manor)
      # The autopilot walks the estate, its cells streamed in and let go, and the pad's bottom
      # button, a jump on the way, picks Walk again once every lantern is found.
      wait_frames 240
      cmd input.button 0 RightFaceDown 2 ;;
    tactics)
      # The computer plays both sides, and a match that has ended is followed by a new one on
      # another map, saved and taken up again now and then.
      wait_frames 240
      if (( i % 3 == 2 )); then cmd tactics.save; cmd tactics.load; fi
      case "$(./e3d command tactics.status "${session[@]}")" in Over*) cmd tactics.new "$i" ;; esac ;;
    tempo)
      # The autopilot plays the song, paused and played on now and then, and Enter plays it again
      # once its results show, the music and what was measured of it started afresh.
      wait_frames 240
      if (( i % 3 == 2 )); then key P 2; wait_frames 20; key P 2; fi
      key Enter 2 ;;
  esac
}

# What the program holds is read every ten seconds on a clock of its own, beside the turns, which
# the program answers between frames while a turn waits on them. A device whose turns take long, as
# a runner drawing eight games on four cores is, where a turn of 240 frames took two minutes, so
# still gives a dozen readings, where reading after each turn gave two.
end=$(( SECONDS + seconds ))
(
  while (( SECONDS < end )); do
    echo "$SECONDS $(./e3d command memory.collect "${session[@]}" --timeout 600)" >> "$out"
    sleep 10
  done
) &
reader="$!"
i=0
while (( SECONDS < end )); do
  turn "$i"
  i=$(( i + 1 ))
done
wait "$reader"
echo "$SECONDS $(./e3d command memory.collect "${session[@]}" --timeout 600)" >> "$out"
echo "$name: $i turns in $seconds seconds, $(wc -l < "$out") readings in $out"
