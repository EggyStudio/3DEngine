using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesBasic3D
{
    public static void Run()
    {
        InitWindow(800, 450, "[shapes] basic 3d shapes");

        var camera = new Camera3D(new Vector3(0, 10, 10), Vector3.Zero, Vector3.UnitY, 45);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Orbital);

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            BeginMode3D(camera);
            DrawCube(new Vector3(-4, 0, 2), 2, 5, 2, Color.Red);
            DrawCubeWires(new Vector3(-4, 0, 2), 2, 5, 2, Color.Gold);
            DrawCubeWires(new Vector3(-4, 0, -2), 3, 6, 2, Color.Maroon);

            DrawSphere(new Vector3(-1, 0, -2), 1, Color.Green);
            DrawSphereWires(new Vector3(1, 0, 2), 2, 16, 16, Color.Lime);

            DrawPlane(new Vector3(4, 0, -2), new Vector2(3, 3), Color.SkyBlue);
            DrawLine3D(new Vector3(4, 0, 2), new Vector3(4, 4, 2), Color.Purple);

            DrawGrid(10, 1);
            EndMode3D();

            DrawFPS(10, 10);
            EndDrawing();
        }

        CloseWindow();
    }
}
