// raylib's core_input_mouse_wheel example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreInputMouseWheel
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] input mouse wheel");

        int boxPositionY = screenHeight/2 - 40;
        int scrollSpeed = 4;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            boxPositionY -= (int)(GetMouseWheelMove()*scrollSpeed);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawRectangle(screenWidth/2 - 40, boxPositionY, 80, 80, Color.Maroon);

                DrawText("Use mouse wheel to move the cube up and down!", 10, 10, 20, Color.Gray);
                DrawText($"Box position Y: {boxPositionY:000}", 10, 40, 20, Color.LightGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
