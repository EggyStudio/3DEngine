using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class Core3DCameraFree
{
    public static void Run()
    {
        InitWindow(800, 450, "[core] 3d camera free");

        var camera = new Camera3D(new Vector3(10, 10, 10), Vector3.Zero, Vector3.UnitY, 45);
        var cubePosition = Vector3.Zero;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Free);

            if (IsKeyPressed(Key.Z)) camera.Target = Vector3.Zero;

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            BeginMode3D(camera);
            DrawCube(cubePosition, 2, 2, 2, Color.Red);
            DrawCubeWires(cubePosition, 2, 2, 2, Color.Maroon);
            DrawGrid(10, 1);
            EndMode3D();

            DrawRectangle(10, 10, 320, 93, Color.SkyBlue.Fade(0.5f));
            DrawRectangleLines(10, 10, 320, 93, Color.Blue);
            DrawText("Free camera default controls:", 20, 20, 10, Color.Black);
            DrawText("- W, A, S, D, Q, E to move", 40, 40, 10, Color.DarkGray);
            DrawText("- Right mouse button drag to turn", 40, 60, 10, Color.DarkGray);
            DrawText("- Z to look at the origin", 40, 80, 10, Color.DarkGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
