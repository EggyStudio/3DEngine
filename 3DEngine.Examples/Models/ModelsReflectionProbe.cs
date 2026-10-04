using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ModelsReflectionProbe
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] reflection probe");

        // A blue sky outside, which every metal reflects with no probe, even indoors.
        var sky = GenImageGradientLinear(256, 128, 0, new Color(150, 190, 240), new Color(40, 80, 160));
        SetEnvironmentMap(sky);
        CreatePointLight(new Vector3(0, 4.5f, 1), new Color(255, 230, 200), 25, 0, castsShadows: true);

        // A room open toward the camera, its walls in three colors, around metal balls from a
        // mirror to rough.
        var wall = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var ball = LoadModelFromMesh(GenMeshSphere(0.9f, 48, 48));
        var walls = new (Vector3 At, Vector3 Size, Color Color)[]
        {
            (new Vector3(0, -0.1f, 0), new Vector3(10, 0.2f, 10), new Color(200, 196, 186)),
            (new Vector3(0, 6.1f, 0), new Vector3(10, 0.2f, 10), new Color(200, 196, 186)),
            (new Vector3(-5.1f, 3, 0), new Vector3(0.2f, 6, 10), new Color(200, 50, 40)),
            (new Vector3(5.1f, 3, 0), new Vector3(0.2f, 6, 10), new Color(40, 160, 70)),
            (new Vector3(0, 3, -5.1f), new Vector3(10, 6, 0.2f), new Color(230, 190, 60)),
        };
        var probe = CreateReflectionProbe(new Vector3(0, 3, 0), new Vector3(10, 6, 10));
        var camera = new Camera3D(new Vector3(0, 2.5f, 9), new Vector3(0, 1.5f, 0), Vector3.UnitY, 50);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Space))
            {
                if (probe.IsValid)
                {
                    UnloadReflectionProbe(probe);
                    probe = default;
                }
                else probe = CreateReflectionProbe(new Vector3(0, 3, 0), new Vector3(10, 6, 10));
            }

            BeginDrawing();
            ClearBackground(new Color(150, 190, 240));

            BeginMode3D(camera);
            foreach (var (at, size, color) in walls) DrawModelEx(wall, at, Vector3.UnitY, 0, size, color);
            for (int i = 0; i < 3; i++)
            {
                ball.Materials[0] = new ModelMaterial(new Color(235, 235, 240)) { Metallic = 1, Roughness = 0.05f + i * 0.3f };
                DrawModel(ball, new Vector3(-2.5f + i * 2.5f, 0.9f, 0), 1, Color.White);
            }
            EndMode3D();

            DrawText(probe.IsValid ? "The balls reflect the room, through its probe" : "With no probe, the balls reflect the sky", 10, 10, 20, Color.DarkGray);
            DrawText("Space turns the probe on and off", 10, 36, 20, Color.DarkGray);
            DrawFPS(10, 420);

            EndDrawing();
        }

        UnloadModel(wall);
        UnloadModel(ball);
        UnloadEnvironmentMap();
        CloseWindow();
    }
}
