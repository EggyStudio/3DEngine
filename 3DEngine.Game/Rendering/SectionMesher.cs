using System.Numerics;

namespace Engine.Game;

/// <summary>
/// Turns a section into the faces of its blocks that touch air, one list of faces for each surface,
/// and the places of the blocks that give off light, which are drawn apart.
/// </summary>
/// <remarks>
/// Each face is two triangles of its own, so a flat floor of 256 blocks is 512 triangles, where
/// greedy meshing would join it into a few. The faces are kept apart so a texture can be laid on
/// each later. The mesher reads the section and the layer of each of its six neighbors across
/// its faces into one padded copy, so the loop over its blocks never asks the world where a block
/// is. It is used on the main thread, and holds its lists between calls so meshing allocates
/// nothing but the arrays that are uploaded.
/// </remarks>
public sealed class SectionMesher
{
    private const int P = Section.Size + 2;

    /// <summary>The faces of one surface, as the vertices and triangle indices of a mesh.</summary>
    public sealed class Faces
    {
        public readonly List<ModelVertex> Vertices = [];
        public readonly List<uint> Indices = [];
    }

    private readonly ushort[] _padded = new ushort[P * P * P];
    private readonly Faces[] _faces = [.. Surfaces.All.Select(_ => new Faces())];

    /// <summary>The surfaces the last section showed a face of.</summary>
    public List<int> Shown { get; } = [];

    /// <summary>The blocks of the last section that give off light, by their index in it.</summary>
    public List<(int Index, BlockId Block)> Emitters { get; } = [];

    public Faces Of(int surface) => _faces[surface];

    // The six directions in the order +x, -x, +y, -y, +z, -z, each face's corners counterclockwise
    // seen from outside the block, which is the side the model pass draws, with their texture
    // coordinates as GenMeshCube lays them.
    private static readonly Vector3[] Normals = [Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ];
    private static readonly int[] Steps = [1, -1, P * P, -P * P, P, -P];
    private static readonly Vector3[][] Corners =
    [
        [new(1, 0, 0), new(1, 1, 0), new(1, 1, 1), new(1, 0, 1)],
        [new(0, 0, 0), new(0, 0, 1), new(0, 1, 1), new(0, 1, 0)],
        [new(0, 1, 0), new(0, 1, 1), new(1, 1, 1), new(1, 1, 0)],
        [new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1)],
        [new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)],
        [new(0, 0, 0), new(0, 1, 0), new(1, 1, 0), new(1, 0, 0)],
    ];
    private static readonly Vector2[][] Uvs =
    [
        [new(1, 0), new(1, 1), new(0, 1), new(0, 0)],
        [new(0, 0), new(1, 0), new(1, 1), new(0, 1)],
        [new(0, 1), new(0, 0), new(1, 0), new(1, 1)],
        [new(1, 1), new(0, 1), new(0, 0), new(1, 0)],
        [new(0, 0), new(1, 0), new(1, 1), new(0, 1)],
        [new(1, 0), new(1, 1), new(0, 1), new(0, 0)],
    ];

    private static int Pad(int x, int y, int z) => ((y + 1) * P + z + 1) * P + x + 1;

    /// <summary>Meshes a section into <see cref="Shown"/>, <see cref="Of"/> and <see cref="Emitters"/>.</summary>
    /// <returns>False when a column beside it is not loaded, so its edge cannot be told yet, and nothing is meshed.</returns>
    public bool Mesh(VoxelWorld world, SectionKey key)
    {
        foreach (var surface in Shown)
        {
            _faces[surface].Vertices.Clear();
            _faces[surface].Indices.Clear();
        }
        Shown.Clear();
        Emitters.Clear();

        if (!world.TryGetColumn(key.X, key.Z, out var column)
            || !world.TryGetColumn(key.X - 1, key.Z, out var west) || !world.TryGetColumn(key.X + 1, key.Z, out var east)
            || !world.TryGetColumn(key.X, key.Z - 1, out var north) || !world.TryGetColumn(key.X, key.Z + 1, out var south))
            return false;
        if (column.Sections[key.Y] is not { Filled: > 0 } section) return true;

        Fill(section, column, west, east, north, south, key.Y);
        for (int y = 0; y < Section.Size; y++)
            for (int z = 0; z < Section.Size; z++)
                for (int x = 0; x < Section.Size; x++)
                {
                    var at = Pad(x, y, z);
                    var block = (BlockId)_padded[at];
                    if (block == BlockId.Air) continue;
                    var info = Blocks.Get(block);
                    if (info.Emits)
                    {
                        Emitters.Add((Section.Index(x, y, z), block));
                        continue;
                    }
                    for (int face = 0; face < 6; face++)
                    {
                        // Every block but air hides the face beside it, an emitter's cube included.
                        if (_padded[at + Steps[face]] != 0) continue;
                        Add(face == 2 ? info.Top : face == 3 ? info.Bottom : info.Side, face, new Vector3(x, y, z));
                    }
                }
        return true;
    }

    private void Add(int surface, int face, Vector3 block)
    {
        var faces = _faces[surface];
        if (faces.Vertices.Count == 0) Shown.Add(surface);
        var first = (uint)faces.Vertices.Count;
        for (int i = 0; i < 4; i++) faces.Vertices.Add(new ModelVertex(block + Corners[face][i], Normals[face], Uvs[face][i]));
        var indices = faces.Indices;
        indices.Add(first);
        indices.Add(first + 1);
        indices.Add(first + 2);
        indices.Add(first);
        indices.Add(first + 2);
        indices.Add(first + 3);
    }

    // The section into the middle of the padded copy, and the layer of each neighbor across a face
    // into the copy's border. Below the world counts as solid, since its faces are never seen, and
    // above the world as air.
    private void Fill(Section section, ChunkColumn column, ChunkColumn west, ChunkColumn east, ChunkColumn north, ChunkColumn south, int sectionY)
    {
        Array.Clear(_padded);
        var blocks = section.Blocks;
        for (int y = 0; y < Section.Size; y++)
            for (int z = 0; z < Section.Size; z++)
                blocks.AsSpan(Section.Index(0, y, z), Section.Size).CopyTo(_padded.AsSpan(Pad(0, y, z), Section.Size));

        var below = sectionY > 0 ? column.Sections[sectionY - 1] : null;
        var above = sectionY < ChunkColumn.SectionCount - 1 ? column.Sections[sectionY + 1] : null;
        var westSection = west.Sections[sectionY];
        var eastSection = east.Sections[sectionY];
        var northSection = north.Sections[sectionY];
        var southSection = south.Sections[sectionY];
        for (int a = 0; a < Section.Size; a++)
            for (int b = 0; b < Section.Size; b++)
            {
                _padded[Pad(a, -1, b)] = sectionY == 0 ? (ushort)BlockId.Bedrock : below?.Blocks[Section.Index(a, Section.Mask, b)] ?? 0;
                _padded[Pad(a, Section.Size, b)] = above?.Blocks[Section.Index(a, 0, b)] ?? 0;
                _padded[Pad(-1, a, b)] = westSection?.Blocks[Section.Index(Section.Mask, a, b)] ?? 0;
                _padded[Pad(Section.Size, a, b)] = eastSection?.Blocks[Section.Index(0, a, b)] ?? 0;
                _padded[Pad(a, b, -1)] = northSection?.Blocks[Section.Index(a, b, Section.Mask)] ?? 0;
                _padded[Pad(a, b, Section.Size)] = southSection?.Blocks[Section.Index(a, b, 0)] ?? 0;
            }
    }
}
