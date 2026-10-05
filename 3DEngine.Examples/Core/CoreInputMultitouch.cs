// raylib's core_input_multitouch example, Copyright (c) 2019-2025 Berni (@Berni8k) and Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreInputMultitouch
{
    private const int MAX_TOUCH_POINTS = 10;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] input multitouch");

        Vector2[] touchPositions = new Vector2[MAX_TOUCH_POINTS];

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            int tCount = GetTouchPointCount();

            if (tCount > MAX_TOUCH_POINTS) tCount = MAX_TOUCH_POINTS;

            for (int i = 0; i < tCount; i++) touchPositions[i] = GetTouchPosition(i);

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                for (int i = 0; i < tCount; i++)
                {
                    if ((touchPositions[i].X > 0) && (touchPositions[i].Y > 0))
                    {
                        DrawCircleV(touchPositions[i], 34, Color.Orange);
                        DrawText($"{i}", (int)touchPositions[i].X - 10, (int)touchPositions[i].Y - 70, 40, Color.Black);
                    }
                }

                DrawText("touch the screen at multiple locations to get multiple balls", 10, 10, 20, Color.DarkGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
