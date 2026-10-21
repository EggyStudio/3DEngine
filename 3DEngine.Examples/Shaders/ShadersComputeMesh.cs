using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

/// <summary>
/// A hill of voxels meshed on the GPU each frame as waves roll across it, a compute shader writing
/// its vertices and their count into shader buffers that a mesh is drawn from, with nothing read
/// back but the count a frame later for the line of text, through a readback that does not wait.
/// </summary>
public static class ShadersComputeMesh
{
    private const int Size = 24, Height = 12, Capacity = 96 * 1024;

    public static void Run()
    {
        InitWindow(800, 450, "[shaders] compute mesh");

        // The positions, three floats a vertex, the four bytes packed.slang unpacks, and the count.
        var positions = LoadShaderBuffer(Capacity * 12);
        var packed = LoadShaderBuffer(Capacity * 4);
        var counter = LoadShaderBuffer(4);
        var mesher = LoadComputeShader("resources/shaders/mesher.slang");
        SetShaderValue(mesher, GetShaderLocation(mesher, "size"), Size);
        SetShaderValue(mesher, GetShaderLocation(mesher, "height"), Height);
        SetShaderValue(mesher, GetShaderLocation(mesher, "capacity"), Capacity);
        SetShaderValueBuffer(mesher, GetShaderLocation(mesher, "positions"), positions);
        SetShaderValueBuffer(mesher, GetShaderLocation(mesher, "packed"), packed);
        SetShaderValueBuffer(mesher, GetShaderLocation(mesher, "counter"), counter);
        var (timeAt, clearAt) = (GetShaderLocation(mesher, "time"), GetShaderLocation(mesher, "clear"));

        var hill = LoadMeshFromShaderBuffers(positions, packed, MeshStreamFormat.UInt, counter, Capacity);
        SetMeshBoundingBox(hill, new BoundingBox(new Vector3(-Size / 2f, 0, -Size / 2f), new Vector3(Size / 2f, Height, Size / 2f)));
        var shader = LoadShader("resources/shaders/packed.slang");
        var material = new ModelMaterial(Color.White) { Shader = shader, Roughness = 0.9f };

        CreateDirectionalLight(new Vector3(-0.5f, -1, -0.3f), new Color(255, 244, 225), 1.6f, castsShadows: true);
        SetAmbientLight(new Color(150, 175, 215), 0.3f);
        var camera = new Camera3D(new Vector3(22, 20, 26), new Vector3(0, 3, 0), Vector3.UnitY, 45);
        var asked = new Queue<ShaderReadback>();
        var count = new uint[1];
        uint vertices = 0;
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            // The count emptied, then the hill meshed for this frame's waves, both run on the GPU
            // before the frame draws the mesh.
            SetShaderValue(mesher, clearAt, 1);
            ComputeShaderDispatch(mesher, 1, 1, 1);
            SetShaderValue(mesher, clearAt, 0);
            SetShaderValue(mesher, timeAt, (float)GetTime());
            ComputeShaderDispatch(mesher, (Size + 7) / 8, (Size + 7) / 8, 1);

            // The count read back once the GPU has copied it, a frame or two later, for the text.
            asked.Enqueue(ReadShaderBufferAsync(counter));
            while (asked.Count > 0 && IsShaderReadbackReady(asked.Peek()))
            {
                var ready = asked.Dequeue();
                GetShaderReadback<uint>(ready, count);
                UnloadShaderReadback(ready);
                vertices = Math.Min(count[0], Capacity);
            }

            BeginDrawing();
            ClearBackground(new Color(125, 165, 210));
            BeginMode3D(camera);
            DrawMesh(hill, material, Matrix4x4.Identity);
            EndMode3D();

            var line = $"meshed on the GPU each frame: {vertices} vertices";
            DrawRectangle(0, 0, MeasureText(line, 20) + 20, 40, new Color(0, 0, 0, 160));
            DrawText(line, 10, 10, 20, Color.RayWhite);
            DrawFPS(GetScreenWidth() - 100, GetScreenHeight() - 30);
            EndDrawing();
        }

        foreach (var left in asked) UnloadShaderReadback(left);
        UnloadMesh(hill);
        UnloadShader(shader);
        UnloadShader(mesher);
        UnloadShaderBuffer(positions);
        UnloadShaderBuffer(packed);
        UnloadShaderBuffer(counter);
        CloseWindow();
    }
}
