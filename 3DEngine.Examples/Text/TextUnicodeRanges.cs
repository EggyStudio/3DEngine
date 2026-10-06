// raylib's text_unicode_ranges example, Copyright (c) 2025 Vadim Gunko (@GuvaCode) and Ramon Santamaria
// (@raysan5), under the zlib license, written again for the flat API.

using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Examples;

public static class TextUnicodeRanges
{
    private const string FontPath = "resources/NotoSansTC-Regular.ttf";

    public static void Run()
    {
        const int screenWidth = 800;
        const int screenHeight = 450;

        InitWindow(screenWidth, screenHeight, "[text] unicode ranges");

        // The font with its default characters, which here are Latin-1 where raylib's are ASCII.
        Font font = LoadFont(FontPath);
        SetTextureFilter(font.Texture, TextureFilter.Bilinear);

        int unicodeRange = 0;
        int prevUnicodeRange = 0;

        SetTargetFPS(60);

        while (!WindowShouldClose())
        {
            if (unicodeRange != prevUnicodeRange)
            {
                UnloadFont(font);

                font = LoadFont(FontPath);

                // Each range adds the ones below it, as raylib's cases fall through to the next.
                if (unicodeRange >= 4)
                {
                    // CJK, Japanese and Chinese. Thousands of code points take a long time, and a
                    // game would bake only those its text uses.
                    AddCodepointRange(ref font, FontPath, 0x4e00, 0x9fff);
                    AddCodepointRange(ref font, FontPath, 0x3400, 0x4dbf);
                    AddCodepointRange(ref font, FontPath, 0x3000, 0x303f);
                    AddCodepointRange(ref font, FontPath, 0x3040, 0x309f);
                    AddCodepointRange(ref font, FontPath, 0x30A0, 0x30ff);
                    AddCodepointRange(ref font, FontPath, 0x31f0, 0x31ff);
                    AddCodepointRange(ref font, FontPath, 0xff00, 0xffef);
                    AddCodepointRange(ref font, FontPath, 0xac00, 0xd7af);
                    AddCodepointRange(ref font, FontPath, 0x1100, 0x11ff);
                }
                if (unicodeRange >= 3)
                {
                    // Cyrillic
                    AddCodepointRange(ref font, FontPath, 0x400, 0x4ff);
                    AddCodepointRange(ref font, FontPath, 0x500, 0x52f);
                    AddCodepointRange(ref font, FontPath, 0x2de0, 0x2Dff);
                    AddCodepointRange(ref font, FontPath, 0xa640, 0xA69f);
                }
                if (unicodeRange >= 2)
                {
                    // Greek
                    AddCodepointRange(ref font, FontPath, 0x370, 0x3ff);
                    AddCodepointRange(ref font, FontPath, 0x1f00, 0x1fff);
                }
                if (unicodeRange >= 1)
                {
                    // European languages
                    AddCodepointRange(ref font, FontPath, 0xc0, 0x17f);
                    AddCodepointRange(ref font, FontPath, 0x180, 0x24f);
                }

                prevUnicodeRange = unicodeRange;
                SetTextureFilter(font.Texture, TextureFilter.Bilinear);
            }

            if (IsKeyPressed(Key.Zero)) unicodeRange = 0;
            else if (IsKeyPressed(Key.One)) unicodeRange = 1;
            else if (IsKeyPressed(Key.Two)) unicodeRange = 2;
            else if (IsKeyPressed(Key.Three)) unicodeRange = 3;
            else if (IsKeyPressed(Key.Four)) unicodeRange = 4;

            BeginDrawing();

                ClearBackground(Color.RayWhite);

                DrawText("ADD CODEPOINTS: [1][2][3][4]", 20, 20, 20, Color.Maroon);

                DrawTextEx(font, "> English: Hello World!", new Vector2(50, 70), 32, 1, Color.DarkGray);
                DrawTextEx(font, "> Español: Hola mundo!", new Vector2(50, 120), 32, 1, Color.DarkGray);
                DrawTextEx(font, "> Ελληνικά: Γειά σου κόσμε!", new Vector2(50, 170), 32, 1, Color.DarkGray);
                DrawTextEx(font, "> Русский: Привет мир!", new Vector2(50, 220), 32, 0, Color.DarkGray);
                DrawTextEx(font, "> 中文: 你好世界!", new Vector2(50, 270), 32, 1, Color.DarkGray);
                DrawTextEx(font, "> 日本語: こんにちは世界!", new Vector2(50, 320), 32, 1, Color.DarkGray);

                // The atlas, scaled to fit
                float atlasScale = 380.0f/font.Texture.Width;
                DrawRectangleRec(new Rectangle(400.0f, 16.0f, font.Texture.Width*atlasScale, font.Texture.Height*atlasScale), Color.Black);
                DrawTexturePro(font.Texture, new Rectangle(0, 0, (float)font.Texture.Width, (float)font.Texture.Height),
                    new Rectangle(400.0f, 16.0f, font.Texture.Width*atlasScale, font.Texture.Height*atlasScale), Vector2.Zero, 0.0f, Color.White);
                DrawRectangleLines(400, 16, 380, 380, Color.Red);

                DrawText($"ATLAS SIZE: {font.Texture.Width}x{font.Texture.Height} px (x{atlasScale:0.00})", 20, 380, 20, Color.Blue);
                DrawText($"CODEPOINTS GLYPHS LOADED: {font.Glyphs.Count}", 20, 410, 20, Color.Lime);

                DrawText("Font: Noto Sans TC. License: SIL Open Font License 1.1", screenWidth - 300, screenHeight - 20, 10, Color.Gray);

                if (prevUnicodeRange != unicodeRange)
                {
                    DrawRectangle(0, 0, screenWidth, screenHeight, Fade(Color.White, 0.8f));
                    DrawRectangle(0, 125, screenWidth, 200, Color.Gray);
                    DrawText("GENERATING FONT ATLAS...", 120, 210, 40, Color.Black);
                }

            EndDrawing();
        }

        UnloadFont(font);

        CloseWindow();
    }

    // Bakes the font again with the code points it has and those from start to stop.
    private static void AddCodepointRange(ref Font font, string fontPath, int start, int stop)
    {
        int[] updatedCodepoints = [.. font.Glyphs.Keys, .. Enumerable.Range(start, stop - start + 1)];

        UnloadFont(font);
        font = LoadFontEx(fontPath, 32, updatedCodepoints);
    }
}
