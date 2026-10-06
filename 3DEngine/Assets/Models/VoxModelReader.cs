// MagicaVoxel's .vox files, read as raylib's vox_loader.h reads them, written again in C#. Altered
// from the original, which is C, to build one mesh with 32-bit indices where it builds several of
// 16-bit ones.
//
// The MIT License (MIT)
// Copyright (c) 2021 Johann Nadalutti.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
// associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute,
// sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
// NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
// DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
// OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Buffers.Binary;
using System.Numerics;

namespace Engine;

/// <summary>
/// Reads a MagicaVoxel <c>.vox</c> file into a scene of one mesh, a quad for each face of a voxel
/// that no other voxel covers, colored from the file's palette, as raylib's <c>LoadModel</c> reads one.
/// </summary>
/// <remarks>
/// A voxel is a quarter of a unit across, MagicaVoxel's z up is turned to y up, and the volume is
/// rounded up to sixteen voxels each way, as raylib's loader keeps it in chunks of sixteen, which
/// moves a model whose depth is not a multiple of sixteen along z as raylib's is moved. A file with
/// several models keeps its last, and one with no palette colors its voxels clear black, as raylib's does.
/// </remarks>
internal static class VoxModelReader
{
    private const int ChunkSize = 16;
    private const float VoxelSize = 0.25f;

    // A cube's corners, and each face's four, counterclockwise seen from outside, with its normal.
    private static readonly Vector3[] Corners =
    [
        new(0, 0, 0), new(1, 0, 0), new(0, 1, 0), new(1, 1, 0),
        new(0, 0, 1), new(1, 0, 1), new(0, 1, 1), new(1, 1, 1),
    ];

    private static readonly int[][] Faces =
    [
        [0, 2, 6, 4], [5, 7, 3, 1], [0, 4, 5, 1], [6, 2, 3, 7], [1, 3, 2, 0], [4, 6, 7, 5],
    ];

    private static readonly Vector3[] Normals =
    [
        -Vector3.UnitX, Vector3.UnitX, -Vector3.UnitY, Vector3.UnitY, -Vector3.UnitZ, Vector3.UnitZ,
    ];

    /// <summary>Reads a file's bytes into a scene.</summary>
    /// <exception cref="InvalidOperationException">The bytes are not a MagicaVoxel file of version 150 or 200.</exception>
    public static Scene Read(ReadOnlySpan<byte> data, string name)
    {
        if (data.Length < 8 || !data[..4].SequenceEqual("VOX "u8))
            throw new InvalidOperationException("it is not a MagicaVoxel file");
        var version = BinaryPrimitives.ReadUInt32LittleEndian(data[4..]);
        if (version is not (150 or 200))
            throw new InvalidOperationException($"it is MagicaVoxel version {version}, where 150 and 200 are read");

        var palette = new Color[256];
        byte[] voxels = [];
        int sizeX = 0, sizeY = 0, sizeZ = 0;
        var at = 8;
        while (data.Length - at >= 12)
        {
            var id = data.Slice(at, 4);
            var contentSize = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(data[(at + 4)..]), int.MaxValue);
            at += 12;
            if (id.SequenceEqual("SIZE"u8))
            {
                if (data.Length - at < 12) break;
                // MagicaVoxel's y and z swapped for y up, each rounded up to whole chunks.
                static int Round(uint size) => (int)((size + ChunkSize - 1) / ChunkSize * ChunkSize);
                sizeX = Round(BinaryPrimitives.ReadUInt32LittleEndian(data[at..]));
                sizeZ = Round(BinaryPrimitives.ReadUInt32LittleEndian(data[(at + 4)..]));
                sizeY = Round(BinaryPrimitives.ReadUInt32LittleEndian(data[(at + 8)..]));
                voxels = new byte[sizeX * sizeY * sizeZ];
                at += 12;
            }
            else if (id.SequenceEqual("XYZI"u8))
            {
                if (data.Length - at < 4) break;
                var count = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(data[at..]), (uint)((data.Length - at - 4) / 4));
                at += 4;
                for (int i = 0; i < count; i++, at += 4)
                {
                    int vx = data[at], vy = data[at + 1], vz = data[at + 2];
                    Set(vx, vz, sizeZ - vy - 1, data[at + 3]);
                }
            }
            else if (id.SequenceEqual("RGBA"u8))
            {
                if (data.Length - at < 255 * 4) break;
                for (int i = 0; i < 255; i++, at += 4)
                    palette[i + 1] = new Color(data[at], data[at + 1], data[at + 2], data[at + 3]);
            }
            else
            {
                if (contentSize > data.Length - at) break;
                at += contentSize;
            }
        }

        void Set(int x, int y, int z, byte value)
        {
            if (x < 0 || y < 0 || z < 0 || x >= sizeX || y >= sizeY || z >= sizeZ) return;
            voxels[(x * sizeZ + z) * sizeY + y] = value;
        }
        byte Get(int x, int y, int z) =>
            x < 0 || y < 0 || z < 0 || x >= sizeX || y >= sizeY || z >= sizeZ ? (byte)0 : voxels[(x * sizeZ + z) * sizeY + y];

        var positions = new List<Vector3>();
        var normals = new List<Vector3>();
        var colors = new List<Vector4>();
        var indices = new List<int>();
        for (int x = 0; x < sizeX; x++)
        for (int z = 0; z < sizeZ; z++)
        for (int y = 0; y < sizeY; y++)
        {
            var material = Get(x, y, z);
            if (material == 0) continue;
            // A face shows where the voxel beside it is empty, -x, +x, -y, +y, -z, +z.
            ReadOnlySpan<bool> open =
            [
                Get(x - 1, y, z) == 0, Get(x + 1, y, z) == 0, Get(x, y - 1, z) == 0,
                Get(x, y + 1, z) == 0, Get(x, y, z - 1) == 0, Get(x, y, z + 1) == 0,
            ];
            var color = palette[material];
            var color4 = new Vector4(color.R, color.G, color.B, color.A) / 255f;
            for (int face = 0; face < 6; face++)
            {
                if (!open[face]) continue;
                var first = positions.Count;
                foreach (var corner in Faces[face])
                {
                    positions.Add((Corners[corner] + new Vector3(x, y, z)) * VoxelSize);
                    normals.Add(Normals[face]);
                    colors.Add(color4);
                }
                indices.AddRange([first, first + 2, first + 1, first, first + 3, first + 2]);
            }
        }

        var mesh = new SceneMeshPayload
        {
            Name = name,
            Positions = [.. positions],
            Normals = [.. normals],
            Colors = [.. colors],
            Indices = [.. indices],
            LocalBounds = SceneBounds.FromPositions([.. positions]),
        };
        var node = new SceneNode { Name = name, SourcePath = "/" + name };
        node.Components.Add(mesh);
        var scene = new Scene { Name = name };
        scene.Roots.Add(node);
        return scene;
    }
}
