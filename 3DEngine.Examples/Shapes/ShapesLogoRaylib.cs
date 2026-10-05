// raylib's shapes_logo_raylib example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesLogoRaylib
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] logo raylib");

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawRectangle(screenWidth/2 - 128, screenHeight/2 - 128, 256, 256, Color.Black);
                DrawRectangle(screenWidth/2 - 112, screenHeight/2 - 112, 224, 224, Color.RayWhite);
                DrawText("raylib", screenWidth/2 - 44, screenHeight/2 + 48, 50, Color.Black);

                DrawText("this is NOT a texture!", 350, 370, 10, Color.Gray);

            EndDrawing();
        }

        CloseWindow();
    }
}
