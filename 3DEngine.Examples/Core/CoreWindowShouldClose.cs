// raylib's core_window_should_close example, Copyright (c) 2013-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreWindowShouldClose
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] window should close");

        SetExitKey(Key.Null);

        bool exitWindowRequested = false;
        bool exitWindow = false;

        SetTargetFPS(60);

        while (!exitWindow)
        {
            if (WindowShouldClose() || IsKeyPressed(Key.Escape)) exitWindowRequested = true;

            if (exitWindowRequested)
            {
                if (IsKeyPressed(Key.Y)) exitWindow = true;
                else if (IsKeyPressed(Key.N)) exitWindowRequested = false;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (exitWindowRequested)
                {
                    DrawRectangle(0, 100, screenWidth, 200, Color.Black);
                    DrawText("Are you sure you want to exit program? [Y/N]", 40, 180, 30, Color.White);
                }
                else DrawText("Try to close the window to get confirmation message!", 120, 200, 20, Color.LightGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
