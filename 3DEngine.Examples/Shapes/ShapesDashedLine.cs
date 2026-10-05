// raylib's shapes_dashed_line example, Copyright (c) 2025 Luís Almeida (@luis605), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesDashedLine
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] dashed line");

        Vector2 lineStartPosition = new(20.0f, 50.0f);
        Vector2 lineEndPosition = new(780.0f, 400.0f);
        float dashLength = 25.0f;
        float blankLength = 15.0f;

        Color[] lineColors = { Color.Red, Color.Orange, Color.Gold, Color.Green, Color.Blue, Color.Violet, Color.Pink, Color.Black };
        int colorIndex = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            lineEndPosition = GetMousePosition();

            if (IsKeyDown(Key.Up)) dashLength += 1.0f;
            if (IsKeyDown(Key.Down) && dashLength > 1.0f) dashLength -= 1.0f;

            if (IsKeyDown(Key.Right)) blankLength += 1.0f;
            if (IsKeyDown(Key.Left) && blankLength > 1.0f) blankLength -= 1.0f;

            if (IsKeyPressed(Key.C)) colorIndex = (colorIndex + 1)%lineColors.Length;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawLineDashed(lineStartPosition, lineEndPosition, (int)dashLength, (int)blankLength, lineColors[colorIndex]);

                DrawRectangle(5, 5, 265, 95, Fade(Color.SkyBlue, 0.5f));
                DrawRectangleLines(5, 5, 265, 95, Color.Blue);

                DrawText("CONTROLS:", 15, 15, 10, Color.Black);
                DrawText("UP/DOWN: Change Dash Length", 15, 35, 10, Color.Black);
                DrawText("LEFT/RIGHT: Change Space Length", 15, 55, 10, Color.Black);
                DrawText("C: Cycle Color", 15, 75, 10, Color.Black);

                DrawText($"Dash: {dashLength:0.} | Space: {blankLength:0.}", 15, 115, 10, Color.DarkGray);

                DrawFPS(screenWidth - 80, 10);

            EndDrawing();
        }

        CloseWindow();
    }
}
