using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

public static class EcsParentedParticles
{
    public static void Run()
    {
        InitWindow(800, 450, "[ecs] parented particles");
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(new Color(6, 8, 22));
            DrawText("Stars placed on a turning sky, and a comet's tail born in its space.", 10, 10, 20, Color.RayWhite);
            EndDrawing();
        }

        CloseWindow();
    }
}

/// <summary>
/// Spawns a camera and a sky that turns, and as the sky's children a field of stars, particles
/// placed at points, and a comet whose particles are born in the sky's space, so both turn with it.
/// </summary>
[Behavior]
public struct TurningSky
{
    public static bool Running => Example.Current == "ecs_parented_particles";

    // The sky turns about an axis tilted toward the north, as the night's does.
    private static readonly Vector3 Axis = Vector3.Normalize(new Vector3(0, 0.8f, -0.6f));

    [OnStartup]
    [RunIf(nameof(Running))]
    public static void Start(BehaviorContext ctx)
    {
        var camera = ctx.Ecs.Spawn();
        ctx.Ecs.Add(camera, new Camera(fovY: 60f, near: 0.1f, far: 1000f));
        ctx.Ecs.Add(camera, new Transform(new Vector3(0, 1.6f, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.35f), Vector3.One));

        var sky = ctx.Ecs.Spawn();
        ctx.Ecs.Add(sky, new Transform(Vector3.Zero));
        ctx.Ecs.Add(sky, new TurningSky());

        // Placed once, the stars stand where they were put and turn with the sky.
        var stars = ctx.Ecs.Spawn();
        ctx.Ecs.Add(stars, new Transform(Vector3.Zero));
        ctx.Ecs.Add(stars, new Parent(ctx.Ecs.Handle(sky)));
        ctx.Ecs.Add(stars, ParticleEmitter.Default with
        {
            Emitting = false,
            MaxParticles = 1,
            Points = Stars(1500, 60),
            StartSize = 0.35f,
            EndSize = 0.35f,
            StartColor = new Color(255, 248, 230),
            EndColor = new Color(255, 248, 230),
            Intensity = 2,
        });

        // Born in the sky's space, the comet's tail keeps its shape as the sky turns, where in the
        // world's it would trail behind.
        var comet = ctx.Ecs.Spawn();
        ctx.Ecs.Add(comet, new Transform(new Vector3(18, 26, -40)));
        ctx.Ecs.Add(comet, new Parent(ctx.Ecs.Handle(sky)));
        ctx.Ecs.Add(comet, ParticleEmitter.Default with
        {
            Local = true,
            MaxParticles = 300,
            Rate = 80,
            Life = 3,
            Velocity = new Vector3(-4, -1.2f, 0),
            Spread = 5,
            Gravity = Vector3.Zero,
            StartSize = 1.2f,
            EndSize = 0.2f,
            StartColor = new Color(170, 210, 255),
            EndColor = new Color(170, 210, 255, 0),
            Intensity = 3,
        });
    }

    /// <summary>Turns the sky, the one entity carrying this behavior, and its children with it.</summary>
    [OnUpdate]
    public void Turn(BehaviorContext ctx, ref Transform transform)
    {
        var dt = (float)ctx.Time.DeltaSeconds;
        transform.Rotation = Quaternion.Normalize(transform.Rotation * Quaternion.CreateFromAxisAngle(Axis, 0.12f * dt));
    }

    // Points scattered evenly over a sphere, the same each run.
    private static Vector3[] Stars(int count, float radius)
    {
        var random = new Random(7);
        var points = new Vector3[count];
        for (int i = 0; i < count; i++)
        {
            var z = random.NextSingle() * 2 - 1;
            var angle = random.NextSingle() * MathF.Tau;
            var r = MathF.Sqrt(1 - z * z);
            points[i] = new Vector3(r * MathF.Cos(angle), z, r * MathF.Sin(angle)) * radius;
        }
        return points;
    }
}
