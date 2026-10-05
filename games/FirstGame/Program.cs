using System.Numerics;
using Engine;
using static Engine.Engine3D;

InitWindow(1280, 720, "Coins");
InitAudioDevice();
SetTargetFPS(60);

// A sun that casts shadows, and a little light from all around.
CreateDirectionalLight(new Vector3(-0.4f, -1, -0.3f), Color.White, 1.5f, castsShadows: true);
SetAmbientLight(new Color(180, 200, 255), 0.4f);

var box = LoadModelFromMesh(GenMeshCube(1, 1, 1));
var ball = LoadModelFromMesh(GenMeshSphere(0.3f, 16, 16));
var floor = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));

// A short rising chime, made from its samples rather than read from a file.
var samples = new float[11025];
for (int i = 0; i < samples.Length; i++)
{
    var t = i / 44100f;
    samples[i] = MathF.Sin(t * MathF.Tau * (880 + 880 * t * 4)) * (1 - i / (float)samples.Length) * 0.4f;
}
var ding = LoadSoundFromWave(new Wave { Samples = samples, SampleRate = 44100, Channels = 1 });

// The level: where the coins and the crates are, read from a scene file.
var coinsAtStart = new List<Vector3>();
var crates = new List<BoundingBox>();
var ecs = GetApp().World.Resource<EcsWorld>();
foreach (var entity in LoadScene("resources/level.json"))
{
    var at = ecs.GetReadOnly<Transform>(entity).Position;
    if (ecs.Has<Coin>(entity)) coinsAtStart.Add(at);
    if (ecs.Has<Crate>(entity))
    {
        var half = ecs.GetReadOnly<Crate>(entity).Size / 2;
        crates.Add(new BoundingBox(at - half, at + half));
    }
}

var player = Vector3.Zero;
var coins = new List<Vector3>(coinsAtStart);
var score = 0;
var time = 0f;

while (!WindowShouldClose())
{
    var dt = GetFrameTime();
    var won = coins.Count == 0;

    // Walking, four units a second, the way the keys say.
    var move = Vector3.Zero;
    if (IsKeyDown(Key.W) || IsKeyDown(Key.Up)) move.Z -= 1;
    if (IsKeyDown(Key.S) || IsKeyDown(Key.Down)) move.Z += 1;
    if (IsKeyDown(Key.A) || IsKeyDown(Key.Left)) move.X -= 1;
    if (IsKeyDown(Key.D) || IsKeyDown(Key.Right)) move.X += 1;
    if (move != Vector3.Zero && !won)
    {
        var next = player + Vector3.Normalize(move) * 4 * dt;
        next = Vector3.Clamp(next, new Vector3(-9.5f, 0, -9.5f), new Vector3(9.5f, 0, 9.5f));
        var body = new BoundingBox(next - new Vector3(0.4f, 0, 0.4f), next + new Vector3(0.4f, 0.8f, 0.4f));
        if (!crates.Any(crate => CheckCollisionBoxes(body, crate))) player = next;
    }

    // A coin within reach is collected.
    for (int i = coins.Count - 1; i >= 0; i--)
        if (Vector3.Distance(player with { Y = 0.5f }, coins[i]) < 0.8f)
        {
            coins.RemoveAt(i);
            score++;
            PlaySound(ding);
        }
    if (!won) time += dt;

    // Every coin collected, R starts again.
    if (won && IsKeyPressed(Key.R))
    {
        (player, coins, score, time) = (Vector3.Zero, new List<Vector3>(coinsAtStart), 0, 0);
    }

    var camera = new Camera3D(player + new Vector3(0, 9, 8), player, Vector3.UnitY, 45);

    BeginDrawing();
    ClearBackground(new Color(120, 170, 230));
    BeginMode3D(camera);
    DrawModel(floor, Vector3.Zero, 1, new Color(90, 140, 80));
    foreach (var crate in crates)
        DrawModelEx(box, (crate.Min + crate.Max) / 2, Vector3.UnitY, 0, crate.Max - crate.Min, new Color(170, 120, 70));
    DrawModel(box, player + new Vector3(0, 0.4f, 0), 0.8f, new Color(230, 90, 60));
    foreach (var coin in coins)
        DrawModel(ball, coin + new Vector3(0, MathF.Sin((float)GetTime() * 3 + coin.X) * 0.15f, 0), 1, Color.Gold);
    EndMode3D();

    DrawText($"Coins {score} of {coinsAtStart.Count}   {time:0.0} s", 20, 20, 30, Color.White);
    if (won) DrawText($"All the coins in {time:0.0} seconds! R to play again", 20, 60, 30, Color.Yellow);
    EndDrawing();
}

CloseAudioDevice();
CloseWindow();

/// <summary>A coin to collect, placed by the level file.</summary>
[SceneComponent]
public struct Coin;

/// <summary>A crate in the way, its size in each direction.</summary>
[SceneComponent]
public struct Crate
{
    public Vector3 Size;
}
