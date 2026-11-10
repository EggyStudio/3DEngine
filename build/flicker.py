#!/usr/bin/env python3
"""Reads a recording picture to picture for the pixels that move, the way a flicker is measured.

    build/flicker.py <recording> [--levels 12,25] [--each] [--map <out.png>] [--from <n>] [--to <n>]
                     [--turn <degrees> --eye <x,y,z> --target <x,y,z> [--fov <degrees>] [--mask <png>]]
                     [--against <recording> [--against-from <n>]] [--blur <pixels>]
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
picture's camera, as a column's, carried along the turn with it. With --turn, --map shows each
pair's moved pixels carried back into the first picture's camera, each pixel's share of the pairs
that showed it, so a place that flickers stays one place on the map rather than a band the turn
drew it along.

--against names a second recording of the same scene and pictures, read pair by pair beside the
first from --against-from, by default --from, and the shares become those of the pixels that move by
a level in the first and not in the second. Two runs of one turn, one with a part of the frame left
out, so read that part's flicker apart from what both share, as a game of pixel textures does, whose
own texels' edges move by more than a few levels in any turn.

--blur blurs both pictures of each pair by a Gaussian of that radius in pixels before they are
compared, about a screen probe's tile, so the edges a fraction of a pixel's turn moves read still and
the blocks a probe's light changes over stay.

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


def turned_target(eye, target, degrees):
    """The point the camera at eye looking at target looks at once turned about eye by degrees, to
    the right above 0, about the world's up, as sponza.turn turns it."""
    import math
    angle = -math.radians(degrees)
    x, y, z = (t - e for t, e in zip(target, eye))
    return [eye[0] + math.cos(angle) * x + math.sin(angle) * z, eye[1] + y, eye[2] - math.sin(angle) * x + math.cos(angle) * z]


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
    parser.add_argument("--against", help="a second recording, its pixels that move by a level left out of the first's")
    parser.add_argument("--against-from", dest="against_first", type=int, help="the second recording's first picture read, from 1, by default --from")
    parser.add_argument("--blur", type=float, default=0, help="the radius in pixels of the Gaussian blur both pictures of a pair take first")
    args = parser.parse_args()
    if args.turn is not None and not (args.eye and args.target):
        sys.exit("--turn needs the first picture's camera, --eye and --target")
    try:
        from PIL import Image, ImageChops, ImageFilter, ImageMath
    except ImportError:
        sys.exit("build/flicker.py reads the pictures with Pillow, which is not installed here.")
    levels = [int(level) for level in args.levels.split(",")]

    def read(path, first):
        for picture in pictures(path, first, args.last and args.last - args.first + first):
            yield picture.filter(ImageFilter.GaussianBlur(args.blur)) if args.blur > 0 else picture

    def changed(picture, before, step):
        if step is not None:
            before = before.transform(picture.size, Image.PERSPECTIVE, step, Image.BILINEAR)
        r, g, b = ImageChops.difference(picture, before).split()
        return ImageChops.lighter(ImageChops.lighter(r, g), b)

    start, moved, shown = None, None, None
    befores = None
    shares = [[] for _ in levels]
    skipped = 0
    eye = [float(v) for v in args.eye.split(",")] if args.eye else None
    target = [float(v) for v in args.target.split(",")] if args.target else None
    mask = Image.open(args.mask).convert("L") if args.mask else None
    streams = [read(args.recording, args.first)]
    if args.against:
        streams.append(read(args.against, args.against_first or args.first))
    for frames in zip(*streams):
        if befores is None:
            befores, start = frames, frames[0]
            moved = Image.new("I", start.size, 0)
            shown = Image.new("I", start.size, 0)
            continue
        picture = frames[0]
        if any(frame.size != start.size for frame in frames):
            sys.exit(f"picture {args.first + len(shares[0]) + skipped + 1} is {picture.size}, where the first is {start.size}")
        turns = len(shares[0]) + skipped + 1
        counted, step = None, None
        if args.turn is not None:
            # The picture before carried into this one, and the pixels it showed, a white picture
            # carried alike and kept where whole, with the mask carried from the first.
            step = turned(eye, target, args.fov, picture.size, args.turn, 1)
            counted = Image.new("L", picture.size, 255).transform(picture.size, Image.PERSPECTIVE, step, Image.BILINEAR)
            counted = counted.point(lambda v: 255 if v == 255 else 0)
            if mask is not None:
                carried = turned(eye, target, args.fov, picture.size, args.turn, turns)
                region = mask.transform(picture.size, Image.PERSPECTIVE, carried, Image.NEAREST)
                counted = ImageChops.darker(counted, region.point(lambda v: 255 if v >= 128 else 0))
        changes = [changed(frame, before, step) for frame, before in zip(frames, befores)]
        befores = frames
        pixels = counted.histogram()[255] if counted is not None else picture.size[0] * picture.size[1]
        if pixels == 0:
            # A pair with no pixel counted, as once a turn has carried the mask out of the picture.
            skipped += 1
            continue
        flags = []
        for k, level in enumerate(levels):
            # A pixel's flag where it moved by the level, in the second recording too where there
            # is one, and kept in the first's where it did not move in the second's.
            over = [change.point(lambda v: 255 if v >= level else 0) for change in changes]
            flag = ImageChops.subtract(over[0], over[1]) if len(over) > 1 else over[0]
            if counted is not None:
                flag = ImageChops.darker(flag, counted)
            shares[k].append(flag.histogram()[255] / pixels)
            flags.append(flag)
        whole = counted if counted is not None else Image.new("L", picture.size, 255)
        if args.turn is not None:
            # The pair's flags and the pixels it counted carried back into the first picture's
            # camera, by the turn from it to this picture's camera undone.
            back = turned(eye, turned_target(eye, target, args.turn * turns), args.fov, picture.size, args.turn, -turns)
            flags[0] = flags[0].transform(picture.size, Image.PERSPECTIVE, back, Image.NEAREST)
            whole = whole.transform(picture.size, Image.PERSPECTIVE, back, Image.NEAREST)
        # Each pixel's count of the pairs it moved in by the first level and of those that showed
        # it, added up in 32 bits.
        moved = ImageMath.lambda_eval(lambda a: a["moved"] + a["flags"], moved=moved, flags=flags[0].point(lambda v: 1 if v else 0).convert("I"))
        shown = ImageMath.lambda_eval(lambda a: a["shown"] + a["whole"], shown=shown, whole=whole.point(lambda v: 1 if v else 0).convert("I"))
        if args.each:
            line = ", ".join(f"{s[-1] * 100:.3f}% by {level}" for s, level in zip(shares, levels))
            print(f"{args.first + turns - 1} to {args.first + turns}: {line}")

    pairs = len(shares[0])
    if pairs == 0:
        sys.exit(f"{args.recording} holds fewer than two pictures to compare")
    print(f"{pairs} pairs of {start.size[0]} by {start.size[1]} pictures" + (f", {skipped} more with no pixel counted" if skipped else ""))
    for s, level in zip(shares, levels):
        print(f"by {level} levels or more: {sum(s) / pairs * 100:.3f}% of the pixels a pair on the mean, {max(s) * 100:.3f}% at most")

    if args.map:
        heat = ImageMath.lambda_eval(lambda a: a["moved"] * 255 / a["max"](a["shown"], 1), moved=moved, shown=shown).convert("L")
        dark = start.point(lambda v: v // 5)
        orange = Image.merge("RGB", (heat, heat.point(lambda v: v * 45 // 100), Image.new("L", heat.size, 0)))
        Image.composite(orange, dark, heat.point(lambda v: 255 if v else 0)).save(args.map)
        print(f"where the pixels moved by {levels[0]}: {args.map}")


if __name__ == "__main__":
    main()
