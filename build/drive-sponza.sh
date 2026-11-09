#!/usr/bin/env bash
# Drives the scenes_sponza example through each of its named views at noon and at evening, Intel's
# Sponza fetched first where it is not (./e3d scenes fetch intel-sponza), each view captured and its
# frame's time read from the profile, then the session's log checked for validation errors. The
# pack run's macOS captures job runs it on the runner's GPU, and the readings' pictures of Sponza
# are taken with it.
#
#   build/drive-sponza.sh <folder> [--offscreen|--hidden]
#
# Each picture is <folder>/sponza-<view>-<hour>.png, at the example's fixed camera, which the
# readings of its light take, and <folder>/sponza-<view>-<hour>-metered.png, metered view by view as
# the example starts, center-weighted between EV -1 and 14, for the look as a player sees it, and
# <folder>/times.tsv holds each fixed one's frame in
# milliseconds. A view is held until the field is built around it and its light has run 60 frames
# on it (field.wait), so the screen's probes and their history settle on it too, and 60 more
# metered. At 2:00 the vestibule's floor under its lamp by the wooden door is read with the lamps
# off, as the example starts, and on, the night's pictures sponza-door-2-off.png and
# sponza-door-2-on.png.
set -euo pipefail

mkdir -p "$1"
out=$(cd "$1" && pwd)
mode="${2:---offscreen}"
cd "$(dirname "$0")/.."

./e3d scenes fetch intel-sponza
build/open-game.sh scenes_sponza "$mode"
pid=$(cat build/sessions/scenes_sponza.pid)
trap './e3d stop --session "$pid" >/dev/null 2>&1 || true' EXIT

views=$(./e3d command sponza.view --session "$pid" | tr ',' '\n' | awk '{print $2}')
printf 'view\thour\tframe ms\n' > "$out/times.tsv"
for hour in 12 16; do
  ./e3d command sponza.time "$hour" --session "$pid" --quiet
  for view in $views; do
    ./e3d command sponza.view "$view" --session "$pid" --quiet
    ./e3d command sponza.metered off --session "$pid" --quiet
    ./e3d command field.wait 1800 --session "$pid" --timeout 1200
    ./e3d shot "$out/sponza-$view-$hour.png" --session "$pid" --quiet
    frame=$(./e3d command profile --session "$pid" | awk '$1 == "frame" {print $2}')
    printf '%s\t%s\t%s\n' "$view" "$hour" "$frame" >> "$out/times.tsv"
    echo "$view at $hour: $frame ms a frame"
    ./e3d command sponza.metered on --session "$pid" --quiet
    ./e3d command frames.wait 60 --session "$pid" --quiet --timeout 600
    ./e3d shot "$out/sponza-$view-$hour-metered.png" --session "$pid" --quiet
  done
done

# At night with the lamps off the floor under the vestibule's lamp reads dark in every channel,
# metered, and lit with them on it reads 7 levels a channel or more, 21 over the three summed, a
# bound with room under what the lamp's light passing its glass gives there (scenes_sponza).
./e3d command sponza.time 2 --session "$pid" --quiet
./e3d command sponza.view door --session "$pid" --quiet
./e3d command sponza.metered on --session "$pid" --quiet
for lamps in off on; do
  ./e3d command sponza.lamps "$lamps" --session "$pid" --quiet
  ./e3d command field.wait 1800 --session "$pid" --timeout 1200
  ./e3d command frames.wait 400 --session "$pid" --quiet --timeout 600
  ./e3d shot "$out/sponza-door-2-$lamps.png" --session "$pid" --quiet
  floor=$(./e3d command frame.read 240 200 200 100 --session "$pid" | awk '{print $2 + $3 + $4}')
  echo "the floor under the vestibule's lamp at 2:00 with the lamps $lamps: $floor, its three channels summed"
  if [ "$lamps" = off ] && [ "$floor" -ne 0 ]; then
    echo "::error title=Sponza::the floor under the vestibule's lamp at night with the lamps off reads $floor where it reads 0"
    exit 1
  fi
  if [ "$lamps" = on ] && [ "$floor" -lt 21 ]; then
    echo "::error title=Sponza::the floor under the vestibule's lamp at night with the lamps on reads $floor, its three channels summed, under 21"
    exit 1
  fi
done

./e3d stop --session "$pid" >/dev/null
trap - EXIT
if grep -F '[Vulkan.Validation]' build/sessions/scenes_sponza.log | grep -F 'Validation Error'; then
  echo "::error title=Sponza::the validation layer reported an error while Sponza was driven"
  exit 1
fi
