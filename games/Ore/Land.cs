using System.Numerics;
using Engine;
using static Engine.E3D;

/// <summary>The landscape, its heights from a value noise and the blocks each column holds.</summary>
public static class Land
{
    public const int Size = 48;

    // A value from 0 to 1 at each whole point, and between them the four around it blended smoothly.
    static float Hash(int x, int z)
    {
        var h = (uint)(x * 374761393 + z * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return (h ^ (h >> 16)) / (float)uint.MaxValue;
    }

    static float Noise(float x, float z)
    {
        int ix = (int)MathF.Floor(x), iz = (int)MathF.Floor(z);
        float fx = x - ix, fz = z - iz, sx = fx * fx * (3 - 2 * fx), sz = fz * fz * (3 - 2 * fz);
        var top = float.Lerp(Hash(ix, iz), Hash(ix + 1, iz), sx);
        return float.Lerp(top, float.Lerp(Hash(ix, iz + 1), Hash(ix + 1, iz + 1), sx), sz);
    }

    /// <summary>A column's height, 1 to 12 blocks, from hills twelve blocks across and smaller ones on them.</summary>
    public static int Height(int x, int z) => 1 + (int)(11.99f * (0.7f * Noise(x / 12f, z / 12f) + 0.3f * Noise(x / 5f + 40, z / 5f)));

    enum Kind { Grass, Dirt, Stone, Ore }

    static readonly Color[] Colors = [new(95, 159, 53), new(134, 96, 67), new(128, 128, 132), new(255, 160, 70)];

    // The height from which a column is bare rock, the hilltops, where ore shows.
    const int Rock = 9;

    // Grass on top of a column lower than the rock, two blocks of dirt under it and stone below, a
    // column as high as the rock or higher stone throughout, and ore in a stone block where its
    // hash says so.
    static Kind Block(int[,] heights, int x, int y, int z)
    {
        var h = heights[x, z];
        if (h < Rock && y == h - 1) return Kind.Grass;
        if (h < Rock && y >= h - 3) return Kind.Dirt;
        return Hash(x * 31 + y * 17, z * 13 + y * 7) < 0.12f ? Kind.Ore : Kind.Stone;
    }

    static bool Air(int[,] heights, int x, int y, int z) => x < 0 || z < 0 || x >= Size || z >= Size || y >= heights[x, z];

    // Each way a face can look, with two edges along it whose cross product is that way, so its
    // corners taken in their order wind counterclockwise seen from outside.
    static readonly (Vector3 Normal, Vector3 U, Vector3 V)[] Faces =
    [
        (Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ), (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY),
        (Vector3.UnitY, Vector3.UnitZ, Vector3.UnitX), (-Vector3.UnitY, Vector3.UnitX, Vector3.UnitZ),
        (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY), (-Vector3.UnitZ, Vector3.UnitY, Vector3.UnitX),
    ];

    /// <summary>
    /// The faces of the landscape's blocks that the air touches, the ore's alone or every other
    /// block's, each block's color at its vertices, since global illumination reads a mesh's colors.
    /// </summary>
    public static ModelMesh Mesh(int[,] heights, bool ore)
    {
        var (vertices, colors, indices) = (new List<ModelVertex>(), new List<Color>(), new List<uint>());
        for (int x = 0; x < Size; x++)
            for (int z = 0; z < Size; z++)
                for (int y = 0; y < heights[x, z]; y++)
                {
                    var kind = Block(heights, x, y, z);
                    if ((kind == Kind.Ore) != ore) continue;
                    foreach (var (normal, u, v) in Faces)
                    {
                        if (!Air(heights, x + (int)normal.X, y + (int)normal.Y, z + (int)normal.Z)) continue;
                        var corner = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) + (normal - u - v) * 0.5f;
                        var first = (uint)vertices.Count;
                        foreach (var at in new[] { corner, corner + u, corner + u + v, corner + v })
                        {
                            vertices.Add(new ModelVertex(at, normal, Vector2.Zero));
                            colors.Add(Colors[(int)kind]);
                        }
                        indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
                    }
                }
        return UploadMesh(vertices.ToArray(), indices.ToArray(), colors.ToArray(), null);
    }

    /// <summary>The feet of someone standing on top of a column.</summary>
    public static Vector3 Top(int[,] heights, int x, int z)
    {
        (x, z) = (Math.Clamp(x, 0, Size - 1), Math.Clamp(z, 0, Size - 1));
        return new Vector3(x + 0.5f, heights[x, z] + 0.05f, z + 0.5f);
    }

    /// <summary>The autopilot's points, eight round the middle, by turns near and far.</summary>
    public static Vector3 Point(int index)
    {
        var angle = index % 8 * MathF.Tau / 8;
        var reach = index % 2 == 0 ? 8 : 16;
        return new Vector3(Size / 2f + MathF.Cos(angle) * reach, 0, Size / 2f + MathF.Sin(angle) * reach);
    }
}
