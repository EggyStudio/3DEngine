#!/usr/bin/env python3
"""Reads a recording picture to picture for the pixels that move, the way a flicker is measured.

    build/flicker.py <recording> [--levels 12,25] [--each] [--map <out.png>] [--from <n>] [--to <n>]
                     [--turn <degrees> --eye <x,y,z> --target <x,y,z> [--fov <degrees>] [--mask <png>]]
    build/flicker.py screenshots/11-09-29-scenes_sponza-recording --map where.png
    build/flicker.py turn.png --from 11 --turn 0.588 --eye 9.5,1.7,0 --target -8,5,0 --levels 4,12

The recording is a folder of pictures numbered from 0001.png, as F2 held in a program writes one
into its screenshots folder, or an animated PNG, as `./e3d record <path> 120f --every 1f` writes one
with a picture each frame. A pixel moves by a level when any of its three channels, in the picture's
8 bits, changes by that many or more from one picture to the next, and the shares are of the
picture's pixels, so a still scene reads 0 and a recording read twice reads the same.

It prints, for each level, the mean share over every pair of pictures and the largest, and with
--each the shares of each pair, by which an hour's change is seen settling. --map writes where the
pixels moved, each pixel's share of the pairs it moved in by the first level in orange, over the
first picture darkened to a fifth, so the edges that flicker are found by eye.

With --turn, the recording is of a camera turning about its point by that many degrees a picture, to
the right where above 0, as `./e3d command sponza.turn` turns scenes_sponza's, and --eye, --target
and --fov, the vertical field of view, give the camera of the first picture read. Each picture is
carried into the next by the turn, which moves a pixel the same whatever its depth, and the pixels
whose place the picture before did not show, the strip the turn brings in at the edge, are left out
of the shares, so what a turn moves is the light the camera sees and not the picture sliding by.
--mask names a picture of the first picture's size, white where the pixels are counted at the first
picture's camera, as a column's, carried along the turn with it.

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


def turned(eye, target, fov, size, degrees, turns):
    """The coefficients Pillow's perspective transform takes to carry a picture of the camera at
    eye looking at target into the camera turned about eye by degrees, turns times, about the
    world's up, each pixel of the turned picture taken from where its way fell in the first."""
    import math
    width, height = size
    focal = height / 2 / math.tan(math.radians(fov) / 2)
    k = [[focal, 0, width / 2], [0, focal, height / 2], [0, 0, 1]]
    inverse = [[1 / focal, 0, -width / 2 / focal], [0, 1 / focal, -height / 2 / focal], [0, 0, 1]]
    forward = [t - e for t, e in zip(target, eye)]
    length = math.sqrt(sum(v * v for v in forward))
    forward = [v / length for v in forward]
    # The way to the right, ahead crossed with the world's up.
    right = [-forward[2], 0.0, forward[0]]
    length = math.sqrt(sum(v * v for v in right))
    right = [v / length for v in right]
    up = [right[1] * forward[2] - right[2] * forward[1], right[2] * forward[0] - right[0] * forward[2],
          right[0] * forward[1] - right[1] * forward[0]]
    # The camera's axes in the world as columns, x to the right, y down the picture and z ahead.
    camera = [[right[i], -up[i], forward[i]] for i in range(3)]
    angle = -math.radians(degrees) * turns
    turn = [[math.cos(angle), 0, math.sin(angle)], [0, 1, 0], [-math.sin(angle), 0, math.cos(angle)]]

    def times(a, b):
        return [[sum(a[i][m] * b[m][j] for m in range(3)) for j in range(3)] for i in range(3)]

    transposed = [[camera[j][i] for j in range(3)] for i in range(3)]
    carried = times(times(times(times(k, transposed), turn), camera), inverse)
    scale = carried[2][2]
    return [carried[0][0] / scale, carried[0][1] / scale, carried[0][2] / scale,
            carried[1][0] / scale, carried[1][1] / scale, carried[1][2] / scale,
            carried[2][0] / scale, carried[2][1] / scale]


def main():
    parser = argparse.ArgumentParser(description="Reads a recording picture to picture for the pixels that move.")
    parser.add_argument("recording")
    parser.add_argument("--levels", default="12,25", help="the levels a channel's change is counted from, comma separated")
    parser.add_argument("--each", action="store_true", help="prints each pair's shares")
    parser.add_argument("--map", help="writes where the pixels moved by the first level to this PNG")
    parser.add_argument("--from", dest="first", type=int, default=1, help="the first picture read, from 1")
    parser.add_argument("--to", dest="last", type=int, default=0, help="the last picture read")
    parser.add_argument("--turn", type=float, help="the degrees the camera turns about its point a picture, to the right above 0")
    parser.add_argument("--eye", help="the first picture's camera's point, x,y,z, with --turn")
    parser.add_argument("--target", help="the point the first picture's camera looks at, x,y,z, with --turn")
    parser.add_argument("--fov", type=float, default=60, help="the camera's vertical field of view in degrees, with --turn")
    parser.add_argument("--mask", help="a picture white where pixels are counted at the first picture's camera, carried along --turn")
    args = parser.parse_args()
    if args.turn is not None and not (args.eye and args.target):
        sys.exit("--turn needs the first picture's camera, --eye and --target")
    try:
        from PIL import Image, ImageChops, ImageMath
    except ImportError:
        sys.exit("build/flicker.py reads the pictures with Pillow, which is not installed here.")
    levels = [int(level) for level in args.levels.split(",")]

    before, start, moved = None, None, None
    shares = [[] for _ in levels]
    skipped = 0
    eye = [float(v) for v in args.eye.split(",")] if args.eye else None
    target = [float(v) for v in args.target.split(",")] if args.target else None
    mask = Image.open(args.mask).convert("L") if args.mask else None
    for picture in pictures(args.recording, args.first, args.last):
        if before is None:
            before, start = picture, picture
            moved = Image.new("I", picture.size, 0)
            continue
        if picture.size != before.size:
            sys.exit(f"picture {args.first + len(shares[0]) + 1} is {picture.size}, where the first is {before.size}")
        counted = None
        if args.turn is not None:
            # The picture before carried into this one, and the pixels it showed, a white picture
            # carried alike and kept where whole, with the mask carried from the first.
            step = turned(eye, target, args.fov, picture.size, args.turn, 1)
            before = before.transform(picture.size, Image.PERSPECTIVE, step, Image.BILINEAR)
            counted = Image.new("L", picture.size, 255).transform(picture.size, Image.PERSPECTIVE, step, Image.BILINEAR)
            counted = counted.point(lambda v: 255 if v == 255 else 0)
            if mask is not None:
                carried = turned(eye, target, args.fov, picture.size, args.turn, len(shares[0]) + skipped + 1)
                region = mask.transform(picture.size, Image.PERSPECTIVE, carried, Image.NEAREST)
                counted = ImageChops.darker(counted, region.point(lambda v: 255 if v >= 128 else 0))
        r, g, b = ImageChops.difference(picture, before).split()
        change = ImageChops.lighter(ImageChops.lighter(r, g), b)
        if counted is not None:
            change = ImageChops.darker(change, counted)
        histogram = change.histogram()
        pixels = counted.histogram()[255] if counted is not None else picture.size[0] * picture.size[1]
        if pixels == 0:
            # A pair with no pixel counted, as once a turn has carried the mask out of the picture.
            skipped += 1
            before = picture
            continue
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
    print(f"{pairs} pairs of {start.size[0]} by {start.size[1]} pictures" + (f", {skipped} more with no pixel counted" if skipped else ""))
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
