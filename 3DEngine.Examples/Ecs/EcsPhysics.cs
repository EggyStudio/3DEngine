using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class EcsPhysics
{
    public static void Run()
    {
        InitWindow(800, 450, "[ecs] physics");
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(Color.SkyBlue);
            DrawText("Boxes with BepuPhysics bodies, drawn as mesh entities. Space drops more.", 10, 10, 20, Color.DarkBlue);
            DrawText($"{GetApp().World.Resource<EcsWorld>().Count<PhysicsBody>()} bodies", 10, 40, 20, Color.DarkBlue);
            EndDrawing();
        }

        CloseWindow();
    }
}

/// <summary>Spawns the ground, a camera, a light and falling boxes, each box a body and a mesh.</summary>
[Behavior]
public struct Boxes
{
    public static bool Running => Example.Current == "ecs_physics";

    private static readonly Vector3[] Cube = MeshScene.CubePositions();

    [OnStartup]
    [RunIf(nameof(Running))]
    public static void Start(BehaviorContext ctx)
    {
        var camera = ctx.Ecs.Spawn();
        ctx.Ecs.Add(camera, new Camera(fovY: 50f));
        ctx.Ecs.Add(camera, new Transform(new Vector3(0, 6, 14), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.35f), Vector3.One));

        var sun = ctx.Ecs.Spawn();
        ctx.Ecs.Add(sun, Light.Directional(Vector3.One, 0.9f) with { CastsShadows = true });
        ctx.Ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.CreateFromYawPitchRoll(0.6f, -1.0f, 0), Vector3.One));
        var sky = ctx.Ecs.Spawn();
        ctx.Ecs.Add(sky, Light.Ambient(new Vector3(0.6f, 0.7f, 0.9f), 0.25f));

        // The ground: a static body and a flat box mesh 20 units wide.
        var ground = ctx.Ecs.Spawn();
        ctx.Physics.CreateStaticBox(new Vector3(0, -0.5f, 0), new Vector3(10, 0.5f, 10), entityId: ground);
        ctx.Ecs.Add(ground, new Mesh(Cube));
        ctx.Ecs.Add(ground, new Material(new Color(115, 153, 89)));
        ctx.Ecs.Add(ground, new Transform(new Vector3(0, -0.5f, 0), new Vector3(20, 1, 20)));

        Drop(ctx, 40);
    }

    [OnUpdate]
    [RunIf(nameof(Running))]
    public static void DropOnSpace(BehaviorContext ctx)
    {
        if (ctx.Input.KeyPressed(Key.Space)) Drop(ctx, 20);
    }

    private static void Drop(BehaviorContext ctx, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var at = new Vector3(Random.Shared.NextSingle() * 6 - 3, 4 + i * 0.6f, Random.Shared.NextSingle() * 6 - 3);
            var box = ctx.Ecs.Spawn();
            ctx.Ecs.Add(box, ctx.Physics.CreateBox(at, new Vector3(0.5f), entityId: box));
            ctx.Ecs.Add(box, new Mesh(Cube));
            ctx.Ecs.Add(box, new Material(new Color(230, (byte)(102 + Random.Shared.Next(102)), 51)));
            ctx.Ecs.Add(box, new Transform(at));
        }
    }
}
