#!/usr/bin/env bash
# Measures the light that bounces on shaders_bounce_rooms: for each of its eight views, a capture,
# path-traced references made at High through the GPU's rays, the frame of each quality compared
# with each region by region, and a probe in the room's middle against a reference of the light
# arriving at it, into a folder.
#
#   build/bounce-rooms.sh <folder> [samples] [qualities] [bounces]
#
# The reference takes 1024 paths a pixel unless given, the qualities are Low, Medium and High
# unless given, as "High" alone, and the references are traced with every bounce unless given, as
# "1 2 all" for light bouncing once, twice and until each path ends. Each view's files are
# view-<n>.png, its references view-<n>-reference-<bounces>.png with the files gi.reference writes
# beside each, view-<n>-<quality>-<bounces>.txt, what gi.compare said, with each reference's
# difference picture the last quality's, and view-<n>-High-probe.txt, what gi.probe said with every
# bounce. A GPU that traces rays is needed, as the references are traced through them. The
# examples are built first, since ./e3d runs whatever build of them is there, and a build older than
# the files measured them as they were.
set -euo pipefail

folder="$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
samples="${2:-1024}"
qualities="${3:-Low Medium High}"
bounces="${4:-all}"
cd "$(dirname "$0")/.."
mkdir -p "$folder"
dotnet build 3DEngine.Examples --nologo -v q > /dev/null || { echo "the examples did not build, so nothing was measured" >&2; exit 1; }

./e3d open shaders_bounce_rooms --hidden --quiet
trap './e3d stop --quiet || true' EXIT
keys=(One Two Three Four Five Six Seven Eight)
# A point in the middle of each room, whose nearest probe of the first cascade is measured.
probes=("0 2.5 0" "40 1.5 0" "80 1.25 0" "120 1.5 0" "162 1 -5" "200 1.5 0" "240 1.5 0" "280 1.5 1.5")
for view in 1 2 3 4 5 6 7 8; do
  ./e3d command input.key "${keys[$((view - 1))]}" 2 --quiet
  ./e3d eval "SetGlobalIllumination(GlobalIllumination.High)" > /dev/null
  # Long enough for the field to settle and the screen's probes' history to fill.
  ./e3d command frames.wait 120 --quiet
  ./e3d shot "$folder/view-$view.png" > /dev/null
  for b in $bounces; do
    if [ "$b" = all ]; then ./e3d command gi.reference "$folder/view-$view-reference-$b.png" "$samples"
    else ./e3d command gi.reference "$folder/view-$view-reference-$b.png" "$samples" "$b"; fi
  done
  for quality in $qualities; do
    ./e3d eval "SetGlobalIllumination(GlobalIllumination.$quality)" > /dev/null
    ./e3d command frames.wait 120 --quiet
    for b in $bounces; do
      ./e3d command gi.compare "$folder/view-$view-reference-$b.png" > "$folder/view-$view-$quality-$b.txt"
    done
    # The probe's reference is traced through the meshes High holds for the GPU's rays.
    # shellcheck disable=SC2086
    [ "$quality" != High ] || ./e3d command gi.probe ${probes[$((view - 1))]} "$((samples / 4))" > "$folder/view-$view-$quality-probe.txt"
  done
done
echo "written into $folder"
