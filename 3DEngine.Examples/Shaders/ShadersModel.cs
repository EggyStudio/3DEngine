using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersModel
{
    public static void Run()
    {
        InitWindow(800, 450, "[shaders] model shader");

        var camera = new Camera3D(new Vector3(0, 2.5f, 8), new Vector3(0, 1, 0), Vector3.UnitY, 45);

        // A model shader of the program's own, its uniforms found by name.
        var toon = LoadShader("resources/shaders/toon.slang");
        var bands = GetShaderLocation(toon, "bands");
        var rimColor = GetShaderLocation(toon, "rimColor");
        var viewer = GetShaderLocation(toon, "viewer");

        var knot = LoadModelFromMesh(GenMeshKnot(1.8f, 1.4f, 160, 24));
        knot.Materials[0].Shader = toon;
        var plain = LoadModelFromMesh(GenMeshKnot(1.8f, 1.4f, 160, 24));

        var levels = 3;
        var angle = 0f;
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            angle += 25 * GetFrameTime();
            if (IsKeyPressed(Key.Up)) levels = Math.Min(8, levels + 1);
            if (IsKeyPressed(Key.Down)) levels = Math.Max(1, levels - 1);

            SetShaderValue(toon, bands, (float)levels);
            SetShaderValue(toon, rimColor, new Vector4(1f, 0.85f, 0.4f, 0.9f));
            SetShaderValue(toon, viewer, camera.Position);

            BeginDrawing();
            ClearBackground(Color.DarkBlue);
            BeginMode3D(camera);
            DrawModelEx(knot, new Vector3(-1.8f, 1.2f, 0), Vector3.UnitY, angle, Vector3.One, new Color(120, 200, 255));
            DrawModelEx(plain, new Vector3(1.8f, 1.2f, 0), Vector3.UnitY, angle, Vector3.One, new Color(120, 200, 255));
            DrawGrid(10, 1);
            EndMode3D();

            DrawText($"Left: toon.slang with {levels} bands (UP and DOWN). Right: the model pass's own.", 10, 10, 20, Color.RayWhite);
            EndDrawing();
        }

        UnloadModel(knot);
        UnloadModel(plain);
        UnloadShader(toon);
        CloseWindow();
    }
}
