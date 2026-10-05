using System.Numerics;
using Engine;
using static Engine.Engine3D;

InitWindow(1280, 720, "Coins");
SetTargetFPS(60);

var camera = new Camera3D(new Vector3(4, 3, 4), Vector3.Zero, Vector3.UnitY, 45);

while (!WindowShouldClose())
{
    UpdateCamera(ref camera, CameraMode.Orbital);

    BeginDrawing();
    ClearBackground(Color.RayWhite);

    BeginMode3D(camera);
    DrawCube(new Vector3(0, 0.5f, 0), 1, 1, 1, Color.Red);
    DrawCubeWires(new Vector3(0, 0.5f, 0), 1, 1, 1, Color.Maroon);
    DrawGrid(10, 1);
    EndMode3D();

    DrawText("A cube, and the camera going round it", 10, 10, 20, Color.DarkGray);
    DrawFPS(10, 40);
    EndDrawing();
}

CloseWindow();
