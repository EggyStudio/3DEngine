using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// Arabic's marks and pairs placed by the GPOS table of <c>arabic-marks.ttf</c>, which
/// <c>build/make-color-test-fonts.py</c> writes: fatha, kasra and shadda put on the anchors of their
/// letters, on each letter of lam-alef and on each other, and pairs of letters kerned, each place
/// expected the one HarfBuzz gives the glyph in the same font, at 100 pixels a tenth of its units.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class ArabicMarksTests : IDisposable
{
    private const string Beh = "ب", Alef = "ا", Lam = "ل", Fatha = "َ", Kasra = "ِ", Shadda = "ّ";
    private static readonly string Marks = Path.Combine(AppContext.BaseDirectory, "Api", "arabic-marks.ttf");
    private static readonly string Unplaced = Path.Combine(AppContext.BaseDirectory, "Api", "arabic.ttf");

    public ArabicMarksTests() => UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private static Font Load(string path) => LoadFontEx(path, 100, LoadCodepoints(Beh + Alef + Lam + Fatha + Kasra + Shadda + " "));

    // Each glyph of a line where it is drawn from the line's left on its baseline, right and down,
    // in the order it is drawn, as the drawing walks the pen.
    private static List<(int Key, float X, float Y)> Drawn(Font font, string text)
    {
        var drawn = new List<(int, float, float)>();
        var pen = 0f;
        foreach (var (key, offset, advance) in PlacedKeys(font, text))
        {
            TryGetGlyph(font, key, out var glyph).Should().BeTrue();
            drawn.Add((key, MathF.Round(pen + offset.X, 2), MathF.Round(offset.Y, 2)));
            pen += advance ?? glyph.Advance;
        }
        return drawn;
    }

    [Fact]
    public void A_Mark_Is_Put_On_Its_Letter_By_The_Anchors_Of_Both()
    {
        var font = Load(Marks);

        Drawn(font, Beh + Fatha).Should().Equal((0x0628, 0f, 0f), (0x064E, 80f, -15f));
        Drawn(font, Beh + Kasra).Should().Equal((0x0628, 0f, 0f), (0x0650, 80f, -5f));
        Drawn(font, Beh + Shadda + Fatha).Should().Equal([(0x0628, 0f, 0f), (0x0651, 80f, -15f), (0x064E, 80f, -35f)],
            "fatha is put on shadda, which is on beh");
        MeasureTextEx(font, Beh + Shadda + Fatha, 100, 0).X.Should().Be(100, "a mark moves the pen nowhere");
        UnloadFont(font);
    }

    [Fact]
    public void A_Letters_Marks_Are_Shaped_In_The_Order_HarfBuzz_Puts_Them_In()
    {
        int[] Reordered(params int[] codepoints)
        {
            ArabicMarks.Reorder(codepoints);
            return codepoints;
        }

        Reordered(0x0628, 0x064E, 0x0651).Should().Equal([0x0628, 0x0651, 0x064E], "shadda comes before the vowel written with it");
        Reordered(0x0628, 0x0650, 0x0655).Should().Equal([0x0628, 0x0655, 0x0650], "hamza below modifies the letter, so it comes first");
        Reordered(0x0628, 0x064E, 0x0654).Should().Equal([0x0628, 0x0654, 0x064E], "and hamza above");
        Reordered(0x0628, 0x0653, 0x064E).Should().Equal([0x0628, 0x064E, 0x0653], "maddah modifies nothing and keeps its class's place");
        Reordered(0x0628, 0x064E, 0x0628, 0x0651, 0x0650).Should().Equal([0x0628, 0x064E, 0x0628, 0x0651, 0x0650], "each letter's marks are a run of their own");

        var font = Load(Marks);
        Drawn(font, Beh + Fatha + Shadda).Should().Equal(Drawn(font, Beh + Shadda + Fatha), "fatha is put on shadda whichever the text stores first");
        UnloadFont(font);
    }

    [Fact]
    public void A_Mark_On_Lam_Alef_Is_Put_On_The_Letter_It_Followed()
    {
        var font = Load(Marks);

        Drawn(font, Lam + Fatha + Alef).Should().Equal([(JoinedKey(13), 0f, 0f), (0x064E, 120f, -30f)],
            "fatha came after lam, the ligature's right stem");
        Drawn(font, Lam + Alef + Kasra).Should().Equal([(0x0650, 70f, -5f), (JoinedKey(13), 0f, 0f)],
            "kasra came after alef, the left stem, and is drawn first as alef's cluster is");
        UnloadFont(font);
    }

    [Fact]
    public void Letters_Are_Kerned_By_The_Fonts_Pairs_And_Contexts()
    {
        var font = Load(Marks);

        Drawn(font, Alef + Beh).Should().Equal([(0x0628, 0f, 0f), (0x0627, 120f, 0f)], "alef before beh is moved 20 to the right");
        MeasureTextEx(font, Alef + Beh, 100, 0).X.Should().Be(180, "and its advance grows by as much");
        Drawn(font, Beh + Alef + Beh).Should().Equal([(0x0628, 0f, 0f), (JoinedKey(9), 110f, 0f), (JoinedKey(8), 180f, 0f)],
            "alef's final glyph before beh, a pair of their classes, by 10");
        Drawn(font, Lam + Beh).Should().Equal([(JoinedKey(6), 0f, 0f), (JoinedKey(12), 70f, -10f)],
            "lam's initial glyph before beh's final one is raised 10");
        UnloadFont(font);
    }

    [Fact]
    public void A_Font_With_No_Positions_Draws_A_Mark_After_Its_Letter_At_Its_Own_Place()
    {
        var font = Load(Unplaced);

        Drawn(font, Beh + Fatha).Should().Equal((0x0628, 0f, 0f), (0x064E, 100f, 0f));
        MeasureTextEx(font, Alef + Beh, 100, 0).X.Should().Be(160);
        UnloadFont(font);
    }

    [Fact]
    public void A_Mark_Is_Drawn_Into_An_Image_Where_Its_Anchor_Puts_It()
    {
        // At 50 down the baseline is 131 from the top, and fatha on beh is drawn from 65 to 95
        // across and up to 93 above it, where the font with no positions draws it from 85 to 115
        // and up to 78.
        int Top(Font font, int x)
        {
            var image = GenImageColor(200, 200, Color.Blank);
            ImageDrawTextEx(ref image, font, Beh + Fatha, new System.Numerics.Vector2(50, 50), 100, 0, Color.White);
            var top = Enumerable.Range(0, image.Height).FirstOrDefault(y => GetImageColor(image, x, y).A > 128, -1);
            UnloadImage(image);
            return top;
        }
        var (marks, unplaced) = (Load(Marks), Load(Unplaced));

        Top(marks, 70).Should().BeInRange(36, 40);
        Top(unplaced, 70).Should().BeGreaterThan(100, "the column holds only beh's bar there");
        Top(unplaced, 100).Should().BeInRange(51, 55);
        UnloadFont(marks);
        UnloadFont(unplaced);
    }
}
