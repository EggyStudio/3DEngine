#!/usr/bin/env python3
# Reads how a frame's light lies across each surface against a path-traced reference's, apart from
# its level, from the files gi.reference and gi.compare write, the reference's PFM, its regions and
# their names beside <reference.png>, and the frame's light, <reference>-frame.pfm by default.
#
#   build/region-pattern.py <reference.png> [<frame.pfm>] [<region id>...]
#
# For each of the ten largest regions, or those named, it prints the region's pixels, its level (the
# frame's light over the reference's), the spread (the root mean square of the logarithm of the
# frame's light over the reference's, each smoothed over 9 by 9 pixels within the region, with the
# level divided out), and the light within 6 pixels of the region's edge over the light 24 pixels in
# or more, in the frame and in the reference. Light is read as its luminance. A darkening of the
# light from all around that falls where the reference's does narrows the spread and brings the
# frame's edge over inside to the reference's.
import math
import os
import struct
import sys
from collections import defaultdict, deque


def read_pfm(path):
    data = open(path, 'rb').read()
    at, lines = 0, []
    for _ in range(3):
        end = data.index(b'\n', at)
        lines.append(data[at:end].decode())
        at = end + 1
    width, height = map(int, lines[1].split())
    floats = struct.unpack('<%df' % (width * height * 3), data[at:at + width * height * 12])
    return width, height, [0.2126 * floats[i * 3] + 0.7152 * floats[i * 3 + 1] + 0.0722 * floats[i * 3 + 2]
                           for i in range(width * height)]


reference = sys.argv[1]
frame = sys.argv[2] if len(sys.argv) > 2 and sys.argv[2].endswith('.pfm') else os.path.splitext(reference)[0] + '-frame.pfm'
asked = [int(a) for a in sys.argv[2:] if not a.endswith('.pfm')]
w, h, light = read_pfm(reference + '.pfm')
_, _, drawn = read_pfm(frame)
regions = struct.unpack('<%di' % (w * h), open(reference + '.regions', 'rb').read())
names = {}
for line in open(reference + '.regions.txt'):
    parts = line.rstrip('\n').split(None, 1)
    if len(parts) == 2 and parts[0].lstrip('-').isdigit():
        names[int(parts[0])] = parts[1]

# Each pixel's distance in pixels from its region's edge, walked out from the pixels at the edge.
edge = [10 ** 9] * (w * h)
queue = deque()
for y in range(h):
    for x in range(w):
        i = y * w + x
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            X, Y = x + dx, y + dy
            if X < 0 or Y < 0 or X >= w or Y >= h or regions[Y * w + X] != regions[i]:
                edge[i] = 0
                queue.append(i)
                break
while queue:
    i = queue.popleft()
    x, y = i % w, i // w
    for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
        X, Y = x + dx, y + dy
        if 0 <= X < w and 0 <= Y < h:
            j = Y * w + X
            if regions[j] == regions[i] and edge[j] > edge[i] + 1:
                edge[j] = edge[i] + 1
                queue.append(j)


def smoothed(values):
    # The mean over every other pixel of the 9 by 9 square about each, within its region alone.
    out = [0.0] * (w * h)
    for y in range(h):
        for x in range(w):
            i = y * w + x
            total, count = values[i], 1
            for Y in range(max(0, y - 4), min(h, y + 5), 2):
                for X in range(max(0, x - 4), min(w, x + 5), 2):
                    j = Y * w + X
                    if regions[j] == regions[i]:
                        total += values[j]
                        count += 1
            out[i] = total / count
    return out


smooth_light, smooth_drawn = smoothed(light), smoothed(drawn)
by_region = defaultdict(list)
for i in range(w * h):
    by_region[regions[i]].append(i)
# A region with no name holds the pixels whose ray met nothing.
chosen = asked or sorted((r for r in by_region if r in names), key=lambda r: -len(by_region[r]))[:10]
print('region, pixels, level, spread, edge over inside in the frame, in the reference')
for region in chosen:
    pixels = by_region[region]
    frame_sum, reference_sum = sum(drawn[i] for i in pixels), sum(light[i] for i in pixels)
    if len(pixels) < 400 or frame_sum <= 0 or reference_sum <= 0:
        continue
    level = frame_sum / reference_sum
    logs = [math.log(smooth_drawn[i] / smooth_light[i] / level) for i in pixels if smooth_light[i] > 0 and smooth_drawn[i] > 0]
    spread = math.sqrt(sum(v * v for v in logs) / len(logs))

    def edge_over_inside(values):
        near = [values[i] for i in pixels if edge[i] < 6]
        inside = [values[i] for i in pixels if edge[i] >= 24]
        if not near or not inside or sum(inside) <= 0:
            return 'none'
        return f'{(sum(near) / len(near)) / (sum(inside) / len(inside)):.3f}'

    print(f'{names.get(region, region)}, {len(pixels)}, {level:.3f}, {spread:.3f}, {edge_over_inside(drawn)}, {edge_over_inside(light)}')
