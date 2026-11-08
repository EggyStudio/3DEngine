#!/usr/bin/env python3
"""Reads a recording picture to picture for the pixels that move, the way a flicker is measured.

    build/flicker.py <recording> [--levels 12,25] [--each] [--map <out.png>] [--from <n>] [--to <n>]
    build/flicker.py screenshots/11-09-29-scenes_sponza-recording --map where.png

The recording is a folder of pictures numbered from 0001.png, as F2 held in a program writes one
into its screenshots folder, or an animated PNG, as `./e3d record <path> 120f --every 1f` writes one
with a picture each frame. A pixel moves by a level when any of its three channels, in the picture's
8 bits, changes by that many or more from one picture to the next, and the shares are of the
picture's pixels, so a still scene reads 0 and a recording read twice reads the same.

It prints, for each level, the mean share over every pair of pictures and the largest, and with
--each the shares of each pair, by which an hour's change is seen settling. --map writes where the
pixels moved, each pixel's share of the pairs it moved in by the first level in orange, over the
first picture darkened to a fifth, so the edges that flicker are found by eye.

Pillow reads the pictures, which it does wherever the README's pictures are made.
"""
import argparse
import os
import sys


def pictures(path, first, last):
    from PIL import Image, ImageSequence
    if os.path.isdir(path):
        names = sorted(n for n in os.listdir(path) if n.endswith(".png"))
        frames = (Image.open(os.path.join(path, n)) for n in names)
    else:
        frames = ImageSequence.Iterator(Image.open(path))
    for i, frame in enumerate(frames, start=1):
        if i < first:
            continue
        if last and i > last:
            return
        yield frame.convert("RGB")


def main():
    parser = argparse.ArgumentParser(description="Reads a recording picture to picture for the pixels that move.")
    parser.add_argument("recording")
    parser.add_argument("--levels", default="12,25", help="the levels a channel's change is counted from, comma separated")
    parser.add_argument("--each", action="store_true", help="prints each pair's shares")
    parser.add_argument("--map", help="writes where the pixels moved by the first level to this PNG")
    parser.add_argument("--from", dest="first", type=int, default=1, help="the first picture read, from 1")
    parser.add_argument("--to", dest="last", type=int, default=0, help="the last picture read")
    args = parser.parse_args()
    try:
        from PIL import Image, ImageChops, ImageMath
    except ImportError:
        sys.exit("build/flicker.py reads the pictures with Pillow, which is not installed here.")
    levels = [int(level) for level in args.levels.split(",")]

    before, start, moved = None, None, None
    shares = [[] for _ in levels]
    for picture in pictures(args.recording, args.first, args.last):
        if before is None:
            before, start = picture, picture
            moved = Image.new("I", picture.size, 0)
            continue
        if picture.size != before.size:
            sys.exit(f"picture {args.first + len(shares[0]) + 1} is {picture.size}, where the first is {before.size}")
        r, g, b = ImageChops.difference(picture, before).split()
        change = ImageChops.lighter(ImageChops.lighter(r, g), b)
        histogram = change.histogram()
        pixels = picture.size[0] * picture.size[1]
        for k, level in enumerate(levels):
            shares[k].append(sum(histogram[level:]) / pixels)
        # Each pixel's count of pairs it moved in by the first level, added up in 32 bits.
        flags = change.point(lambda v: 1 if v >= levels[0] else 0).convert("I")
        moved = ImageMath.lambda_eval(lambda a: a["moved"] + a["flags"], moved=moved, flags=flags)
        if args.each:
            line = ", ".join(f"{s[-1] * 100:.3f}% by {level}" for s, level in zip(shares, levels))
            print(f"{args.first + len(shares[0]) - 1} to {args.first + len(shares[0])}: {line}")
        before = picture

    pairs = len(shares[0])
    if pairs == 0:
        sys.exit(f"{args.recording} holds fewer than two pictures to compare")
    print(f"{pairs} pairs of {start.size[0]} by {start.size[1]} pictures")
    for s, level in zip(shares, levels):
        print(f"by {level} levels or more: {sum(s) / pairs * 100:.3f}% of the pixels a pair on the mean, {max(s) * 100:.3f}% at most")

    if args.map:
        scale = 255.0 / pairs
        heat = ImageMath.lambda_eval(lambda a: a["moved"] * 255 / pairs, moved=moved).convert("L")
        dark = start.point(lambda v: v // 5)
        orange = Image.merge("RGB", (heat, heat.point(lambda v: v * 45 // 100), Image.new("L", heat.size, 0)))
        Image.composite(orange, dark, heat.point(lambda v: 255 if v else 0)).save(args.map)
        print(f"where the pixels moved by {levels[0]}: {args.map}")


if __name__ == "__main__":
    main()
