#!/usr/bin/env python3
"""Makes Manor's art and sound under resources/: the textures as PNG files, the models as OBJ files
each with its own MTL naming its textures, and the sounds and the music as WAV files, synthesized
here. The level's scene files are written by the game itself (`Manor build-level resources`), from
the models made here. Needs only Python."""
import math, os, random, struct, wave, zlib

here = os.path.join(os.path.dirname(os.path.abspath(__file__)), "resources")
MODELS = os.path.join(here, "models")
TEXTURES = os.path.join(MODELS, "textures")
SOUNDS = os.path.join(here, "sounds")
for folder in (MODELS, TEXTURES, SOUNDS):
    os.makedirs(folder, exist_ok=True)

# -- Textures, 128 texels across, from value noise and patterns, with few colors so they pack small

SIZE = 128

def png(name, pixels):
    """Writes rows of (r, g, b) tuples as an RGB PNG."""
    raw = b"".join(b"\x00" + bytes(c for px in row for c in px) for row in pixels)
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data) & 0xFFFFFFFF)
    data = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", SIZE, SIZE, 8, 2, 0, 0, 0))
    data += chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b"")
    with open(os.path.join(TEXTURES, name + ".png"), "wb") as f:
        f.write(data)

def noise(seed, cells):
    """Smooth value noise that tiles, 0 to 1, as a function of texel coordinates."""
    rng = random.Random(seed)
    grid = [[rng.random() for _ in range(cells)] for _ in range(cells)]
    def at(x, y):
        fx, fy = x * cells / SIZE, y * cells / SIZE
        x0, y0 = int(fx) % cells, int(fy) % cells
        x1, y1 = (x0 + 1) % cells, (y0 + 1) % cells
        tx, ty = fx - int(fx), fy - int(fy)
        tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
        top = grid[y0][x0] * (1 - tx) + grid[y0][x1] * tx
        bottom = grid[y1][x0] * (1 - tx) + grid[y1][x1] * tx
        return top * (1 - ty) + bottom * ty
    return at

def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * max(0, min(1, t))) for i in range(3))

def quantize(c, step=6):
    return tuple(min(255, (v // step) * step) for v in c)

def texture(name, shade):
    png(name, [[quantize(shade(x, y)) for x in range(SIZE)] for y in range(SIZE)])

def speckled(name, dark, light, seed, cells=16, fine=0.35):
    big, small = noise(seed, cells), noise(seed + 1, 64)
    texture(name, lambda x, y: mix(dark, light, big(x, y) * (1 - fine) + small(x, y) * fine))

def planks(name, dark, light, seed, rows=4, across=False):
    grain, rng = noise(seed, 32), random.Random(seed)
    shift = [rng.random() for _ in range(rows)]
    def shade(x, y):
        a, b = (y, x) if across else (x, y)
        row = b * rows // SIZE
        edge = (b * rows) % SIZE < 3 or ((a + int(shift[row] * SIZE)) % SIZE) < 2
        c = mix(dark, light, grain((a * 4) % SIZE, b) * 0.7 + shift[row] * 0.3)
        return mix(c, (30, 20, 12), 0.6) if edge else c
    texture(name, shade)

def tiles(name, a, b, grout, count=4, checker=False, seed=1):
    vary = noise(seed, 16)
    def shade(x, y):
        tx, ty = x * count // SIZE, y * count // SIZE
        if (x * count) % SIZE < 3 or (y * count) % SIZE < 3:
            return grout
        base = b if checker and (tx + ty) % 2 else a
        return mix(base, (255, 255, 255), vary(x, y) * 0.15)
    texture(name, shade)

def stripes(name, a, b, count=8, seed=1):
    vary = noise(seed, 8)
    texture(name, lambda x, y: mix(a if (x * count // SIZE) % 2 else b, (0, 0, 0), vary(x, y) * 0.12))

def damask(name, ground, figure, seed=1):
    vary = noise(seed, 8)
    def shade(x, y):
        u, v = (x % 32) / 32 - 0.5, (y % 32) / 32 - 0.5
        diamond = abs(u) + abs(v * 1.4) < 0.32 + 0.06 * math.sin(math.atan2(v, u) * 4)
        return mix(figure if diamond else ground, (0, 0, 0), vary(x, y) * 0.1)
    texture(name, shade)

def stone_blocks(name, dark, light, seed, rows=4):
    vary, rng = noise(seed, 16), random.Random(seed)
    tone = {}
    def shade(x, y):
        row = y * rows // SIZE
        offset = SIZE // 4 if row % 2 else 0
        col = ((x + offset) % SIZE) * 2 // SIZE
        if (y * rows) % SIZE < 3 or ((x + offset) * 2) % SIZE < 3:
            return (90, 88, 84)
        t = tone.setdefault((row, col), rng.random())
        return mix(dark, light, t * 0.6 + vary(x, y) * 0.4)
    texture(name, shade)

def books(name, seed):
    rng = random.Random(seed)
    colors = [(120, 30, 30), (30, 60, 110), (40, 90, 50), (140, 110, 50), (80, 40, 90), (60, 40, 30)]
    shelves = []
    for _ in range(4):
        spines, x = [], 0
        while x < SIZE:
            w = rng.randint(4, 9)
            spines.append((x, x + w, rng.choice(colors), rng.randint(18, 28)))
            x += w
        shelves.append(spines)
    def shade(x, y):
        shelf, inside = y * 4 // SIZE, (y * 4) % SIZE
        if inside >= 120 or inside < 4:
            return (70, 45, 28)
        for x0, x1, c, h in shelves[shelf]:
            if x0 <= x < x1:
                return c if inside > 120 - h * 4 and x1 - x > 1 else (35, 22, 14)
        return (35, 22, 14)
    texture(name, shade)

speckled("grass", (52, 92, 38), (104, 148, 62), 1)
speckled("gravel", (120, 114, 104), (186, 178, 164), 2, fine=0.7)
stone_blocks("stone", (128, 122, 112), (178, 170, 156), 3)
tiles("slate", (62, 66, 78), (62, 66, 78), (38, 40, 46), count=8, seed=4)
texture("bark", lambda x, y, n=noise(5, 32): mix((58, 40, 26), (104, 78, 52), n((x * 6) % SIZE, y // 3)))
speckled("leaves", (30, 70, 28), (82, 130, 50), 6, cells=24, fine=0.5)
speckled("hedge", (26, 60, 26), (60, 104, 44), 7, cells=32, fine=0.6)
texture("water", lambda x, y, n=noise(8, 8): mix((40, 90, 120), (110, 170, 190), n(x, y) ** 2))
speckled("iron", (34, 34, 36), (70, 70, 74), 9)
planks("wood", (92, 58, 32), (140, 96, 58), 10)
planks("door", (74, 44, 24), (118, 76, 42), 11, rows=3, across=True)
books("books", 12)
speckled("plaster", (214, 208, 196), (236, 232, 222), 13, fine=0.3)
speckled("flowers", (40, 90, 30), (210, 80, 120), 14, cells=48, fine=0.8)
speckled("rug", (120, 30, 34), (170, 60, 50), 15, cells=8, fine=0.4)
texture("canvas", lambda x, y, n=noise(16, 6): mix((60, 90, 140), (220, 180, 90), n(x, y)))

# Each room's walls and floor, so walking through the house loads textures of its own in each room.
ROOMS = {
    "hall": (lambda: damask("wall-hall", (120, 36, 40), (156, 66, 60)),
             lambda: tiles("floor-hall", (226, 222, 214), (40, 40, 44), (120, 120, 120), checker=True)),
    "library": (lambda: stripes("wall-library", (36, 74, 50), (52, 92, 62)),
                lambda: planks("floor-library", (60, 36, 20), (96, 62, 36), 21, rows=8)),
    "gallery": (lambda: tiles("wall-gallery", (220, 208, 180), (220, 208, 180), (180, 168, 140), count=2, seed=22),
                lambda: planks("floor-gallery", (170, 130, 86), (210, 170, 120), 23, rows=6)),
    "dining": (lambda: stripes("wall-dining", (44, 64, 120), (60, 84, 140), count=16),
               lambda: planks("floor-dining", (110, 70, 40), (150, 104, 64), 24, rows=8, across=True)),
    "study": (lambda: damask("wall-study", (150, 116, 46), (176, 140, 66)),
              lambda: speckled("floor-study", (60, 40, 70), (90, 60, 96), 25, cells=8)),
    "kitchen": (lambda: tiles("wall-kitchen", (232, 234, 230), (232, 234, 230), (170, 170, 168), count=8, seed=26),
                lambda: tiles("floor-kitchen", (170, 84, 52), (150, 70, 44), (110, 90, 80), count=4, checker=True, seed=27)),
}
for wall, floor in ROOMS.values():
    wall()
    floor()

# -- Models: boxes and quads with texture coordinates taken from where they are, so a texture
#    repeats across a wall at the same scale whatever the wall's size

FACES = [((1,0,0),(0,0,-1),(0,1,0)), ((-1,0,0),(0,0,1),(0,1,0)), ((0,1,0),(1,0,0),(0,0,-1)),
         ((0,-1,0),(1,0,0),(0,0,1)), ((0,0,1),(1,0,0),(0,1,0)), ((0,0,-1),(-1,0,0),(0,1,0))]

class Model:
    def __init__(self):
        self.parts = {}  # material -> (positions, normals, uvs, indices)
    def part(self, material):
        return self.parts.setdefault(material, ([], [], [], []))
    def quad(self, material, corners, normal, u, v, tile):
        pos, nrm, uv, idx = self.part(material)
        base = len(pos)
        for c in corners:
            pos.append(c); nrm.append(normal)
            uv.append((sum(c[i] * u[i] for i in range(3)) / tile, sum(c[i] * v[i] for i in range(3)) / tile))
        idx += [base, base + 1, base + 2, base, base + 2, base + 3]
    def box(self, material, lo, hi, tile=2.0):
        if any(hi[i] - lo[i] <= 1e-4 for i in range(3)):
            return
        c = [(lo[i] + hi[i]) / 2 for i in range(3)]; h = [(hi[i] - lo[i]) / 2 for i in range(3)]
        for n, u, v in FACES:
            corners = [tuple(c[i] + (n[i] + u[i] * su + v[i] * sv) * h[i] for i in range(3)) for su, sv in ((-1,-1),(1,-1),(1,1),(-1,1))]
            self.quad(material, corners, n, u, v, tile)
    def triangle(self, material, a, b, c, tile=2.0):
        e1 = [b[i] - a[i] for i in range(3)]; e2 = [c[i] - a[i] for i in range(3)]
        n = (e1[1]*e2[2]-e1[2]*e2[1], e1[2]*e2[0]-e1[0]*e2[2], e1[0]*e2[1]-e1[1]*e2[0])
        length = math.sqrt(sum(x * x for x in n)) or 1
        n = tuple(x / length for x in n)
        u = (1, 0, 0) if abs(n[0]) < 0.9 else (0, 0, 1)
        v = (n[1]*u[2]-n[2]*u[1], n[2]*u[0]-n[0]*u[2], n[0]*u[1]-n[1]*u[0])
        pos, nrm, uv, idx = self.part(material)
        base = len(pos)
        for p in (a, b, c):
            pos.append(p); nrm.append(n)
            uv.append((sum(p[i] * u[i] for i in range(3)) / tile, sum(p[i] * v[i] for i in range(3)) / tile))
        idx += [base, base + 1, base + 2]
    def write(self, name, materials):
        """materials: name -> (texture or None, (r, g, b) diffuse, emissive or None)"""
        with open(os.path.join(MODELS, name + ".mtl"), "w") as f:
            f.write(f"# {name}'s materials, made by make-art.py.\n")
            for m in self.parts:
                texture, color, emissive = materials[m]
                f.write(f"newmtl {m}\nKd {color[0]:.3f} {color[1]:.3f} {color[2]:.3f}\n")
                if emissive:
                    f.write(f"Ke {emissive[0]:.3f} {emissive[1]:.3f} {emissive[2]:.3f}\n")
                if texture:
                    f.write(f"map_Kd textures/{texture}.png\n")
        with open(os.path.join(MODELS, name + ".obj"), "w") as f:
            f.write(f"# {name}, made by make-art.py.\nmtllib {name}.mtl\n")
            offset = 1
            for m, (pos, nrm, uv, idx) in self.parts.items():
                f.write(f"o {m}\nusemtl {m}\n")
                for p in pos: f.write("v %.3f %.3f %.3f\n" % p)
                for t in uv: f.write("vt %.3f %.3f\n" % t)
                for n in nrm: f.write("vn %.3f %.3f %.3f\n" % n)
                for i in range(0, len(idx), 3):
                    a, b, c = (idx[i] + offset, idx[i + 1] + offset, idx[i + 2] + offset)
                    f.write(f"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}\n")
                offset += len(pos)

WHITE = (1, 1, 1)
def tex(name, color=WHITE, emissive=None):
    return (name, color, emissive)

def wall(model, material, axis, fixed, a0, a1, t0, t1, height, gaps, tile=2.0):
    """A wall along x (axis 'x', at z between t0 and t1) or z (axis 'z', at x between t0 and t1)
    from a0 to a1, with openings: (center, width, bottom, top)."""
    def put(b0, b1, y0, y1):
        if axis == "x":
            model.box(material, (b0, y0, t0), (b1, y1, t1), tile)
        else:
            model.box(material, (t0, y0, b0), (t1, y1, b1), tile)
    cuts = sorted(gaps)
    at = a0
    for center, width, bottom, top in cuts:
        put(at, center - width / 2, 0, height)
        put(center - width / 2, center + width / 2, 0, bottom)
        put(center - width / 2, center + width / 2, top, height)
        at = center + width / 2
    put(at, a1, 0, height)

# -- The house: six rooms of 16 by 16 meters in two columns and three rows, from x -16 to 16 and z
#    -32 to 16. The game's Level class places them by the same numbers.

CELL, HEIGHT, DOOR_W, DOOR_H = 16, 4.8, 2.4, 3.0
HOUSE = {  # room: (cell center, sides that open to another room, sides that are outside)
    "hall": ((-8, 8), {"e", "n", "s"}, {"w", "s"}),
    "library": ((8, 8), {"w", "n"}, {"e", "s"}),
    "gallery": ((-8, -8), {"s", "e", "n"}, {"w"}),
    "dining": ((8, -8), {"w", "s", "n"}, {"e"}),
    "study": ((-8, -24), {"e", "s"}, {"w", "n"}),
    "kitchen": ((8, -24), {"w", "s", "n"}, {"e", "n"}),
}
# The two outside doors are in a side that is outside, which "s" of the hall and "n" of the kitchen are.
WINDOW = (1.2, 3.4)

def side_gaps(side, doors, outside):
    gaps = []
    if side in doors:
        gaps.append((0, DOOR_W, 0, DOOR_H))
        if side in outside:
            gaps += [(-5, 2.4, *WINDOW), (5, 2.4, *WINDOW)]
    elif side in outside:
        gaps += [(-4, 2.4, *WINDOW), (4, 2.4, *WINDOW)]
    return gaps

def room(name, doors, outside):
    """A room in its own coordinates, its middle at the origin: its floor, its ceiling and its four
    walls, each 0.2 thick inside the cell's edge, with doorways and windows cut where they go."""
    m = Model()
    half, t = CELL / 2, 0.2
    m.box("floor", (-half, -0.2, -half), (half, 0, half), 2)
    m.box("ceiling", (-half, HEIGHT - 0.2, -half), (half, HEIGHT, half), 4)
    wall(m, "wall", "x", None, -half, half, -half, -half + t, HEIGHT, side_gaps("n", doors, outside))
    wall(m, "wall", "x", None, -half, half, half - t, half, HEIGHT, side_gaps("s", doors, outside))
    wall(m, "wall", "z", None, -half, half, -half, -half + t, HEIGHT, side_gaps("w", doors, outside))
    wall(m, "wall", "z", None, -half, half, half - t, half, HEIGHT, side_gaps("e", doors, outside))
    m.write("room-" + name, {"floor": tex("floor-" + name), "ceiling": tex("plaster"), "wall": tex("wall-" + name)})

for name, (_, doors, outside) in HOUSE.items():
    room(name, doors, outside)

def shell():
    """The house from outside: stone walls 0.4 thick round the rooms, openings matching theirs, and
    a slate roof over them with a ridge along the house's length."""
    m = Model()
    x0, x1, z0, z1, t = -16, 16, -32, 16, 0.4
    for name, ((cx, cz), doors, outside) in HOUSE.items():
        for side in outside:
            gaps = side_gaps(side, doors, outside)
            if side == "n":
                wall(m, "stone", "x", None, cx - 8 - t, cx + 8 + t, z0 - t, z0, HEIGHT + 0.4, [(cx + g[0], *g[1:]) for g in gaps], 2)
            if side == "s":
                wall(m, "stone", "x", None, cx - 8 - t, cx + 8 + t, z1, z1 + t, HEIGHT + 0.4, [(cx + g[0], *g[1:]) for g in gaps], 2)
            if side == "w":
                wall(m, "stone", "z", None, cz - 8, cz + 8, x0 - t, x0, HEIGHT + 0.4, [(cz + g[0], *g[1:]) for g in gaps], 2)
            if side == "e":
                wall(m, "stone", "z", None, cz - 8, cz + 8, x1, x1 + t, HEIGHT + 0.4, [(cz + g[0], *g[1:]) for g in gaps], 2)
    # The roof, two slopes from the eaves to a ridge along z, and the gables closing its ends.
    e, top, ridge = 0.8, HEIGHT + 0.4, HEIGHT + 6
    a, b = (x0 - t - e, top, z0 - t - e), (x1 + t + e, top, z0 - t - e)
    c, d = (x1 + t + e, top, z1 + t + e), (x0 - t - e, top, z1 + t + e)
    r0, r1 = (0, ridge, z0 - t - e), (0, ridge, z1 + t + e)
    m.triangle("slate", a, d, r1, 2); m.triangle("slate", a, r1, r0, 2)
    m.triangle("slate", c, b, r0, 2); m.triangle("slate", c, r0, r1, 2)
    m.triangle("stone", a, r0, b, 2); m.triangle("stone", d, c, r1, 2)
    m.write("shell", {"stone": tex("stone"), "slate": tex("slate")})

shell()

def ground():
    """The grass of the whole estate, 128 meters across, in four pieces round the house so no floor
    lies on it, and the stone wall round its edge."""
    m = Model()
    E, (hx0, hx1, hz0, hz1) = 64, (-16.4, 16.4, -32.4, 16.4)
    for lo, hi in (((-E, -0.2, -E), (E, 0, hz0)), ((-E, -0.2, hz1), (E, 0, E)),
                   ((-E, -0.2, hz0), (hx0, 0, hz1)), ((hx1, -0.2, hz0), (E, 0, hz1))):
        m.box("grass", lo, hi, 4)
    m.write("ground", {"grass": tex("grass")})
    w = Model()
    for lo, hi in (((-E - 1, 0, -E - 1), (E + 1, 3, -E)), ((-E - 1, 0, E), (E + 1, 3, E + 1)),
                   ((-E - 1, 0, -E), (-E, 3, E)), ((E, 0, -E), (E + 1, 3, E))):
        w.box("stone", lo, hi, 2)
    w.write("boundary", {"stone": tex("stone")})

ground()

# -- Furniture and the yard's pieces, each with its feet at the origin

def save(name, build, materials):
    m = Model()
    build(m)
    m.write(name, materials)

save("table", lambda m: (m.box("wood", (-1.2, 0.74, -0.7), (1.2, 0.8, 0.7), 1),
    [m.box("wood", (x - 0.05, 0, z - 0.05), (x + 0.05, 0.74, z + 0.05), 1) for x in (-1.1, 1.1) for z in (-0.6, 0.6)]),
    {"wood": tex("wood")})
save("long-table", lambda m: (m.box("wood", (-0.8, 0.74, -4), (0.8, 0.8, 4), 1),
    [m.box("wood", (x - 0.06, 0, z - 0.06), (x + 0.06, 0.74, z + 0.06), 1) for x in (-0.7, 0.7) for z in (-3.8, 0, 3.8)]),
    {"wood": tex("wood")})
save("chair", lambda m: (m.box("wood", (-0.25, 0.44, -0.25), (0.25, 0.48, 0.25), 1),
    m.box("wood", (-0.25, 0.48, 0.21), (0.25, 1.0, 0.25), 1),
    [m.box("wood", (x - 0.03, 0, z - 0.03), (x + 0.03, 0.44, z + 0.03), 1) for x in (-0.22, 0.22) for z in (-0.22, 0.22)]),
    {"wood": tex("wood")})
save("bookcase", lambda m: (m.box("wood", (-1.5, 0, -0.2), (1.5, 2.6, -0.16), 1), m.box("books", (-1.4, 0.05, -0.16), (1.4, 2.5, 0.2), 2.8),
    m.box("wood", (-1.5, 0, -0.2), (-1.4, 2.6, 0.22), 1), m.box("wood", (1.4, 0, -0.2), (1.5, 2.6, 0.22), 1),
    m.box("wood", (-1.5, 2.5, -0.2), (1.5, 2.6, 0.22), 1)),
    {"wood": tex("wood"), "books": tex("books")})
save("fireplace", lambda m: (m.box("stone", (-1.4, 0, -0.5), (-0.9, 1.4, 0.3), 1), m.box("stone", (0.9, 0, -0.5), (1.4, 1.4, 0.3), 1),
    m.box("stone", (-1.5, 1.4, -0.5), (1.5, 1.7, 0.4), 1), m.box("stone", (-0.9, 0, -0.5), (0.9, 1.4, -0.4), 1),
    m.box("stone", (-0.9, 0, -0.4), (0.9, 0.08, 0.3), 1), m.box("stone", (-1.0, 1.7, -0.5), (1.0, 4.6, -0.1), 2)),
    {"stone": tex("stone")})
save("counter", lambda m: (m.box("wood", (-2, 0, -0.35), (2, 0.86, 0.35), 1), m.box("stone", (-2.02, 0.86, -0.37), (2.02, 0.92, 0.37), 1)),
    {"wood": tex("wood"), "stone": tex("stone")})
save("stove", lambda m: (m.box("iron", (-0.6, 0, -0.4), (0.6, 0.9, 0.4), 1), m.box("iron", (-0.15, 0.9, -0.15), (0.15, 4.6, 0.15), 1)),
    {"iron": tex("iron")})
save("desk", lambda m: (m.box("wood", (-1, 0.72, -0.5), (1, 0.78, 0.5), 1), m.box("wood", (-1, 0, -0.5), (-0.55, 0.72, 0.5), 1),
    m.box("wood", (0.55, 0, -0.5), (1, 0.72, 0.5), 1)),
    {"wood": tex("wood")})
save("pedestal", lambda m: m.box("stone", (-0.35, 0, -0.35), (0.35, 1.1, 0.35), 1), {"stone": tex("plaster")})
save("painting", lambda m: (m.box("wood", (-1.1, -0.8, -0.04), (1.1, 0.8, 0.0), 1), m.box("canvas", (-1.0, -0.7, 0.0), (1.0, 0.7, 0.02), 2.0)),
    {"wood": tex("wood"), "canvas": tex("canvas")})
save("rug", lambda m: m.box("rug", (-2.5, 0, -1.6), (2.5, 0.02, 1.6), 5), {"rug": tex("rug")})
save("sofa", lambda m: (m.box("rug", (-1.1, 0, -0.45), (1.1, 0.45, 0.45), 2), m.box("rug", (-1.1, 0.45, 0.25), (1.1, 1.0, 0.45), 2),
    m.box("rug", (-1.1, 0.45, -0.45), (-0.9, 0.7, 0.25), 2), m.box("rug", (0.9, 0.45, -0.45), (1.1, 0.7, 0.25), 2)),
    {"rug": tex("rug")})

def tree(m):
    m.box("bark", (-0.25, 0, -0.25), (0.25, 3.2, 0.25), 1)
    for y, r in ((2.6, 2.0), (3.6, 1.7), (4.5, 1.1)):
        m.box("leaves", (-r, y, -r), (r, y + 1.2, r), 2)
save("tree", tree, {"bark": tex("bark"), "leaves": tex("leaves")})

def pine(m):
    m.box("bark", (-0.18, 0, -0.18), (0.18, 2.0, 0.18), 1)
    for i in range(5):
        r, y = 1.8 - i * 0.34, 1.2 + i * 1.0
        m.box("leaves", (-r, y, -r), (r, y + 0.9, r), 2)
save("pine", pine, {"bark": tex("bark"), "leaves": tex("hedge")})
save("hedge", lambda m: m.box("hedge", (-3, 0, -0.5), (3, 1.4, 0.5), 2), {"hedge": tex("hedge")})
save("bench", lambda m: (m.box("wood", (-1, 0.45, -0.25), (1, 0.5, 0.25), 1), m.box("wood", (-1, 0.5, 0.2), (1, 0.9, 0.25), 1),
    [m.box("iron", (x - 0.05, 0, -0.22), (x + 0.05, 0.45, 0.22), 1) for x in (-0.85, 0.85)]),
    {"wood": tex("wood"), "iron": tex("iron")})
save("lamppost", lambda m: (m.box("iron", (-0.08, 0, -0.08), (0.08, 3.2, 0.08), 1), m.box("iron", (-0.25, 3.2, -0.25), (0.25, 3.3, 0.25), 1),
    m.box("bulb", (-0.18, 3.3, -0.18), (0.18, 3.7, 0.18), 1), m.box("iron", (-0.28, 3.7, -0.28), (0.28, 3.8, 0.28), 1)),
    {"iron": tex("iron"), "bulb": (None, (1.0, 0.9, 0.7), (4.0, 3.2, 2.0))})
save("rock", lambda m: (m.box("stone", (-0.8, 0, -0.6), (0.8, 0.6, 0.6), 1), m.box("stone", (-0.5, 0.6, -0.4), (0.4, 0.9, 0.3), 1)),
    {"stone": tex("gravel")})
save("flowerbed", lambda m: (m.box("stone", (-2, 0, -1), (2, 0.3, 1), 1), m.box("flowers", (-1.85, 0.3, -0.85), (1.85, 0.5, 0.85), 2)),
    {"stone": tex("stone"), "flowers": tex("flowers")})

def fountain(m):
    for i in range(12):
        a0, a1 = i * math.tau / 12, (i + 1) * math.tau / 12
        x0, z0, x1, z1 = 3 * math.cos(a0), 3 * math.sin(a0), 3 * math.cos(a1), 3 * math.sin(a1)
        m.box("stone", (min(x0, x1) - 0.25, 0, min(z0, z1) - 0.25), (max(x0, x1) + 0.25, 0.6, max(z0, z1) + 0.25), 1)
    m.box("water", (-2.8, 0, -2.8), (2.8, 0.4, 2.8), 2)
    m.box("stone", (-0.4, 0.4, -0.4), (0.4, 1.6, 0.4), 1)
    m.box("stone", (-1.0, 1.6, -1.0), (1.0, 1.8, 1.0), 1)
save("fountain", fountain, {"stone": tex("stone"), "water": tex("water")})
save("path-ns", lambda m: m.box("gravel", (-2, 0, -8), (2, 0.03, 8), 2), {"gravel": tex("gravel")})
save("path-ew", lambda m: m.box("gravel", (-8, 0, -2), (8, 0.03, 2), 2), {"gravel": tex("gravel")})
save("path-cross", lambda m: (m.box("gravel", (-2, 0, -8), (2, 0.03, 8), 2), m.box("gravel", (-8, 0.001, -2), (-2, 0.031, 2), 2),
    m.box("gravel", (2, 0.001, -2), (8, 0.031, 2), 2)), {"gravel": tex("gravel")})

# -- Sounds, synthesized: a short chime for a lantern, a creak for a door, steps, and the music

RATE = 22050

def wav(name, samples):
    with wave.open(os.path.join(SOUNDS, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32000)) for s in samples))

def tone(freqs, seconds, decay):
    n = int(RATE * seconds)
    return [sum(math.sin(math.tau * f * i / RATE) for f in freqs) / len(freqs) * math.exp(-i / RATE * decay) for i in range(n)]

wav("chime", [s * 0.6 for s in tone([880, 1320, 1760], 1.2, 3.5)])
rng = random.Random(30)
wav("creak", [0.4 * math.sin(math.tau * (180 + 60 * math.sin(i / RATE * 9)) * i / RATE) * (rng.random() * 0.6 + 0.4) * math.exp(-i / RATE * 1.5) for i in range(int(RATE * 0.9))])
wav("step", [(rng.random() * 2 - 1) * math.exp(-i / RATE * 40) * 0.5 for i in range(int(RATE * 0.12))])
wav("door-shut", [(rng.random() * 2 - 1) * math.exp(-i / RATE * 18) * 0.7 + 0.3 * math.sin(math.tau * 70 * i / RATE) * math.exp(-i / RATE * 10) for i in range(int(RATE * 0.4))])
wav("click", [math.sin(math.tau * 1200 * i / RATE) * math.exp(-i / RATE * 60) * 0.4 for i in range(int(RATE * 0.06))])

def music():
    """Sixteen seconds of slow chords on a soft organ, which loop."""
    chords = [(220, 277.2, 329.6), (196, 246.9, 293.7), (174.6, 220, 261.6), (196, 246.9, 329.6)]
    out = []
    for chord in chords:
        n = RATE * 4
        for i in range(n):
            t = i / RATE
            swell = min(1, t / 0.6) * min(1, (4 - t) / 0.6)
            out.append(0.18 * swell * sum(math.sin(math.tau * f * t) + 0.3 * math.sin(math.tau * f * 2 * t) for f in chord) / 3)
    return out
wav("music", music())
print("made", len(os.listdir(TEXTURES)), "textures,", len([f for f in os.listdir(MODELS) if f.endswith(".obj")]), "models and", len(os.listdir(SOUNDS)), "sounds")
