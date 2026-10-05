// raylib's shapes_polygon_lines example, Copyright (c) 2026 Matthew Roush (@MatthewRoush), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesPolygonLines
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] polygon lines");

        int thick = 2;
        float rotation = 0.0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Up))   thick = thick + 1;
            if (IsKeyPressed(Key.Down)) thick = thick - 1;

            thick = (thick < 1) ? 1 : (thick > 60) ? 60 : thick;

            rotation += (360.0f/10.0f)*GetFrameTime();

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText($"thick = {thick}", 10, 280, 20, Color.Lime);

                DrawText("These should look identical!", 10, 10, 20, Color.Maroon);

                DrawRectangle(10, 40, 50, 50, Color.LightGray);
                DrawRectangleLinesEx(new Rectangle(10, 40, 50, 50), (float)thick, Color.Red);

                DrawCircle(95, 65, 25, Color.LightGray);
                DrawCircleLinesEx(new Vector2(95, 65), 25, (float)thick, Color.Red);

                DrawText("DrawRectangleLinesEx() and DrawCircleLinesEx()", 130, 60, 10, Color.Black);

                DrawPoly(new Vector2(35, 125), 4, 35.355f, 45, Color.LightGray);
                DrawPolyLinesEx(new Vector2(35, 125), 4, 35.355f, 45, (float)thick, Color.Red);

                DrawPoly(new Vector2(95, 125), 36, 25, 0, Color.LightGray);
                DrawPolyLinesEx(new Vector2(95, 125), 36, 25, 0, (float)thick, Color.Red);

                DrawText("DrawPolyLinesEx()", 130, 120, 10, Color.Black);

                DrawPoly(new Vector2(290, 220), 3, 60, rotation, Color.LightGray);
                DrawPolyLinesEx(new Vector2(290, 220), 3, 60, rotation, (float)thick, Color.Blue);

                DrawPoly(new Vector2(430, 220), 4, 60, rotation, Color.LightGray);
                DrawPolyLinesEx(new Vector2(430, 220), 4, 60, rotation, (float)thick, Color.Blue);

                DrawPoly(new Vector2(570, 220), 5, 60, rotation, Color.LightGray);
                DrawPolyLinesEx(new Vector2(570, 220), 5, 60, rotation, (float)thick, Color.Blue);

                DrawPoly(new Vector2(710, 220), 6, 60, rotation, Color.LightGray);
                DrawPolyLinesEx(new Vector2(710, 220), 6, 60, rotation, (float)thick, Color.Blue);

                DrawPoly(new Vector2(290, 360), 7, 60, rotation, Color.LightGray);
                DrawPolyLinesEx(new Vector2(290, 360), 7, 60, rotation, (float)thick, Color.Blue);

                DrawPoly(new Vector2(430, 360), 8, 60, rotation, Color.LightGray);
                DrawPolyLinesEx(new Vector2(430, 360), 8, 60, rotation, (float)thick, Color.Blue);

                DrawPoly(new Vector2(570, 360), 9, 60, rotation, Color.LightGray);
                DrawPolyLinesEx(new Vector2(570, 360), 9, 60, rotation, (float)thick, Color.Blue);

                DrawPoly(new Vector2(710, 360), 10, 60, rotation, Color.LightGray);
                DrawPolyLinesEx(new Vector2(710, 360), 10, 60, rotation, (float)thick, Color.Blue);

            EndDrawing();
        }

        CloseWindow();
    }
}
