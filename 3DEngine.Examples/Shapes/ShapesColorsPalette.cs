// raylib's shapes_colors_palette example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class ShapesColorsPalette
{
    private const int MAX_COLORS_COUNT = 21;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[shapes] colors palette");

        Color[] colors = {
            Color.DarkGray, Color.Maroon, Color.Orange, Color.DarkGreen, Color.DarkBlue, Color.DarkPurple, Color.DarkBrown,
            Color.Gray, Color.Red, Color.Gold, Color.Lime, Color.Blue, Color.Violet, Color.Brown, Color.LightGray, Color.Pink, Color.Yellow,
            Color.Green, Color.SkyBlue, Color.Purple, Color.Beige };

        string[] colorNames = [
            "DARKGRAY", "MAROON", "ORANGE", "DARKGREEN", "DARKBLUE", "DARKPURPLE",
            "DARKBROWN", "GRAY", "RED", "GOLD", "LIME", "BLUE", "VIOLET", "BROWN",
            "LIGHTGRAY", "PINK", "YELLOW", "GREEN", "SKYBLUE", "PURPLE", "BEIGE" ];

        Rectangle[] colorsRecs = new Rectangle[MAX_COLORS_COUNT];

        for (int i = 0; i < MAX_COLORS_COUNT; i++)
        {
            colorsRecs[i] = colorsRecs[i] with { X = 20.0f + 100.0f *(i%7) + 10.0f *(i%7) };
            colorsRecs[i] = colorsRecs[i] with { Y = 80.0f + 100.0f *((int)i/7) + 10.0f *((float)i/7) };
            colorsRecs[i] = colorsRecs[i] with { Width = 100.0f };
            colorsRecs[i] = colorsRecs[i] with { Height = 100.0f };
        }

        int[] colorState = new int[MAX_COLORS_COUNT];

        Vector2 mousePoint = new(0.0f, 0.0f);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            mousePoint = GetMousePosition();

            for (int i = 0; i < MAX_COLORS_COUNT; i++)
            {
                if (CheckCollisionPointRec(mousePoint, colorsRecs[i])) colorState[i] = 1;
                else colorState[i] = 0;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("raylib colors palette", 28, 42, 20, Color.Black);
                DrawText("press SPACE to see all colors", GetScreenWidth() - 180, GetScreenHeight() - 40, 10, Color.Gray);

                for (int i = 0; i < MAX_COLORS_COUNT; i++)
                {
                    DrawRectangleRec(colorsRecs[i], Fade(colors[i], colorState[i] != 0 ? 0.6f : 1.0f));

                    if (IsKeyDown(Key.Space) || colorState[i] != 0)
                    {
                        DrawRectangle((int)colorsRecs[i].X, (int)(colorsRecs[i].Y + colorsRecs[i].Height - 26), (int)colorsRecs[i].Width, 20, Color.Black);
                        DrawRectangleLinesEx(colorsRecs[i], 6, Fade(Color.Black, 0.3f));
                        DrawText(colorNames[i], (int)(colorsRecs[i].X + colorsRecs[i].Width - MeasureText(colorNames[i], 10) - 12),
                            (int)(colorsRecs[i].Y + colorsRecs[i].Height - 20), 10, colors[i]);
                    }
                }

            EndDrawing();
        }

        CloseWindow();
    }
}
