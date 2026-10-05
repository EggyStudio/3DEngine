// raylib's core_input_mouse example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreInputMouse
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] input mouse");

        Vector2 ballPosition = new(-100.0f, -100.0f);
        Color ballColor = Color.DarkBlue;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.H))
            {
                if (IsCursorHidden()) ShowCursor();
                else HideCursor();
            }

            ballPosition = GetMousePosition();

            if (IsMouseButtonPressed(MouseButton.Left)) ballColor = Color.Maroon;
            else if (IsMouseButtonPressed(MouseButton.Middle)) ballColor = Color.Lime;
            else if (IsMouseButtonPressed(MouseButton.Right)) ballColor = Color.DarkBlue;
            // raylib's side and extra buttons are SDL's first and second extra ones. Its forward and
            // back buttons are a mouse's that SDL, raylib's backend here as well, never reports.
            else if (IsMouseButtonPressed(MouseButton.X1)) ballColor = Color.Purple;
            else if (IsMouseButtonPressed(MouseButton.X2)) ballColor = Color.Yellow;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawCircleV(ballPosition, 40, ballColor);

                DrawText("move ball with mouse and click mouse button to change color", 10, 10, 20, Color.DarkGray);
                DrawText("Press 'H' to toggle cursor visibility", 10, 30, 20, Color.DarkGray);

                if (IsCursorHidden()) DrawText("CURSOR HIDDEN", 20, 60, 20, Color.Red);
                else DrawText("CURSOR VISIBLE", 20, 60, 20, Color.Lime);

            EndDrawing();
        }

        CloseWindow();
    }
}
