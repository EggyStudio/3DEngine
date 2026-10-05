using System.Numerics;
using Engine;
using static Engine.Engine3D;

InitWindow(1280, 720, "Hello");
SetTargetFPS(60);
GetApp().World.InsertResource(new Tuning());

var camera = new Camera3D(new Vector3(6, 4, 6), new Vector3(0, 0.5f, 0), Vector3.UnitY, 45);
var cube = LoadModelFromMesh(GenMeshCube(1, 1, 1));
var ecs = GetApp().World.Resource<EcsWorld>();

while (!WindowShouldClose())
{
    // The behaviors run in BeginDrawing, so the loop draws what they left this frame.
    BeginDrawing();
    ClearBackground(Color.RayWhite);

    BeginMode3D(camera);
    foreach (var (_, spinner) in ecs.Query<Spinner>())
        DrawModelEx(cube, spinner.Position, Vector3.UnitY, spinner.Angle, Vector3.One, spinner.Color);
    DrawGrid(10, 1);
    EndMode3D();

    DrawText("Three cubes turned by a behavior. source/behaviors/Speed.cs sets how fast.", 10, 10, 20, Color.DarkGray);
    DrawFPS(10, 40);
    EndDrawing();
}

CloseWindow();

/// <summary>The numbers the scripts in source/behaviors set while the game runs.</summary>
public sealed class Tuning
{
    public float DegreesPerSecond = 90;
}

/// <summary>A cube that turns. Its fields are each entity's state, and its methods run every frame.</summary>
[Behavior]
public struct Spinner
{
    public Vector3 Position;
    public float Angle;
    public Color Color;

    [OnStartup]
    public static void Spawn(BehaviorContext ctx)
    {
        Color[] colors = [Color.Red, Color.Green, Color.Blue];
        for (int i = 0; i < colors.Length; i++)
        {
            var spinner = new Spinner { Position = new Vector3((i - 1) * 2, 0.5f, 0), Angle = i * 30, Color = colors[i] };
            ctx.Cmd.Spawn((entity, world) => world.Add(entity, spinner));
        }
    }

    [OnUpdate]
    public void Turn(BehaviorContext ctx) => Angle += ctx.Res<Tuning>().DegreesPerSecond * (float)ctx.Time.DeltaSeconds;
}
