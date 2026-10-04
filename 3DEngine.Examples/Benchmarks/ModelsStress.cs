using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

/// <summary>
/// Lit mesh entities in four materials, turning, under a sun that casts a shadow and two point
/// lights, beside eight skinned arms each on its own frame, with as many entities as a frame holds
/// at 60 frames a second, found by <see cref="StressRamp"/>. <c>e3d command profile</c> reports
/// where the time goes.
/// </summary>
/// <remarks>
/// <c>E3D_STRESS_ARMS</c> sets how many arms there are, so the entities' own cost can be measured
/// apart from the arms', as <c>E3D_STRESS_ARMS=0 ./e3d open ... models_stress</c>.
/// </remarks>
public static class ModelsStress
{
    public static void Run()
    {
        InitWindow(800, 450, "[models] stress");
        SetTargetFPS(0);
        var ecs = GetApp().World.Resource<EcsWorld>();

        CreateDirectionalLight(new Vector3(-0.4f, -0.8f, -0.45f), new Color(255, 248, 235), 2.5f, castsShadows: true);
        CreatePointLight(new Vector3(-6, 3, 0), new Color(255, 160, 80), 6);
        CreatePointLight(new Vector3(6, 3, 0), new Color(80, 160, 255), 6);

        // One mesh, so every entity shares its upload, and four materials across them.
        var cube = MeshScene.CubePositions();
        Material[] materials =
        [
            new(new Color(200, 196, 186)) { RoughnessFactor = 0.8f },
            new(new Color(190, 120, 60)) { RoughnessFactor = 0.5f },
            new(new Color(90, 200, 120)) { RoughnessFactor = 0.3f, MetallicFactor = 0.2f },
            new(new Color(230, 200, 90)) { RoughnessFactor = 0.25f, MetallicFactor = 1 },
        ];

        var ground = ecs.Spawn();
        ecs.Add(ground, new Mesh(cube));
        ecs.Add(ground, new Material(new Color(120, 124, 130)) { RoughnessFactor = 0.9f });
        ecs.Add(ground, new Transform(new Vector3(0, -0.6f, 0)));

        var view = ecs.Spawn();
        ecs.Add(view, new Camera(fovY: 45));
        ecs.Add(view, new Transform(Vector3.Zero));

        // Each arm its own model, since posing one model moves every draw of it.
        var arms = new Model[int.TryParse(Environment.GetEnvironmentVariable("E3D_STRESS_ARMS"), out var armCount) ? armCount : 8];
        for (int i = 0; i < arms.Length; i++) arms[i] = LoadModel("resources/arm.gltf");
        var animations = LoadModelAnimations("resources/arm.gltf");
        var bend = animations[0];

        var entities = new List<int>();
        var ramp = new StressRamp(500);
        var frame = 0;

        while (!WindowShouldClose())
        {
            while (entities.Count < ramp.Count)
            {
                var entity = ecs.Spawn();
                var index = entities.Count;
                ecs.Add(entity, new Mesh(cube));
                ecs.Add(entity, materials[index % materials.Length]);
                ecs.Add(entity, new Transform(Spot(index)));
                entities.Add(entity);
            }
            while (entities.Count > ramp.Count)
            {
                ecs.Despawn(entities[^1]);
                entities.RemoveAt(entities.Count - 1);
            }

            // Every entity turns, so every transform changes each frame, as moving things would.
            var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, GetFrameTime());
            foreach (var entity in entities)
            {
                ref var transform = ref ecs.GetRef<Transform>(entity);
                transform.Rotation = Quaternion.Normalize(transform.Rotation * turn);
            }

            // The camera backs away as the grid grows, to keep all of it in view.
            var side = MathF.Sqrt(Math.Max(entities.Count, 1)) * 1.5f;
            var camera = new Camera3D(new Vector3(0, side * 0.8f + 4, side * 0.9f + 6), Vector3.Zero, Vector3.UnitY, 45);
            Matrix4x4.Invert(Matrix4x4.CreateLookAt(camera.Position, camera.Target, camera.Up), out var placed);
            // The ground fits the grid and the arms' row behind it, so the shadow map is spread
            // over no more than it needs, and the arms grow with the grid to stay in sight.
            var size = Math.Max(1, side / 20);
            ref var floor = ref ecs.GetRef<Transform>(ground);
            floor.Position = new Vector3(0, -0.6f, -1.25f * size);
            floor.Scale = new Vector3(Math.Max(side, arms.Length * 2 * size) + 4, 0.2f, side + 2.5f * size + 4);
            ref var eye = ref ecs.GetRef<Transform>(view);
            eye.Position = camera.Position;
            eye.Rotation = Quaternion.CreateFromRotationMatrix(placed);

            frame++;
            BeginDrawing();
            ClearBackground(new Color(30, 34, 44));
            BeginMode3D(camera);
            for (int i = 0; i < arms.Length; i++)
            {
                UpdateModelAnimation(arms[i], bend, (frame + i * 7) % bend.FrameCount);
                DrawModel(arms[i], new Vector3((i * 2 - arms.Length + 1) * size, -0.5f, -side * 0.5f - 1.5f * size), size, new Color(230, 160, 60));
            }
            EndMode3D();

            DrawRectangle(0, 0, GetScreenWidth(), 40, Color.Black);
            DrawText(ramp.Limit > 0 ? $"{ramp.Limit} entities hold 60 frames a second" : $"entities: {ramp.Count}, {ramp.FrameMs:0.0} ms",
                10, 10, 20, Color.Green);
            DrawFPS(GetScreenWidth() - 110, 10);
            EndDrawing();
            ramp.Measure();
        }

        UnloadModelAnimations(animations);
        foreach (var arm in arms) UnloadModel(arm);
        CloseWindow();
    }

    // The index'th place on a square spiral around the middle, 1.5 apart.
    private static Vector3 Spot(int index)
    {
        int x = 0, z = 0, dx = 1, dz = 0, leg = 1, step = 0, turns = 0;
        for (int i = 0; i < index; i++)
        {
            x += dx;
            z += dz;
            if (++step < leg) continue;
            step = 0;
            (dx, dz) = (-dz, dx);
            if (++turns % 2 == 0) leg++;
        }
        return new Vector3(x * 1.5f, 0, z * 1.5f);
    }
}
