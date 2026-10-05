using System.Numerics;
using Engine;
using static Engine.Engine3D;

InitWindow(1280, 720, "Coins");
SetTargetFPS(60);

// A sun that casts shadows, and a little light from all around.
CreateDirectionalLight(new Vector3(-0.4f, -1, -0.3f), Color.White, 1.5f, castsShadows: true);
SetAmbientLight(new Color(180, 200, 255), 0.4f);

var box = LoadModelFromMesh(GenMeshCube(1, 1, 1));
var floor = LoadModelFromMesh(GenMeshPlane(20, 20, 1, 1));

var player = Vector3.Zero;

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

    var camera = new Camera3D(player + new Vector3(0, 9, 8), player, Vector3.UnitY, 45);

    BeginDrawing();
    ClearBackground(new Color(120, 170, 230));
    BeginMode3D(camera);
    DrawModel(floor, Vector3.Zero, 1, new Color(90, 140, 80));
    DrawModel(box, player + new Vector3(0, 0.4f, 0), 0.8f, new Color(230, 90, 60));
    EndMode3D();
    EndDrawing();
}

CloseWindow();
