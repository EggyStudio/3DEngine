// raylib's text_codepoints_loading example, Copyright (c) 2022-2025 Ramon Santamaria (@raysan5), under the
// zlib license, written again for the flat API.

using System.Numerics;
using System.Text;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextCodepointsLoading
{
    // The text drawn. A game's own text is scanned for the code points its font needs.
    private const string text = "いろはにほへと　ちりぬるを\nわかよたれそ　つねならむ\nうゐのおくやま　けふこえて\nあさきゆめみし　ゑひもせす";

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] codepoints loading");

        // Each character's code point in the font file
        int[] codepoints = LoadCodepoints(text);
        int codepointCount = codepoints.Length;

        // Without duplicates the atlas is smaller.
        int[] codepointsNoDups = codepoints.Distinct().ToArray();

        // A font of the glyphs of those code points, its atlas made as it loads
        Font font = LoadFontEx("resources/DotGothic16-Regular.ttf", 36, codepointsNoDups);

        SetTextureFilter(font.Texture, TextureFilter.Bilinear);

        SetTextLineSpacing(20);

        bool showFontAtlas = false;

        // raylib walks a pointer through the UTF-8 bytes with GetCodepointNext and GetCodepointPrevious,
        // and C#'s Rune walks an index through the string's UTF-16 the same way.
        int position = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Space)) showFontAtlas = !showFontAtlas;

            if (IsKeyPressed(Key.Right))
            {
                Rune.DecodeFromUtf16(text.AsSpan(position), out _, out int codepointSize);
                position += codepointSize;
            }
            else if (IsKeyPressed(Key.Left))
            {
                Rune.DecodeLastFromUtf16(text.AsSpan(0, position), out _, out int codepointSize);
                position -= codepointSize;
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawRectangle(0, 0, GetScreenWidth(), 70, Color.Black);
                DrawText($"Total codepoints contained in provided text: {codepointCount}", 10, 10, 20, Color.Green);
                DrawText($"Total codepoints required for font atlas (duplicates excluded): {codepointsNoDups.Length}", 10, 40, 20, Color.Green);

                if (showFontAtlas)
                {
                    DrawTexture(font.Texture, 150, 100, Color.Black);
                    DrawRectangleLines(150, 100, font.Texture.Width, font.Texture.Height, Color.Black);
                }
                else
                {
                    DrawTextEx(font, text, new Vector2(160, 110), 48, 5, Color.Black);
                }

                DrawText("Press SPACE to toggle font atlas view!", 10, GetScreenHeight() - 30, 20, Color.Gray);

            EndDrawing();
        }

        UnloadFont(font);

        CloseWindow();
    }
}
