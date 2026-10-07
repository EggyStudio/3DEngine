using System.Numerics;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

// The frames of text: a font from a file, color glyphs, text read right to left, and Arabic joined.
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

    [NeedsVulkanFact]
    public void Arabic_Joined_Matches_Its_Reference()
    {
        // The test fonts' beh, alef and lam, bars with ticks of their own whose final, medial and
        // initial glyphs are narrower: three behs joined, beh before alef, lam joined to alef alone
        // and after a letter, the joined lam and alef carrying a mark, and the same words in the font
        // drawn by the presentation forms it maps.
        Open(256, 160);
        string Font(string name) => Path.Combine(AppContext.BaseDirectory, "Api", name);
        const string Letters = "\u0628\u0627\u0644\u064E ";
        var substituting = LoadFontEx(Font("arabic.ttf"), 24, LoadCodepoints(Letters));
        var forms = LoadFontEx(Font("arabic-forms.ttf"), 24, LoadCodepoints(Letters));

        var frame = Capture(() =>
        {
            ClearBackground(Color.RayWhite);
            DrawTextEx(substituting, "\u0628\u0628\u0628 \u0628\u0627\u0628", new Vector2(10, 10), 24, 0, Color.DarkBlue);
            DrawTextEx(substituting, "\u0644\u0627 \u0628\u0644\u0627", new Vector2(10, 50), 24, 0, Color.Maroon);
            DrawTextEx(substituting, "\u0644\u064E\u0627", new Vector2(10, 90), 24, 0, Color.DarkGreen);
            DrawTextEx(forms, "\u0628\u0628\u0628 \u0628\u0644\u0627", new Vector2(10, 120), 24, 0, Color.Black);
        });
        Matches(frame, "arabic_joined");
        UnloadFont(substituting);
        UnloadFont(forms);
    }

    [NeedsVulkanFact]
    public void Arabic_Marks_Match_Their_Reference()
    {
        // The test font with positions: fatha, kasra and shadda put on beh by their anchors, fatha on
        // shadda over it, fatha on lam's stem of lam-alef and kasra under alef's, alef kerned away
        // from beh and lam raised before beh, and the same marks in the font with no positions,
        // drawn after their letters at their own places.
        Open(256, 160);
        string Font(string name) => Path.Combine(AppContext.BaseDirectory, "Api", name);
        const string Letters = "بالَِّ ";
        var marks = LoadFontEx(Font("arabic-marks.ttf"), 24, LoadCodepoints(Letters));
        var unplaced = LoadFontEx(Font("arabic.ttf"), 24, LoadCodepoints(Letters));

        var frame = Capture(() =>
        {
            ClearBackground(Color.RayWhite);
            DrawTextEx(marks, "بَ بِ بَّ", new Vector2(10, 20), 24, 0, Color.DarkBlue);
            DrawTextEx(marks, "لَا لاِ", new Vector2(10, 60), 24, 0, Color.Maroon);
            DrawTextEx(marks, "اب لب", new Vector2(10, 95), 24, 0, Color.DarkGreen);
            DrawTextEx(unplaced, "بَ لَا", new Vector2(10, 125), 24, 0, Color.Black);
        });
        Matches(frame, "arabic_marks");
        UnloadFont(marks);
        UnloadFont(unplaced);
    }
}
