using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

/// <summary>
/// A hill of voxels as one mesh of positions alone, each vertex carrying four bytes of the
/// program's own (<see cref="MeshStream"/>) with the face it lies on, its corner of the face and its
/// tile, which a shader of the program's own unpacks into the normal, the texture coordinate and the
/// color it lights, sixteen bytes a vertex where a whole one with the stream is thirty-six.
/// </summary>
public static class ShadersPackedVertices
{
    private const int Size = 24, Height = 12;

    // Each face's normal's index, as the shader's table holds them, and its four corners in a unit
    // cube, wound to face outward.
    private static readonly (int X, int Y, int Z)[] Ways = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];
    private static readonly Vector3[][] Faces =
    [
        [new(1, 0, 1), new(1, 0, 0), new(1, 1, 0), new(1, 1, 1)],
        [new(0, 0, 0), new(0, 0, 1), new(0, 1, 1), new(0, 1, 0)],
        [new(0, 1, 1), new(1, 1, 1), new(1, 1, 0), new(0, 1, 0)],
        [new(0, 0, 0), new(1, 0, 0), new(1, 0, 1), new(0, 0, 1)],
        [new(0, 0, 1), new(1, 0, 1), new(1, 1, 1), new(0, 1, 1)],
        [new(1, 0, 0), new(0, 0, 0), new(0, 1, 0), new(1, 1, 0)],
    ];

    public static void Run()
    {
        InitWindow(800, 450, "[shaders] packed vertices");

        var shader = LoadShader("resources/shaders/packed.slang");
        var material = new ModelMaterial(Color.White) { Shader = shader, Roughness = 0.9f };

        // A hill of columns, the ground the height of two waves, each voxel's faces against the air
        // kept, as a voxel game meshes its chunks.
        var heights = new int[Size, Size];
        for (int x = 0; x < Size; x++)
            for (int z = 0; z < Size; z++)
            {
                var (dx, dz) = (x - Size / 2f, z - Size / 2f);
                heights[x, z] = Math.Clamp((int)(Height - MathF.Sqrt(dx * dx + dz * dz) * 0.7f + 2 * MathF.Sin(x * 0.6f) * MathF.Cos(z * 0.5f)), 1, Height);
            }
        bool Solid(int x, int y, int z) => x >= 0 && z >= 0 && x < Size && z < Size && y >= 0 && y < heights[x, z];

        var positions = new List<Vector3>();
        var packed = new List<uint>();
        var indices = new List<uint>();
        for (int x = 0; x < Size; x++)
            for (int z = 0; z < Size; z++)
                for (int y = 0; y < heights[x, z]; y++)
                    for (int face = 0; face < 6; face++)
                    {
                        var (wx, wy, wz) = Ways[face];
                        if (Solid(x + wx, y + wy, z + wz)) continue;
                        // Grass on a column's top, snow on a high one, dirt under the top and stone
                        // deep in the hill.
                        var top = y == heights[x, z] - 1;
                        uint tile = top && y >= Height - 3 ? 3u : top ? 0u : y >= heights[x, z] - 3 ? 1u : 2u;
                        var first = (uint)positions.Count;
                        for (int corner = 0; corner < 4; corner++)
                        {
                            positions.Add(new Vector3(x - Size / 2f, y, z - Size / 2f) + Faces[face][corner]);
                            packed.Add((uint)face | (uint)corner << 3 | tile << 5);
                        }
                        indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
                    }
        var hill = UploadMesh([.. positions], [.. indices], MeshStream.From(MeshStreamFormat.UInt, packed.ToArray().AsSpan()), null);

        CreateDirectionalLight(new Vector3(-0.5f, -1, -0.3f), new Color(255, 244, 225), 1.6f, castsShadows: true);
        SetAmbientLight(new Color(150, 175, 215), 0.3f);
        var camera = new Camera3D(new Vector3(22, 20, 26), new Vector3(0, 3, 0), Vector3.UnitY, 45);
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            BeginDrawing();
            ClearBackground(new Color(125, 165, 210));
            BeginMode3D(camera);
            DrawMesh(hill, material, Matrix4x4.Identity);
            EndMode3D();

            var line = $"{positions.Count} vertices, 16 bytes each: a position and four bytes packed";
            DrawRectangle(0, 0, MeasureText(line, 20) + 20, 40, new Color(0, 0, 0, 160));
            DrawText(line, 10, 10, 20, Color.RayWhite);
            DrawFPS(GetScreenWidth() - 100, GetScreenHeight() - 30);
            EndDrawing();
        }

        UnloadMesh(hill);
        UnloadShader(shader);
        CloseWindow();
    }
}
