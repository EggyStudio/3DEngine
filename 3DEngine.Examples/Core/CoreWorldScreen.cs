// raylib's core_world_screen example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreWorldScreen
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] world screen");

        Camera3D camera = default;
        camera.Position = new Vector3(10.0f, 10.0f, 10.0f);
        camera.Target = new Vector3(0.0f, 0.0f, 0.0f);
        camera.Up = new Vector3(0.0f, 1.0f, 0.0f);
        camera.FovY = 45.0f;
        camera.Projection = CameraProjection.Perspective;

        Vector3 cubePosition = new(0.0f, 0.0f, 0.0f);
        Vector2 cubeScreenPosition = new(0.0f, 0.0f);

        DisableCursor();

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            UpdateCamera(ref camera, CameraMode.ThirdPerson);

            cubeScreenPosition = GetWorldToScreen(new Vector3(cubePosition.X, cubePosition.Y + 2.5f, cubePosition.Z), camera);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode3D(camera);

                    DrawCube(cubePosition, 2.0f, 2.0f, 2.0f, Color.Red);
                    DrawCubeWires(cubePosition, 2.0f, 2.0f, 2.0f, Color.Maroon);

                    DrawGrid(10, 1.0f);

                EndMode3D();

                DrawText("Enemy: 100/100", (int)cubeScreenPosition.X - MeasureText("Enemy: 100/100", 20)/2, (int)cubeScreenPosition.Y, 20, Color.Black);

                DrawText($"Cube position in screen space coordinates: [{(int)cubeScreenPosition.X}, {(int)cubeScreenPosition.Y}]", 10, 10, 20, Color.Lime);
                DrawText("Text 2d should be always on top of the cube", 10, 40, 20, Color.Gray);

            EndDrawing();
        }

        CloseWindow();
    }
}
