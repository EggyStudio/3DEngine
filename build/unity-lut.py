#!/usr/bin/env python3
"""Writes a lookup table of Unity's HDRP as a KTX2 file, which SetTonemapLookup reads.

    build/unity-lut.py <strip.exr> <table.ktx2>

The strip is an image of N squares of N by N side by side, each a slice of the table's blue from
the left, with red across and green up, as HDRP reads a table for its external tonemapping and its
color lookup. ImageMagick's magick reads it as half floats, and the table is written as a cube of
half-float RGBA, red fastest, then green, then blue, its one level compressed by zlib, KTX2's
scheme 3, which the engine reads as it reads Bevy's tables (build/bevy-luts.py).

The Sponza example's resources/agx_powerful.ktx2 is written from AgX - Powerful.exr, the AgX
Powerful look from the Tonemaps package of Unity Essentials, which the Sponza scene's own HDRP
profile tonemaps by.
"""
import struct
import subprocess
import sys
import zlib

IDENTIFIER = b"\xabKTX 20\xbb\r\n\x1a\n"
R16G16B16A16_SFLOAT, ZLIB = 97, 3


def descriptor():
    """The data format descriptor of half-float RGBA in linear light, as Bevy's AgX table has it."""
    # Each channel a signed float of 16 bits from its offset, its sample bounds -1 and 1.
    samples = b"".join(struct.pack("<HBBIff", offset, 15, 0xC0 | channel, 0, -1.0, 1.0)
                       for offset, channel in ((0, 0), (16, 1), (32, 2), (48, 15)))
    # RGBSDA in BT.709's primaries with a linear transfer, eight bytes a texel.
    block = struct.pack("<IHHBBBBI8B", 0, 2, 24 + len(samples), 1, 1, 1, 0, 0, 8, 0, 0, 0, 0, 0, 0, 0) + samples
    return struct.pack("<I", 4 + len(block)) + block


def main():
    if len(sys.argv) != 3:
        sys.exit(__doc__.split("\n\n")[1])
    source, target = sys.argv[1], sys.argv[2]
    halves = subprocess.run(["magick", source, "-define", "quantum:format=floating-point", "-depth", "16", "RGBA:-"],
                            check=True, capture_output=True).stdout
    size = round((len(halves) // 8) ** (1 / 3))
    if size ** 3 * 8 != len(halves):
        sys.exit(f"{source} holds {len(halves) // 8} texels, not a cube's.")

    # The strip's rows run from its top, where green is greatest.
    width = size * size
    texels = bytearray()
    for blue in range(size):
        for green in range(size):
            row = (size - 1 - green) * width + blue * size
            texels += halves[row * 8:(row + size) * 8]
    packed = zlib.compress(bytes(texels), 9)

    # The header, the level index and the descriptor, then the level.
    dfd = descriptor()
    level_at = 80 + 24 + len(dfd)
    out = bytearray(IDENTIFIER)
    out += struct.pack("<9I", R16G16B16A16_SFLOAT, 2, size, size, size, 0, 1, 1, ZLIB)
    out += struct.pack("<4I", 80 + 24, len(dfd), 0, 0)
    out += struct.pack("<2Q", 0, 0)
    out += struct.pack("<3Q", level_at, len(packed), len(texels))
    out += dfd + packed
    with open(target, "wb") as f:
        f.write(out)
    print(f"{target}: a table {size} a side, {len(texels)} bytes of texels, {len(out)} bytes written")


if __name__ == "__main__":
    main()
