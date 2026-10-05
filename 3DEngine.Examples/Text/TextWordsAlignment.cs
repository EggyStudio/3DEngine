// raylib's text_words_alignment example, Copyright (c) 2025 JP Mortiboys (@themushroompirates), under the
// zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextWordsAlignment
{
    // Left and top are 0, the two middles 1, right and bottom 2, as raylib's names share values.
    private enum TextAlignment
    {
        Left = 0,
        Top = 0,
        Centre = 1,
        Middle = 1,
        Right = 2,
        Bottom = 2,
    }

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] words alignment");

        // The rectangle the text is drawn in
        Rectangle textContainerRect = new((float)screenWidth/2 - (float)screenWidth/4, (float)screenHeight/2 - (float)screenHeight/3, (float)screenWidth/2, (float)screenHeight*2/3);

        string[] textAlignNameH = ["Left", "Centre", "Right"];
        string[] textAlignNameV = ["Top", "Middle", "Bottom"];

        // raylib's TextSplit is C#'s Split.
        int wordIndex = 0;
        string[] words = "raylib is a simple and easy-to-use library to enjoy videogames programming".Split(' ');
        int wordCount = words.Length;

        int fontSize = 40;

        Font font = GetFontDefault();

        TextAlignment hAlign = TextAlignment.Centre;
        TextAlignment vAlign = TextAlignment.Middle;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Left))
            {
                if (hAlign > 0) hAlign = hAlign - 1;
            }

            if (IsKeyPressed(Key.Right))
            {
                hAlign = hAlign + 1;
                if ((int)hAlign > 2) hAlign = (TextAlignment)2;
            }

            if (IsKeyPressed(Key.Up))
            {
                if (vAlign > 0) vAlign = vAlign - 1;
            }

            if (IsKeyPressed(Key.Down))
            {
                vAlign = vAlign + 1;
                if ((int)vAlign > 2) vAlign = (TextAlignment)2;
            }

            // A word a second
            if (wordCount > 0) wordIndex = (int)GetTime()%wordCount;
            else wordIndex = 0;

            BeginDrawing();

                ClearBackground(Color.DarkBlue);

                DrawText("Use Arrow Keys to change the text alignment", 20, 20, 20, Color.LightGray);
                DrawText($"Alignment: Horizontal = {textAlignNameH[(int)hAlign]}, Vertical = {textAlignNameV[(int)vAlign]}", 20, 40, 20, Color.LightGray);

                DrawRectangleRec(textContainerRect, Color.Blue);

                Vector2 textSize = MeasureTextEx(font, words[wordIndex], (float)fontSize, fontSize*.1f);

                // The text's top left corner, from the rectangle and the alignment
                Vector2 textPos = new(
                    textContainerRect.X + float.Lerp(0.0f, textContainerRect.Width - textSize.X, ((float)hAlign)*0.5f),
                    textContainerRect.Y + float.Lerp(0.0f, textContainerRect.Height - textSize.Y, ((float)vAlign)*0.5f));

                DrawTextEx(font, words[wordIndex], textPos, (float)fontSize, fontSize*.1f, Color.RayWhite);

            EndDrawing();
        }

        CloseWindow();
    }
}
