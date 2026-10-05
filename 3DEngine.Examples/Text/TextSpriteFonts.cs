// raylib's text_sprite_fonts example, Copyright (c) 2017-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextSpriteFonts
{
    private const int MAX_FONTS = 8;

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] sprite fonts");

        Font[] fonts =
        [
            LoadFont("resources/sprite_fonts/alagard.png"),
            LoadFont("resources/sprite_fonts/pixelplay.png"),
            LoadFont("resources/sprite_fonts/mecha.png"),
            LoadFont("resources/sprite_fonts/setback.png"),
            LoadFont("resources/sprite_fonts/romulus.png"),
            LoadFont("resources/sprite_fonts/pixantiqua.png"),
            LoadFont("resources/sprite_fonts/alpha_beta.png"),
            LoadFont("resources/sprite_fonts/jupiter_crash.png"),
        ];

        string[] messages =
        [
            "ALAGARD FONT designed by Hewett Tsoi",
            "PIXELPLAY FONT designed by Aleksander Shevchuk",
            "MECHA FONT designed by Captain Falcon",
            "SETBACK FONT designed by Brian Kent (AEnigma)",
            "ROMULUS FONT designed by Hewett Tsoi",
            "PIXANTIQUA FONT designed by Gerhard Grossmann",
            "ALPHA_BETA FONT designed by Brian Kent (AEnigma)",
            "JUPITER_CRASH FONT designed by Brian Kent (AEnigma)",
        ];

        int[] spacings = [2, 4, 8, 4, 3, 4, 4, 1];

        Vector2[] positions = new Vector2[MAX_FONTS];

        for (int i = 0; i < MAX_FONTS; i++)
        {
            positions[i].X = screenWidth/2.0f - MeasureTextEx(fonts[i], messages[i], fonts[i].BaseSize*2.0f, (float)spacings[i]).X/2.0f;
            positions[i].Y = 60.0f + fonts[i].BaseSize + 45.0f*i;
        }

        // Small corrections of height
        positions[3].Y += 8;
        positions[4].Y += 2;
        positions[7].Y -= 8;

        Color[] colors = [Color.Maroon, Color.Orange, Color.DarkGreen, Color.DarkBlue, Color.DarkPurple, Color.Lime, Color.Gold, Color.Red];

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("free sprite fonts included with raylib", 220, 20, 20, Color.DarkGray);
                DrawLine(220, 50, 600, 50, Color.DarkGray);

                for (int i = 0; i < MAX_FONTS; i++)
                {
                    DrawTextEx(fonts[i], messages[i], positions[i], fonts[i].BaseSize*2.0f, (float)spacings[i], colors[i]);
                }

            EndDrawing();
        }

        for (int i = 0; i < MAX_FONTS; i++) UnloadFont(fonts[i]);

        CloseWindow();
    }
}
