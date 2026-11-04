#!/usr/bin/env bash
# Makes a recording the screenshot key held into one animated WebP beside it, its pictures played at
# the rate they were taken, fifteen a second unless given, with a line in the folder's captions.md
# where it has one. ImageMagick encodes it, as build/webp.sh's pictures are, since the engine
# carries no WebP encoder.
#
#   build/dump-webp.sh <recording folder> [pictures a second]
set -euo pipefail

folder="${1%/}"
rate="${2:-15}"
[ -d "$folder" ] || { echo "no folder $folder" >&2; exit 1; }
out="$folder.webp"
count=$(find "$folder" -maxdepth 1 -name '*.png' | wc -l)
[ "$count" -gt 0 ] || { echo "no pictures in $folder" >&2; exit 1; }

magick -delay "1x$rate" -loop 0 "$folder"/*.png -quality 85 "$out"

captions="$(dirname "$folder")/captions.md"
if [ -f "$captions" ]; then
  printf '%s | The recording %s played as one animation, %s pictures at %s a second.\n' "$(basename "$out")" "$(basename "$folder")" "$count" "$rate" >> "$captions"
fi
echo "$out"
