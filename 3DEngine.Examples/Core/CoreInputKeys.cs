// raylib's core_input_keys example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreInputKeys
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] input keys");

        Vector2 ballPosition = new((float)screenWidth/2, (float)screenHeight/2);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyDown(Key.Right)) ballPosition.X += 2.0f;
            if (IsKeyDown(Key.Left)) ballPosition.X -= 2.0f;
            if (IsKeyDown(Key.Up)) ballPosition.Y -= 2.0f;
            if (IsKeyDown(Key.Down)) ballPosition.Y += 2.0f;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("move the ball with arrow keys", 10, 10, 20, Color.DarkGray);

                DrawCircleV(ballPosition, 50, Color.Maroon);

            EndDrawing();
        }

        CloseWindow();
    }
}
