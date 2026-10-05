using System.Numerics;
using Engine;
using static Engine.Engine3D;

InitWindow(1280, 720, "Coins");
SetTargetFPS(60);

// A sun that casts shadows, and a little light from all around.
CreateDirectionalLight(new Vector3(-0.4f, -1, -0.3f), Color.White, 1.5f, castsShadows: true);
SetAmbientLight(new Color(180, 200, 255), 0.4f);

var box = LoadModelFromMesh(GenMeshCube(1, 1, 1));
var ball = LoadModelFromMesh(GenMeshSphere(0.3f, 16, 16));
var floor = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));

var player = Vector3.Zero;
// Coins to collect, each where it floats over the floor.
var coins = new List<Vector3>
{
    new(3, 0.5f, 0), new(-4, 0.5f, 2), new(0, 0.5f, -5), new(6, 0.5f, -6), new(-7, 0.5f, -3),
};
var score = 0;

while (!WindowShouldClose())
{
    var dt = GetFrameTime();

    // Walking, four units a second, the way the keys say.
    var move = Vector3.Zero;
    if (IsKeyDown(Key.W) || IsKeyDown(Key.Up)) move.Z -= 1;
    if (IsKeyDown(Key.S) || IsKeyDown(Key.Down)) move.Z += 1;
    if (IsKeyDown(Key.A) || IsKeyDown(Key.Left)) move.X -= 1;
    if (IsKeyDown(Key.D) || IsKeyDown(Key.Right)) move.X += 1;
    if (move != Vector3.Zero) player += Vector3.Normalize(move) * 4 * dt;

    // A coin within reach is collected.
    for (int i = coins.Count - 1; i >= 0; i--)
        if (Vector3.Distance(player with { Y = 0.5f }, coins[i]) < 0.8f)
        {
            coins.RemoveAt(i);
            score++;
        }

    var camera = new Camera3D(player + new Vector3(0, 9, 8), player, Vector3.UnitY, 45);

    BeginDrawing();
    ClearBackground(new Color(120, 170, 230));
    BeginMode3D(camera);
    DrawModel(floor, Vector3.Zero, 1, new Color(90, 140, 80));
    DrawModel(box, player + new Vector3(0, 0.4f, 0), 0.8f, new Color(230, 90, 60));
    foreach (var coin in coins)
        DrawModel(ball, coin + new Vector3(0, MathF.Sin((float)GetTime() * 3 + coin.X) * 0.15f, 0), 1, Color.Gold);
    EndMode3D();
    EndDrawing();
}

CloseWindow();
