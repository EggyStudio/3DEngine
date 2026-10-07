#!/usr/bin/env python3
"""Writes resources/jelly.gltf, the runner of games/Jelly: a round blob of jelly a unit high,
standing on y 0 and facing -Z, with two eyes, and two morph targets on both meshes, "Squash",
which presses it to six tenths of its height and a quarter wider, and "Stretch", which draws it
to a third taller and narrower. The game sets the two weights as it runs, jumps and lands. Run
again after changing the shapes."""
import base64, json, math, os, struct

def sphere(cx, cy, cz, r, rings, slices):
    """A sphere's positions, normals and triangles, its poles on y."""
    positions, normals, indices = [], [], []
    for i in range(rings + 1):
        theta = math.pi * i / rings
        for j in range(slices + 1):
            phi = 2 * math.pi * j / slices
            n = (math.sin(theta) * math.cos(phi), math.cos(theta), math.sin(theta) * math.sin(phi))
            normals.append(n)
            positions.append((cx + r * n[0], cy + r * n[1], cz + r * n[2]))
    # Wound counterclockwise seen from outside, glTF's front.
    for i in range(rings):
        for j in range(slices):
            a, b = i * (slices + 1) + j, (i + 1) * (slices + 1) + j
            indices += [a, a + 1, b, a + 1, b + 1, b]
    return positions, normals, indices

# The shapes a target moves a point to, from the ground up, so the blob stays standing on y 0.
squash = lambda p: (p[0] * 1.25, p[1] * 0.6, p[2] * 1.25)
stretch = lambda p: (p[0] * 0.85, p[1] * 1.35, p[2] * 0.85)

body = sphere(0, 0.5, 0, 0.5, 16, 24)
left, right = sphere(-0.17, 0.66, -0.43, 0.08, 6, 10), sphere(0.17, 0.66, -0.43, 0.08, 6, 10)
eyes = (left[0] + right[0], left[1] + right[1], left[2] + [i + len(left[0]) for i in right[2]])

blob = bytearray(); views = []; accessors = []
FLOAT, USHORT = 5126, 5123
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
vec3 = lambda vs: b"".join(struct.pack("<3f", *v) for v in vs)
bounds = lambda vs: ([min(v[i] for v in vs) for i in range(3)], [max(v[i] for v in vs) for i in range(3)])

def mesh(name, shape, material):
    positions, normals, indices = shape
    p = add(vec3(positions), FLOAT, len(positions), "VEC3", 34962, bounds(positions))
    n = add(vec3(normals), FLOAT, len(normals), "VEC3", 34962)
    i = add(b"".join(struct.pack("<H", v) for v in indices), USHORT, len(indices), "SCALAR", 34963)
    targets = []
    for move in (squash, stretch):
        offsets = [tuple(m - o for m, o in zip(move(v), v)) for v in positions]
        targets.append({"POSITION": add(vec3(offsets), FLOAT, len(offsets), "VEC3", 34962, bounds(offsets))})
    return {"name": name, "weights": [0.0, 0.0], "extras": {"targetNames": ["Squash", "Stretch"]},
            "primitives": [{"attributes": {"POSITION": p, "NORMAL": n}, "indices": i, "material": material, "targets": targets}]}

meshes = [mesh("Body", body, 0), mesh("Eyes", eyes, 1)]
gltf = {
    "asset": {"version": "2.0", "generator": "games/Jelly/make-jelly.py"},
    "scene": 0, "scenes": [{"nodes": [0, 1]}],
    "nodes": [{"name": "Body", "mesh": 0}, {"name": "Eyes", "mesh": 1}],
    "meshes": meshes,
    "materials": [
        {"name": "Jelly", "pbrMetallicRoughness": {"baseColorFactor": [0.25, 0.85, 0.45, 1], "metallicFactor": 0, "roughnessFactor": 0.25}},
        {"name": "Eye", "pbrMetallicRoughness": {"baseColorFactor": [0.05, 0.05, 0.08, 1], "metallicFactor": 0, "roughnessFactor": 0.1}},
    ],
    "buffers": [{"byteLength": len(blob), "uri": "data:application/octet-stream;base64," + base64.b64encode(bytes(blob)).decode()}],
    "bufferViews": views, "accessors": accessors,
}
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "resources", "jelly.gltf")
with open(out, "w") as f:
    json.dump(gltf, f)
print(out)
