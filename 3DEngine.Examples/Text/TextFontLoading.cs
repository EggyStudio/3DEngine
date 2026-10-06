// raylib's text_font_loading example, Copyright (c) 2016-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextFontLoading
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] font loading");

        // Define characters to draw
        // NOTE: raylib supports UTF-8 encoding, following list is actually codified as UTF8 internally
        const string msg = "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHI\nJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklm\nnopqrstuvwxyz{|}~¿ÀÁÂÃÄÅÆÇÈÉÊËÌÍÎÏÐÑÒ\nÓÔÕÖ×ØÙÚÛÜÝÞßàáâãäåæçèéêëìíîïðñòóôõö\n÷øùúûüýþÿ";

        // BMFont (AngelCode) : Font data and image atlas have been generated using external program
        Font fontBm = LoadFont("resources/pixantiqua.fnt"); // Requires "resources/pixantiqua.png"

        // TTF font : Font data and atlas are generated directly from TTF
        // NOTE: We define a font base size of 32 pixels tall and up-to 250 characters, from the space on
        Font fontTtf = LoadFontEx("resources/pixantiqua.ttf", 32, Enumerable.Range(32, 250).ToArray());

        SetTextLineSpacing(16);         // Set line spacing for multiline text (when line breaks are included '\n')

        bool useTtf = false;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyDown(Key.Space)) useTtf = true;
            else useTtf = false;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("Hold SPACE to use TTF generated font", 20, 20, 20, Color.LightGray);

                if (!useTtf)
                {
                    DrawTextEx(fontBm, msg, new Vector2(20.0f, 100.0f), (float)fontBm.BaseSize, 2, Color.Maroon);
                    DrawText("Using BMFont (Angelcode) imported", 20, GetScreenHeight() - 30, 20, Color.Gray);
                }
                else
                {
                    DrawTextEx(fontTtf, msg, new Vector2(20.0f, 100.0f), (float)fontTtf.BaseSize, 2, Color.Lime);
                    DrawText("Using TTF font generated", 20, GetScreenHeight() - 30, 20, Color.Gray);
                }

            EndDrawing();
        }

        UnloadFont(fontBm);     // AngelCode Font unloading
        UnloadFont(fontTtf);    // TTF Font unloading

        CloseWindow();
    }
}
