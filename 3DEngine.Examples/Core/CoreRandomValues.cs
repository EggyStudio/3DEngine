// raylib's core_random_values example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreRandomValues
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] random values");

        int randValue = GetRandomValue(-8, 5);

        uint framesCounter = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            framesCounter++;

            if (((framesCounter/120)%2) == 1)
            {
                randValue = GetRandomValue(-8, 5);
                framesCounter = 0;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("Every 2 seconds a new random value is generated:", 130, 100, 20, Color.Maroon);

                DrawText($"{randValue}", 360, 180, 80, Color.LightGray);

            EndDrawing();
        }

        CloseWindow();
    }
}
