using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

/// <summary>
/// A town of block columns as one mesh, each vertex carrying four bytes of the program's own beside
/// its position, normal and texture coordinates (<see cref="MeshStream"/>): a palette index and a
/// shade, which a shader of the program's own unpacks. A wave moves the palette through the town,
/// the packed bytes written in place each frame and nothing else of the mesh uploaded again.
/// </summary>
public static class ShadersPackedVertices
{
    private const int Size = 16;

    public static void Run()
    {
        InitWindow(800, 450, "[shaders] packed vertices");

        var shader = LoadShader("resources/shaders/packed.slang");
        var material = new ModelMaterial(Color.White) { Shader = shader, Roughness = 0.8f };

        // A column on each cell of the grid, its height from two waves over the ground.
        var vertices = new List<ModelVertex>();
        var indices = new List<uint>();
        var shades = new List<uint>();
        var heights = new int[Size, Size];
        for (int x = 0; x < Size; x++)
            for (int z = 0; z < Size; z++)
            {
                heights[x, z] = 1 + (int)(4 + 3 * MathF.Sin(x * 0.5f) * MathF.Cos(z * 0.4f));
                Column(vertices, indices, shades, new Vector3(x - Size / 2f, 0, z - Size / 2f), heights[x, z]);
            }
        var packed = new uint[vertices.Count];
        Paint(packed, shades, heights, 0);
        var town = UploadMesh([.. vertices], [.. indices], null, null, MeshStream.From(MeshStreamFormat.UInt, packed.AsSpan()));

        CreateDirectionalLight(new Vector3(-0.5f, -1, -0.3f), Color.White, 1.4f, castsShadows: true);
        SetAmbientLight(new Color(150, 170, 200), 0.25f);
        var camera = new Camera3D(new Vector3(14, 16, 18), new Vector3(0, 2, 0), Vector3.UnitY, 45);
        var time = 0f;
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            time += GetFrameTime();
            UpdateCamera(ref camera, CameraMode.Orbital);
            Paint(packed, shades, heights, time);
            UpdateMeshBuffer(town, MeshStreamBuffer, packed.AsSpan(), 0);

            BeginDrawing();
            ClearBackground(new Color(30, 34, 44));
            BeginMode3D(camera);
            DrawMesh(town, material, Matrix4x4.Identity);
            EndMode3D();

            DrawRectangle(0, 0, 570, 40, new Color(0, 0, 0, 160));
            DrawText($"{vertices.Count} vertices, four bytes each written in place", 10, 10, 20, Color.RayWhite);
            DrawFPS(GetScreenWidth() - 100, 10);
            EndDrawing();
        }

        UnloadMesh(town);
        UnloadShader(shader);
        CloseWindow();
    }

    // A box of one unit by height units on a cell, each face four vertices of its own with the
    // face's normal, and the shade each face's vertices carry: the top brightest and the sides by
    // the way they face.
    private static void Column(List<ModelVertex> vertices, List<uint> indices, List<uint> shades, Vector3 at, int height)
    {
        (Vector3 Normal, Vector3 Right, Vector3 Up, uint Shade)[] faces =
        [
            (Vector3.UnitY, Vector3.UnitX, -Vector3.UnitZ, 255),
            (Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY, 215),
            (-Vector3.UnitZ, -Vector3.UnitX, Vector3.UnitY, 175),
            (Vector3.UnitX, -Vector3.UnitZ, Vector3.UnitY, 195),
            (-Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY, 165),
        ];
        var size = new Vector3(0.9f, height, 0.9f);
        var middle = at + new Vector3(0.5f, height / 2f, 0.5f);
        foreach (var (normal, right, up, shade) in faces)
        {
            var first = (uint)vertices.Count;
            var center = middle + normal * size / 2;
            var (r, u) = (right * size / 2, up * size / 2);
            vertices.Add(new ModelVertex(center - r - u, normal, new Vector2(0, 1)));
            vertices.Add(new ModelVertex(center + r - u, normal, new Vector2(1, 1)));
            vertices.Add(new ModelVertex(center + r + u, normal, new Vector2(1, 0)));
            vertices.Add(new ModelVertex(center - r + u, normal, new Vector2(0, 0)));
            indices.AddRange([first, first + 1, first + 2, first, first + 2, first + 3]);
            shades.AddRange([shade, shade, shade, shade]);
        }
    }

    // Each vertex's four bytes, its column's palette index in the low four bits, moved by a wave
    // through the town, and its face's shade in the eight above them.
    private static void Paint(uint[] packed, List<uint> shades, int[,] heights, float time)
    {
        int vertex = 0;
        for (int x = 0; x < Size; x++)
            for (int z = 0; z < Size; z++)
            {
                var index = (uint)((heights[x, z] + (int)(time * 4 - MathF.Sqrt((x - 8) * (x - 8) + (z - 8) * (z - 8)))) & 15);
                for (int i = 0; i < 20; i++, vertex++) packed[vertex] = index | shades[vertex] << 4;
            }
    }
}
