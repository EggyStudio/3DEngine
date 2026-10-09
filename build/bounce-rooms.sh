#!/usr/bin/env bash
# Measures the light that bounces on shaders_bounce_rooms: for each of its eight views, a capture,
# a path-traced reference made at High through the GPU's rays, and the frame of each quality
# compared with it region by region, into a folder.
#
#   build/bounce-rooms.sh <folder> [samples] [qualities]
#
# The reference takes 1024 paths a pixel unless given, and the qualities are Low, Medium and High
# unless given, as "High" alone. Each view's files are view-<n>.png, its reference
# view-<n>-reference.png with the files gi.reference writes beside it, and view-<n>-<quality>.txt,
# what gi.compare said, with view-<n>-reference-difference.png the last quality's. A GPU that
# traces rays is needed, as the reference is traced through them.
set -euo pipefail

folder="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
samples="${2:-1024}"
qualities="${3:-Low Medium High}"
cd "$(dirname "$0")/.."
mkdir -p "$folder"

./e3d open shaders_bounce_rooms --hidden --quiet
trap './e3d stop --quiet || true' EXIT
keys=(One Two Three Four Five Six Seven Eight)
for view in 1 2 3 4 5 6 7 8; do
  ./e3d command input.key "${keys[$((view - 1))]}" 2 --quiet
  ./e3d eval "SetGlobalIllumination(GlobalIllumination.High)" > /dev/null
  # Long enough for the field to settle and the screen's probes' history to fill.
  ./e3d command frames.wait 120 --quiet
  ./e3d shot "$folder/view-$view.png" > /dev/null
  ./e3d command gi.reference "$folder/view-$view-reference.png" "$samples"
  for quality in $qualities; do
    ./e3d eval "SetGlobalIllumination(GlobalIllumination.$quality)" > /dev/null
    ./e3d command frames.wait 120 --quiet
    ./e3d command gi.compare "$folder/view-$view-reference.png" > "$folder/view-$view-$quality.txt"
  done
done
echo "written into $folder"
