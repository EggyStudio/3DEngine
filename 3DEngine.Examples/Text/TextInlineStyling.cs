// raylib's text_inline_styling example, Copyright (c) 2025 Wagner Barongello (@SultansOfCode) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using System.Text;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextInlineStyling
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] inline styling");

        Vector2 textSize = Vector2.Zero;    // Measure text box for provided font and text
        Color colRandom = Color.Red;        // Random color used on text
        int frameCounter = 0;               // Used to generate a new random color every certain frames

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            frameCounter++;

            if ((frameCounter%20) == 0)
            {
                colRandom = new Color((byte)GetRandomValue(0, 255), (byte)GetRandomValue(0, 255), (byte)GetRandomValue(0, 255), 255);
            }

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                // Text inline styling strategy used: [ ] delimiters for format
                // - Define foreground color:      [cRRGGBBAA]
                // - Define background color:      [bRRGGBBAA]
                // - Reset formating:              [r]
                // Colors defined with [cRRGGBBAA] or [bRRGGBBAA] are multiplied by the base color alpha
                // This allows global transparency control while keeping per-section styling (ex. text fade effects)
                // Example: [bAA00AAFF][cFF0000FF]red text on gray background[r] normal text

                DrawTextStyled(GetFontDefault(), "This changes the [cFF0000FF]foreground color[r] of provided text!!!",
                    new Vector2(100, 80), 20.0f, 2.0f, Color.Black);

                DrawTextStyled(GetFontDefault(), "This changes the [bFF00FFFF]background color[r] of provided text!!!",
                    new Vector2(100, 120), 20.0f, 2.0f, Color.Black);

                DrawTextStyled(GetFontDefault(), "This changes the [c00ff00ff][bff0000ff]foreground and background colors[r]!!!",
                    new Vector2(100, 160), 20.0f, 2.0f, Color.Black);

                DrawTextStyled(GetFontDefault(), "This changes the [c00ff00ff]alpha[r] relative [cffffffff][b000000ff]from source[r] [cff000088]color[r]!!!",
                    new Vector2(100, 200), 20.0f, 2.0f, new Color(0, 0, 0, 100));

                // The formatted text
                string text = $"Let's be [c{colRandom.R:x2}{colRandom.G:x2}{colRandom.B:x2}FF]CREATIVE[r] !!!";
                DrawTextStyled(GetFontDefault(), text, new Vector2(100, 240), 40.0f, 2.0f, Color.Black);

                textSize = MeasureTextStyled(GetFontDefault(), text, 40.0f, 2.0f);
                DrawRectangleLines(100, 240, (int)textSize.X, (int)textSize.Y, Color.Green);

            EndDrawing();
        }

        CloseWindow();
    }

    // The code point at a byte of UTF-8 text and how many bytes it takes, '?' and one byte for a
    // byte that begins none, as raylib's GetCodepointNext reads it
    private static int GetCodepointNext(ReadOnlySpan<byte> text, out int codepointByteCount)
    {
        if (Rune.DecodeFromUtf8(text, out Rune rune, out codepointByteCount) != System.Buffers.OperationStatus.Done)
        {
            codepointByteCount = 1;
            return 0x3f;
        }
        return rune.Value;
    }

    // Whether a byte is a hex digit
    private static bool IsHex(byte c) => ((c >= '0') && (c <= '9')) || ((c >= 'A') && (c <= 'F')) || ((c >= 'a') && (c <= 'f'));

    // Draw text using inline styling
    // PARAM: color is the default text color, background color is BLANK by default
    // NOTE: Using input color as the base alpha multiplied to inline styles
    private static void DrawTextStyled(Font font, string str, Vector2 position, float fontSize, float spacing, Color color)
    {
        // Text inline styling strategy used: [ ] delimiters for format
        // - Define foreground color:      [cRRGGBBAA]
        // - Define background color:      [bRRGGBBAA]
        // - Reset formating:              [r]
        // Example: [bAA00AAFF][cFF0000FF]red text on gray background[r] normal text

        if (!font.Texture.IsValid) font = GetFontDefault();

        // The text as UTF-8, which raylib scans a code point at a time by its bytes
        byte[] text = Encoding.UTF8.GetBytes(str);
        int textLen = text.Length;

        Color colFront = color;
        Color colBack = Color.Blank;
        int backRecPadding = 4; // Background rectangle padding

        float textOffsetY = 0.0f;
        float textOffsetX = 0.0f;
        float textLineSpacing = 0.0f;
        float scaleFactor = fontSize/font.BaseSize;

        for (int i = 0; i < textLen;)
        {
            int codepoint = GetCodepointNext(text.AsSpan(i), out int codepointByteCount);

            if (codepoint == '\n')
            {
                textOffsetY += (fontSize + textLineSpacing);
                textOffsetX = 0.0f;
            }
            else
            {
                if (codepoint == '[') // Process pipe styling
                {
                    if (((i + 2) < textLen) && (text[i + 1] == 'r') && (text[i + 2] == ']')) // Reset styling
                    {
                        colFront = color;
                        colBack = Color.Blank;

                        i += 3;     // Skip "[r]"
                        continue;   // Do not draw characters
                    }
                    else if (((i + 1) < textLen) && ((text[i + 1] == 'c') || (text[i + 1] == 'b')))
                    {
                        i += 2;     // Skip "[c" or "[b" to start parsing color

                        // Parse following color
                        int colHexCount = 0;
                        while ((i + colHexCount < textLen) && (text[i + colHexCount] != ']'))
                        {
                            if (IsHex(text[i + colHexCount])) colHexCount++;
                            else break; // Only affects while loop
                        }

                        // Convert hex color text into actual Color
                        uint colHexValue = colHexCount > 0 ? Convert.ToUInt32(Encoding.ASCII.GetString(text, i, Math.Min(colHexCount, 8)), 16) : 0;
                        if (text[i - 1] == 'c')
                        {
                            colFront = GetColor(colHexValue);
                        }
                        else if (text[i - 1] == 'b')
                        {
                            colBack = GetColor(colHexValue);
                        }

                        i += (colHexCount + 1); // Skip color value retrieved and ']'
                        continue;   // Do not draw characters
                    }
                }

                // raylib reads the glyph's advance and width by its index, which GetGlyphIndex
                // gives, and a font here keeps its glyphs by code point, which GetGlyphInfo takes,
                // its width X1 - X0.
                Glyph glyph = GetGlyphInfo(font, codepoint) ?? default;
                float increaseX = 0.0f;

                if (glyph.Advance == 0) increaseX = ((float)(glyph.X1 - glyph.X0)*scaleFactor + spacing);
                else increaseX += ((float)glyph.Advance*scaleFactor + spacing);

                // Draw background rectangle color (if required)
                if (colBack.A > 0) DrawRectangleRec(new Rectangle(position.X + textOffsetX, position.Y + textOffsetY - backRecPadding, increaseX, fontSize + 2*backRecPadding), colBack);

                if ((codepoint != ' ') && (codepoint != '\t'))
                {
                    DrawTextCodepoint(font, codepoint, new Vector2(position.X + textOffsetX, position.Y + textOffsetY), fontSize, colFront);
                }

                textOffsetX += increaseX;
            }

            i += codepointByteCount;
        }
    }

    // Measure inline styled text
    // NOTE: Measuring styled text requires skipping styling data
    // WARNING: Not considering line breaks
    private static Vector2 MeasureTextStyled(Font font, string str, float fontSize, float spacing)
    {
        Vector2 textSize = Vector2.Zero;

        if (!font.Texture.IsValid || string.IsNullOrEmpty(str)) return textSize; // Security check

        byte[] text = Encoding.UTF8.GetBytes(str);
        int textLen = text.Length; // Get size in bytes of text

        float textWidth = 0.0f;
        float textHeight = fontSize;
        float scaleFactor = fontSize/(float)font.BaseSize;

        int codepoint = 0;              // Current character
        int validCodepointCounter = 0;

        for (int i = 0; i < textLen;)
        {
            codepoint = GetCodepointNext(text.AsSpan(i), out int codepointByteCount);

            if (codepoint == '[') // Ignore pipe inline styling
            {
                if (((i + 2) < textLen) && (text[i + 1] == 'r') && (text[i + 2] == ']')) // Reset styling
                {
                    i += 3;     // Skip "[r]"
                    continue;   // Do not measure characters
                }
                else if (((i + 1) < textLen) && ((text[i + 1] == 'c') || (text[i + 1] == 'b')))
                {
                    i += 2;     // Skip "[c" or "[b" to start parsing color

                    int colHexCount = 0;
                    while ((i + colHexCount < textLen) && (text[i + colHexCount] != ']'))
                    {
                        if (IsHex(text[i + colHexCount])) colHexCount++;
                        else break; // Only affects while loop
                    }

                    i += (colHexCount + 1); // Skip color value retrieved and ']'
                    continue;   // Do not measure characters
                }
            }
            else if (codepoint != '\n')
            {
                // The glyph by its code point, where raylib takes it by its index
                Glyph glyph = GetGlyphInfo(font, codepoint) ?? default;

                if (glyph.Advance > 0) textWidth += glyph.Advance;
                else textWidth += ((glyph.X1 - glyph.X0) + glyph.X0);

                validCodepointCounter++;
                i += codepointByteCount;
            }
        }

        textSize.X = textWidth*scaleFactor + (validCodepointCounter - 1)*spacing;
        textSize.Y = textHeight;

        return textSize;
    }
}
