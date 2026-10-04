using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersShadowmap
{
    public static void Run()
    {
        InitWindow(800, 450, "[shaders] shadowmap");

        // A dim sun for the shape of things, and a lamp circling between pillars, both casting
        // shadows. Space turns the lamp's shadows off and on.
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, -0.3f)), new Color(120, 140, 170), 0.6f, castsShadows: true);
        var lamp = CreatePointLight(new Vector3(0, 1.5f, 0), new Color(255, 200, 140), 12, castsShadows: true);
        var shadows = true;

        var ground = LoadModelFromMesh(GenMeshPlane(30, 30, 1, 1));
        var pillar = LoadModelFromMesh(GenMeshCube(0.6f, 3, 0.6f));
        var camera = new Camera3D(new Vector3(9, 8, 9), Vector3.Zero, Vector3.UnitY, 45);
        var t = 0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            t += GetFrameTime();
            var lampAt = new Vector3(MathF.Cos(t * 0.6f) * 2.2f, 1.5f, MathF.Sin(t * 0.6f) * 2.2f);
            SetLightPosition(lamp, lampAt);
            if (IsKeyPressed(Key.Space)) SetLightCastsShadows(lamp, shadows = !shadows);

            BeginDrawing();
            ClearBackground(new Color(20, 22, 28));

            BeginMode3D(camera);
            DrawModel(ground, Vector3.Zero, 1, Color.LightGray);
            for (int i = 0; i < 8; i++)
            {
                var angle = i * MathF.Tau / 8;
                DrawModel(pillar, new Vector3(MathF.Cos(angle) * 4, 1.5f, MathF.Sin(angle) * 4), 1, Color.Beige);
            }
            DrawModel(pillar, new Vector3(0, 1.5f, 0), 1, Color.Beige);
            // Drawn as a shape rather than a model, since a model around the lamp would shadow
            // everything from it.
            DrawSphere(lampAt, 0.12f, new Color(255, 230, 190));
            EndMode3D();

            DrawText(shadows ? "The lamp casts shadows all around it. Space turns them off." : "The lamp casts no shadows. Space turns them on.",
                10, 10, 20, Color.RayWhite);
            DrawFPS(10, 420);
            EndDrawing();
        }

        UnloadModel(ground);
        UnloadModel(pillar);
        CloseWindow();
    }
}
