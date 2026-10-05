// raylib's shapes_basic_shapes example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesBasicShapes
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] basic shapes");

        float rotation = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            rotation += 0.2f;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("some basic shapes available on raylib", 20, 20, 20, Color.DarkGray);

                DrawCircle(screenWidth/5, 120, 35, Color.DarkBlue);
                DrawCircleGradient(new Vector2(screenWidth/5.0f, 220.0f), 60, Color.Green, Color.SkyBlue);
                DrawCircleLines(screenWidth/5, 340, 80, Color.DarkBlue);
                DrawEllipse(screenWidth/5, 120, 25, 20, Color.Yellow);
                DrawEllipseLines(screenWidth/5, 120, 30, 25, Color.Yellow);

                DrawRectangle(screenWidth/4*2 - 60, 100, 120, 60, Color.Red);
                DrawRectangleGradientH(screenWidth/4*2 - 90, 170, 180, 130, Color.Maroon, Color.Gold);
                DrawRectangleLines(screenWidth/4*2 - 40, 320, 80, 60, Color.Orange);

                DrawTriangle(new Vector2(screenWidth/4.0f *3.0f, 80.0f),
                             new Vector2(screenWidth/4.0f *3.0f - 60.0f, 150.0f),
                             new Vector2(screenWidth/4.0f *3.0f + 60.0f, 150.0f), Color.Violet);

                DrawTriangleLines(new Vector2(screenWidth/4.0f*3.0f, 160.0f),
                                  new Vector2(screenWidth/4.0f*3.0f - 20.0f, 230.0f),
                                  new Vector2(screenWidth/4.0f*3.0f + 20.0f, 230.0f), Color.DarkBlue);

                DrawPoly(new Vector2(screenWidth/4.0f*3, 330), 6, 80, rotation, Color.Brown);
                DrawPolyLines(new Vector2(screenWidth/4.0f*3, 330), 6, 90, rotation, Color.Brown);
                DrawPolyLinesEx(new Vector2(screenWidth/4.0f*3, 330), 6, 85, rotation, 6, Color.Beige);

                DrawLine(18, 42, screenWidth - 18, 42, Color.Black);
            EndDrawing();
        }

        CloseWindow();
    }
}
