#!/usr/bin/env python3
"""Writes 3DEngine.Tests/Api/bitmaps.ttf and layers.ttf, two color fonts the font tests load, so
their colors are known. 1000 units to the em, ascender 800 and descender -200, each glyph 1000 wide.

bitmaps.ttf holds its glyphs as PNG images (CBDT and CBLC) and no outlines, as color emoji fonts
built like Noto Color Emoji do, at one size of 8 pixels to the em: U+1F600 is red over blue, its top
half red, standing on the baseline, and U+2600 green, a character of the first plane in color.

layers.ttf holds outlines and colors them by layers (COLR version 0 with CPAL), as Segoe UI Emoji
does: U+1F600's outline is a square, drawn in color as a red left half and a blue right half, each
an outline of its own, and 'A' is a triangle with no color, which the atlas builder bakes."""
import os, struct, zlib


def png(width, height, rows):
    """A PNG of RGBA rows, each a list of (r, g, b, a)."""
    raw = b"".join(b"\0" + bytes(c for pixel in row for c in pixel) for row in rows)
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b""))


def simple(contours):
    """A simple glyph of contours, each a list of (x, y) on the curve."""
    points = [p for c in contours for p in c]
    xs, ys = [p[0] for p in points], [p[1] for p in points]
    out = struct.pack(">hhhhh", len(contours), min(xs), min(ys), max(xs), max(ys))
    end = -1
    for c in contours:
        end += len(c)
        out += struct.pack(">H", end)
    out += struct.pack(">H", 0) + bytes(1 for _ in points)
    x = y = 0
    for px, _ in points: out += struct.pack(">h", px - x); x = px
    for _, py in points: out += struct.pack(">h", py - y); y = py
    while len(out) % 4: out += b"\0"
    return out


def font(path, glyph_count, codes, extra):
    """A font of the common tables for glyph_count glyphs mapped from codes, and the extra tables."""
    hmtx = b"".join(struct.pack(">Hh", 1000, 0) for _ in range(glyph_count))
    head = struct.pack(">IIIIHHqqhhhhHHhhh", 0x00010000, 0x00010000, 0, 0x5F0F3CF5, 0, 1000, 0, 0,
                       0, -200, 1000, 800, 0, 8, 2, 1, 0)
    hhea = struct.pack(">IhhhHhhhhhhhhhhhH", 0x00010000, 800, -200, 0, 1000, 0, 0, 1000, 1, 0, 0, 0, 0, 0, 0, 0, glyph_count)
    maxp = struct.pack(">IH", 0x00005000, glyph_count)
    f12 = b"".join(struct.pack(">III", c, c, g) for c, g in sorted(codes))
    f12 = struct.pack(">HHIII", 12, 0, 16 + len(f12), 0, len(codes)) + f12
    cmap = struct.pack(">HH", 0, 1) + struct.pack(">HHI", 3, 10, 12) + f12
    tables = {b"cmap": cmap, b"head": head, b"hhea": hhea, b"hmtx": hmtx, b"maxp": maxp, **extra}
    n = len(tables)
    out = struct.pack(">IHHHH", 0x00010000, n, 64, 2, n * 16 - 64)
    offset = 12 + 16 * n
    body = b""
    for tag in sorted(tables):
        data = tables[tag]
        out += struct.pack(">4sIII", tag, 0, offset + len(body), len(data))
        body += data + b"\0" * (-len(data) % 4)
    with open(path, "wb") as f: f.write(out + body)
    print(path, len(out + body), "bytes")


here = os.path.join(os.path.dirname(__file__), "..", "3DEngine.Tests", "Api")

# bitmaps.ttf: glyph 1 U+1F600, glyph 2 U+2600, each 8 by 8 at 8 pixels to the em, standing on the
# baseline (bearing 0 across and 8 up), advancing 8.
red, blue, green = (255, 0, 0, 255), (0, 0, 255, 255), (0, 255, 0, 255)
images = [png(8, 8, [[red] * 8] * 4 + [[blue] * 8] * 4), png(8, 8, [[green] * 8] * 8)]
cbdt = struct.pack(">HH", 3, 0)
offsets = []
for image in images:
    offsets.append(len(cbdt))
    cbdt += struct.pack(">BBbbB", 8, 8, 0, 8, 8) + struct.pack(">I", len(image)) + image
offsets.append(len(cbdt))
index = struct.pack(">HHI", 1, 17, 0) + b"".join(struct.pack(">I", o) for o in offsets)
array = struct.pack(">HHI", 1, 2, 8)
metrics = struct.pack(">bbBbbbbbbbbb", 8, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0)
size = struct.pack(">IIII", 8 + 48, len(array) + len(index), 1, 0) + metrics + metrics + struct.pack(">HHBBBb", 1, 2, 8, 8, 32, 1)
cblc = struct.pack(">HHI", 3, 0, 1) + size + array + index
font(os.path.join(here, "bitmaps.ttf"), 3, [(0x1F600, 1), (0x2600, 2)], {b"CBDT": cbdt, b"CBLC": cblc})

# layers.ttf: glyph 1 'A', glyph 2 U+1F600's square, glyphs 3 and 4 its left and right halves.
glyphs = [
    b"",
    simple([[(100, 0), (500, 700), (900, 0)]]),
    simple([[(100, 0), (100, 800), (900, 800), (900, 0)]]),
    simple([[(100, 0), (100, 800), (500, 800), (500, 0)]]),
    simple([[(500, 0), (500, 800), (900, 800), (900, 0)]]),
]
glyf = b"".join(glyphs)
loca, at = b"", 0
for g in glyphs: loca += struct.pack(">I", at); at += len(g)
loca += struct.pack(">I", at)
colr = struct.pack(">HHIIH", 0, 1, 14, 20, 2) + struct.pack(">HHH", 2, 0, 2) + struct.pack(">HHHH", 3, 0, 4, 1)
cpal = struct.pack(">HHHHIH", 0, 2, 1, 2, 14, 0) + bytes([0, 0, 255, 255]) + bytes([255, 0, 0, 255])
font(os.path.join(here, "layers.ttf"), 5, [(0x41, 1), (0x1F600, 2)], {b"glyf": glyf, b"loca": loca, b"COLR": colr, b"CPAL": cpal})
