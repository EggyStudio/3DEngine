// raylib's core_3d_camera_mode example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class Core3DCameraMode
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] 3d camera mode");

        Camera3D camera = default;
        camera.Position = new Vector3(0.0f, 10.0f, 10.0f);
        camera.Target = new Vector3(0.0f, 0.0f, 0.0f);
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
        camera.FovY = 45.0f;
        camera.Projection = CameraProjection.Perspective;

        Vector3 cubePosition = new(0.0f, 0.0f, 0.0f);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawCube(cubePosition, 2.0f, 2.0f, 2.0f, Color.Red);
                    DrawCubeWires(cubePosition, 2.0f, 2.0f, 2.0f, Color.Maroon);

                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawText("Welcome to the third dimension!", 10, 40, 20, Color.DarkGray);

                DrawFPS(10, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
