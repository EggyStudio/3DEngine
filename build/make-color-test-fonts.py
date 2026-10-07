#!/usr/bin/env python3
"""Writes 3DEngine.Tests/Api/bitmaps.ttf and layers.ttf, two color fonts the font tests load, so
their colors are known. 1000 units to the em, ascender 800 and descender -200, each glyph 1000 wide.

bitmaps.ttf holds its glyphs as PNG images (CBDT and CBLC) and no outlines, as color emoji fonts
built like Noto Color Emoji do, at one size of 8 pixels to the em: U+1F600 is red over blue, its top
half red, standing on the baseline, and U+2600 green, a character of the first plane in color. Its
GSUB table joins U+1F600, the zero width joiner and U+2600 into a yellow glyph, as Noto Color Emoji
joins a family, and turns U+2600 before U+FE0F into the same glyph by a chained context, as Segoe UI
Emoji chooses its glyphs. The joiner and U+FE0F have no image and no width.

layers.ttf holds outlines and colors them by layers (COLR version 0 with CPAL), as Segoe UI Emoji
does: U+1F600's outline is a square, drawn in color as a red left half and a blue right half, each
an outline of its own, and 'A' is a triangle with no color, which the atlas builder bakes.

sbix.ttf holds U+1F600 as Apple's bitmaps (sbix), as Apple Color Emoji does, the same red over blue
image at 8 pixels to the em standing on the baseline, and U+2600 as a duplicate of it.

cff.otf holds U+1F600's outline in CFF, as an OpenType font of PostScript outlines does, the same
square as layers.ttf's, drawn by a global subroutine after a move that carries the glyph's width.

layers.ttc is layers.ttf as the one font of a collection, as Noto Sans CJK and Apple Color Emoji
are shipped, its tables found from the file's start.

paints.ttf colors U+1F600 by paints (COLR version 1), as Noto Color Emoji does: two layers in a clip
box from 100 to 900 across and 0 to 800 up, a square filled with a linear gradient from red at its
left to blue at its right, and over it a small square, from 400 to 600 across and 300 to 500 up,
filled green and moved 200 to the right by a translation.

rtl.ttf holds outlines for text read right to left, each glyph a shape its place can be told by:
alef (U+05D0), bet (U+05D1) and gimel (U+05D2) are bars from 100 to 900 across, 800, 400 and 200
high, qamats (U+05B8) a mark of no width, a bar from -700 to -300 across under the letter before
it, '1' a narrow bar and '2' two, '(' a bracket open to the right and ')' one open to the left, 'a'
a triangle and the space 500 wide."""
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


def font(path, glyph_count, codes, extra, advances=None):
    """A font of the common tables for glyph_count glyphs mapped from codes, and the extra tables,
    each glyph 1000 wide unless advances gives its width."""
    hmtx = b"".join(struct.pack(">Hh", (advances or {}).get(g, 1000), 0) for g in range(glyph_count))
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

def gsub(lookups, ccmp):
    """A GSUB table of the default script whose ccmp feature applies the lookups numbered in ccmp,
    each lookup a type and one subtable."""
    language = struct.pack(">HHHH", 0, 0xFFFF, 1, 0)
    script = struct.pack(">HH", 4, 0) + language
    scripts = struct.pack(">H4sH", 1, b"DFLT", 8) + script
    feature = struct.pack(">HH", 0, len(ccmp)) + b"".join(struct.pack(">H", l) for l in ccmp)
    features = struct.pack(">H4sH", 1, b"ccmp", 8) + feature
    tables = [struct.pack(">HHHH", kind, 0, 1, 8) + sub for kind, sub in lookups]
    at, offsets = 2 + 2 * len(tables), []
    for t in tables: offsets.append(at); at += len(t)
    lookup_list = struct.pack(">H", len(tables)) + b"".join(struct.pack(">H", o) for o in offsets) + b"".join(tables)
    return (struct.pack(">IHHH", 0x00010000, 10, 10 + len(scripts), 10 + len(scripts) + len(features))
            + scripts + features + lookup_list)


def coverage(glyph):
    return struct.pack(">HHH", 1, 1, glyph)


# bitmaps.ttf: glyph 1 U+1F600, glyph 2 U+2600 and glyph 3 the glyph they join into, each 8 by 8 at
# 8 pixels to the em, standing on the baseline (bearing 0 across and 8 up), advancing 8, and glyphs
# 4 and 5 the joiner and U+FE0F, with no image.
red, blue, green, yellow = (255, 0, 0, 255), (0, 0, 255, 255), (0, 255, 0, 255), (255, 255, 0, 255)
images = [png(8, 8, [[red] * 8] * 4 + [[blue] * 8] * 4), png(8, 8, [[green] * 8] * 8), png(8, 8, [[yellow] * 8] * 8), b"", b""]
cbdt = struct.pack(">HH", 3, 0)
offsets = []
for image in images:
    offsets.append(len(cbdt))
    if image: cbdt += struct.pack(">BBbbB", 8, 8, 0, 8, 8) + struct.pack(">I", len(image)) + image
offsets.append(len(cbdt))
index = struct.pack(">HHI", 1, 17, 0) + b"".join(struct.pack(">I", o) for o in offsets)
array = struct.pack(">HHI", 1, 5, 8)
metrics = struct.pack(">bbBbbbbbbbbb", 8, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0)
size = struct.pack(">IIII", 8 + 48, len(array) + len(index), 1, 0) + metrics + metrics + struct.pack(">HHBBBb", 1, 5, 8, 8, 32, 1)
cblc = struct.pack(">HHI", 3, 0, 1) + size + array + index
# Lookup 0 joins glyphs 1, 4 and 2 into 3. Lookup 1 applies lookup 2, which turns 2 into 3, to a 2
# that 5 follows.
ligature = struct.pack(">HHHH", 1, 8, 1, 14) + coverage(1) + struct.pack(">HH", 1, 4) + struct.pack(">HHHH", 3, 3, 4, 2)
chained = struct.pack(">HHHHHHHHH", 3, 0, 1, 18, 1, 24, 1, 0, 2) + coverage(2) + coverage(5)
single = struct.pack(">HHHH", 2, 8, 1, 3) + coverage(2)
font(os.path.join(here, "bitmaps.ttf"), 6, [(0x1F600, 1), (0x2600, 2), (0x200D, 4), (0xFE0F, 5)],
     {b"CBDT": cbdt, b"CBLC": cblc, b"GSUB": gsub([(4, ligature), (6, chained), (1, single)], [0, 1])}, {4: 0, 5: 0})

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

# paints.ttf: glyph 1 U+1F600, which has no outline of its own, glyph 2 the large square and glyph 3
# the small one.
glyphs = [
    b"",
    b"",
    simple([[(100, 0), (100, 800), (900, 800), (900, 0)]]),
    simple([[(400, 300), (400, 500), (600, 500), (600, 300)]]),
]
glyf = b"".join(glyphs)
loca, at = b"", 0
for g in glyphs: loca += struct.pack(">I", at); at += len(g)
loca += struct.pack(">I", at)
# Glyph 1's paint, the two layers from the first.
base_list = struct.pack(">IHI", 1, 1, 10) + struct.pack(">BBI", 1, 2, 0)
# The first layer, glyph 2 filled with a gradient from palette color 0 at x 100 to color 1 at x 900.
def offset24(n):
    return struct.pack(">I", n)[1:]
color_line = struct.pack(">BH", 0, 2) + struct.pack(">hHh", 0, 0, 16384) + struct.pack(">hHh", 16384, 1, 16384)
linear = struct.pack(">B", 4) + offset24(16) + struct.pack(">hhhhhh", 100, 0, 900, 0, 100, 800) + color_line
first = struct.pack(">B", 10) + offset24(6) + struct.pack(">H", 2) + linear
# The second, glyph 3 filled with palette color 2 and moved 200 to the right.
solid = struct.pack(">BHh", 2, 2, 16384)
second = struct.pack(">B", 14) + offset24(8) + struct.pack(">hh", 200, 0) + struct.pack(">B", 10) + offset24(6) + struct.pack(">H", 3) + solid
layer_list = struct.pack(">III", 2, 12, 12 + len(first)) + first + second
clip_list = struct.pack(">BI", 1, 1) + struct.pack(">HH", 1, 1) + offset24(12) + struct.pack(">Bhhhh", 1, 100, 0, 900, 800)
header = 34
colr = (struct.pack(">HHIIHIIIII", 1, 0, 0, 0, 0, header, header + len(base_list), header + len(base_list) + len(layer_list), 0, 0)
        + base_list + layer_list + clip_list)
cpal = struct.pack(">HHHHIH", 0, 3, 1, 3, 14, 0) + bytes([0, 0, 255, 255]) + bytes([255, 0, 0, 255]) + bytes([0, 255, 0, 255])
font(os.path.join(here, "paints.ttf"), 4, [(0x1F600, 1)], {b"glyf": glyf, b"loca": loca, b"COLR": colr, b"CPAL": cpal})

# layers.ttc: a collection's header before layers.ttf, each table's offset moved past it.
with open(os.path.join(here, "layers.ttf"), "rb") as f: single = bytearray(f.read())
count = struct.unpack_from(">H", single, 4)[0]
for i in range(count):
    record = 12 + i * 16
    struct.pack_into(">I", single, record + 8, struct.unpack_from(">I", single, record + 8)[0] + 16)
collection = b"ttcf" + struct.pack(">III", 0x00010000, 1, 16) + bytes(single)
with open(os.path.join(here, "layers.ttc"), "wb") as f: f.write(collection)
print(os.path.join(here, "layers.ttc"), len(collection), "bytes")

# sbix.ttf: glyph 1 U+1F600 an image of 8 by 8 at 8 pixels to the em, its origin at its bottom left,
# and glyph 2 U+2600 marked as glyph 1's duplicate.
image = images[0]
records = [b"", struct.pack(">hh4s", 0, 0, b"png ") + image, struct.pack(">hh4sH", 0, 0, b"dupe", 1)]
offsets, at = [], 4 + 4 * (len(records) + 1)
for r in records: offsets.append(at); at += len(r)
offsets.append(at)
strike = struct.pack(">HH", 8, 72) + b"".join(struct.pack(">I", o) for o in offsets) + b"".join(records)
sbix = struct.pack(">HHII", 1, 1, 1, 12) + strike
font(os.path.join(here, "sbix.ttf"), 3, [(0x1F600, 1), (0x2600, 2)], {b"sbix": sbix})

# cff.otf: glyph 1 U+1F600, its charstring "600 100 0 rmoveto -107 callgsubr endchar", whose 600 is
# its width, and the global subroutine "800 800 -800 hlineto return", the square's sides.
def number(v):
    if -107 <= v <= 107: return bytes([v + 139])
    if 108 <= v <= 1131: v -= 108; return bytes([247 + v // 256, v % 256])
    if -1131 <= v <= -108: v = -v - 108; return bytes([251 + v // 256, v % 256])
    return bytes([28]) + struct.pack(">h", v)


def index(items):
    out = struct.pack(">H", len(items))
    if not items: return out
    out += bytes([1])
    at = 1
    for item in [b""] + items:
        at += len(item)
        out += bytes([at - len(item) if item == b"" else at])
    return out + b"".join(items)


glyph = number(600) + number(100) + number(0) + bytes([21]) + number(-107) + bytes([29, 14])
subr = number(800) + number(800) + number(-800) + bytes([6, 11])
names, strings, globals_ = index([b"Test"]), index([]), index([subr])
top_size = len(index([bytes(6)]))
char_strings_at = 4 + len(names) + top_size + len(strings) + len(globals_)
top = index([bytes([29]) + struct.pack(">i", char_strings_at) + bytes([17])])
cff = bytes([1, 0, 4, 1]) + names + top + strings + globals_ + index([bytes([14]), glyph])
font(os.path.join(here, "cff.otf"), 2, [(0x1F600, 1)], {b"CFF ": cff})

# rtl.ttf: glyphs 1 to 3 alef, bet and gimel, 4 qamats, 5 '1', 6 '2', 7 '(', 8 ')', 9 the space and
# 10 'a', a glyph the em wide each but the mark and the space.
glyphs = [
    b"",
    simple([[(100, 0), (100, 800), (900, 800), (900, 0)]]),
    simple([[(100, 0), (100, 400), (900, 400), (900, 0)]]),
    simple([[(100, 0), (100, 200), (900, 200), (900, 0)]]),
    simple([[(-700, -180), (-700, -80), (-300, -80), (-300, -180)]]),
    simple([[(400, 0), (400, 600), (600, 600), (600, 0)]]),
    simple([[(200, 0), (200, 600), (400, 600), (400, 0)], [(600, 0), (600, 600), (800, 600), (800, 0)]]),
    simple([[(200, -100), (200, 800), (700, 800), (700, 700), (300, 700), (300, 0), (700, 0), (700, -100)]]),
    simple([[(300, -100), (300, 0), (700, 0), (700, 700), (300, 700), (300, 800), (800, 800), (800, -100)]]),
    b"",
    simple([[(100, 0), (500, 600), (900, 0)]]),
]
glyf = b"".join(glyphs)
loca, at = b"", 0
for g in glyphs: loca += struct.pack(">I", at); at += len(g)
loca += struct.pack(">I", at)
font(os.path.join(here, "rtl.ttf"), 11,
     [(0x05D0, 1), (0x05D1, 2), (0x05D2, 3), (0x05B8, 4), (0x31, 5), (0x32, 6), (0x28, 7), (0x29, 8), (0x20, 9), (0x61, 10)],
     {b"glyf": glyf, b"loca": loca}, {4: 0, 9: 500})
