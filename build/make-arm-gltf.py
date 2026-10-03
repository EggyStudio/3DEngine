#!/usr/bin/env python3
"""Writes 3DEngine.Examples/resources/arm.gltf, a skinned arm of two boxes and two bones with one
clip, "bend", that turns the upper bone 90 degrees about Z over a second. The examples and the
animation tests load it, so its numbers are known: the arm stands from y 0 to y 2, its elbow is at
y 1, and at the end of the clip the tip at (0, 2, 0) has swung to (-1, 1, 0)."""
import base64, json, math, struct, os

def box(y0, y1, w=0.2):
    faces = [((1,0,0),(0,0,-1),(0,1,0)), ((-1,0,0),(0,0,1),(0,1,0)), ((0,1,0),(1,0,0),(0,0,-1)),
             ((0,-1,0),(1,0,0),(0,0,1)), ((0,0,1),(1,0,0),(0,1,0)), ((0,0,-1),(-1,0,0),(0,1,0))]
    c = (0, (y0 + y1) / 2, 0); h = (w, (y1 - y0) / 2, w)
    pos, nrm, idx = [], [], []
    for n, u, v in faces:
        base = len(pos)
        for su, sv in ((-1,-1),(1,-1),(1,1),(-1,1)):
            pos.append(tuple(c[i] + (n[i] + u[i]*su + v[i]*sv) * h[i] for i in range(3)))
            nrm.append(n)
        idx += [base, base+1, base+2, base, base+2, base+3]
    return pos, nrm, idx

lp, ln, li = box(0, 1)
up, un, ui = box(1, 2)
positions = lp + up
normals = ln + un
indices = li + [i + len(lp) for i in ui]
joints = [(0,0,0,0)] * len(lp) + [(1,0,0,0)] * len(up)
weights = [(1,0,0,0)] * len(positions)

times = [0.0, 0.5, 1.0]
rotations = [(0, 0, math.sin(a / 2), math.cos(a / 2)) for a in (0, math.pi / 4, math.pi / 2)]
ibms = [[1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1], [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,-1,0,1]]

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
p = add(b"".join(struct.pack("<3f", *v) for v in positions), FLOAT, len(positions), "VEC3", 34962,
        ([min(v[i] for v in positions) for i in range(3)], [max(v[i] for v in positions) for i in range(3)]))
n = add(b"".join(struct.pack("<3f", *v) for v in normals), FLOAT, len(normals), "VEC3", 34962)
j = add(b"".join(struct.pack("<4H", *v) for v in joints), USHORT, len(joints), "VEC4", 34962)
w = add(b"".join(struct.pack("<4f", *v) for v in weights), FLOAT, len(weights), "VEC4", 34962)
i = add(b"".join(struct.pack("<H", v) for v in indices), USHORT, len(indices), "SCALAR", 34963)
ib = add(b"".join(struct.pack("<16f", *m) for m in ibms), FLOAT, 2, "MAT4")
t = add(b"".join(struct.pack("<f", v) for v in times), FLOAT, len(times), "SCALAR", None, ([0.0], [1.0]))
r = add(b"".join(struct.pack("<4f", *v) for v in rotations), FLOAT, len(rotations), "VEC4")

gltf = {
    "asset": {"version": "2.0", "generator": "3DEngine build/make-arm-gltf.py"},
    "scene": 0,
    "scenes": [{"nodes": [0, 1]}],
    "nodes": [
        {"name": "Arm", "mesh": 0, "skin": 0},
        {"name": "Shoulder", "children": [2]},
        {"name": "Elbow", "translation": [0, 1, 0]},
    ],
    "meshes": [{"name": "Arm", "primitives": [{"attributes": {"POSITION": p, "NORMAL": n, "JOINTS_0": j, "WEIGHTS_0": w}, "indices": i}]}],
    "skins": [{"inverseBindMatrices": ib, "joints": [1, 2], "skeleton": 1}],
    "animations": [{"name": "bend", "samplers": [{"input": t, "output": r, "interpolation": "LINEAR"}],
                    "channels": [{"sampler": 0, "target": {"node": 2, "path": "rotation"}}]}],
    "buffers": [{"byteLength": len(blob), "uri": "data:application/octet-stream;base64," + base64.b64encode(bytes(blob)).decode()}],
    "bufferViews": views,
    "accessors": accessors,
}
out = os.path.join(os.path.dirname(__file__), "..", "3DEngine.Examples", "resources", "arm.gltf")
with open(out, "w") as f:
    json.dump(gltf, f, indent=1)
print(out)
