using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class EcsBehaviors
{
    public static void Run()
    {
        InitWindow(800, 450, "[ecs] behaviors");

        var camera = new Camera3D(new Vector3(12, 8, 12), new Vector3(0, 2, 0), Vector3.UnitY, 45);
        var ecs = GetApp().World.Resource<EcsWorld>();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Free);

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            // The behavior below moves the balls in Stage.Update, inside BeginDrawing, and the
            // loop draws them from the same world.
            BeginMode3D(camera);
            foreach (var (_, ball) in ecs.Query<Ball>())
                DrawSphereEx(ball.Position, 0.3f, 8, 8, ball.Color);
            DrawGrid(20, 1);
            EndMode3D();

            DrawText($"{ecs.Count<Ball>()} balls. Space adds a hundred.", 10, 10, 20, Color.DarkGray);
            EndDrawing();
        }

        CloseWindow();
    }
}

/// <summary>A ball that falls and bounces. Its fields are its per-entity state.</summary>
[Behavior]
public struct Ball
{
    public Vector3 Position;
    public Vector3 Velocity;
    public Color Color;

    public static bool Running => Example.Current == "ecs_behaviors";

    [OnStartup]
    [RunIf(nameof(Running))]
    public static void Start(BehaviorContext ctx) => Spawn(ctx, 100);

    [OnUpdate]
    [RunIf(nameof(Running))]
    public static void AddOnSpace(BehaviorContext ctx)
    {
        if (ctx.Input.KeyPressed(Key.Space)) Spawn(ctx, 100);
    }

    /// <summary>Runs once per entity that has a <see cref="Ball"/>, with the ball by reference.</summary>
    [OnPostUpdate]
    public void Move(BehaviorContext ctx)
    {
        var dt = (float)ctx.Time.DeltaSeconds;
        Velocity.Y -= 9.81f * dt;
        Position += Velocity * dt;
        if (Position.Y < 0.3f)
        {
            Position.Y = 0.3f;
            Velocity.Y = MathF.Abs(Velocity.Y) * 0.8f;
        }
    }

    private static void Spawn(BehaviorContext ctx, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var ball = new Ball
            {
                Position = new Vector3(Random.Shared.NextSingle() * 10 - 5, 4 + Random.Shared.NextSingle() * 6, Random.Shared.NextSingle() * 10 - 5),
                Velocity = new Vector3(Random.Shared.NextSingle() * 2 - 1, 0, Random.Shared.NextSingle() * 2 - 1),
                Color = new Color((byte)Random.Shared.Next(60, 255), (byte)Random.Shared.Next(60, 255), (byte)Random.Shared.Next(60, 255)),
            };
            ctx.Cmd.Spawn((entity, world) => world.Add(entity, ball));
        }
    }
}
