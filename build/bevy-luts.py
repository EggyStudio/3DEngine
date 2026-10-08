#!/usr/bin/env python3
"""Writes Bevy's tonemapping tables into 3DEngine/Shaders/tonemapping, as the engine carries them.

    build/bevy-luts.py [luts folder]

The folder is bevy_core_pipeline 0.19.1's src/tonemapping/luts, by default the one in Cargo's
registry. Each KTX2 file is carried as Bevy has it, its format, its data format descriptor, its
key and value data and its texels, with its one level's Zstandard supercompression swapped for
zlib's, KTX2's scheme 3, which .NET reads with no library of its own. Bevy's info.txt, which says
how each table was made, is copied beside them. Python 3.14 or newer reads Zstandard.
"""
import glob
import os
import struct
import sys
import zlib
from compression import zstd

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "3DEngine", "Shaders", "tonemapping")
NAMES = {"AgX-default_contrast.ktx2": "agx.ktx2", "Blender_-11_12.ktx2": "blender_filmic.ktx2",
         "tony_mc_mapface.ktx2": "tony_mc_mapface.ktx2"}
IDENTIFIER = b"\xabKTX 20\xbb\r\n\x1a\n"
ZSTANDARD, ZLIB = 2, 3


def registry_luts():
    found = glob.glob(os.path.expanduser("~/.cargo/registry/src/*/bevy_core_pipeline-0.19.1/src/tonemapping/luts"))
    if not found:
        sys.exit("No bevy_core_pipeline 0.19.1 in Cargo's registry; name its luts folder.")
    return found[0]


def recompress(data):
    if data[:12] != IDENTIFIER:
        raise ValueError("not a KTX2 file")
    fields = list(struct.unpack_from("<9I", data, 12))
    levels, scheme = fields[7], fields[8]
    if levels != 1 or scheme != ZSTANDARD:
        raise ValueError(f"a table of one level with Zstandard was expected, not {levels} levels with scheme {scheme}")
    dfd_offset, dfd_length, kvd_offset, kvd_length = struct.unpack_from("<4I", data, 48)
    sgd_offset, sgd_length = struct.unpack_from("<2Q", data, 64)
    level_offset, level_length, uncompressed = struct.unpack_from("<3Q", data, 80)
    texels = zstd.decompress(data[level_offset:level_offset + level_length])
    if len(texels) != uncompressed:
        raise ValueError("the level did not decompress to its stated length")
    packed = zlib.compress(texels, 9)

    # The header, the level index, the descriptor and the key and value data, then the level.
    fields[8] = ZLIB
    dfd = data[dfd_offset:dfd_offset + dfd_length]
    kvd = data[kvd_offset:kvd_offset + kvd_length]
    start = 80 + 24
    new_dfd = start
    new_kvd = new_dfd + len(dfd) if kvd_length else 0
    body = dfd + kvd
    level_at = start + len(body)
    out = bytearray(IDENTIFIER)
    out += struct.pack("<9I", *fields)
    out += struct.pack("<4I", new_dfd, len(dfd), new_kvd, len(kvd))
    out += struct.pack("<2Q", 0, 0)
    out += struct.pack("<3Q", level_at, len(packed), uncompressed)
    out += body + packed
    return bytes(out), len(texels)


def main():
    luts = sys.argv[1] if len(sys.argv) > 1 else registry_luts()
    os.makedirs(OUT, exist_ok=True)
    for source, name in NAMES.items():
        with open(os.path.join(luts, source), "rb") as f:
            data = f.read()
        written, texels = recompress(data)
        with open(os.path.join(OUT, name), "wb") as f:
            f.write(written)
        print(f"{name}: {len(data)} bytes as Bevy has it, {texels} bytes of texels, {len(written)} bytes written")
    with open(os.path.join(luts, "info.txt"), "rb") as f:
        info = f.read()
    with open(os.path.join(OUT, "info.txt"), "wb") as f:
        f.write(info)


if __name__ == "__main__":
    main()
