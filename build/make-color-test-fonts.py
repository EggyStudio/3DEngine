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
a triangle and the space 500 wide.

arabic.ttf joins Arabic by its own substitutions, as a text font of Arabic does, under the arab
script of its GSUB table. beh (U+0628), alef (U+0627) and lam (U+0644) are mapped to their isolated
glyphs, a bar on the baseline the em wide with a tick of its own, and fatha (U+064E) to a mark of
no width, which the GDEF table classes as one. The features fina, medi and init give beh and lam
glyphs of their own in those positions, and alef a final one, each a bar its advance wide, init 600,
medi 500 and fina 700, and rlig joins lam's initial or medial glyph and alef's final one into the
isolated or final lam-alef, 900 wide, passing over a mark between them as the lookup's flag says.

arabic-marks.ttf is arabic.ttf with kasra (U+0650), a mark under the letter, and shadda (U+0651), a
mark over it, and a GPOS table under the arab script, as a text font of Arabic positions its
harakat. Its mark feature puts fatha, kasra and shadda on anchors of each letter, through an
extension, and on each letter of lam-alef, lam's above its right stem and alef's above its left,
and its mkmk feature puts fatha on shadda. Its kern feature moves alef before beh 200 to the
right, the pair by its glyphs, alef's final glyph before beh 100, the pair by their classes, and
lam's initial glyph 100 up before beh's final one, by a chained context.

arabic-forms.ttf has no substitutions, as an older font of Arabic has none, and maps the same three
letters and their presentation forms, beh's four, alef's two and lam's four, the isolated and final
lam-alef and the space, each a glyph of its own, so text in it is drawn by those forms."""
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


def single(pairs):
    """A single substitution of the second format, each (glyph, substitute), the glyphs ascending."""
    pairs = sorted(pairs)
    cov = struct.pack(">HH", 1, len(pairs)) + b"".join(struct.pack(">H", g) for g, _ in pairs)
    head = struct.pack(">HHH", 2, 6 + 2 * len(pairs), len(pairs)) + b"".join(struct.pack(">H", t) for _, t in pairs)
    return head + cov


def ligatures(first_to_rules):
    """A ligature substitution, each first glyph ascending with its rules, each (ligature, components after the first)."""
    firsts = sorted(first_to_rules)
    sets = []
    for g in firsts:
        rules = first_to_rules[g]
        bodies = [struct.pack(">HH", lig, len(rest) + 1) + b"".join(struct.pack(">H", c) for c in rest) for lig, rest in rules]
        at, offsets = 2 + 2 * len(bodies), []
        for body in bodies: offsets.append(at); at += len(body)
        sets.append(struct.pack(">H", len(bodies)) + b"".join(struct.pack(">H", o) for o in offsets) + b"".join(bodies))
    cov = struct.pack(">HH", 1, len(firsts)) + b"".join(struct.pack(">H", g) for g in firsts)
    at = 6 + 2 * len(sets)
    offsets = []
    for st in sets: offsets.append(at); at += len(st)
    return struct.pack(">HHH", 1, at, len(sets)) + b"".join(struct.pack(">H", o) for o in offsets) + b"".join(sets) + cov


def gsub_of(script, features, lookups):
    """A GSUB table of one script, whose default language system has the features, each (tag,
    lookups), and the lookups, each (type, flag, subtable), or a GPOS table, whose header and lists
    are a GSUB's."""
    language = struct.pack(">HHH", 0, 0xFFFF, len(features)) + b"".join(struct.pack(">H", i) for i in range(len(features)))
    script_table = struct.pack(">HH", 4, 0) + language
    scripts = struct.pack(">H4sH", 1, script, 8) + script_table
    records, bodies, at = b"", b"", 2 + 6 * len(features)
    for tag, indices in features:
        body = struct.pack(">HH", 0, len(indices)) + b"".join(struct.pack(">H", l) for l in indices)
        records += struct.pack(">4sH", tag, at)
        bodies += body
        at += len(body)
    feature_list = struct.pack(">H", len(features)) + records + bodies
    tables = [struct.pack(">HHHH", kind, flag, 1, 8) + sub for kind, flag, sub in lookups]
    at, offsets = 2 + 2 * len(tables), []
    for t in tables: offsets.append(at); at += len(t)
    lookup_list = struct.pack(">H", len(tables)) + b"".join(struct.pack(">H", o) for o in offsets) + b"".join(tables)
    return (struct.pack(">IHHH", 0x00010000, 10, 10 + len(scripts), 10 + len(scripts) + len(feature_list))
            + scripts + feature_list + lookup_list)


def bar(width, tick):
    """A bar on the baseline from 0 to width across and 150 up, with a tick 500 high at tick across."""
    return simple([[(0, 0), (0, 150), (width, 150), (width, 0)], [(tick, 150), (tick, 650), (tick + 80, 650), (tick + 80, 150)]])


# arabic.ttf: glyph 1 beh, 2 alef, 3 lam, 4 fatha and 5 the space, mapped; 6 to 8 beh's final, medial
# and initial glyphs, 9 alef's final, 10 to 12 lam's final, medial and initial, 13 and 14 the
# isolated and final lam-alef.
glyphs = [
    b"",
    bar(1000, 460), simple([[(400, 0), (400, 800), (600, 800), (600, 0)]]), bar(1000, 150),
    simple([[(-650, 700), (-650, 780), (-350, 780), (-350, 700)]]), b"",
    bar(700, 300), bar(500, 210), bar(600, 260),
    simple([[(300, 0), (300, 800), (500, 800), (500, 0)], [(0, 0), (0, 150), (300, 150), (300, 0)]]),
    bar(700, 100), bar(500, 100), bar(600, 100),
    simple([[(100, 0), (100, 900), (300, 900), (300, 0)], [(600, 0), (600, 900), (800, 900), (800, 0)]]),
    simple([[(0, 0), (0, 150), (900, 150), (900, 0)], [(100, 150), (100, 900), (300, 900), (300, 150)], [(600, 150), (600, 900), (800, 900), (800, 150)]]),
]
glyf = b"".join(glyphs)
loca, at = b"", 0
for g in glyphs: loca += struct.pack(">I", at); at += len(g)
loca += struct.pack(">I", at)
lookups = [
    (1, 0, single([(1, 6), (2, 9), (3, 10)])),
    (1, 0, single([(1, 7), (3, 11)])),
    (1, 0, single([(1, 8), (3, 12)])),
    (4, 8, ligatures({11: [(14, [9])], 12: [(13, [9])]})),
]
features = [(b"fina", [0]), (b"init", [2]), (b"medi", [1]), (b"rlig", [3])]
classes = [(1, 3, 1), (4, 4, 3), (5, 12, 1), (13, 14, 2)]
class_def = struct.pack(">HH", 2, len(classes)) + b"".join(struct.pack(">HHH", *c) for c in classes)
gdef = struct.pack(">IHHHH", 0x00010000, 12, 0, 0, 0) + class_def
font(os.path.join(here, "arabic.ttf"), 15, [(0x0628, 1), (0x0627, 2), (0x0644, 3), (0x064E, 4), (0x20, 5)],
     {b"glyf": glyf, b"loca": loca, b"GSUB": gsub_of(b"arab", features, lookups), b"GDEF": gdef},
     {2: 600, 4: 0, 5: 500, 6: 700, 7: 500, 8: 600, 9: 700, 10: 700, 11: 500, 12: 600, 13: 900, 14: 900})

# arabic-marks.ttf: arabic.ttf's glyphs and 15 kasra and 16 shadda.
def cover(glyphs):
    return struct.pack(">HH", 1, len(glyphs)) + b"".join(struct.pack(">H", g) for g in glyphs)


def anchor(x, y):
    return struct.pack(">Hhh", 1, x, y)


def offsets_then(header_size, parts):
    """The offsets of parts laid one after another past a header of header_size bytes, and the parts."""
    at, out = header_size, []
    for part in parts: out.append(at); at += len(part)
    return out, b"".join(parts)


def mark_array(marks):
    """A mark array of (class, x, y) each, its anchors after its records."""
    offsets, anchors = offsets_then(2 + 4 * len(marks), [anchor(x, y) for _, x, y in marks])
    return struct.pack(">H", len(marks)) + b"".join(struct.pack(">HH", c, o) for (c, _, _), o in zip(marks, offsets)) + anchors


def anchor_rows(rows):
    """Rows of anchors, (x, y) each or None, one offset to each from the table's start."""
    flat = [a for row in rows for a in row]
    offsets, anchors = offsets_then(2 + 2 * len(flat), [anchor(*a) for a in flat if a])
    it = iter(offsets)
    return struct.pack(">H", len(rows)) + b"".join(struct.pack(">H", next(it) if a else 0) for a in flat) + anchors


def mark_attachment(marks, mark_glyphs, targets, rows, classes):
    """A mark-to-base or mark-to-mark subtable: marks (class, x, y) for mark_glyphs, and rows of
    anchors, one for each class, for the target glyphs."""
    parts = [cover(mark_glyphs), cover(targets), mark_array(marks), anchor_rows(rows)]
    o, body = offsets_then(12, parts)
    return struct.pack(">HHHHHH", 1, o[0], o[1], classes, o[2], o[3]) + body


def ligature_attachment(marks, mark_glyphs, ligatures, components, classes):
    """A mark-to-ligature subtable, each ligature's components a row of anchors."""
    attaches = [anchor_rows(rows) for rows in components]
    o, body = offsets_then(2 + 2 * len(attaches), attaches)
    array = struct.pack(">H", len(attaches)) + b"".join(struct.pack(">H", x) for x in o) + body
    parts = [cover(mark_glyphs), cover(ligatures), mark_array(marks), array]
    o, body = offsets_then(12, parts)
    return struct.pack(">HHHHHH", 1, o[0], o[1], classes, o[2], o[3]) + body


above, below = 0, 1
bases = [1, 2, 3, 6, 7, 8, 9, 10, 11, 12]
tops = {1: 300, 2: 500, 3: 500, 6: 350, 7: 250, 8: 300, 9: 400, 10: 350, 11: 250, 12: 300}
mark_base = mark_attachment([(above, -500, 650), (below, -500, -100), (above, -500, 650)], [4, 15, 16], bases,
                            [[(tops[g], 850 if g in (2, 9) else 800), (tops[g], -50)] for g in bases], 2)
mark_ligature = ligature_attachment([(above, -500, 650), (below, -500, -100), (above, -500, 650)], [4, 15, 16], [13, 14],
                                    [[[(700, 950), (700, -50)], [(200, 950), (200, -50)]]] * 2, 2)
mark_mark = mark_attachment([(above, -500, 650)], [4], [16], [[(-500, 850)]], 1)
# The pairs: alef then beh by their glyphs, alef's final glyph then beh by their classes, each
# moving the first, as a font moves the glyph before a pair read right to left.
pair_glyphs = (struct.pack(">HHHHHH", 1, 12, 0x05, 0, 1, 12 + len(cover([2]))) + cover([2])
               + struct.pack(">HHhh", 1, 1, 200, 200))
class1 = struct.pack(">HHHH", 1, 9, 1, 1)
class2 = struct.pack(">HHHH", 1, 1, 1, 1)
pair_classes_header = 16 + 2 * 2 * 2 * 2
pair_classes = (struct.pack(">HHHHHHHH", 2, pair_classes_header, 0x05, 0, pair_classes_header + 6, pair_classes_header + 14, 2, 2)
                + struct.pack(">hhhhhhhh", 0, 0, 0, 0, 0, 0, 100, 100) + cover([9]) + class1 + class2)
raised = struct.pack(">HHHh", 1, 8, 0x02, 100) + cover([12])
chain = struct.pack(">HHHHHH", 3, 0, 1, 18, 1, 24) + struct.pack(">HHH", 1, 0, 6) + cover([12]) + cover([6])
gpos_lookups = [
    (9, 0, struct.pack(">HHI", 1, 4, 8) + mark_base),
    (5, 0, mark_ligature),
    (6, 0, mark_mark),
    (2, 0, pair_glyphs),
    (2, 0, pair_classes),
    (8, 0, chain),
    (1, 0, raised),
]
glyphs = glyphs + [simple([[(-650, -250), (-650, -170), (-350, -170), (-350, -250)]]),
                   simple([[(-700, 680), (-700, 800), (-300, 800), (-300, 680)]])]
glyf = b"".join(glyphs)
loca, at = b"", 0
for g in glyphs: loca += struct.pack(">I", at); at += len(g)
loca += struct.pack(">I", at)
classes = [(1, 3, 1), (4, 4, 3), (5, 12, 1), (13, 14, 2), (15, 16, 3)]
class_def = struct.pack(">HH", 2, len(classes)) + b"".join(struct.pack(">HHH", *c) for c in classes)
gdef = struct.pack(">IHHHH", 0x00010000, 12, 0, 0, 0) + class_def
font(os.path.join(here, "arabic-marks.ttf"), 17,
     [(0x0628, 1), (0x0627, 2), (0x0644, 3), (0x064E, 4), (0x20, 5), (0x0650, 15), (0x0651, 16)],
     {b"glyf": glyf, b"loca": loca, b"GSUB": gsub_of(b"arab", features, lookups), b"GDEF": gdef,
      b"GPOS": gsub_of(b"arab", [(b"kern", [3, 4, 5]), (b"mark", [0, 1]), (b"mkmk", [2])], gpos_lookups)},
     {2: 600, 4: 0, 5: 500, 6: 700, 7: 500, 8: 600, 9: 700, 10: 700, 11: 500, 12: 600, 13: 900, 14: 900, 15: 0, 16: 0})

# arabic-forms.ttf: the letters and their presentation forms, each a glyph of its own, the same
# shapes as arabic.ttf's by position, and no GSUB.
forms = [(0x0628, 1, 1000, 460), (0xFE8F, 1, 1000, 460), (0xFE90, 6, 700, 300), (0xFE91, 8, 600, 260), (0xFE92, 7, 500, 210),
         (0x0644, 3, 1000, 150), (0xFEDD, 3, 1000, 150), (0xFEDE, 10, 700, 100), (0xFEDF, 12, 600, 100), (0xFEE0, 11, 500, 100)]
shapes = [b""] + [bar(width, tick) for _, _, width, tick in forms] + [glyphs[2], glyphs[2], glyphs[9], glyphs[13], glyphs[14], b""]
codes = [(c, i + 1) for i, (c, _, _, _) in enumerate(forms)] + [(0x0627, 11), (0xFE8D, 12), (0xFE8E, 13), (0xFEFB, 14), (0xFEFC, 15), (0x20, 16)]
advances = {i + 1: width for i, (_, _, width, _) in enumerate(forms)}
advances.update({11: 600, 12: 600, 13: 700, 14: 900, 15: 900, 16: 500})
glyf = b"".join(shapes)
loca, at = b"", 0
for g in shapes: loca += struct.pack(">I", at); at += len(g)
loca += struct.pack(">I", at)
font(os.path.join(here, "arabic-forms.ttf"), len(shapes), codes, {b"glyf": glyf, b"loca": loca}, advances)
