#!/usr/bin/env bash
# Encodes a capture as WebP for the README's gallery: a lit 3D scene at quality 85, and flat color,
# 2D shapes or text losslessly, which keeps their edges exact and is the smaller of the two for
# them. ImageMagick encodes it where it was built with WebP, and cwebp where it was not. Given a
# size, the picture is scaled until it fills that size and cut to it, from the middle or from the
# side a gravity names, so every picture of the gallery is one size and fills its box, where fitted
# inside it and padded with black the pictures of other shapes read as smaller beside the rest.
# cwebp fits it where it is installed, cutting the box of the size's shape from the source's own
# pixels and scaling that, so a runner needs the encoder alone, as macOS's has no ImageMagick;
# ImageMagick fits it where cwebp is absent.
#
#   build/webp.sh <png> <webp> lossy|lossless [<width>x<height> [<gravity>]]
set -euo pipefail

in="$1"
out="$2"
kind="$3"
size="${4:-}"
gravity=$(printf '%s' "${5:-center}" | tr '[:upper:]' '[:lower:]')

if [ "$kind" = lossy ]; then quality=(-q 85); else quality=(-lossless); fi

if [ -n "$size" ] && command -v cwebp >/dev/null; then
  width="${size%x*}"
  height="${size#*x}"
  # The PNG's own width and height, from its header's first chunk, big-endian at bytes 16 and 20.
  set -- $(od -An -tu1 -j16 -N8 "$in")
  source_width=$(( ($1 << 24) | ($2 << 16) | ($3 << 8) | $4 ))
  source_height=$(( ($5 << 24) | ($6 << 16) | ($7 << 8) | $8 ))
  # The largest box of the size's shape within the source, its side rounded to the nearest pixel,
  # and where the gravity places it.
  if (( source_width * height > source_height * width )); then
    box_width=$(( (source_height * width + height / 2) / height ))
    box_height=$source_height
  else
    box_width=$source_width
    box_height=$(( (source_width * height + width / 2) / width ))
  fi
  case "$gravity" in
    *west) x=0 ;;
    *east) x=$(( source_width - box_width )) ;;
    *) x=$(( (source_width - box_width) / 2 )) ;;
  esac
  case "$gravity" in
    north*) y=0 ;;
    south*) y=$(( source_height - box_height )) ;;
    *) y=$(( (source_height - box_height) / 2 )) ;;
  esac
  cwebp -quiet "${quality[@]}" -crop "$x" "$y" "$box_width" "$box_height" -resize "$width" "$height" "$in" -o "$out"
  exit 0
fi

if [ -n "$size" ]; then
  if command -v magick >/dev/null; then fit=(magick); elif command -v convert >/dev/null; then fit=(convert); else
    echo "webp.sh: neither cwebp nor ImageMagick, which fit a picture to a size, is installed (apt install webp, brew install webp)" >&2
    exit 1
  fi
  folder="$(mktemp -d)"
  trap 'rm -rf "$folder"' EXIT
  "${fit[@]}" "$in" -resize "$size^" -gravity "$gravity" -extent "$size" "$folder/fitted.png"
  in="$folder/fitted.png"
fi

if command -v magick >/dev/null && magick -list format 2>/dev/null | grep -q '^ *WEBP'; then
  if [ "$kind" = lossy ]; then magick "$in" -quality 85 "$out"; else magick "$in" -define webp:lossless=true "$out"; fi
elif command -v cwebp >/dev/null; then
  cwebp -quiet "${quality[@]}" "$in" -o "$out"
elif command -v convert >/dev/null && convert -list format 2>/dev/null | grep -q '^ *WEBP'; then
  if [ "$kind" = lossy ]; then convert "$in" -quality 85 "$out"; else convert "$in" -define webp:lossless=true "$out"; fi
else
  echo "webp.sh: neither ImageMagick with WebP nor cwebp is installed (apt install webp)" >&2
  exit 1
fi
