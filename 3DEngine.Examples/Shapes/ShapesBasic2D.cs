using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesBasic2D
{
    public static void Run()
    {
        InitWindow(800, 450, "[shapes] basic 2d shapes");
        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();
            ClearBackground(Color.RayWhite);

            DrawText("some basic shapes available", 20, 20, 20, Color.DarkGray);

            DrawCircle(GetScreenWidth() / 5, 120, 35, Color.DarkBlue);
            DrawCircleLines(GetScreenWidth() / 5, 220, 80, Color.DarkBlue);

            DrawRectangle(GetScreenWidth() / 4 * 2 - 60, 100, 120, 60, Color.Red);
            DrawRectangleLines(GetScreenWidth() / 4 * 2 - 40, 320, 80, 60, Color.Orange);

            DrawTriangle(new Vector2(GetScreenWidth() / 4f * 3, 80), new Vector2(GetScreenWidth() / 4f * 3 - 60, 150),
                new Vector2(GetScreenWidth() / 4f * 3 + 60, 150), Color.Violet);

            DrawLine(18, 42, GetScreenWidth() - 18, 42, Color.Black);

            EndDrawing();
        }

        CloseWindow();
    }
}
