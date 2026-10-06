// raylib's text_font_sdf example, Copyright (c) 2015-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextFontSdf
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] font sdf");

        const string msg = "Signed Distance Fields";

        // The 95 characters of ASCII, which raylib's font data is made for. raylib builds each font
        // from the file's glyph data and an atlas of its own, and the flat API bakes one in a call,
        // the distance field drawn sharp by the engine's own text shader where raylib's example
        // loads one of its own.
        int[] ascii = Enumerable.Range(32, 95).ToArray();
        Font fontDefault = LoadFontEx("resources/anonymous_pro_bold.ttf", 16, ascii, FontType.Default);
        Font fontSDF = LoadFontEx("resources/anonymous_pro_bold.ttf", 16, ascii, FontType.Sdf);
        SetTextureFilter(fontSDF.Texture, TextureFilter.Bilinear);

        Vector2 fontPosition = new(40, screenHeight/2.0f - 50);
        Vector2 textSize = Vector2.Zero;
        float fontSize = 16.0f;
        int currentFont = 0;            // 0 the default font, 1 the distance field

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            fontSize += GetMouseWheelMove()*8.0f;

            if (fontSize < 6) fontSize = 6;

            if (IsKeyDown(Key.Space)) currentFont = 1;
            else currentFont = 0;

            if (currentFont == 0) textSize = MeasureTextEx(fontDefault, msg, fontSize, 0);
            else textSize = MeasureTextEx(fontSDF, msg, fontSize, 0);

            fontPosition.X = (float)GetScreenWidth()/2 - textSize.X/2;
            fontPosition.Y = (float)GetScreenHeight()/2 - textSize.Y/2 + 80;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                if (currentFont == 1)
                {
                    DrawTextEx(fontSDF, msg, fontPosition, fontSize, 0, Color.Black);
                    DrawTexture(fontSDF.Texture, 10, 10, Color.Black);
                }
                else
                {
                    DrawTextEx(fontDefault, msg, fontPosition, fontSize, 0, Color.Black);
                    DrawTexture(fontDefault.Texture, 10, 10, Color.Black);
                }

                if (currentFont == 1) DrawText("SDF!", 320, 20, 80, Color.Red);
                else DrawText("default font", 315, 40, 30, Color.Gray);

                DrawText("FONT SIZE: 16.0", GetScreenWidth() - 240, 20, 20, Color.DarkGray);
                DrawText($"RENDER SIZE: {fontSize:00.00}", GetScreenWidth() - 240, 50, 20, Color.DarkGray);
                DrawText("Use MOUSE WHEEL to SCALE TEXT!", GetScreenWidth() - 240, 90, 10, Color.DarkGray);

                DrawText("HOLD SPACE to USE SDF FONT VERSION!", 340, GetScreenHeight() - 30, 20, Color.Maroon);

            EndDrawing();
        }

        UnloadFont(fontDefault);
        UnloadFont(fontSDF);

        CloseWindow();
    }
}
