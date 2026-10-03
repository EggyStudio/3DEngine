using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class Core3DCameraFirstPerson
{
    public static void Run()
    {
        InitWindow(800, 450, "[core] 3d camera first person");

        var camera = new Camera3D(new Vector3(0, 2, 4), new Vector3(0, 2, 0), Vector3.UnitY, 60);
        var mode = CameraMode.FirstPerson;

        // Columns of random heights and colors around the floor.
        var random = new Random(7);
        var columns = Enumerable.Range(0, 20).Select(_ =>
        {
            var height = random.Next(1, 12);
            return (Position: new Vector3(random.Next(-15, 16), height / 2f, random.Next(-15, 16)), Height: (float)height,
                Color: new Color((byte)random.Next(20, 255), (byte)random.Next(10, 55), 30));
        }).ToArray();

        DisableCursor();
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Alpha1)) mode = CameraMode.Free;
            if (IsKeyPressed(Key.Alpha2)) mode = CameraMode.FirstPerson;
            if (IsKeyPressed(Key.Alpha3)) mode = CameraMode.ThirdPerson;
            if (IsKeyPressed(Key.Alpha4)) mode = CameraMode.Orbital;
            if (IsKeyPressed(Key.Tab))
            {
                if (IsCursorHidden()) EnableCursor();
                else DisableCursor();
            }

            UpdateCamera(ref camera, mode);

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            BeginMode3D(camera);
            DrawPlane(Vector3.Zero, new Vector2(32, 32), Color.LightGray);
            DrawCube(new Vector3(-16, 2.5f, 0), 1, 5, 32, Color.Blue);
            DrawCube(new Vector3(16, 2.5f, 0), 1, 5, 32, Color.Lime);
            DrawCube(new Vector3(0, 2.5f, 16), 32, 5, 1, Color.Gold);
            foreach (var (position, height, color) in columns)
            {
                DrawCube(position, 2, height, 2, color);
                DrawCubeWires(position, 2, height, 2, Color.Maroon);
            }
            if (mode == CameraMode.ThirdPerson)
            {
                DrawCube(camera.Target, 0.5f, 0.5f, 0.5f, Color.Purple);
                DrawCubeWires(camera.Target, 0.5f, 0.5f, 0.5f, Color.DarkPurple);
            }
            EndMode3D();

            DrawRectangle(5, 5, 330, 100, Color.SkyBlue.Fade(0.5f));
            DrawText("Camera modes:", 15, 15, 10, Color.Black);
            DrawText("1 free, 2 first person, 3 third person, 4 orbital", 15, 35, 10, Color.Black);
            DrawText("W A S D move, the mouse looks, Tab frees the cursor", 15, 55, 10, Color.Black);
            DrawText($"Mode: {mode}", 15, 75, 10, Color.Black);
            EndDrawing();
        }

        CloseWindow();
    }
}
