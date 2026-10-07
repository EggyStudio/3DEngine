using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

// The frames of text: a font from a file, color glyphs, and text read right to left.
public sealed partial class ReferenceFrameTests
{
    [NeedsVulkanFact]
    public void Text_In_A_Font_From_A_File_Matches_Its_Reference()
    {
        Open(256, 160);
        var font = LoadFontEx(Engine.Tests.Api.FontTests.Lato(), 28);

        var frame = Capture(() =>
        {
            ClearBackground(Color.RayWhite);
            DrawTextEx(font, "Lato from a file", new Vector2(10, 20), 28, 1, Color.DarkBlue);
            DrawTextEx(font, "Small and spaced", new Vector2(10, 70), 18, 3, Color.Maroon);
            DrawTextEx(font, "0123456789 ÆØÅ éü", new Vector2(10, 110), 22, 0, Color.Black);
        });
        Matches(frame, "font_from_file");
        UnloadFont(font);
    }

    [NeedsVulkanFact]
    public void Color_Text_Matches_Its_Reference()
    {
        // The test fonts' color glyphs drawn as text: paints of a gradient and a moved square
        // (COLR version 1), a sequence the bitmap font joins into its yellow glyph beside the sun
        // alone, and layers with a letter of no color, tinted.
        Open(256, 96);
        string Font(string name) => Path.Combine(AppContext.BaseDirectory, "Api", name);
        var paints = LoadFontEx(Font("paints.ttf"), 48, [0x1F600]);
        var bitmaps = LoadFontEx(Font("bitmaps.ttf"), 32, LoadCodepoints("\U0001F600\u200D\u2600 "));
        var layers = LoadFontEx(Font("layers.ttf"), 40, ['A', 0x1F600]);

        var frame = Capture(() =>
        {
            ClearBackground(Color.RayWhite);
            DrawTextEx(paints, "\U0001F600", new Vector2(8, 8), 48, 0, Color.White);
            DrawTextEx(bitmaps, "\U0001F600\u200D\u2600 \u2600", new Vector2(72, 16), 32, 2, Color.White);
            DrawTextEx(layers, "A\U0001F600", new Vector2(170, 12), 40, 2, Color.DarkBlue);
        });
        Matches(frame, "color_text");
        UnloadFont(paints);
        UnloadFont(bitmaps);
        UnloadFont(layers);
    }

    [NeedsVulkanFact]
    public void Text_Read_Right_To_Left_Matches_Its_Reference()
    {
        // The test font's letters, bars of three heights, so their order is seen: a line of Hebrew
        // alone, a run of it in a line read left to right, a number in it, brackets turned in it,
        // and a mark kept on its letter.
        Open(256, 160);
        var font = LoadFontEx(Path.Combine(AppContext.BaseDirectory, "Api", "rtl.ttf"), 20, LoadCodepoints("\u05D0\u05D1\u05D2\u05B8 12()a"));

        var frame = Capture(() =>
        {
            ClearBackground(Color.RayWhite);
            DrawTextEx(font, "\u05D0\u05D1\u05D2", new Vector2(10, 10), 20, 2, Color.DarkBlue);
            DrawTextEx(font, "a \u05D0\u05D1\u05D2 a", new Vector2(10, 40), 20, 2, Color.Maroon);
            DrawTextEx(font, "\u05D0\u05D1 12", new Vector2(10, 70), 20, 2, Color.DarkGreen);
            DrawTextEx(font, "\u05D0(\u05D1)", new Vector2(10, 100), 20, 2, Color.Purple);
            DrawTextEx(font, "\u05D0\u05B8\u05D1", new Vector2(10, 130), 20, 2, Color.Black);
        });
        Matches(frame, "right_to_left");
        UnloadFont(font);
    }
}
