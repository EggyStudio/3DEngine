#!/usr/bin/env python3
"""Writes 3DEngine.Tests/Modeling/two-pieces.gltf, which the edit mesh's import is counted on. It
holds a cube a unit wide from the origin, each face its own four vertices and two triangles, in the
material "Stone Wall", and a square a unit wide lying flat and facing up, in the material "Grass",
on a node moved three units along X and scaled two along X and Z. Read into an edit mesh, the cube is
eight vertices and six faces and the square four vertices and one face, from (0, 0, 0) to (5, 1, 2)."""
import base64, json, struct, os

blob = bytearray(); views = []; accessors = []
def add(data, component, count, kind, target, minmax=None):
    while len(blob) % 4: blob.append(0)
    offset = len(blob); blob.extend(data)
    views.append({"buffer": 0, "byteOffset": offset, "byteLength": len(data), "target": target})
    acc = {"bufferView": len(views) - 1, "componentType": component, "count": count, "type": kind}
    if minmax: acc["min"], acc["max"] = minmax
    accessors.append(acc)
    return len(accessors) - 1

FLOAT, UINT, VERTICES, INDICES = 5126, 5125, 34962, 34963
vec = lambda vs, n: b"".join(struct.pack(f"<{n}f", *v) for v in vs)

def primitive(faces):
    """Each face's four corners, counterclockwise seen from outside, with its normal."""
    positions, normals, uvs, indices = [], [], [], []
    for corners, normal in faces:
        first = len(positions)
        positions += corners; normals += [normal] * 4; uvs += [(0, 0), (1, 0), (1, 1), (0, 1)]
        indices += [first, first + 1, first + 2, first, first + 2, first + 3]
    low = [min(p[i] for p in positions) for i in range(3)]
    high = [max(p[i] for p in positions) for i in range(3)]
    return {"attributes": {
                "POSITION": add(vec(positions, 3), FLOAT, len(positions), "VEC3", VERTICES, (low, high)),
                "NORMAL": add(vec(normals, 3), FLOAT, len(normals), "VEC3", VERTICES),
                "TEXCOORD_0": add(vec(uvs, 2), FLOAT, len(uvs), "VEC2", VERTICES)},
            "indices": add(struct.pack(f"<{len(indices)}I", *indices), UINT, len(indices), "SCALAR", INDICES)}

cube = [
    ([(0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1)], (0, 0, 1)),
    ([(1, 0, 0), (0, 0, 0), (0, 1, 0), (1, 1, 0)], (0, 0, -1)),
    ([(1, 0, 1), (1, 0, 0), (1, 1, 0), (1, 1, 1)], (1, 0, 0)),
    ([(0, 0, 0), (0, 0, 1), (0, 1, 1), (0, 1, 0)], (-1, 0, 0)),
    ([(0, 1, 1), (1, 1, 1), (1, 1, 0), (0, 1, 0)], (0, 1, 0)),
    ([(0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1)], (0, -1, 0)),
]
square = [([(0, 0, 1), (1, 0, 1), (1, 0, 0), (0, 0, 0)], (0, 1, 0))]

block = primitive(cube); block["material"] = 0
floor = primitive(square); floor["material"] = 1
gltf = {
    "asset": {"version": "2.0", "generator": "build/make-modeling-gltf.py"},
    "scene": 0,
    "scenes": [{"nodes": [0, 1]}],
    "nodes": [{"name": "Block", "mesh": 0}, {"name": "Floor", "mesh": 1, "translation": [3, 0, 0], "scale": [2, 1, 2]}],
    "meshes": [{"name": "Block", "primitives": [block]}, {"name": "Floor", "primitives": [floor]}],
    "materials": [{"name": "Stone Wall", "pbrMetallicRoughness": {"baseColorFactor": [0.5, 0.5, 0.5, 1]}},
                  {"name": "Grass", "pbrMetallicRoughness": {"baseColorFactor": [0.2, 0.6, 0.2, 1]}}],
    "buffers": [{"byteLength": len(blob), "uri": "data:application/octet-stream;base64," + base64.b64encode(bytes(blob)).decode()}],
    "bufferViews": views,
    "accessors": accessors,
}
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
with open(os.path.join(root, "3DEngine.Tests", "Modeling", "two-pieces.gltf"), "w") as f:
    json.dump(gltf, f, indent=1)
    f.write("\n")
