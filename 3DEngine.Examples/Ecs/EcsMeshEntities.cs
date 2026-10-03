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
            DrawText("A camera and a mesh spawned as entities, drawn by the mesh pass.", 10, 10, 20, Color.RayWhite);
            EndDrawing();
        }

        CloseWindow();
    }
}

/// <summary>Spawns a camera entity and a triangle entity, which the renderer's mesh pass draws.</summary>
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
        ctx.Ecs.Add(camera, new Transform(new Vector3(0, 0, 3)));

        var mesh = ctx.Ecs.Spawn();
        ctx.Ecs.Add(mesh, new Mesh([new Vector3(0, 1, 0), new Vector3(-1, -1, 0), new Vector3(1, -1, 0)]));
        ctx.Ecs.Add(mesh, new Material(new Vector4(1f, 0.63f, 0f, 1f)));
        ctx.Ecs.Add(mesh, new Transform(Vector3.Zero));
    }
}
