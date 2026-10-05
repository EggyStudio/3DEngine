using FluentAssertions;

namespace Engine.Tests.Api;

/// <summary>
/// The engine's own TrueType reader on <c>planes.ttf</c>, whose 'A' is a triangle and whose
/// U+10348, U+1F600 and U+1F7E0 are a diamond, a square with a square hole and a circle of curves,
/// 1000 units to the em.
/// </summary>
[Trait("Category", "Unit")]
public class TrueTypeFontTests
{
    internal static readonly string Planes = Path.Combine(AppContext.BaseDirectory, "Api", "planes.ttf");

    [Fact]
    public void Characters_In_Every_Plane_Find_Their_Glyphs()
    {
        var font = TrueTypeFont.Read(File.ReadAllBytes(Planes))!;

        font.UnitsPerEm.Should().Be(1000);
        (font.Ascent, font.Descent).Should().Be((800, -200));
        font.GlyphIndex('A').Should().Be(1);
        font.GlyphIndex(0x10348).Should().Be(2);
        font.GlyphIndex(0x1F600).Should().Be(3);
        font.GlyphIndex(0x1F7E0).Should().Be(4);
        font.GlyphIndex(0x1F601).Should().Be(0, "a character the font does not have");
        font.Advance(3).Should().Be(1000);
    }

    [Fact]
    public void A_Glyph_Is_Filled_By_Its_Contours_With_Its_Holes_Left_Open()
    {
        var font = TrueTypeFont.Read(File.ReadAllBytes(Planes))!;

        // The square, 800 units across with a 400 unit hole, at a tenth of a pixel a unit.
        var (alpha, width, height, left, top) = font.Rasterize(3, 0.1f)!.Value;
        (width, height, left, top).Should().Be((80, 80, 10, -80), "800 units square standing on the baseline, 100 units in");
        alpha[40 * width + 40].Should().Be(0, "the middle is the hole");
        alpha[10 * width + 10].Should().Be(255, "between the edges is filled");
        alpha[5 * width + 40].Should().Be(255);

        // The circle of curves, 760 units across.
        var circle = font.Rasterize(4, 0.1f)!.Value;
        circle.Alpha[circle.Height / 2 * circle.Width + circle.Width / 2].Should().Be(255, "the circle is filled");
        circle.Alpha[0].Should().Be(0, "its box's corner lies outside it");
    }

    [Fact]
    public void A_File_Without_Outlines_Is_Not_Read()
    {
        TrueTypeFont.Read([0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]).Should().BeNull();
    }
}
