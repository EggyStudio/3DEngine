// raylib's core_3d_camera_free example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.E3D;

namespace Engine.Examples;

public static class Core3DCameraFree
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] 3d camera free");

        Camera3D camera = default;
        camera.Position = new Vector3(10.0f, 10.0f, 10.0f);
        camera.Target = new Vector3(0.0f, 0.0f, 0.0f);
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
        camera.FovY = 45.0f;
        camera.Projection = CameraProjection.Perspective;

        Vector3 cubePosition = new(0.0f, 0.0f, 0.0f);

        DisableCursor();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.Free);

            if (IsKeyPressed(Key.Z)) camera.Target = new Vector3(0.0f, 0.0f, 0.0f);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawCube(cubePosition, 2.0f, 2.0f, 2.0f, Color.Red);
                    DrawCubeWires(cubePosition, 2.0f, 2.0f, 2.0f, Color.Maroon);

                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawRectangle( 10, 10, 320, 93, Fade(Color.SkyBlue, 0.5f));
                DrawRectangleLines( 10, 10, 320, 93, Color.Blue);

                DrawText("Free camera default controls:", 20, 20, 10, Color.Black);
                DrawText("- Mouse Wheel to Zoom in-out", 40, 40, 10, Color.DarkGray);
                DrawText("- Mouse Wheel Pressed to Pan", 40, 60, 10, Color.DarkGray);
                DrawText("- Z to zoom to (0, 0, 0)", 40, 80, 10, Color.DarkGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
