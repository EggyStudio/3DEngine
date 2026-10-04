#!/usr/bin/env python3
"""Draws Hopper's pixel art: resources/hero.png, a run cycle of four 16 by 16 frames in a row, and
resources/tiles.png, an atlas of 16 by 16 tiles (grass, dirt, brick, then four frames of a coin
turning). Needs Pillow."""
import os
from PIL import Image, ImageDraw

here = os.path.join(os.path.dirname(__file__), "resources")

hero = Image.new("RGBA", (64, 16), (0, 0, 0, 0))
d = ImageDraw.Draw(hero)
for f in range(4):
    x = f * 16
    d.rectangle([x + 5, 2, x + 10, 6], fill=(250, 210, 170, 255))         # head
    d.rectangle([x + 8, 3, x + 9, 4], fill=(30, 30, 40, 255))             # eye, so it faces right
    d.rectangle([x + 4, 7, x + 11, 11], fill=(60, 120, 220, 255))         # body
    stride = [0, 2, 0, -2][f]
    d.rectangle([x + 5 + stride, 12, x + 6 + stride, 15], fill=(50, 40, 40, 255))   # legs swing
    d.rectangle([x + 9 - stride, 12, x + 10 - stride, 15], fill=(50, 40, 40, 255))
hero.save(os.path.join(here, "hero.png"))

tiles = Image.new("RGBA", (112, 16), (0, 0, 0, 0))
d = ImageDraw.Draw(tiles)
d.rectangle([0, 0, 15, 15], fill=(120, 80, 50, 255)); d.rectangle([0, 0, 15, 4], fill=(90, 190, 80, 255))   # grass
d.rectangle([16, 0, 31, 15], fill=(120, 80, 50, 255)); d.point([(20, 5), (27, 10), (23, 13)], fill=(90, 60, 40, 255))  # dirt
d.rectangle([32, 0, 47, 15], fill=(170, 80, 60, 255))                                                       # brick
for y in (0, 8): d.line([32, y + 7, 47, y + 7], fill=(110, 50, 40, 255))
for x in (39, 35, 43): d.line([x, 0 if x == 39 else 8, x, 7 if x == 39 else 15], fill=(110, 50, 40, 255))
for f, w in enumerate([6, 4, 1, 4]):                                                                         # coin turning
    cx = 48 + f * 16 + 8
    d.ellipse([cx - w, 3, cx + w, 13], fill=(250, 200, 50, 255), outline=(200, 140, 20, 255))
tiles.save(os.path.join(here, "tiles.png"))
print("hero.png and tiles.png written to", here)
