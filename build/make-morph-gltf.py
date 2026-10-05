#!/usr/bin/env python3
"""Writes 3DEngine.Examples/resources/morph.gltf, a strip two units wide and one tall facing +Z,
whose one morph target, "Raise", lifts its top edge a unit, so at full weight the strip is two
tall, with one clip, "pulse", that takes the weight from 0 to 1 and back over a second. It also
writes 3DEngine.Tests/Api/layered-morph.gltf, the same strip held by one bone, "Root", with two
clips that keep the bone still, "rest" at a weight of 0 and "lift" at 1, which a clip on part of
the skeleton blends. The morph tests load both, so their numbers are known."""
import base64, json, struct, os

positions = [(-1, 0, 0), (1, 0, 0), (1, 1, 0), (-1, 1, 0)]
normals = [(0, 0, 1)] * 4
raise_ = [(0, 0, 0), (0, 0, 0), (0, 1, 0), (0, 1, 0)]
indices = [0, 1, 2, 0, 2, 3]
times = [0.0, 0.5, 1.0]
weights = [0.0, 1.0, 0.0]

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
vec3 = lambda vs: b"".join(struct.pack("<3f", *v) for v in vs)
bounds = lambda vs: ([min(v[i] for v in vs) for i in range(3)], [max(v[i] for v in vs) for i in range(3)])
p = add(vec3(positions), FLOAT, 4, "VEC3", 34962, bounds(positions))
n = add(vec3(normals), FLOAT, 4, "VEC3", 34962)
r = add(vec3(raise_), FLOAT, 4, "VEC3", 34962, bounds(raise_))
i = add(b"".join(struct.pack("<H", v) for v in indices), USHORT, 6, "SCALAR", 34963)
t = add(b"".join(struct.pack("<f", v) for v in times), FLOAT, 3, "SCALAR", None, ([0.0], [1.0]))
w = add(b"".join(struct.pack("<f", v) for v in weights), FLOAT, 3, "SCALAR")

gltf = {
    "asset": {"version": "2.0", "generator": "3DEngine build/make-morph-gltf.py"},
    "scene": 0, "scenes": [{"nodes": [0]}],
    "nodes": [{"name": "Strip", "mesh": 0}],
    "meshes": [{"name": "Strip", "weights": [0.0], "extras": {"targetNames": ["Raise"]},
                "primitives": [{"attributes": {"POSITION": p, "NORMAL": n}, "indices": i, "targets": [{"POSITION": r}]}]}],
    "animations": [{"name": "pulse", "samplers": [{"input": t, "output": w, "interpolation": "LINEAR"}],
                    "channels": [{"sampler": 0, "target": {"node": 0, "path": "weights"}}]}],
    "buffers": [{"byteLength": len(blob), "uri": "data:application/octet-stream;base64," + base64.b64encode(bytes(blob)).decode()}],
    "bufferViews": views, "accessors": accessors,
}
out = os.path.join(os.path.dirname(__file__), "..", "3DEngine.Examples", "resources", "morph.gltf")
with open(out, "w") as f:
    json.dump(gltf, f, indent=1)
print(out)

# -- The same strip on a skeleton of one bone, with a clip at each weight.
blob = bytearray(); views = []; accessors = []
p = add(vec3(positions), FLOAT, 4, "VEC3", 34962, bounds(positions))
n = add(vec3(normals), FLOAT, 4, "VEC3", 34962)
r = add(vec3(raise_), FLOAT, 4, "VEC3", 34962, bounds(raise_))
i = add(b"".join(struct.pack("<H", v) for v in indices), USHORT, 6, "SCALAR", 34963)
joints = add(bytes([0, 0, 0, 0] * 4), 5121, 4, "VEC4", 34962)
held = add(b"".join(struct.pack("<4f", 1, 0, 0, 0) for _ in range(4)), FLOAT, 4, "VEC4", 34962)
inverse = add(struct.pack("<16f", 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1), FLOAT, 1, "MAT4")
t = add(b"".join(struct.pack("<f", v) for v in (0.0, 1.0)), FLOAT, 2, "SCALAR", None, ([0.0], [1.0]))
still = add(b"".join(struct.pack("<4f", 0, 0, 0, 1) for _ in range(2)), FLOAT, 2, "VEC4")
zero = add(b"".join(struct.pack("<f", 0.0) for _ in range(2)), FLOAT, 2, "SCALAR")
one = add(b"".join(struct.pack("<f", 1.0) for _ in range(2)), FLOAT, 2, "SCALAR")

def clip(name, weight):
    return {"name": name,
            "samplers": [{"input": t, "output": still, "interpolation": "LINEAR"}, {"input": t, "output": weight, "interpolation": "LINEAR"}],
            "channels": [{"sampler": 0, "target": {"node": 1, "path": "rotation"}}, {"sampler": 1, "target": {"node": 0, "path": "weights"}}]}

gltf = {
    "asset": {"version": "2.0", "generator": "3DEngine build/make-morph-gltf.py"},
    "scene": 0, "scenes": [{"nodes": [0, 1]}],
    "nodes": [{"name": "Strip", "mesh": 0, "skin": 0}, {"name": "Root"}],
    "skins": [{"joints": [1], "inverseBindMatrices": inverse}],
    "meshes": [{"name": "Strip", "weights": [0.0], "extras": {"targetNames": ["Raise"]},
                "primitives": [{"attributes": {"POSITION": p, "NORMAL": n, "JOINTS_0": joints, "WEIGHTS_0": held}, "indices": i,
                                "targets": [{"POSITION": r}]}]}],
    "animations": [clip("rest", zero), clip("lift", one)],
    "buffers": [{"byteLength": len(blob), "uri": "data:application/octet-stream;base64," + base64.b64encode(bytes(blob)).decode()}],
    "bufferViews": views, "accessors": accessors,
}
out = os.path.join(os.path.dirname(__file__), "..", "3DEngine.Tests", "Api", "layered-morph.gltf")
with open(out, "w") as f:
    json.dump(gltf, f, indent=1)
print(out)

