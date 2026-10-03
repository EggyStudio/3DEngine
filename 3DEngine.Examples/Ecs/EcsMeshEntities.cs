using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class EcsMeshEntities
{
    public static void Run()
    {
        InitWindow(800, 450, "[ecs] mesh entities");
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(Color.DarkGray);
            DrawText("A camera, two meshes and two lights, spawned as entities.", 10, 10, 20, Color.RayWhite);
            EndDrawing();
        }

        CloseWindow();
    }
}

/// <summary>Spawns a camera entity and two mesh entities, which the model pass draws lit.</summary>
[Behavior]
public struct MeshScene
{
    public static bool Running => Example.Current == "ecs_mesh_entities";

    [OnStartup]
    [RunIf(nameof(Running))]
    public static void Start(BehaviorContext ctx)
    {
        var camera = ctx.Ecs.Spawn();
        ctx.Ecs.Add(camera, new Camera(fovY: 60f, near: 0.1f, far: 1000f));
        ctx.Ecs.Add(camera, new Transform(new Vector3(0, 1, 5), Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.2f), Vector3.One));

        var triangle = ctx.Ecs.Spawn();
        ctx.Ecs.Add(triangle, new Mesh([new Vector3(0, 1, 0), new Vector3(-1, -1, 0), new Vector3(1, -1, 0)]));
        ctx.Ecs.Add(triangle, new Material(new Vector4(1f, 0.63f, 0f, 1f)));
        ctx.Ecs.Add(triangle, new Transform(new Vector3(-1.5f, 0, 0)));

        var cube = ctx.Ecs.Spawn();
        ctx.Ecs.Add(cube, new Mesh(Cube()));
        ctx.Ecs.Add(cube, new Material(new Vector4(0.2f, 0.6f, 1f, 1f)));
        ctx.Ecs.Add(cube, new Transform(new Vector3(1.5f, 0, 0), Quaternion.Identity, Vector3.One));
        ctx.Ecs.Add(cube, new MeshScene());

        // A dim light from above, and a warm one that circles between the two meshes.
        var sun = ctx.Ecs.Spawn();
        ctx.Ecs.Add(sun, Light.Directional(Vector3.One, 0.25f));
        ctx.Ecs.Add(sun, new Transform(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitX, -1.2f), Vector3.One));

        var lamp = ctx.Ecs.Spawn();
        ctx.Ecs.Add(lamp, Light.Point(new Vector3(1f, 0.8f, 0.5f), 3f));
        ctx.Ecs.Add(lamp, new Transform(new Vector3(0, 0, 1.5f)));
        ctx.Ecs.Add(lamp, new Lamp());
    }

    /// <summary>Turns the cube, the one entity carrying this behavior.</summary>
    [OnUpdate]
    public void Spin(BehaviorContext ctx)
    {
        var dt = (float)ctx.Time.DeltaSeconds;
        ref var transform = ref ctx.Ecs.GetRef<Transform>(ctx.EntityId);
        transform.Rotation *= Quaternion.CreateFromYawPitchRoll(0.8f * dt, 0.5f * dt, 0);
    }

    private static Vector3[] Cube() => CubePositions();

    /// <summary>A unit cube as twelve triangles, counterclockwise from outside, which light flat by face.</summary>
    public static Vector3[] CubePositions()
    {
        Vector3[] c = [new(-.5f, -.5f, -.5f), new(.5f, -.5f, -.5f), new(.5f, .5f, -.5f), new(-.5f, .5f, -.5f),
                       new(-.5f, -.5f, .5f), new(.5f, -.5f, .5f), new(.5f, .5f, .5f), new(-.5f, .5f, .5f)];
        int[] quads = [4, 5, 6, 7, 1, 0, 3, 2, 0, 4, 7, 3, 5, 1, 2, 6, 7, 6, 2, 3, 0, 1, 5, 4];
        var positions = new List<Vector3>();
        for (int q = 0; q < quads.Length; q += 4)
            positions.AddRange([c[quads[q]], c[quads[q + 1]], c[quads[q + 2]], c[quads[q]], c[quads[q + 2]], c[quads[q + 3]]]);
        return [.. positions];
    }
}

/// <summary>Moves its light along a circle in front of the meshes.</summary>
[Behavior]
public struct Lamp
{
    public float Angle;

    [OnUpdate]
    public void Circle(BehaviorContext ctx)
    {
        Angle += (float)ctx.Time.DeltaSeconds;
        ref var transform = ref ctx.Ecs.GetRef<Transform>(ctx.EntityId);
        transform.Position = new Vector3(MathF.Sin(Angle) * 2.5f, 0.5f, 1.2f);
    }
}
