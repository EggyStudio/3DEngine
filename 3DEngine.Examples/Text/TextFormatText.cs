// raylib's text_format_text example, Copyright (c) 2014-2025 Ramon Santamaria (@raysan5), under the zlib
// license, written again for the flat API.

using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextFormatText
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] format text");

        int score = 100020;
        int hiscore = 200450;
        int lives = 5;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // raylib's TextFormat is C#'s string interpolation, %08i being D8 and %02.02f 0.00.
                DrawText($"Score: {score:D8}", 200, 80, 20, Color.Red);

                DrawText($"HiScore: {hiscore:D8}", 200, 120, 20, Color.Green);

                DrawText($"Lives: {lives:D2}", 200, 160, 40, Color.Blue);

                DrawText($"Elapsed Time: {GetFrameTime()*1000:0.00} ms", 200, 220, 20, Color.Black);

            EndDrawing();
        }

        CloseWindow();
    }
}
