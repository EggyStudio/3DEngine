using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

public static class CoreDaySky
{
    public static void Run()
    {
        InitWindow(800, 450, "[core] day sky");

        // A day of two minutes from a quarter past six, its sun's path leaning 35 degrees to the
        // south, so the sun at noon and the moon at midnight stand in view.
        SetSkyDay(new SkyDay { Minutes = 2, Tilt = 35 });
        SetSkyTime(6.25f);
        SetShadowDistance(60);

        var ground = LoadModelFromMesh(GenMeshPlane(400, 400, 1, 1));
        ground.Materials[0] = new ModelMaterial(new Color(105, 135, 80)) { Roughness = 0.9f };
        var stone = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        stone.Materials[0] = new ModelMaterial(new Color(170, 165, 155)) { Roughness = 0.8f };
        // A ring of standing stones about the eye, whose shadows turn with the sun, set half a step
        // round so the sun rises between two of them.
        var stones = Enumerable.Range(0, 12)
            .Select(i => (At: new Vector3(MathF.Cos((i + 0.5f) * MathF.Tau / 12), 0, MathF.Sin((i + 0.5f) * MathF.Tau / 12)) * 16, Height: 2.5f + i % 3))
            .ToArray();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Right)) SetSkyTime(GetSkyTime() + 1);
            if (IsKeyPressed(Key.Left)) SetSkyTime(MathF.Max(0, GetSkyTime() - 1));
            if (IsKeyPressed(Key.Space)) SetSkyDay(GetSkyDay() with { Minutes = GetSkyDay().Minutes > 0 ? 0 : 2 });

            var camera = Facing(GetSkyTime(), GetSkyDay().Tilt);

            BeginDrawing();
            ClearBackground(Color.Black);

            BeginMode3D(camera);
            DrawSkybox();
            DrawModel(ground, Vector3.Zero, 1, Color.White);
            foreach (var (at, height) in stones)
                DrawModelEx(stone, at + new Vector3(0, height / 2, 0), Vector3.UnitY, 0, new Vector3(1.2f, height, 0.8f), Color.White);
            EndMode3D();

            var hours = GetSkyTime();
            var (hour, minute) = ((int)(hours % 24), (int)(hours * 60 % 60));
            DrawText($"{hour:00}:{minute:00} of day {(int)(hours / 24) + 1}", 10, 10, 20, Color.White);
            DrawText(GetSkyDay().Minutes > 0 ? "Left and Right move an hour, Space stops the day" : "Left and Right move an hour, Space starts the day",
                10, 420, 20, Color.White);

            EndDrawing();
        }

        UnloadModel(ground);
        UnloadModel(stone);
        CloseWindow();
    }

    // The eye among the stones, looking along the ground toward the sun by day and the moon by night,
    // tipped up 25 degrees. The sun rises at +X at 6, the sky turning 15 degrees an hour about its
    // axis from north to south, and its path leans the day's tilt toward +Z, the south.
    private static Camera3D Facing(float hours, float tilt)
    {
        var (lean, turn) = (float.DegreesToRadians(tilt), float.DegreesToRadians((hours - 6) * 15));
        var toSun = new Vector3(MathF.Cos(lean) * MathF.Cos(turn), MathF.Cos(lean) * MathF.Sin(turn), MathF.Sin(lean));
        var toward = toSun.Y > -0.05f ? toSun : -toSun;
        var level = Vector3.Normalize(new Vector3(toward.X, 0, toward.Z));
        var eye = new Vector3(0, 1.7f, 0);
        return new Camera3D(eye, eye + level + Vector3.UnitY * MathF.Tan(float.DegreesToRadians(25)), Vector3.UnitY, 70);
    }
}
