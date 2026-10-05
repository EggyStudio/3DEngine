// raylib's core_text_file_loading example, Copyright (c) 0 Aanjishnu Bhattacharyya (@NimComPoo-04), under the zlib
// license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class CoreTextFileLoading
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[core] text file loading");

        var cam = new Camera2D(Vector2.Zero, Vector2.Zero, 0, 1);

        const string fileName = "resources/text_file.txt";
        string text = LoadFileText(fileName) ?? "";

        // raylib's C cuts each line into wrapped pieces in place, a newline where a word would run
        // past the width, which a string here is given whole.
        var lines = text.Replace("\r", "").Split('\n');
        int lineCount = lines.Length;

        int fontSize = 20;
        int textTop = 25 + fontSize;
        int wrapWidth = screenWidth - 20;

        for (int i = 0; i < lineCount; i++)
        {
            var line = lines[i].ToCharArray();
            int lastSpace = 0;
            int lastWrapStart = 0;

            for (int j = 0; j <= line.Length; j++)
            {
                if (j == line.Length || line[j] == ' ')
                {
                    if (MeasureText(new string(line, lastWrapStart, j - lastWrapStart), fontSize) > wrapWidth)
                    {
                        line[lastSpace] = '\n';
                        lastWrapStart = lastSpace + 1;
                    }
                    lastSpace = j;
                }
            }
            lines[i] = new string(line);
        }

        int textHeight = 0;

        for (int i = 0; i < lineCount; i++)
        {
            Vector2 size = MeasureTextEx(GetFontDefault(), lines[i], (float)fontSize, 2);
            textHeight += (int)size.Y + 10;
        }

        // The scroll bar's height is a share of the window, as the text's height above it.
        var scrollBar = new Rectangle((float)screenWidth - 5, 0, 5, screenHeight*100.0f/(textHeight - screenHeight));

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            float scroll = GetMouseWheelMove();
            cam = cam with { Target = cam.Target with { Y = cam.Target.Y - scroll*fontSize*1.5f } };

            if (cam.Target.Y < 0) cam = cam with { Target = cam.Target with { Y = 0 } };

            if (cam.Target.Y > textHeight - screenHeight + textTop)
                cam = cam with { Target = cam.Target with { Y = (float)textHeight - screenHeight + textTop } };

            scrollBar = scrollBar with { Y = float.Lerp((float)textTop, (float)screenHeight - scrollBar.Height, (float)(cam.Target.Y - textTop)/(textHeight - screenHeight)) };

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                BeginMode2D(cam);
                    for (int i = 0, t = textTop; i < lineCount; i++)
                    {
                        // An empty line is measured as a space, so it takes its height too.
                        Vector2 size = MeasureTextEx(GetFontDefault(), lines[i].Length > 0 ? lines[i] : " ", (float)fontSize, 2);

                        DrawText(lines[i], 10, t, fontSize, Color.Red);

                        t += (int)size.Y + 10;
                    }
                EndMode2D();

                DrawRectangle(0, 0, screenWidth, textTop - 10, Color.Beige);
                DrawText($"File: {fileName}", 10, 10, fontSize, Color.Maroon);

                DrawRectangleRec(scrollBar, Color.Maroon);

            EndDrawing();
        }

        CloseWindow();
    }
}
