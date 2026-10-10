using System.Numerics;

namespace Engine.Game;

/// <summary>
/// Turns a section into the faces of its blocks that touch air, each corner colored by its surface
/// and shaded by the light and the blocks around it, and the places of the blocks that give off
/// light, which are drawn apart.
/// </summary>
/// <remarks>
/// <para>
/// Each face is two triangles of its own, so a flat floor of 256 blocks is 512 triangles, where
/// greedy meshing would join it into a few. The faces are kept apart so a texture can be laid on
/// each later, and so each corner keeps a color of its own. A section's opaque faces are one mesh,
/// their surfaces told apart by their vertices' colors, which the scene's distance field reads as
/// each block's color as the model pass does, and its see-through faces of glass, ice and water a
/// second, each vertex's alpha its surface's.
/// </para>
/// <para>
/// A corner is shaded as Minecraft's smooth lighting shades it, from the four cells in front of the
/// face that meet at the corner: the one the face looks into, the two beside it and the one across
/// the corner. Its light is the average of those that are open, and its occlusion counts those that
/// are solid, so a corner where two blocks meet is darker, and both come out of the same four reads.
/// The shade multiplies the surface's color into the vertex's, so a face where neither the sky nor
/// a lamp reaches is drawn nearly black and sends nearly none of the light that bounces on, as an
/// unlit cave in Minecraft shows nothing.
/// </para>
/// <para>
/// The mesher reads the section and the blocks and light of the 26 sections around it into one
/// padded copy, so the loop over its blocks never asks the world where a block is. It is used on
/// the main thread, and holds its lists between calls so meshing allocates nothing but the arrays
/// that are uploaded.
/// </para>
/// </remarks>
public sealed class SectionMesher
{
    private const int P = Section.Size + 2;

    private readonly ushort[] _blocks = new ushort[P * P * P];
    private readonly byte[] _light = new byte[P * P * P];
    private readonly Section?[] _around = new Section?[27];
    private ChunkColumn _column = null!;

    /// <summary>Whether a face's corners are darkened where neither the sky nor a block's light reaches them.</summary>
    public bool LightLevels { get; set; } = true;

    /// <summary>Whether a face's corners are darkened where blocks meet around them.</summary>
    public bool CornerShade { get; set; } = true;

    /// <summary>Faces as the vertices of a mesh, the color of each vertex, its surface's darkened by its shade, and their triangles.</summary>
    public sealed class Faces
    {
        public readonly List<ModelVertex> Vertices = [];
        public readonly List<Color> Colors = [];
        public readonly List<uint> Indices = [];

        public void Clear()
        {
            Vertices.Clear();
            Colors.Clear();
            Indices.Clear();
        }
    }

    /// <summary>The last section's faces of opaque blocks.</summary>
    public Faces Solid { get; } = new();

    /// <summary>The last section's faces of see-through blocks, glass, ice and water, each vertex's alpha its surface's.</summary>
    public Faces SeeThrough { get; } = new();

    /// <summary>The blocks of the last section that give off light, by their index in it.</summary>
    public List<(int Index, BlockId Block)> Emitters { get; } = [];

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

    // For each face and corner, the steps in the padded copy from the cell the face looks into to the
    // two cells beside it toward the corner and to the cell across the corner.
    private static readonly int[,] Side1 = new int[6, 4], Side2 = new int[6, 4], Across = new int[6, 4];

    // How much of its color a corner keeps on the screen for each count of the three cells around it
    // left open, as Minecraft's corners darken.
    private static readonly float[] Occlusion = [0.45f, 0.62f, 0.8f, 1];

    static SectionMesher()
    {
        int[] unit = [1, P * P, P];
        for (int face = 0; face < 6; face++)
        {
            var normal = face / 2;
            int first = normal == 0 ? 1 : 0, second = normal == 2 ? 1 : 2;
            for (int corner = 0; corner < 4; corner++)
            {
                var at = Corners[face][corner];
                int s1 = Component(at, first) > 0.5f ? unit[first] : -unit[first];
                int s2 = Component(at, second) > 0.5f ? unit[second] : -unit[second];
                (Side1[face, corner], Side2[face, corner], Across[face, corner]) = (s1, s2, s1 + s2);
            }
        }

        static float Component(Vector3 v, int axis) => axis == 0 ? v.X : axis == 1 ? v.Y : v.Z;
    }

    private static int Pad(int x, int y, int z) => ((y + 1) * P + z + 1) * P + x + 1;

    /// <summary>
    /// The share of the light reaching it that a face keeps at a light level from 0 to 15, in linear
    /// light, nearly all of it down to a few levels from dark and next to none at 0.
    /// </summary>
    /// <remarks>
    /// The light that bounces brings a lamp's falloff itself, so the level marks where no light
    /// reaches rather than dimming the light by distance a second time, as Minecraft's own curve
    /// would, which keeps a quarter at level 9 on the screen and a twentieth of the light.
    /// </remarks>
    public static float Lit(float level)
    {
        var dark = 1 - level / Lighting.Max;
        return 1 - 0.98f * dark * dark * dark * dark;
    }

    /// <summary>Meshes a section into <see cref="Solid"/>, <see cref="SeeThrough"/> and <see cref="Emitters"/>.</summary>
    /// <returns>False when one of the eight columns around it is not loaded, so its edges cannot be told yet, and nothing is meshed.</returns>
    public bool Mesh(VoxelWorld world, SectionKey key)
    {
        Solid.Clear();
        SeeThrough.Clear();
        Emitters.Clear();

        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (!world.TryGetColumn(key.X + dx, key.Z + dz, out var column)) return false;
                if (dx == 0 && dz == 0) _column = column;
                for (int dy = -1; dy <= 1; dy++)
                    _around[(dy + 1) * 9 + (dz + 1) * 3 + dx + 1] = (uint)(key.Y + dy) < ChunkColumn.SectionCount ? column.Sections[key.Y + dy] : null;
            }
        if (_around[13] is not { Filled: > 0 }) return true;

        Fill();
        for (int y = 0; y < Section.Size; y++)
            for (int z = 0; z < Section.Size; z++)
                for (int x = 0; x < Section.Size; x++)
                {
                    var at = Pad(x, y, z);
                    var block = (BlockId)_blocks[at];
                    if (block == BlockId.Air) continue;
                    var info = Blocks.Get(block);
                    if (info.Emits)
                    {
                        Emitters.Add((Section.Index(x, y, z), block));
                        continue;
                    }
                    // Water whose top is open stands a block's eighth low, as Minecraft's does.
                    var low = block == BlockId.Water && _blocks[at + Steps[2]] != (ushort)BlockId.Water;
                    for (int face = 0; face < 6; face++)
                    {
                        if (!Shows(block, info.Kind, (BlockId)_blocks[at + Steps[face]])) continue;
                        Add(face == 2 ? info.Top : face == 3 ? info.Bottom : info.Side, face, at, new Vector3(x, y, z), low);
                    }
                }
        return true;
    }

    // Whether a block's face shows beside a neighbor. An opaque block shows a face to anything that is
    // not opaque, and a see-through block to anything that is neither opaque nor of its own kind, so
    // two panes of glass or two blocks of water meet with no face between them.
    private static bool Shows(BlockId block, BlockKind kind, BlockId neighbor)
    {
        if (Blocks.IsOpaque(neighbor)) return false;
        return kind == BlockKind.Opaque || neighbor != block;
    }

    private void Add(int surface, int face, int at, Vector3 block, bool low)
    {
        // A tinted surface takes its column's grass or foliage color at the block's place.
        var place = (int)block.Z * Section.Size + (int)block.X;
        var look = Surfaces.All[surface];
        var color = look.Under(_column.GrassTint[place], _column.FoliageTint[place]);
        var faces = color.A < 255 ? SeeThrough : Solid;
        var first = (uint)faces.Vertices.Count;
        var front = at + Steps[face];
        Span<int> occlusion = stackalloc int[4];

        for (int i = 0; i < 4; i++)
        {
            int side1 = front + Side1[face, i], side2 = front + Side2[face, i], across = front + Across[face, i];
            bool closed1 = Blocks.IsOpaque((BlockId)_blocks[side1]), closed2 = Blocks.IsOpaque((BlockId)_blocks[side2]), closed3 = Blocks.IsOpaque((BlockId)_blocks[across]);
            // Two blocks on either side close the corner whatever is across it, as Minecraft has it.
            occlusion[i] = closed1 && closed2 ? 0 : 3 - (closed1 ? 1 : 0) - (closed2 ? 1 : 0) - (closed3 ? 1 : 0);

            int sky = _light[front] >> 4, lit = _light[front] & 15, open = 1;
            if (!closed1) Gather(side1, ref sky, ref lit, ref open);
            if (!closed2) Gather(side2, ref sky, ref lit, ref open);
            if (!closed3 && !(closed1 && closed2)) Gather(across, ref sky, ref lit, ref open);

            // The light kept is linear and the vertex's color sRGB, so it is encoded as the color is.
            var kept = LightLevels ? MathF.Pow(Lit((float)Math.Max(sky, lit) / open), 1 / 2.2f) : 1;
            var shade = kept * (CornerShade ? Occlusion[occlusion[i]] : 1);
            var corner = Corners[face][i];
            if (low && corner.Y == 1) corner.Y = 0.875f;
            faces.Vertices.Add(new ModelVertex(block + corner, Normals[face], Uvs[face][i]));
            faces.Colors.Add(new Color((byte)MathF.Round(color.R * shade), (byte)MathF.Round(color.G * shade), (byte)MathF.Round(color.B * shade), color.A));
        }

        // The quad is split along the diagonal whose corners are less closed, so a closed corner
        // darkens one triangle softly rather than both along a line. The choice follows the blocks
        // alone, so a change of light keeps the triangles and changes only the colors.
        var indices = faces.Indices;
        if (occlusion[0] + occlusion[2] < occlusion[1] + occlusion[3])
        {
            indices.Add(first + 1);
            indices.Add(first + 2);
            indices.Add(first + 3);
            indices.Add(first + 1);
            indices.Add(first + 3);
            indices.Add(first);
        }
        else
        {
            indices.Add(first);
            indices.Add(first + 1);
            indices.Add(first + 2);
            indices.Add(first);
            indices.Add(first + 2);
            indices.Add(first + 3);
        }
    }

    private void Gather(int cell, ref int sky, ref int lit, ref int open)
    {
        sky += _light[cell] >> 4;
        lit += _light[cell] & 15;
        open++;
    }

    // The section and the cells of the 26 around it that touch it into the padded copy. Below the
    // world counts as solid and dark, since its faces are never seen, and above it as open sky.
    private void Fill()
    {
        for (int py = -1; py <= Section.Size; py++)
        {
            var sy = py < 0 ? 0 : py == Section.Size ? 2 : 1;
            for (int pz = -1; pz <= Section.Size; pz++)
            {
                var sz = pz < 0 ? 0 : pz == Section.Size ? 2 : 1;
                for (int px = -1; px <= Section.Size; px++)
                {
                    var sx = px < 0 ? 0 : px == Section.Size ? 2 : 1;
                    var at = Pad(px, py, pz);
                    if (_around[sy * 9 + sz * 3 + sx] is { } section)
                    {
                        var index = Section.Index(px & Section.Mask, py & Section.Mask, pz & Section.Mask);
                        _blocks[at] = section.Blocks[index];
                        _light[at] = section.Light[index];
                    }
                    else if (sy == 0)
                    {
                        _blocks[at] = (ushort)BlockId.Bedrock;
                        _light[at] = 0;
                    }
                    else
                    {
                        _blocks[at] = 0;
                        _light[at] = Lighting.Max << 4;
                    }
                }
            }
        }
    }
}
