using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Api;

/// <summary>
/// Latin text kerned and joined by the tables of <c>latin.ttf</c> and <c>latin-kern.ttf</c>, which
/// <c>build/make-color-test-fonts.py</c> writes, V moved 150 units toward an A before it and o 100
/// toward a T, by a GPOS kern feature in the one and a kern table in the other, and f and i joined
/// into their ligature by the first's liga feature, each place at 100 pixels a tenth of the units.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Integration")]
public sealed class LatinShapingTests : IDisposable
{
    private const string Letters = "AVTofi ";
    private static readonly string Shaped = Path.Combine(AppContext.BaseDirectory, "Api", "latin.ttf");
    private static readonly string Tabled = Path.Combine(AppContext.BaseDirectory, "Api", "latin-kern.ttf");

    // The glyph the liga feature makes of f and i, glyph 8 of the font.
    private static readonly int Ligature = JoinedKey(8);

    public LatinShapingTests() => UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // Each glyph of a line where it is drawn from the line's left, in the order it is drawn.
    private static List<(int Key, float X)> Drawn(Font font, string text)
    {
        var drawn = new List<(int, float)>();
        var pen = 0f;
        foreach (var (key, offset, advance) in PlacedKeys(font, text))
        {
            TryGetGlyph(font, key, out var glyph).Should().BeTrue();
            drawn.Add((key, MathF.Round(pen + offset.X, 2)));
            pen += advance ?? glyph.Advance;
        }
        return drawn;
    }

    [Fact]
    public void A_Pair_Is_Kerned_By_The_Kern_Feature_In_Drawing_And_Measuring()
    {
        var font = LoadFontEx(Shaped, 100, LoadCodepoints(Letters));

        Drawn(font, "AV").Should().Equal([('A', 0f), ('V', 45f)], "V is moved 150 units toward the A, 15 pixels");
        Drawn(font, "To").Should().Equal([('T', 0f), ('o', 50f)]);
        Drawn(font, "VA").Should().Equal([('V', 0f), ('A', 60f)], "the font kerns no V before an A");
        MeasureTextEx(font, "AV", 100, 0).X.Should().Be(105);
        MeasureTextCodepoints(font, LoadCodepoints("AV"), 100, 0).X.Should().Be(105);
        MeasureTextEx(font, "AV", 50, 0).X.Should().Be(52.5f, "the kerning scales with the size the text is drawn at");
        UnloadFont(font);
    }

    [Fact]
    public void A_Font_With_No_Kern_Feature_Is_Kerned_By_Its_Kern_Table()
    {
        var font = LoadFontEx(Tabled, 100, LoadCodepoints(Letters));

        Drawn(font, "AV").Should().Equal([('A', 0f), ('V', 45f)]);
        Drawn(font, "To").Should().Equal([('T', 0f), ('o', 50f)]);
        Drawn(font, "A V").Should().Equal([('A', 0f), (' ', 60f), ('V', 90f)], "a pair is two glyphs side by side");
        MeasureTextEx(font, "AV", 100, 0).X.Should().Be(105);
        UnloadFont(font);
    }

    [Fact]
    public void F_And_I_Are_Drawn_As_Their_Ligature_Unless_Ligatures_Are_Off()
    {
        var font = LoadFontEx(Shaped, 100, LoadCodepoints(Letters));

        font.Glyphs.Should().ContainKey(Ligature, "the ligature the characters baked can make is baked with them");
        font.Glyphs[Ligature].Advance.Should().Be(55);
        Drawn(font, "fi").Should().Equal([(Ligature, 0f)]);
        Drawn(font, "Tofi").Should().Equal([('T', 0f), ('o', 50f), (Ligature, 100f)]);
        MeasureTextEx(font, "fi", 100, 0).X.Should().Be(55);
        MeasureTextEx(font, "fi", 100, 10).X.Should().Be(55, "the ligature is one glyph, with no spacing within it");

        SetFontLigatures(font, false);
        Drawn(font, "fi").Should().Equal([('f', 0f), ('i', 40f)], "a program that needs a glyph for each character turns ligatures off");
        MeasureTextEx(font, "fi", 100, 0).X.Should().Be(60);
        Drawn(font, "AV").Should().Equal([('A', 0f), ('V', 45f)], "kerning stays");
        SetFontLigatures(font, true);
        Drawn(font, "fi").Should().Equal([(Ligature, 0f)]);
        UnloadFont(font);
    }

    [Fact]
    public void A_Font_Loaded_With_Latin_1_Is_Kerned_And_Joined_Too()
    {
        var font = LoadFontEx(Shaped, 100);

        Drawn(font, "AV").Should().Equal([('A', 0f), ('V', 45f)]);
        Drawn(font, "fi").Should().Equal([(Ligature, 0f)]);
        UnloadFont(font);
    }

    [Fact]
    public void A_Ligature_Of_A_Letter_Not_Baked_Leaves_The_Letters_Apart()
    {
        // Asked for without i, the font bakes no ligature of f and i, and the i is drawn as the
        // font draws a character it lacks.
        var font = LoadFontEx(Shaped, 100, LoadCodepoints("AVTof "));

        font.Glyphs.Should().NotContainKey(Ligature);
        PlacedKeys(font, "fi").Select(p => p.Key).Should().Equal('f', 'i');
        UnloadFont(font);
    }
}
