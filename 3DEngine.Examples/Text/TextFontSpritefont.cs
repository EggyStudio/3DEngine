// raylib's text_font_spritefont example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextFontSpritefont
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] font spritefont");

        const string msg1 = "THIS IS A custom SPRITE FONT...";
        const string msg2 = "...and this is ANOTHER CUSTOM font...";
        const string msg3 = "...and a THIRD one! GREAT! :D";

        Font font1 = LoadFont("resources/custom_mecha.png");
        Font font2 = LoadFont("resources/custom_alagard.png");
        Font font3 = LoadFont("resources/custom_jupiter_crash.png");

        Vector2 fontPosition1 = new(screenWidth/2.0f - MeasureTextEx(font1, msg1, (float)font1.BaseSize, -3).X/2,
                                    screenHeight/2.0f - font1.BaseSize/2.0f - 80.0f);

        Vector2 fontPosition2 = new(screenWidth/2.0f - MeasureTextEx(font2, msg2, (float)font2.BaseSize, -2.0f).X/2.0f,
                                    screenHeight/2.0f - font2.BaseSize/2.0f - 10.0f);

        Vector2 fontPosition3 = new(screenWidth/2.0f - MeasureTextEx(font3, msg3, (float)font3.BaseSize, 2.0f).X/2.0f,
                                    screenHeight/2.0f - font3.BaseSize/2.0f + 50.0f);

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawTextEx(font1, msg1, fontPosition1, (float)font1.BaseSize, -3, Color.White);
                DrawTextEx(font2, msg2, fontPosition2, (float)font2.BaseSize, -2, Color.White);
                DrawTextEx(font3, msg3, fontPosition3, (float)font3.BaseSize, 2, Color.White);

            EndDrawing();
        }

        UnloadFont(font1);
        UnloadFont(font2);
        UnloadFont(font3);

        CloseWindow();
    }
}
