using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

/// <summary>
/// raylib's mesh instancing: ten thousand cubes drawn by one <c>DrawMeshInstanced</c> call, each
/// turning on its own axis, colored by a shader that tells them apart by their instance.
/// </summary>
public static class ShadersMeshInstancing
{
    public static void Run()
    {
        InitWindow(800, 450, "[shaders] mesh instancing");

        const int Count = 10_000;
        var shader = LoadShader("resources/shaders/instancing.slang");
        SetShaderValue(shader, GetShaderLocation(shader, "count"), (float)Count);
        var material = new ModelMaterial(Color.White) { Shader = shader, Roughness = 0.6f };
        var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));

        CreateDirectionalLight(new Vector3(-0.4f, -1, -0.6f), Color.White, 2);

        // A place, an axis and a speed for each cube, scattered through a ball.
        var random = new Random(7);
        var places = new Vector3[Count];
        var axes = new Vector3[Count];
        var speeds = new float[Count];
        for (int i = 0; i < Count; i++)
        {
            Vector3 at;
            do at = new Vector3(random.NextSingle(), random.NextSingle(), random.NextSingle()) * 2 - Vector3.One;
            while (at.LengthSquared() > 1);
            places[i] = at * 50;
            axes[i] = Vector3.Normalize(new Vector3(random.NextSingle(), random.NextSingle(), random.NextSingle()) - new Vector3(0.5f));
            speeds[i] = 30 + random.NextSingle() * 90;
        }
        var transforms = new Matrix4x4[Count];

        var camera = new Camera3D(new Vector3(0, 35, 120), Vector3.Zero, Vector3.UnitY, 45);
        var time = 0f;
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            time += GetFrameTime();
            UpdateCamera(ref camera, CameraMode.Orbital);
            for (int i = 0; i < Count; i++)
                transforms[i] = Matrix4x4.CreateFromAxisAngle(axes[i], float.DegreesToRadians(speeds[i] * time)) * Matrix4x4.CreateTranslation(places[i]);

            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawMeshInstanced(cube.Meshes[0], material, transforms);
            EndMode3D();

            DrawRectangle(0, 0, 400, 40, new Color(0, 0, 0, 160));
            DrawText($"{Count} cubes, one instanced draw", 10, 10, 20, Color.RayWhite);
            DrawFPS(GetScreenWidth() - 100, 10);
            EndDrawing();
        }

        UnloadModel(cube);
        UnloadShader(shader);
        CloseWindow();
    }
}
