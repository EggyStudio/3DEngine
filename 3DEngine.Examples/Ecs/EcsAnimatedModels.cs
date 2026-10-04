using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class EcsAnimatedModels
{
    public static void Run()
    {
        InitWindow(800, 450, "[ecs] animated models");
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(new Color(30, 34, 44));
            DrawText("Five AnimatedModel entities of one file, each at its own speed.", 10, 10, 20, Color.RayWhite);
            EndDrawing();
        }

        CloseWindow();
    }
}

/// <summary>Spawns a camera, a sun, a floor and a row of arms, each playing its clip at its own speed.</summary>
[Behavior]
public struct ArmRow
{
    public static bool Running => Example.Current == "ecs_animated_models";

    [OnStartup]
    [RunIf(nameof(Running))]
    public static void Start(BehaviorContext ctx)
    {
        var camera = ctx.Ecs.Spawn();
        ctx.Ecs.Add(camera, new Camera(fovY: 45f));
        ctx.Ecs.Add(camera, new Transform(new Vector3(0, 3, 9), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.25f), Vector3.One));

        var sun = ctx.Ecs.Spawn();
        ctx.Ecs.Add(sun, Light.Directional(new Vector3(1, 0.97f, 0.92f), 2.5f) with { CastsShadows = true });
        ctx.Ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(0.6f, -0.9f, 0), Vector3.One));

        var floor = ctx.Ecs.Spawn();
        ctx.Ecs.Add(floor, new Mesh(MeshScene.CubePositions()));
        ctx.Ecs.Add(floor, new Material(new Color(120, 124, 130)) { RoughnessFactor = 0.9f });
        ctx.Ecs.Add(floor, new Transform(new Vector3(0, -0.1f, 0), Quaternion.Identity, new Vector3(12, 0.2f, 6)));

        // One file, five entities, each posed on a model of its own.
        for (int i = 0; i < 5; i++)
        {
            var arm = ctx.Ecs.Spawn();
            ctx.Ecs.Add(arm, new Transform(new Vector3((i - 2) * 2, 0, 0)));
            ctx.Ecs.Add(arm, new AnimatedModel("resources/arm.gltf", speed: 0.4f + i * 0.3f) { Time = i * 0.15f });
        }
    }
}
