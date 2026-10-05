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

var camera = new Camera3D(new Vector3(0, 12, 10), Vector3.Zero, Vector3.UnitY, 45);
var player = Vector3.Zero;

while (!WindowShouldClose())
{

    BeginDrawing();
    ClearBackground(new Color(120, 170, 230));
    BeginMode3D(camera);
    DrawModel(floor, Vector3.Zero, 1, new Color(90, 140, 80));
    DrawModel(box, player + new Vector3(0, 0.4f, 0), 0.8f, new Color(230, 90, 60));
    EndMode3D();
    EndDrawing();
}

CloseWindow();
