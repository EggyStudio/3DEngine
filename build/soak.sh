#!/usr/bin/env bash
# Plays a game for a while through ./e3d, as a player left at it would, restarting its level,
# spawning and clearing what it spawns and starting sounds throughout, and reads what the program
# holds (`memory.collect`) every ten seconds into build/soak/<name>.csv. build/soak-check.py then
# fails when anything climbs without leveling off, a leak.
#
#   build/soak.sh <hopper|summit|swarm|rally|manor|tactics|tempo|sumo|wordfall|slide|jelly|wick|ore|pinball|span|siege|tumble|tones|featuretest> <program> <seconds> [--offscreen|--hidden]
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
build/open-game.sh "$program" "$mode"
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
  summit) key Enter 2 ;;
  swarm) cmd swarm.invulnerable true; key Enter 2 ;;
  rally) cmd rally.autopilot true; key Enter 2 ;;
  manor) cmd input.button 0 RightFaceDown 2; cmd manor.autopilot true ;;
  tactics) cmd tactics.new 1; cmd tactics.autopilot true ;;
  tempo) cmd tempo.autopilot true; key Enter 2 ;;
  sumo) cmd sumo.autopilot true; key Enter 2 ;;
  wordfall) cmd wordfall.autopilot true; key Enter 2 ;;
  slide) cmd slide.autopilot true; key Enter 2 ;;
  jelly) cmd jelly.autopilot true; key Enter 2 ;;
  wick) cmd wick.autopilot true; key Enter 2 ;;
  ore) cmd ore.autopilot true; key Enter 2 ;;
  pinball) cmd pinball.autopilot true; key Enter 2 ;;
esac

# One turn of play, each a few seconds, with a restart every few turns. Swarm fights the same
# wave each turn, spawned and despawned again, so what it holds at its most is the same in every
# turn however few a slow device gets through.
turn() {
  local i="$1"
  case "$name" in
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
    sumo)
      # The marbles play themselves round after round, each round setting them back and its
      # bumps sending ripples and dust, paused now and then, and Enter starts a match again once
      # one is won.
      wait_frames 240
      if (( i % 3 == 2 )); then key P 2; wait_frames 20; key P 2; fi
      key Enter 2 ;;
    wordfall)
      # The autopilot types until the town is buried, a screenshot is saved now and then, and a
      # buried town's result is copied and a game started again.
      wait_frames 240
      if (( i % 4 == 3 )); then key F12 2; fi
      key C 2
      key Enter 2 ;;
    slide)
      # The autopilot plays, a move is taken back now and then, and a game with no move left is
      # followed by another.
      wait_frames 240
      if (( i % 3 == 2 )); then key U 2; fi
      key Enter 2 ;;
    jelly)
      # The autopilot runs until it crashes, a crashed run is watched again now and then from what
      # was recorded of it, and Enter runs again.
      wait_frames 240
      if (( i % 3 == 2 )); then key R 2; wait_frames 120; fi
      key Enter 2 ;;
    wick)
      # The autopilot lights the wicks and leaves by the door, every wick put out again now and
      # then, its light let go, and Enter goes in again once out.
      wait_frames 240
      if (( i % 3 == 2 )); then key R 2; fi
      key Enter 2 ;;
    ore)
      # The autopilot walks round the landscape jumping up its blocks, night falls and day comes
      # now and then, and global illumination is stepped through its qualities.
      wait_frames 240
      if (( i % 3 == 2 )); then key N 2; fi
      if (( i % 4 == 3 )); then key G 2; fi ;;
    pinball)
      # The autopilot launches each ball and flips, the table is nudged now and then, a nudge
      # too many tilting it, and Enter starts a game again once its last ball has drained.
      wait_frames 240
      if (( i % 3 == 2 )); then key T 2; fi
      key Enter 2 ;;
    span)
      # The truss and the road alone built in turn and the load sent across, the truss carrying
      # it and the road breaking, each bridge's planks and joints taken down by the next reset. A
      # frame passes between commands, since the game reads one a frame.
      cmd span.reset
      wait_frames 2
      if (( i % 2 == 0 )); then cmd span.build truss; else cmd span.build road; fi
      wait_frames 2
      cmd span.go
      wait_frames 240 ;;
    siege)
      # Stones and bombs thrown at the keep in turn, each landed before the next, the blocks a bomb
      # takes and the welds it breaks let go, and the castle laid again every few turns, its blocks
      # and welds made again.
      if (( i % 4 == 3 )); then cmd siege.reset; wait_frames 2; fi
      if (( i % 2 == 0 )); then cmd siege.throw 250 40 stone; else cmd siege.throw 250 40 bomb; fi
      wait_frames 240 ;;
    tumble)
      # The robot walked about the arena under the cannons and the bar, knocked down as a ragdoll
      # and standing up again as it goes, its ragdoll's bodies and joints made and let go each time,
      # and punching and crouching now and then.
      key W 40; key D 30; key S 40; key A 30
      if (( i % 3 == 2 )); then cmd tumble.strike; key C 60; fi
      cmd input.click 640 300
      wait_frames 60 ;;
    tones)
      # Notes and drums played under the echo and the filter turned on and off in turn, and every
      # few turns, from the first, a note recorded, saved, read back and played, the sound made of
      # it let go each time. The recording is of the note alone, at the start of the turn, since a
      # recording's samples come at the rate it plays, so one as long as a turn on a runner whose
      # turns took 30 seconds held its buffers from past the soak's first quarter, a step its check
      # read as a climb, where each recording now holds as much as the first.
      if (( i % 2 == 0 )); then cmd tones.effect echo on; else cmd tones.effect echo off; fi
      if (( i % 3 == 0 )); then cmd tones.effect filter on; else cmd tones.effect filter off; fi
      if (( i % 4 == 0 )); then cmd tones.record start; key A 10; cmd tones.record stop; wait_frames 2; key Four 2; fi
      key A 10; key D 10; key Z 2; key G 10; key X 2; key K 20
      wait_frames 60 ;;
    featuretest)
      # The player put at each zone in turn and walked a little there, the course's crates pushed
      # and its platform ridden from the start each time, global illumination turned on in its room
      # and off again as the next turn leaves it, the effects shown, the panel shown every fourth
      # turn, and every zone's status read.
      if (( i % 4 == 1 )); then cmd feature.panel on; else cmd feature.panel off; fi
      case $(( i % 9 )) in
        0) cmd feature.zone hub; key W 60 ;;
        1) cmd feature.zone drawing; key W 60 ;;
        2) cmd feature.zone course crates; key W 120 ;;
        3) cmd feature.zone course platform; wait_frames 120 ;;
        4) cmd feature.zone models; key W 60 ;;
        5) cmd feature.zone lights; wait_frames 70 ;;
        6) cmd feature.zone illumination; wait_frames 200 ;;
        7) cmd feature.zone particles; wait_frames 60 ;;
        8) cmd feature.zone effects; wait_frames 150 ;;
      esac
      cmd feature.status
      wait_frames 30 ;;
  esac
}

# What the program holds is read every ten seconds on a clock of its own, beside the turns, which
# the program answers between frames while a turn waits on them. A device whose turns take long, as
# a runner drawing eight games on four cores is, where a turn of 240 frames took two minutes, so
# still gives a dozen readings, where reading after each turn gave two.
# A game that walks a level says where it stands after each reading's figures, so a figure that
# climbs is read with the rooms the walk was in (build/soak-check.py).
where() { case "$name" in manor) echo " | $(./e3d command manor.status "${session[@]}" --timeout 600)" ;; esac; }
end=$(( SECONDS + seconds ))
(
  while (( SECONDS < end )); do
    echo "$SECONDS $(./e3d command memory.collect "${session[@]}" --timeout 600)$(where)" >> "$out"
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
echo "$SECONDS $(./e3d command memory.collect "${session[@]}" --timeout 600)$(where)" >> "$out"
echo "$name: $i turns in $seconds seconds, $(wc -l < "$out") readings in $out"
