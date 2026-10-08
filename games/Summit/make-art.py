#!/usr/bin/env python3
"""Makes Summit's art and sound under resources/: hero.gltf, a jointed character 1.8 tall with its
feet at the origin facing +Z, skinned to six bones, with the clips "idle", "run" and "jump"; the
level's models as OBJ files sharing models/level.mtl; and the sounds and the music as WAV files,
synthesized here. Needs only Python."""
import base64, json, math, os, random, struct, wave

here = os.path.join(os.path.dirname(os.path.abspath(__file__)), "resources")
os.makedirs(os.path.join(here, "models"), exist_ok=True)
os.makedirs(os.path.join(here, "sounds"), exist_ok=True)

FACES = [((1,0,0),(0,0,-1),(0,1,0)), ((-1,0,0),(0,0,1),(0,1,0)), ((0,1,0),(1,0,0),(0,0,-1)),
         ((0,-1,0),(1,0,0),(0,0,1)), ((0,0,1),(1,0,0),(0,1,0)), ((0,0,-1),(-1,0,0),(0,1,0))]

def box(lo, hi):
    """The 24 corners, normals and 36 indices of a box from lo to hi, wound counterclockwise."""
    c = [(lo[i] + hi[i]) / 2 for i in range(3)]; h = [(hi[i] - lo[i]) / 2 for i in range(3)]
    pos, nrm, idx = [], [], []
    for n, u, v in FACES:
        base = len(pos)
        for su, sv in ((-1,-1),(1,-1),(1,1),(-1,1)):
            pos.append(tuple(c[i] + (n[i] + u[i]*su + v[i]*sv) * h[i] for i in range(3)))
            nrm.append(n)
        idx += [base, base+1, base+2, base, base+2, base+3]
    return pos, nrm, idx

# -- The hero, its parts rigid on six bones

BONES = [  # name, parent, place in the world at rest
    ("Hips", -1, (0, 0.95, 0)), ("Head", 0, (0, 1.5, 0)),
    ("LegL", 0, (-0.15, 0.9, 0)), ("LegR", 0, (0.15, 0.9, 0)),
    ("ArmL", 0, (-0.33, 1.4, 0)), ("ArmR", 0, (0.33, 1.4, 0)),
]
PARTS = {  # material: (box from, box to, bone)
    "Suit": [((-0.25, 0.88, -0.15), (0.25, 1.46, 0.15), 0),
             ((-0.42, 0.8, -0.08), (-0.26, 1.42, 0.08), 4), ((0.26, 0.8, -0.08), (0.42, 1.42, 0.08), 5)],
    "Skin": [((-0.18, 1.46, -0.18), (0.18, 1.8, 0.18), 1)],
    "Boots": [((-0.24, 0, -0.1), (-0.06, 0.9, 0.12), 2), ((0.06, 0, -0.1), (0.24, 0.9, 0.12), 3)],
    "Visor": [((-0.13, 1.6, 0.17), (0.13, 1.67, 0.21), 1)],
}
MATERIALS = {
    "Suit": {"pbrMetallicRoughness": {"baseColorFactor": [0.15, 0.35, 0.8, 1], "metallicFactor": 0, "roughnessFactor": 0.6}},
    "Skin": {"pbrMetallicRoughness": {"baseColorFactor": [0.95, 0.72, 0.55, 1], "metallicFactor": 0, "roughnessFactor": 0.8}},
    "Boots": {"pbrMetallicRoughness": {"baseColorFactor": [0.12, 0.1, 0.1, 1], "metallicFactor": 0, "roughnessFactor": 0.7}},
    "Visor": {"pbrMetallicRoughness": {"baseColorFactor": [0.1, 0.1, 0.1, 1], "metallicFactor": 0, "roughnessFactor": 0.3},
              "emissiveFactor": [0.3, 1, 1]},
}

blob = bytearray(); views = []; accessors = []
def add(data, component, count, kind, target=None, minmax=None):
    while len(blob) % 4: blob.append(0)
    offset = len(blob); blob.extend(data)
    view = {"buffer": 0, "byteOffset": offset, "byteLength": len(data)}
    if target: view["target"] = target
    views.append(view)
    acc = {"bufferView": len(views) - 1, "componentType": component, "count": count, "type": kind}
    if minmax: acc["min"], acc["max"] = minmax
    accessors.append(acc)
    return len(accessors) - 1

FLOAT, USHORT = 5126, 5123
primitives = []
for m, (name, parts) in enumerate(PARTS.items()):
    pos, nrm, idx, jnt = [], [], [], []
    for lo, hi, bone in parts:
        p, n, i = box(lo, hi)
        idx += [k + len(pos) for k in i]; pos += p; nrm += n; jnt += [(bone, 0, 0, 0)] * len(p)
    a = {
        "POSITION": add(b"".join(struct.pack("<3f", *v) for v in pos), FLOAT, len(pos), "VEC3", 34962,
                        ([min(v[k] for v in pos) for k in range(3)], [max(v[k] for v in pos) for k in range(3)])),
        "NORMAL": add(b"".join(struct.pack("<3f", *v) for v in nrm), FLOAT, len(nrm), "VEC3", 34962),
        "JOINTS_0": add(b"".join(struct.pack("<4H", *v) for v in jnt), USHORT, len(jnt), "VEC4", 34962),
        "WEIGHTS_0": add(b"".join(struct.pack("<4f", 1, 0, 0, 0) for _ in pos), FLOAT, len(pos), "VEC4", 34962),
    }
    primitives.append({"attributes": a, "indices": add(b"".join(struct.pack("<H", v) for v in idx), USHORT, len(idx), "SCALAR", 34963),
                       "material": m})

ibms = add(b"".join(struct.pack("<16f", 1,0,0,0, 0,1,0,0, 0,0,1,0, -p[0],-p[1],-p[2],1) for _, _, p in BONES), FLOAT, len(BONES), "MAT4")

def local(b):
    _, parent, p = BONES[b]
    if parent < 0: return list(p)
    q = BONES[parent][2]
    return [p[k] - q[k] for k in range(3)]

def turn(axis, degrees):
    s = math.sin(math.radians(degrees) / 2)
    return [axis[0] * s, axis[1] * s, axis[2] * s, math.cos(math.radians(degrees) / 2)]

X, Z = (1, 0, 0), (0, 0, 1)
CLIPS = {  # clip: (times, {bone: ("rotation" or "translation", values)})
    "idle": ([0, 1, 2], {
        0: ("translation", [[0, 0.95, 0], [0, 0.93, 0], [0, 0.95, 0]]),
        4: ("rotation", [turn(Z, -4), turn(Z, -8), turn(Z, -4)]),
        5: ("rotation", [turn(Z, 4), turn(Z, 8), turn(Z, 4)]),
    }),
    "run": ([0, 0.15, 0.3, 0.45, 0.6], {
        0: ("translation", [[0, 0.95, 0], [0, 0.99, 0], [0, 0.95, 0], [0, 0.99, 0], [0, 0.95, 0]]),
        2: ("rotation", [turn(X, a) for a in (40, 0, -40, 0, 40)]),
        3: ("rotation", [turn(X, a) for a in (-40, 0, 40, 0, -40)]),
        4: ("rotation", [turn(X, a) for a in (-35, 0, 35, 0, -35)]),
        5: ("rotation", [turn(X, a) for a in (35, 0, -35, 0, 35)]),
    }),
    "jump": ([0, 1], {
        2: ("rotation", [turn(X, -35)] * 2),
        3: ("rotation", [turn(X, 20)] * 2),
        4: ("rotation", [turn(Z, -150)] * 2),
        5: ("rotation", [turn(Z, 150)] * 2),
    }),
}
animations = []
for name, (times, channels) in CLIPS.items():
    t = add(b"".join(struct.pack("<f", v) for v in times), FLOAT, len(times), "SCALAR", None, ([times[0]], [times[-1]]))
    samplers, targets = [], []
    for bone, (path, values) in channels.items():
        fmt = "<4f" if path == "rotation" else "<3f"
        out = add(b"".join(struct.pack(fmt, *v) for v in values), FLOAT, len(values), "VEC4" if path == "rotation" else "VEC3")
        samplers.append({"input": t, "output": out, "interpolation": "LINEAR"})
        targets.append({"sampler": len(samplers) - 1, "target": {"node": bone + 1, "path": path}})
    animations.append({"name": name, "samplers": samplers, "channels": targets})

nodes = [{"name": "Hero", "mesh": 0, "skin": 0}]
for b, (name, parent, _) in enumerate(BONES):
    node = {"name": name, "translation": local(b)}
    children = [c + 1 for c, (_, p, _) in enumerate(BONES) if p == b]
    if children: node["children"] = children
    nodes.append(node)
gltf = {
    "asset": {"version": "2.0", "generator": "games/Summit/make-art.py"},
    "scene": 0, "scenes": [{"nodes": [0, 1]}], "nodes": nodes,
    "meshes": [{"name": "Hero", "primitives": primitives}],
    "materials": [dict(name=n, **v) for n, v in MATERIALS.items()],
    "skins": [{"inverseBindMatrices": ibms, "joints": list(range(1, len(BONES) + 1)), "skeleton": 1}],
    "animations": animations,
    "buffers": [{"byteLength": len(blob), "uri": "data:application/octet-stream;base64," + base64.b64encode(bytes(blob)).decode()}],
    "bufferViews": views, "accessors": accessors,
}
with open(os.path.join(here, "hero.gltf"), "w") as f:
    json.dump(gltf, f, indent=1)

# -- The level's models, boxes by material, with a material file they share

with open(os.path.join(here, "models", "level.mtl"), "w") as f:
    for name, color in (("Grass", (0.33, 0.6, 0.25)), ("Rock", (0.5, 0.48, 0.45)), ("Stone", (0.78, 0.74, 0.66)),
                        ("Wood", (0.55, 0.36, 0.2)), ("Roof", (0.6, 0.2, 0.15)), ("Floor", (0.42, 0.3, 0.22))):
        f.write(f"newmtl {name}\nKd {color[0]} {color[1]} {color[2]}\n\n")

def obj(name, boxes):
    """Writes models/<name>.obj of (material, from, to) boxes."""
    lines = ["mtllib level.mtl"]; count = 0
    for material, lo, hi in boxes:
        pos, nrm, idx = box(lo, hi)
        lines.append(f"usemtl {material}")
        lines += [f"v {p[0]:.3f} {p[1]:.3f} {p[2]:.3f}" for p in pos]
        lines += [f"vn {n[0]} {n[1]} {n[2]}" for n in nrm]
        for k in range(0, len(idx), 3):
            a, b, c = (idx[k + j] + count + 1 for j in range(3))
            lines.append(f"f {a}//{a} {b}//{b} {c}//{c}")
        count += len(pos)
    with open(os.path.join(here, "models", name + ".obj"), "w") as f:
        f.write("\n".join(lines) + "\n")

# The island the level starts on, its top at y 0, with two mounds to climb.
obj("island", [("Rock", (-14, -3, -14), (14, -0.4, 14)), ("Grass", (-14, -0.4, -14), (14, 0, 14)),
               ("Grass", (5, 0, 3), (9, 0.6, 7)), ("Grass", (-10, 0, -4), (-6, 1.2, 0))])
# A stone block to stand on, 3 wide, its top at y 0.
obj("block", [("Stone", (-1.5, -0.6, -1.5), (1.5, 0, 1.5))])
# Steps up, each 0.4 high and 0.8 deep, rising toward -Z from y 0.
obj("steps", [("Stone", (-1.5, 0, -0.8 * (k + 1)), (1.5, 0.4 * (k + 1), -0.8 * k)) for k in range(5)])
# The house on the summit, 8 by 8 inside walls 0.4 thick and 3.2 high, a door 1.6 wide in the
# +Z wall, its floor at y 0, on a rock that hangs below it.
obj("house", [("Rock", (-4.4, -5, -4.4), (4.4, -0.4, 4.4)), ("Floor", (-4.4, -0.4, -4.4), (4.4, 0, 4.4)),
              ("Stone", (-4.4, 0, -4.4), (4.4, 3.2, -4)), ("Stone", (-4.4, 0, -4), (-4, 3.2, 4)),
              ("Stone", (4, 0, -4), (4.4, 3.2, 4)),
              ("Stone", (-4.4, 0, 4), (-0.8, 3.2, 4.4)), ("Stone", (0.8, 0, 4), (4.4, 3.2, 4.4)),
              ("Stone", (-0.8, 2.4, 4), (0.8, 3.2, 4.4)),
              ("Roof", (-4.8, 3.2, -4.8), (4.8, 3.6, 4.8))])
# A wooden plank, 6 long and 1.4 wide, centered, for the platforms that move.
obj("plank", [("Wood", (-3, -0.15, -0.7), (3, 0.15, 0.7))])

# -- Sound, synthesized

RATE = 22050
def write(name, samples):
    with wave.open(os.path.join(here, name), "w") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 30000)) for s in samples))

def tone(freq, seconds, volume=0.5, shape=lambda t: 1):
    n = int(RATE * seconds); phase = 0; out = []
    for k in range(n):
        t = k / n; f = freq(t) if callable(freq) else freq
        phase += 2 * math.pi * f / RATE
        out.append(volume * shape(t) * (math.sin(phase) + 0.3 * math.sin(2 * phase)))
    return out

decay = lambda t: (1 - t) ** 2
write("sounds/jump.wav", tone(lambda t: 300 + 500 * t, 0.16, 0.5, decay))
write("sounds/orb.wav", tone(988, 0.09, 0.45, decay) + tone(1319, 0.3, 0.45, decay))
write("sounds/fall.wav", tone(lambda t: 600 - 450 * t, 0.6, 0.5, lambda t: 1 - t))
write("sounds/win.wav", sum((tone(f, 0.14, 0.45, decay) for f in (523, 659, 784)), []) + tone(1047, 0.5, 0.45, decay))
random.seed(7)
write("sounds/land.wav", [random.uniform(-1, 1) * 0.4 * (1 - k / 1800) ** 3 for k in range(1800)])

# Eight bars at 120 beats a minute, a bass on the beat and a melody in eighths, looping cleanly.
beat = RATE // 2
bass = [110, 110, 87.3, 98, 110, 110, 87.3, 130.8]
melody = [440, 523, 587, 523, 440, 392, 440, 0, 349, 392, 440, 392, 349, 330, 349, 0,
          392, 440, 494, 440, 392, 349, 392, 0, 440, 523, 587, 659, 587, 523, 440, 0] * 2
music = [0.0] * (beat * 32)
for bar, f in enumerate(bass):
    for b in range(4):
        for k, s in enumerate(tone(f, 0.45, 0.3, decay)):
            music[(bar * 4 + b) * beat + k] += s
for e, f in enumerate(melody):
    if f:
        for k, s in enumerate(tone(f, 0.24, 0.18, lambda t: (1 - t) ** 1.5)):
            music[e * beat // 2 + k] += s
write("music.wav", music)
print("Summit's art and sound written to", here)
