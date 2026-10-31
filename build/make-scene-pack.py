#!/usr/bin/env python3
"""Makes the engine's scene pack from one BevyCSharp publishes, its textures' zstd undone.

    build/make-scene-pack.py <bevy.pack> <out.pack> [--sha256 HASH] [--manifest scenes/NAME.json
        --url URL --title T --source S --license L --attribution A --model FILE]

A BevyCSharp pack holds a glTF scene whose textures are KTX2 files of the blocks a GPU samples
without unpacking, BC7 for a color, BC5 for a normal map and BC1 for the rest, each level
supercompressed with zstd, which Bevy's loader undoes. .NET's library holds no zstd, so this undoes
it here, once, and writes each KTX2 again with its levels as they are on the GPU, the blocks kept
byte for byte, so the engine reads the file and hands the GPU the levels with no decoder of its
own and the same memory as BevyCSharp's. Every other file is copied as it is.

A pack is an index followed by the files' bytes: the eight bytes BCSPACK and a zero, the format's
version and the number of files as little-endian 32-bit integers, then for each file its path (a
16-bit length and UTF-8) and where its bytes start and how many there are as little-endian 64-bit
integers counted from the start of the pack. The files keep the source pack's order, so the same
source makes the same pack byte for byte.

--sha256 refuses a source whose hash is not the one given. With --manifest the pack's size and
hash are written into a manifest naming --url as where the pack is published.
"""
import argparse
import hashlib
import json
import struct
import sys
from compression import zstd

MAGIC = b"BCSPACK\0"
KTX2 = b"\xabKTX 20\xbb\r\n\x1a\n"
# The Vulkan formats of the blocks a pack holds and the bytes of a block of each, the BC1 forms 8
# and the rest 16.
BLOCK_BYTES = {131: 8, 132: 8, 133: 8, 134: 8, 141: 16, 142: 16, 145: 16, 146: 16}


def read_pack(path):
    """The files of a pack, each its path and bytes, in the pack's order."""
    with open(path, "rb") as pack:
        if pack.read(8) != MAGIC:
            sys.exit(f"{path} is not a pack, its first bytes not BCSPACK")
        version, count = struct.unpack("<ii", pack.read(8))
        if version != 1:
            sys.exit(f"{path} is a pack of version {version}, where this reads version 1")
        index = []
        for _ in range(count):
            (length,) = struct.unpack("<H", pack.read(2))
            name = pack.read(length).decode("utf-8")
            offset, size = struct.unpack("<qq", pack.read(16))
            index.append((name, offset, size))
        files = []
        for name, offset, size in index:
            pack.seek(offset)
            files.append((name, pack.read(size)))
        return files


def write_pack(path, files):
    """Writes files as a pack, its index first and then the bytes in the same order."""
    index_size = 16 + sum(2 + len(name.encode("utf-8")) + 16 for name, _ in files)
    with open(path, "wb") as pack:
        pack.write(MAGIC + struct.pack("<ii", 1, len(files)))
        offset = index_size
        for name, data in files:
            encoded = name.encode("utf-8")
            pack.write(struct.pack("<H", len(encoded)) + encoded + struct.pack("<qq", offset, len(data)))
            offset += len(data)
        for _, data in files:
            pack.write(data)


def unsupercompressed(name, data):
    """A KTX2 file's bytes with its zstd undone, the level index, the levels' places and the data
    format descriptor's bytes a block written for levels as they are, and no global data."""
    if data[:12] != KTX2:
        sys.exit(f"{name} is not a KTX2 file")
    vk_format, type_size, width, height, depth, layers, faces, levels, scheme = struct.unpack("<9I", data[12:48])
    if scheme not in (0, 2):
        sys.exit(f"{name} is supercompressed by scheme {scheme}, where this undoes zstd (2) alone")
    if vk_format not in BLOCK_BYTES:
        sys.exit(f"{name} holds Vulkan format {vk_format}, where this keeps BC1, BC5 and BC7")
    dfd_offset, dfd_length, kvd_offset, kvd_length = struct.unpack("<4I", data[48:64])
    level_index = [struct.unpack("<3Q", data[80 + 24 * i:104 + 24 * i]) for i in range(levels)]
    raw = []
    for i, (offset, length, size) in enumerate(level_index):
        level = data[offset:offset + length]
        level = zstd.decompress(level) if scheme == 2 else level
        if len(level) != size:
            sys.exit(f"{name}'s level {i} unpacks to {len(level)} bytes, where its index says {size}")
        raw.append(level)
    dfd = bytearray(data[dfd_offset:dfd_offset + dfd_length])
    # The basic block's bytes of plane 0, after the descriptor's total size and the block's first 16
    # bytes, zero where the levels are supercompressed and a block's bytes where they are not.
    dfd[20] = BLOCK_BYTES[vk_format]
    kvd = data[kvd_offset:kvd_offset + kvd_length]

    # The header, the index and the level index, then the descriptor, the key and value data and
    # the levels, the smallest first, each placed on a multiple of a block's bytes.
    head = 80 + 24 * levels
    dfd_at = head
    kvd_at = dfd_at + len(dfd) if kvd else 0
    end = (kvd_at + len(kvd)) if kvd else dfd_at + len(dfd)
    align = BLOCK_BYTES[vk_format]
    places = [0] * levels
    for i in reversed(range(levels)):
        end = (end + align - 1) // align * align
        places[i] = end
        end += len(raw[i])
    out = bytearray(end)
    out[:48] = data[:12] + struct.pack("<9I", vk_format, type_size, width, height, depth, layers, faces, levels, 0)
    out[48:80] = struct.pack("<4I2Q", dfd_at, len(dfd), kvd_at, len(kvd), 0, 0)
    for i in range(levels):
        out[80 + 24 * i:104 + 24 * i] = struct.pack("<3Q", places[i], len(raw[i]), len(raw[i]))
        out[places[i]:places[i] + len(raw[i])] = raw[i]
    out[dfd_at:dfd_at + len(dfd)] = dfd
    if kvd:
        out[kvd_at:kvd_at + len(kvd)] = kvd
    return bytes(out)


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("source")
    parser.add_argument("out")
    parser.add_argument("--sha256")
    parser.add_argument("--manifest")
    for field in ("url", "title", "source-page", "license", "attribution", "model"):
        parser.add_argument(f"--{field}")
    args = parser.parse_args()

    if args.sha256:
        digest = hashlib.sha256(open(args.source, "rb").read()).hexdigest()
        if digest != args.sha256:
            sys.exit(f"{args.source} hashes to {digest}, where {args.sha256} was given")
    files = [(name, unsupercompressed(name, data) if name.endswith(".ktx2") else data) for name, data in read_pack(args.source)]
    write_pack(args.out, files)
    data = open(args.out, "rb").read()
    size, digest = len(data), hashlib.sha256(data).hexdigest()
    print(f"wrote {args.out}, {len(files)} files, {size:,} bytes, sha256 {digest}")
    if args.manifest:
        manifest = {"title": args.title, "source": args.source_page, "license": args.license, "attribution": args.attribution,
                    "url": args.url, "size": size, "sha256": digest, "model": args.model}
        with open(args.manifest, "w") as out:
            json.dump(manifest, out, indent=2)
            out.write("\n")
        print(f"wrote {args.manifest}")


if __name__ == "__main__":
    main()
