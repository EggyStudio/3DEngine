using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ScenesLevel
{
    public static void Run()
    {
        InitWindow(800, 450, "[scenes] level");
        var world = GetApp().World;
        var ecs = world.Resource<EcsWorld>();
        var file = Path.Combine(AppContext.BaseDirectory, "level.json");

        // A level made in code the first time: a camera, two lights and three models from a file.
        Build(ecs);
        SceneFile.Save(ecs, file);
        var message = $"Saved {Path.GetFileName(file)}. R reloads it, S saves it again, D despawns everything.";

        SetTargetFPS(60);
        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.S))
            {
                SceneFile.Save(ecs, file);
                message = "Saved again.";
            }
            if (IsKeyPressed(Key.D)) message = $"Despawned {Clear(ecs)} entities. R brings them back.";
            if (IsKeyPressed(Key.R))
            {
                Clear(ecs);
                message = $"Loaded {SceneFile.Load(world, file).Count} entities from {Path.GetFileName(file)}.";
            }

            BeginDrawing();
            ClearBackground(Color.SkyBlue);
            DrawText(message, 10, 10, 20, Color.DarkBlue);
            DrawText($"{ecs.EntityCount} entities", 10, 40, 20, Color.DarkBlue);
            EndDrawing();
        }

        CloseWindow();
    }

    private static void Build(EcsWorld ecs)
    {
        var camera = ecs.Spawn();
        ecs.SetName(camera, "Camera");
        ecs.Add(camera, Camera.Default);
        ecs.Add(camera, new Transform(new Vector3(0, 4, 10), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.35f), Vector3.One));

        var sun = ecs.Spawn();
        ecs.SetName(sun, "Sun");
        ecs.Add(sun, new Light { Type = LightType.Distant, Color = Vector3.One, Intensity = 0.9f });
        ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(0.5f, -0.9f, 0), Vector3.One));
        var sky = ecs.Spawn();
        ecs.SetName(sky, "Sky");
        ecs.Add(sky, new Light { Type = LightType.Dome, Color = new Vector3(0.5f, 0.6f, 0.8f), Intensity = 0.3f });

        for (int i = 0; i < 3; i++)
        {
            var torus = ecs.Spawn();
            ecs.SetName(torus, $"Torus {i + 1}");
            ecs.Add(torus, new ModelRef { Path = "../resources/torus.obj" });
            ecs.Add(torus, new Transform((i - 1) * 3.5f * Vector3.UnitX, Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.4f * i), Vector3.One));
        }
    }

    // Everything the level holds, models and the entities they spawned.
    private static int Clear(EcsWorld ecs)
    {
        var all = ecs.AllEntities().ToArray();
        foreach (var entity in all) ecs.Despawn(entity);
        return all.Length;
    }
}
