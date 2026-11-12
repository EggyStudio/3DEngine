#!/usr/bin/env python3
"""Writes 3DEngine/Shaders/dither/blue_noise.bin, the 64 by 64 table of blue noise the window's dither
draws its grain from (frame.slang's frameDither), a byte a texel, row by row.

    build/blue-noise.py

Each texel's byte is its rank among the table's 4,096 by Ulichney's void-and-cluster method, a
sixteenth of it, so thresholding the table at any level leaves its texels spread as evenly as they
can be, with no clumps the eye picks out, wrapping at its edges so the tiles meet without a seam.
The energy is a Gaussian of 1.2 texels, whose tenth of the table has no two texels side by side,
and the seed is fixed, so the file is made again the same.
"""

import math
import os
import random

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "3DEngine", "Shaders", "dither", "blue_noise.bin")
N = 64
SIGMA = 1.2
REACH = 7

KERNEL = [(dx, dy, math.exp(-(dx * dx + dy * dy) / (2 * SIGMA * SIGMA)))
          for dx in range(-REACH, REACH + 1) for dy in range(-REACH, REACH + 1)]


def add(energy, i, sign):
    x, y = i % N, i // N
    for dx, dy, k in KERNEL:
        energy[((y + dy) % N) * N + (x + dx) % N] += sign * k


def energy_of(on):
    energy = [0.0] * (N * N)
    for i, v in enumerate(on):
        if v:
            add(energy, i, 1)
    return energy


def tightest(on, energy):
    return max((i for i in range(N * N) if on[i]), key=lambda i: energy[i])


def largest_void(on, energy):
    return min((i for i in range(N * N) if not on[i]), key=lambda i: energy[i])


def main():
    random.seed(20261112)
    # A tenth of the texels, moved one at a time from the tightest cluster to the largest void
    # until the one moved would go back where it was.
    on = [False] * (N * N)
    for i in random.sample(range(N * N), N * N // 10):
        on[i] = True
    energy = energy_of(on)
    while True:
        c = tightest(on, energy)
        on[c] = False
        add(energy, c, -1)
        v = largest_void(on, energy)
        on[v] = True
        add(energy, v, 1)
        if v == c:
            break
    initial = on[:]
    count = sum(initial)
    rank = [0] * (N * N)
    # Those texels ranked from the last down, the tightest taken away first.
    on = initial[:]
    energy = energy_of(on)
    for r in range(count - 1, -1, -1):
        c = tightest(on, energy)
        on[c] = False
        add(energy, c, -1)
        rank[c] = r
    # Up to half, the largest void filled each time.
    on = initial[:]
    energy = energy_of(on)
    r = count
    while r < N * N // 2:
        v = largest_void(on, energy)
        on[v] = True
        add(energy, v, 1)
        rank[v] = r
        r += 1
    # The rest by the empty texels' own energy, the one in the tightest cluster of them filled next.
    empty = energy_of([not v for v in on])
    while r < N * N:
        v = max((i for i in range(N * N) if not on[i]), key=lambda i: empty[i])
        on[v] = True
        add(empty, v, -1)
        rank[v] = r
        r += 1
    assert sorted(rank) == list(range(N * N))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "wb") as table:
        table.write(bytes(r >> 4 for r in rank))
    print(f"wrote {OUT}, {N} by {N}")


if __name__ == "__main__":
    main()
