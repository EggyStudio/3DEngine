using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextFonts
{
    public static void Run()
    {
        InitWindow(800, 450, "[text] fonts");

        // Lato, under the SIL Open Font License (resources/fonts/Lato-OFL.txt).
        var lato = LoadFontEx("resources/fonts/Lato-Regular.ttf", 48);
        var target = LoadRenderTexture(300, 120);
        var t = 0f;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            t += GetFrameTime();

            BeginDrawing();
            ClearBackground(Color.RayWhite);

            DrawText("The default font, baked at the size it is drawn", 20, 20, 20, Color.DarkGray);
            var y = 60;
            for (int size = 10; size <= 30; size += 5)
            {
                DrawText($"{size} pixels", 20, y, size, Color.Gray);
                y += size + 8;
            }

            DrawTextEx(lato, "Lato, from a file", new Vector2(320, 60), 48, 0, Color.Maroon);
            DrawTextEx(lato, "scaled down to 24", new Vector2(320, 120), 24, 1, Color.DarkBlue);

            var line = "Measured and centered";
            var size2 = MeasureTextEx(lato, line, 32, 0);
            DrawRectangle((int)(560 - size2.X / 2), 200, (int)size2.X, (int)size2.Y, Color.SkyBlue.Fade(0.4f));
            DrawTextEx(lato, line, new Vector2(560 - size2.X / 2, 200), 32, 0, Color.DarkBlue);

            // Text reaches render targets like any shape.
            BeginTextureMode(target);
            ClearBackground(Color.DarkGray);
            DrawTextEx(lato, $"{t:0.0} s", new Vector2(20, 30), 48, 0, Color.Gold);
            EndTextureMode();
            DrawTexturePro(target.Texture, new Rectangle(0, 0, 300, 120), new Rectangle(560, 340, 240, 96),
                new Vector2(120, 48), MathF.Sin(t) * 8, Color.White);

            EndDrawing();
        }

        UnloadFont(lato);
        UnloadRenderTexture(target);
        CloseWindow();
    }
}
