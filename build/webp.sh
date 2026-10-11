#!/usr/bin/env bash
# Encodes a capture as WebP for the README's gallery: a lit 3D scene at quality 85, and flat color,
# 2D shapes or text losslessly, which keeps their edges exact and is the smaller of the two for
# them. ImageMagick encodes it where it was built with WebP, and cwebp where it was not. Given a
# size, the picture is first scaled to fit inside it and padded with black to it, by ImageMagick,
# so every picture of the gallery is one size and the README's tags, which give each the same
# width and height, stretch none.
#
#   build/webp.sh <png> <webp> lossy|lossless [<width>x<height>]
set -euo pipefail

in="$1"
out="$2"
kind="$3"
size="${4:-}"

if [ -n "$size" ]; then
  if command -v magick >/dev/null; then fit=(magick); elif command -v convert >/dev/null; then fit=(convert); else
    echo "webp.sh: ImageMagick fits a picture to a size, and is not installed" >&2
    exit 1
  fi
  fitted="$(mktemp --suffix=.png)"
  trap 'rm -f "$fitted"' EXIT
  "${fit[@]}" "$in" -resize "$size" -background black -gravity center -extent "$size" "$fitted"
  in="$fitted"
fi

if command -v magick >/dev/null && magick -list format 2>/dev/null | grep -q '^ *WEBP'; then
  if [ "$kind" = lossy ]; then magick "$in" -quality 85 "$out"; else magick "$in" -define webp:lossless=true "$out"; fi
elif command -v cwebp >/dev/null; then
  if [ "$kind" = lossy ]; then cwebp -quiet -q 85 "$in" -o "$out"; else cwebp -quiet -lossless "$in" -o "$out"; fi
elif command -v convert >/dev/null && convert -list format 2>/dev/null | grep -q '^ *WEBP'; then
  if [ "$kind" = lossy ]; then convert "$in" -quality 85 "$out"; else convert "$in" -define webp:lossless=true "$out"; fi
else
  echo "webp.sh: neither ImageMagick with WebP nor cwebp is installed (apt install webp)" >&2
  exit 1
fi
