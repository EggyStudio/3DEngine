#!/usr/bin/env bash
# Puts a program through a storm of resizes, minimizes, restores and moves while it draws, under
# the validation layer, then captures a frame and checks it is drawn at the size last asked for and
# that the layer reported nothing.
#
#   build/storm.sh <example-or-program> <png> [--offscreen|--hidden]
#
# An offscreen run has no window, so its resizes make its images again at each size, as a
# window's swapchain is made again, and minimizing makes it zero across.
set -euo pipefail

program="$1"
out="$2"
mode="${3:---offscreen}"
cd "$(dirname "$0")/.."

# Storms run one program at a time, so the calls need not name its session.
session=()
ENGINE_VULKAN_VALIDATION=1 ./e3d open "$program" "$mode" --quiet
trap './e3d stop "${session[@]}" --quiet || true' EXIT
cmd() { ./e3d command "$@" "${session[@]}" --quiet --timeout 600; }

cmd frames.wait 30
# Sizes one after another a frame apart, as a window dragged larger and smaller, some odd.
for size in "1024 640" "320 200" "801 449" "64 48" "1280 720" "500 900" "33 17" "960 540"; do
  cmd window.size $size
  cmd frames.wait 1
done
cmd frames.wait 20
# Minimized for a while, which draws nothing, and back, twice.
for i in 1 2; do
  cmd window.minimize
  cmd frames.wait 15
  cmd window.restore
  cmd frames.wait 20
done
# Moved about the desktop and to each monitor there is, where there is a window to move.
if [ "$mode" != "--offscreen" ]; then
  cmd window.position 40 40
  for m in 0 1 2 3; do
    ./e3d command window.monitor $m --quiet --timeout 60 >/dev/null 2>&1 || break
    cmd frames.wait 10
  done
fi
cmd window.size 800 450
cmd frames.wait 30
./e3d shot "$out" "${session[@]}" --quiet --timeout 120
./e3d stop "${session[@]}" --quiet
trap - EXIT

log="build/sessions/$(basename "$program").log"
# The capture's size, from its PNG header.
size=$(python3 -c "import struct, sys; d = open(sys.argv[1], 'rb').read(24); print('%dx%d' % struct.unpack('>II', d[16:24]))" "$out")
errors=$(grep -F '[Vulkan.Validation]' "$log" | grep -cF 'Validation Error' || true)
echo "$(basename "$program"): captured at $size after the storm, $errors validation errors"
[ "$errors" = 0 ] && [ "$size" = "800x450" ]
