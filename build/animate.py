#!/usr/bin/env python3
"""Makes captures on disk into one animation, for a page of the guide or a devlog.

    build/animate.py <out> <pictures...> --delay <ms> [--loop <n>] [--scale <s>]
    build/animate.py build/walk.gif build/captures/walk-*.png --delay 250

The pictures are PNG or WebP captures, such as those the pack run's capture job takes or `./e3d
shot` writes, shown in the order given, each for the delay, the animation playing as many times as
--loop says after the first, forever unless it says, and each picture made smaller by --scale,
from above zero up to one. The animation's kind is its extension, a GIF, an animated PNG or an
animated WebP.

Pillow writes it where Pillow is installed, which it is wherever the README's pictures are made.
Where it is not, `./e3d animate` writes a GIF or an animated PNG with the engine's own writers, the
ones a recording is written with (`record`), from PNG pictures, and a WebP, read or written, needs
Pillow. E3D_ANIMATE_WRITER=engine asks for the engine's writers where Pillow is installed too, as
the suite does to try both, and E3D_CLIENT names a built client to run in place of the checkout's
./e3d, which builds one first where there is none, as the suite names the one its own build made.
"""

import argparse
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
KINDS = {".gif": "GIF", ".png": "PNG", ".apng": "PNG", ".webp": "WEBP"}


def arguments():
    parser = argparse.ArgumentParser(description="Makes captures on disk into one animation.")
    parser.add_argument("out", help="the animation, a .gif, a .png or a .webp")
    parser.add_argument("pictures", nargs="+", help="the PNG or WebP pictures, in the order they are shown")
    parser.add_argument("--delay", type=int, required=True, help="how long each picture is shown, in milliseconds")
    parser.add_argument("--loop", type=int, default=0, help="how many times it plays after the first, 0 for forever")
    parser.add_argument("--scale", type=float, default=1.0, help="how much smaller each picture is made, up to 1")
    given = parser.parse_args()

    if os.path.splitext(given.out)[1].lower() not in KINDS:
        parser.error(f"{given.out} is none of the kinds this makes, a .gif, a .png or a .webp")
    if given.delay <= 0:
        parser.error("--delay is a time above zero, in milliseconds")
    if given.loop < 0:
        parser.error("--loop is a count of zero or more, zero for forever")
    if not 0 < given.scale <= 1:
        parser.error("--scale is above zero and up to one")
    for picture in given.pictures:
        if not os.path.isfile(picture):
            parser.error(f"{picture} is not a file")
    return given


def with_pillow(given, image):
    frames = []
    for picture in given.pictures:
        with image.open(picture) as opened:
            frame = opened.convert("RGBA")
        if given.scale < 1:
            size = (max(1, round(frame.width * given.scale)), max(1, round(frame.height * given.scale)))
            frame = frame.resize(size, image.Resampling.BOX)
        if frames and frame.size != frames[0].size:
            frame = frame.resize(frames[0].size, image.Resampling.BOX)
        frames.append(frame)

    kind = KINDS[os.path.splitext(given.out)[1].lower()]
    options = {"save_all": True, "append_images": frames[1:], "duration": given.delay, "loop": given.loop}
    if kind == "GIF":
        # One palette cut over every frame laid in a strip, the way the engine's own GIF is cut, so
        # a color that holds still does not change from frame to frame.
        width, height = frames[0].size
        strip = image.new("RGB", (width, height * len(frames)))
        for i, frame in enumerate(frames):
            strip.paste(frame.convert("RGB"), (0, height * i))
        palette = strip.quantize(colors=255, method=image.Quantize.MEDIANCUT)
        frames = [frame.convert("RGB").quantize(palette=palette, dither=image.Dither.FLOYDSTEINBERG) for frame in frames]
        options["append_images"] = frames[1:]
        options["disposal"] = 1
    elif kind == "WEBP":
        options["lossless"] = True
    frames[0].save(given.out, kind, **options)


def with_engine(given):
    kind = os.path.splitext(given.out)[1].lower()
    if kind == ".webp" or any(picture.lower().endswith(".webp") for picture in given.pictures):
        sys.exit("animate.py: a WebP, read or written, needs Pillow (python3 -m pip install Pillow)")

    if os.environ.get("E3D_CLIENT"):
        launcher = [os.environ["E3D_CLIENT"]]
    elif os.name == "nt":
        # The launcher is a bash script, which Git's bash runs on Windows.
        launcher = ["bash", os.path.join(ROOT, "e3d")]
    else:
        launcher = [os.path.join(ROOT, "e3d")]
    command = [*launcher, "animate", given.out, *given.pictures, "--delay", str(given.delay), "--loop", str(given.loop), "--scale", str(given.scale)]
    sys.exit(subprocess.run(command, check=False).returncode)


def main():
    given = arguments()
    if os.environ.get("E3D_ANIMATE_WRITER") != "engine":
        try:
            from PIL import Image
        except ImportError:
            Image = None
        if Image is not None:
            with_pillow(given, Image)
            print(f"wrote {len(given.pictures)} frames to {given.out}")
            return
    with_engine(given)


if __name__ == "__main__":
    main()
