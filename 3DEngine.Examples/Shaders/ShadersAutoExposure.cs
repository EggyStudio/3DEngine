using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShadersAutoExposure
{
    public static void Run()
    {
        InitWindow(800, 450, "[shaders] auto exposure");

        // A tunnel in a sunlit field, which the camera rides through and out of and back. With the
        // exposure following the scene the tunnel's dim lamps are brought up as the eye would, and
        // the field is dazzling for a moment on the way out until it settles. A turns it off and on.
        SetAutoExposure(true, min: 0.3f, max: 6, speed: 1.5f);
        var auto = true;
        CreateDirectionalLight(Vector3.Normalize(new Vector3(-0.4f, -1, 0.3f)), new Color(255, 245, 225), 3, castsShadows: true);
        for (int i = 0; i < 3; i++)
            CreatePointLight(new Vector3(0, 2.4f, -7 + i * 7), new Color(255, 190, 120), 0.6f, range: 5);

        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var field = LoadModelFromMesh(GenMeshPlane(120, 120, 1, 1));
        var tree = LoadModelFromMesh(GenMeshCone(1.2f, 4, 12));
        var trunk = LoadModelFromMesh(GenMeshCylinder(0.25f, 1.2f, 8));
        // The tunnel's floor, walls and roof, from z of -10 to 10.
        var tunnel = new (Vector3 At, Vector3 Size)[]
        {
            (new Vector3(0, 0.05f, 0), new Vector3(4, 0.1f, 20)),
            (new Vector3(-2.25f, 1.5f, 0), new Vector3(0.5f, 3, 20)),
            (new Vector3(2.25f, 1.5f, 0), new Vector3(0.5f, 3, 20)),
            (new Vector3(0, 3.25f, 0), new Vector3(5, 0.5f, 20)),
        };
        var t = 0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            t += GetFrameTime();
            if (IsKeyPressed(Key.A)) SetAutoExposure(auto = !auto, min: 0.3f, max: 6, speed: 1.5f);

            // From deep in the tunnel out into the field and back, five seconds each way, resting
            // eight seconds at each end.
            var phase = t % 26;
            var along = phase < 8 ? 0 : phase < 13 ? Ease((phase - 8) / 5) : phase < 21 ? 1 : 1 - Ease((phase - 21) / 5);
            var z = -6 + along * 24;
            var camera = new Camera3D(new Vector3(0, 1.6f, z), new Vector3(0, 1.4f, z + 5), Vector3.UnitY, 60);

            BeginDrawing();
            ClearBackground(new Color(140, 185, 235));

            BeginMode3D(camera);
            DrawModel(field, Vector3.Zero, 1, new Color(90, 140, 70));
            foreach (var (at, size) in tunnel) DrawModelEx(block, at, Vector3.UnitY, 0, size, new Color(150, 140, 130));
            for (int i = 0; i < 12; i++)
            {
                var at = new Vector3((i % 2 == 0 ? -1 : 1) * (5 + i % 3 * 3), 0, 14 + i * 3);
                DrawModel(trunk, at, 1, new Color(110, 80, 50));
                DrawModel(tree, at + new Vector3(0, 1.2f, 0), 1, new Color(50, 110, 50));
            }
            EndMode3D();

            DrawText(auto ? "The exposure follows the scene. A turns it off." : "A fixed exposure. A makes it follow the scene.", 10, 10, 20, Color.RayWhite);
            DrawFPS(10, 420);
            EndDrawing();
        }

        UnloadModel(block);
        UnloadModel(field);
        UnloadModel(tree);
        UnloadModel(trunk);
        CloseWindow();
    }

    private static float Ease(float x) => 0.5f - 0.5f * MathF.Cos(x * MathF.PI);
}
