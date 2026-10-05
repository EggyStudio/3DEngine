#!/usr/bin/env python3
"""Writes 3DEngine.Tests/Api/planes.ttf, a TrueType font of four glyphs the font tests load, so
their shapes are known. 'A' is a triangle. Past U+FFFF, U+10348 is a diamond, U+1F600 a square
with a square hole, and U+1F7E0 a circle of quadratic curves. 1000 units to the em, ascender 800
and descender -200, each glyph 1000 wide. The character maps are format 4 for the first plane and
format 12 for every plane, as fonts reaching past it carry."""
import math, os, struct

def simple(contours):
    """A simple glyph of contours, each a list of (x, y, on_curve)."""
    points = [p for c in contours for p in c]
    xs, ys = [p[0] for p in points], [p[1] for p in points]
    out = struct.pack(">hhhhh", len(contours), min(xs), min(ys), max(xs), max(ys))
    end = -1
    for c in contours:
        end += len(c)
        out += struct.pack(">H", end)
    out += struct.pack(">H", 0)                                       # no instructions
    out += bytes(1 if on else 0 for _, _, on in points)                 # coordinates are words
    x = y = 0
    for px, _, _ in points: out += struct.pack(">h", px - x); x = px
    for _, py, _ in points: out += struct.pack(">h", py - y); y = py
    while len(out) % 4: out += b"\0"
    return out

def ring(cx, cy, r, steps=8):
    """A circle of quadratic segments, on-curve points between off-curve ones, clockwise."""
    pts = []
    for k in range(steps):
        a = -2 * math.pi * k / steps
        b = a - math.pi / steps
        pts.append((round(cx + r * math.cos(a)), round(cy + r * math.sin(a)), True))
        pts.append((round(cx + r / math.cos(math.pi / steps) * math.cos(b)), round(cy + r / math.cos(math.pi / steps) * math.sin(b)), False))
    return pts

glyphs = [
    b"",                                                                               # .notdef
    simple([[(100, 0, True), (500, 700, True), (900, 0, True)]]),                      # A
    simple([[(500, 0, True), (100, 400, True), (500, 800, True), (900, 400, True)]]),  # U+10348
    simple([[(100, 0, True), (100, 800, True), (900, 800, True), (900, 0, True)],       # U+1F600
            [(300, 200, True), (700, 200, True), (700, 600, True), (300, 600, True)]]),
    simple([ring(500, 400, 380)]),                                                      # U+1F7E0
]
codes = [(0x41, 1), (0x10348, 2), (0x1F600, 3), (0x1F7E0, 4)]

glyf = b"".join(glyphs)
loca, at = b"", 0
for g in glyphs: loca += struct.pack(">I", at); at += len(g)
loca += struct.pack(">I", at)
hmtx = b"".join(struct.pack(">Hh", 1000, 100) for _ in glyphs)
head = struct.pack(">IIIIHHqqhhhhHHhhh", 0x00010000, 0x00010000, 0, 0x5F0F3CF5, 0, 1000, 0, 0,
                   0, -200, 1000, 800, 0, 8, 2, 1, 0)
hhea = struct.pack(">IhhhHhhhhhhhhhhhH", 0x00010000, 800, -200, 0, 1000, 100, 100, 900, 1, 0, 0, 0, 0, 0, 0, 0, len(glyphs))
maxp = struct.pack(">IH", 0x00005000, len(glyphs))
# Format 4: 'A' alone, then the closing segment.
seg = [(0x41, 0x41, 1 - 0x41), (0xFFFF, 0xFFFF, 1)]
f4 = b"".join(struct.pack(">H", e) for _, e, _ in seg) + struct.pack(">H", 0)
f4 += b"".join(struct.pack(">H", s) for s, _, _ in seg)
f4 += b"".join(struct.pack(">h", d) for _, _, d in seg) + b"\0\0" * len(seg)
f4 = struct.pack(">HHHHHHH", 4, 14 + len(f4), 0, len(seg) * 2, 4, 1, 0) + f4
f12 = b"".join(struct.pack(">III", c, c, g) for c, g in codes)
f12 = struct.pack(">HHIII", 12, 0, 16 + len(f12), 0, len(codes)) + f12
cmap = struct.pack(">HH", 0, 2) + struct.pack(">HHI", 3, 1, 20) + struct.pack(">HHI", 3, 10, 20 + len(f4)) + f4 + f12

tables = {b"cmap": cmap, b"glyf": glyf, b"head": head, b"hhea": hhea, b"hmtx": hmtx, b"loca": loca, b"maxp": maxp}
n = len(tables)
font = struct.pack(">IHHHH", 0x00010000, n, 64, 2, n * 16 - 64)
offset = 12 + 16 * n
body = b""
for tag in sorted(tables):
    data = tables[tag]
    font += struct.pack(">4sIII", tag, 0, offset + len(body), len(data))
    body += data + b"\0" * (-len(data) % 4)
out = os.path.join(os.path.dirname(__file__), "..", "3DEngine.Tests", "Api", "planes.ttf")
with open(out, "wb") as f: f.write(font + body)
print(out, len(font + body), "bytes")
