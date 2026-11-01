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
# the camera of the scene its views are matched against meters, for the look beside that scene's
# pictures, and <folder>/times.tsv holds each fixed one's frame in milliseconds. A view is held 90
# frames before it is captured, so the screen's probes and their history settle on it after the
# frame that builds the field around it, and 60 more metered.
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
    ./e3d command frames.wait 90 --session "$pid" --quiet --timeout 600
    ./e3d shot "$out/sponza-$view-$hour.png" --session "$pid" --quiet
    frame=$(./e3d command profile --session "$pid" | awk '$1 == "frame" {print $2}')
    printf '%s\t%s\t%s\n' "$view" "$hour" "$frame" >> "$out/times.tsv"
    echo "$view at $hour: $frame ms a frame"
    ./e3d command sponza.metered on --session "$pid" --quiet
    ./e3d command frames.wait 60 --session "$pid" --quiet --timeout 600
    ./e3d shot "$out/sponza-$view-$hour-metered.png" --session "$pid" --quiet
    ./e3d command sponza.metered off --session "$pid" --quiet
  done
done

./e3d stop --session "$pid" >/dev/null
trap - EXIT
if grep -F '[Vulkan.Validation]' build/sessions/scenes_sponza.log | grep -F 'Validation Error'; then
  echo "::error title=Sponza::the validation layer reported an error while Sponza was driven"
  exit 1
fi
