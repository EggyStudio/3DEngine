#!/usr/bin/env bash
# Plays a game for a while through ./e3d, as a player left at it would, restarting its level,
# spawning and clearing what it spawns and starting sounds throughout, and reads what the program
# holds (`memory.collect`) every interval into build/soak/<name>.csv. build/soak-check.py then fails
# when anything climbs without leveling off, a leak.
#
#   build/soak.sh <pusher|hopper|summit|swarm> <program> <seconds> [--offscreen|--hidden]
#
# The program is the game's executable, built from the package as CI builds it.
set -euo pipefail

name="$1"
program="$2"
seconds="$3"
mode="${4:---offscreen}"
cd "$(dirname "$0")/.."

mkdir -p build/soak
out="build/soak/$name.csv"
: > "$out"

# Every call names the session, so several games are played at once.
session=(--name "$(basename "$program")")
./e3d open "$program" "$mode" --quiet
trap './e3d stop "${session[@]}" --quiet || true' EXIT
wait_frames() { ./e3d command frames.wait "$1" "${session[@]}" --quiet --timeout 600; }
key() { ./e3d command input.key "$1" "$2" "${session[@]}" --quiet --timeout 600; }
cmd() { ./e3d command "$@" "${session[@]}" --quiet --timeout 600; }

# How each game starts.
wait_frames 60
case "$name" in
  pusher|summit) key Enter 2 ;;
  swarm) cmd swarm.invulnerable true; key Enter 2 ;;
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
  esac
}

end=$(( SECONDS + seconds ))
next=$SECONDS
i=0
while (( SECONDS < end )); do
  turn "$i"
  i=$(( i + 1 ))
  if (( SECONDS >= next )); then
    echo "$SECONDS $(./e3d command memory.collect "${session[@]}" --timeout 600)" >> "$out"
    next=$(( SECONDS + 10 ))
  fi
done
echo "$SECONDS $(./e3d command memory.collect "${session[@]}" --timeout 600)" >> "$out"
echo "$name: $i turns in $seconds seconds, $(wc -l < "$out") readings in $out"
