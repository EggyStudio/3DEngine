using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

/// <summary>The screens of the example, as a state the app moves between.</summary>
public enum Screen { Title, Playing, Paused }

public static class EcsStates
{
    public static void Run()
    {
        InitWindow(800, 450, "[ecs] states");

        // Adding the state is what wakes the Orbiter's transition and InState methods. The other
        // examples never add it, so the behavior stays asleep there.
        GetApp().AddState(Screen.Title);
        var world = GetApp().World;
        var camera = new Camera3D(new Vector3(10, 7, 10), Vector3.Zero, Vector3.UnitY, 45);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            // A move is queued here and applied at the start of the next frame, so everything this
            // frame still sees the screen it began on.
            var screen = world.Resource<State<Screen>>().Current;
            var next = world.Resource<NextState<Screen>>();
            if (screen == Screen.Title && IsKeyPressed(Key.Return)) next.Set(Screen.Playing);
            if (screen == Screen.Playing && IsKeyPressed(Key.P)) next.Set(Screen.Paused);
            if (screen == Screen.Paused && IsKeyPressed(Key.P)) next.Set(Screen.Playing);
            if (screen != Screen.Title && IsKeyPressed(Key.Q)) next.Set(Screen.Title);

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            if (screen == Screen.Title)
            {
                DrawText("STATES", 330, 160, 40, Color.DarkGray);
                DrawText("Enter to play", 330, 220, 20, Color.Gray);
            }
            else
            {
                BeginMode3D(camera);
                foreach (var (_, orbiter) in world.Resource<EcsWorld>().Query<Orbiter>())
                    DrawCube(orbiter.Where(), 0.8f, 0.8f, 0.8f, orbiter.Color);
                DrawGrid(10, 1);
                EndMode3D();

                if (screen == Screen.Paused)
                {
                    DrawRectangle(0, 0, GetScreenWidth(), GetScreenHeight(), Color.RayWhite.Fade(0.6f));
                    DrawText("PAUSED", 340, 200, 40, Color.DarkGray);
                }
                DrawText("P pauses, Q returns to the title", 10, 10, 20, Color.DarkGray);
            }

            EndDrawing();
        }

        CloseWindow();
    }
}

/// <summary>A cube circling the origin, which exists while a game is on.</summary>
[Behavior]
public struct Orbiter
{
    public float Angle;
    public float Radius;
    public float Speed;
    public Color Color;

    public readonly Vector3 Where() => new(MathF.Cos(Angle) * Radius, 0.4f, MathF.Sin(Angle) * Radius);

    /// <summary>Builds the level on leaving the title, so resuming from a pause keeps it.</summary>
    [OnExit(Screen.Title)]
    public static void SpawnLevel(BehaviorContext ctx)
    {
        for (int i = 0; i < 12; i++)
        {
            var orbiter = new Orbiter
            {
                Angle = i * MathF.Tau / 12,
                Radius = 2 + i % 3,
                Speed = 0.5f + i % 3 * 0.25f,
                Color = new Color((byte)(80 + i * 14), 120, (byte)(230 - i * 14)),
            };
            ctx.Cmd.Spawn((entity, ecs) => ecs.Add(entity, orbiter));
        }
    }

    /// <summary>Clears the level on returning to the title.</summary>
    [OnEnter(Screen.Title)]
    public static void ClearLevel(BehaviorContext ctx)
    {
        foreach (var (entity, _) in ctx.Ecs.Query<Orbiter>())
            ctx.Cmd.Despawn(entity);
    }

    /// <summary>Runs per orbiter while playing and not while paused.</summary>
    [OnUpdate]
    [InState(Screen.Playing)]
    public void Orbit(BehaviorContext ctx) => Angle += Speed * (float)ctx.Time.DeltaSeconds;
}
