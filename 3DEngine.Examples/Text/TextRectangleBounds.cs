// raylib's text_rectangle_bounds example, Copyright (c) 2018-2025 Vlad Adrian (@demizdor) and Ramon
// Santamaria (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using System.Text;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextRectangleBounds
{
    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] rectangle bounds");

        const string text = "Text cannot escape\tthis container\t...word wrap also works when active so here's " +
            "a long text for testing.\n\nLorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod " +
            "tempor incididunt ut labore et dolore magna aliqua. Nec ullamcorper sit amet risus nullam eget felis eget.";

        bool resizing = false;
        bool wordWrap = true;

        Rectangle container = new(25.0f, 25.0f, screenWidth - 50.0f, screenHeight - 250.0f);
        Rectangle resizer = new(container.X + container.Width - 17, container.Y + container.Height - 17, 14, 14);

        // Minimum width and heigh for the container rectangle
        const float minWidth = 60;
        const float minHeight = 60;
        const float maxWidth = screenWidth - 50.0f;
        const float maxHeight = screenHeight - 160.0f;

        Vector2 lastMouse = new(0.0f, 0.0f); // Stores last mouse coordinates
        Color borderColor = Color.Maroon;   // Container border color
        Font font = GetFontDefault();       // Get default system font

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (IsKeyPressed(Key.Space)) wordWrap = !wordWrap;

            Vector2 mouse = GetMousePosition();

            // Check if the mouse is inside the container and toggle border color
            if (CheckCollisionPointRec(mouse, container)) borderColor = Fade(Color.Maroon, 0.4f);
            else if (!resizing) borderColor = Color.Maroon;

            // Container resizing logic
            if (resizing)
            {
                if (IsMouseButtonReleased(MouseButton.Left)) resizing = false;

                float width = container.Width + (mouse.X - lastMouse.X);
                container = container with { Width = (width > minWidth)? ((width < maxWidth)? width : maxWidth) : minWidth };

                float height = container.Height + (mouse.Y - lastMouse.Y);
                container = container with { Height = (height > minHeight)? ((height < maxHeight)? height : maxHeight) : minHeight };
            }
            else
            {
                // Check if we're resizing
                if (IsMouseButtonDown(MouseButton.Left) && CheckCollisionPointRec(mouse, resizer)) resizing = true;
            }

            // Move resizer rectangle properly
            resizer = resizer with { X = container.X + container.Width - 17, Y = container.Y + container.Height - 17 };

            lastMouse = mouse; // Update mouse

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawRectangleLinesEx(container, 3, borderColor);    // Draw container border

                // Draw text in container (add some padding)
                DrawTextBoxed(font, text, new Rectangle(container.X + 4, container.Y + 4, container.Width - 4, container.Height - 4), 20.0f, 2.0f, wordWrap, Color.Gray);

                DrawRectangleRec(resizer, borderColor);             // Draw the resize box

                // Draw bottom info
                DrawRectangle(0, screenHeight - 54, screenWidth, 54, Color.Gray);
                DrawRectangleRec(new Rectangle(382.0f, screenHeight - 34.0f, 12.0f, 12.0f), Color.Maroon);

                DrawText("Word Wrap: ", 313, screenHeight - 115, 20, Color.Black);
                if (wordWrap) DrawText("ON", 447, screenHeight - 115, 20, Color.Red);
                else DrawText("OFF", 447, screenHeight - 115, 20, Color.Black);

                DrawText("Press [SPACE] to toggle word wrap", 218, screenHeight - 86, 20, Color.Gray);

                DrawText("Click hold & drag the    to resize the container", 155, screenHeight - 38, 20, Color.RayWhite);

            EndDrawing();
        }

        CloseWindow();
    }

    // Draw text using font inside rectangle limits
    private static void DrawTextBoxed(Font font, string text, Rectangle rec, float fontSize, float spacing, bool wordWrap, Color tint)
    {
        DrawTextBoxedSelectable(font, text, rec, fontSize, spacing, wordWrap, tint, 0, 0, Color.White, Color.White);
    }

    // The code point at a byte of UTF-8 text and how many bytes it takes, '?' and one byte for a
    // byte that begins none, as raylib's GetCodepoint reads it
    private static int GetCodepoint(ReadOnlySpan<byte> text, out int codepointByteCount)
    {
        if (Rune.DecodeFromUtf8(text, out Rune rune, out codepointByteCount) != System.Buffers.OperationStatus.Done)
        {
            codepointByteCount = 1;
            return 0x3f;
        }
        return rune.Value;
    }

    // How far a glyph moves the pen at the font's size, or its width where it moves none. raylib
    // reads both from the font's arrays by the glyph's index, which GetGlyphIndex gives, and a font
    // here keeps its glyphs by code point, which GetGlyphInfo takes, its width X1 - X0.
    private static float GlyphAdvance(Font font, int codepoint)
    {
        Glyph glyph = GetGlyphInfo(font, codepoint) ?? default;
        return (glyph.Advance == 0) ? glyph.X1 - glyph.X0 : glyph.Advance;
    }

    // Draw text using font inside rectangle limits with support for text selection
    private static void DrawTextBoxedSelectable(Font font, string text, Rectangle rec, float fontSize, float spacing, bool wordWrap, Color tint, int selectStart, int selectLength, Color selectTint, Color selectBackTint)
    {
        // The text as UTF-8, which raylib scans a code point at a time by its bytes
        byte[] bytes = Encoding.UTF8.GetBytes(text);
        int length = bytes.Length;      // Total length in bytes of the text, scanned by codepoints in loop

        float textOffsetY = 0;          // Offset between lines (on line break '\n')
        float textOffsetX = 0.0f;       // Offset X to next character to draw

        float scaleFactor = fontSize/(float)font.BaseSize;     // Character rectangle scaling factor

        // Word/character wrapping mechanism variables
        const int MEASURE_STATE = 0, DRAW_STATE = 1;
        int state = wordWrap? MEASURE_STATE : DRAW_STATE;

        int startLine = -1;         // Index where to begin drawing (where a line begins)
        int endLine = -1;           // Index where to stop drawing (where a line ends)
        int lastk = -1;             // Holds last value of the character position

        for (int i = 0, k = 0; i < length; i++, k++)
        {
            // Get next codepoint from byte string
            int codepoint = GetCodepoint(bytes.AsSpan(i), out int codepointByteCount);

            // NOTE: Normally we exit the decoding sequence as soon as a bad byte is found (and return 0x3f)
            // but we need to draw all of the bad bytes using the '?' symbol moving one byte
            if (codepoint == 0x3f) codepointByteCount = 1;
            i += (codepointByteCount - 1);

            float glyphWidth = 0;
            if (codepoint != '\n')
            {
                glyphWidth = GlyphAdvance(font, codepoint)*scaleFactor;

                if (i + 1 < length) glyphWidth = glyphWidth + spacing;
            }

            // NOTE: When wordWrap is ON we first measure how much of the text we can draw before going outside of the rec container
            // We store this info in startLine and endLine, then we change states, draw the text between those two variables
            // and change states again and again recursively until the end of the text (or until we get outside of the container)
            // When wordWrap is OFF we don't need the measure state so we go to the drawing state immediately
            // and begin drawing on the next line before we can get outside the container
            if (state == MEASURE_STATE)
            {
                // There are multiple types of spaces in UNICODE, which this leaves out
                // Ref: http://jkorpela.fi/chars/spaces.html
                if ((codepoint == ' ') || (codepoint == '\t') || (codepoint == '\n')) endLine = i;

                if ((textOffsetX + glyphWidth) > rec.Width)
                {
                    endLine = (endLine < 1)? i : endLine;
                    if (i == endLine) endLine -= codepointByteCount;
                    if ((startLine + codepointByteCount) == endLine) endLine = (i - codepointByteCount);

                    state = 1 - state;
                }
                else if ((i + 1) == length)
                {
                    endLine = i;
                    state = 1 - state;
                }
                else if (codepoint == '\n') state = 1 - state;

                if (state == DRAW_STATE)
                {
                    textOffsetX = 0;
                    i = startLine;
                    glyphWidth = 0;

                    // Save character position when we switch states
                    int tmp = lastk;
                    lastk = k - 1;
                    k = tmp;
                }
            }
            else
            {
                if (codepoint == '\n')
                {
                    if (!wordWrap)
                    {
                        textOffsetY += (font.BaseSize + (float)font.BaseSize/2)*scaleFactor;
                        textOffsetX = 0;
                    }
                }
                else
                {
                    if (!wordWrap && ((textOffsetX + glyphWidth) > rec.Width))
                    {
                        textOffsetY += (font.BaseSize + (float)font.BaseSize/2)*scaleFactor;
                        textOffsetX = 0;
                    }

                    // When text overflows rectangle height limit, stop drawing
                    if ((textOffsetY + font.BaseSize*scaleFactor) > rec.Height) break;

                    // Draw selection background
                    bool isGlyphSelected = false;
                    if ((selectStart >= 0) && (k >= selectStart) && (k < (selectStart + selectLength)))
                    {
                        DrawRectangleRec(new Rectangle(rec.X + textOffsetX - 1, rec.Y + textOffsetY, glyphWidth, (float)font.BaseSize*scaleFactor), selectBackTint);
                        isGlyphSelected = true;
                    }

                    // Draw current character glyph
                    if ((codepoint != ' ') && (codepoint != '\t'))
                    {
                        DrawTextCodepoint(font, codepoint, new Vector2(rec.X + textOffsetX, rec.Y + textOffsetY), fontSize, isGlyphSelected? selectTint : tint);
                    }
                }

                if (wordWrap && (i == endLine))
                {
                    textOffsetY += (font.BaseSize + (float)font.BaseSize/2)*scaleFactor;
                    textOffsetX = 0;
                    startLine = endLine;
                    endLine = -1;
                    glyphWidth = 0;
                    selectStart += lastk - k;
                    k = lastk;

                    state = 1 - state;
                }
            }

            if ((textOffsetX != 0) || (codepoint != ' ')) textOffsetX += glyphWidth;  // avoid leading spaces
        }
    }
}
