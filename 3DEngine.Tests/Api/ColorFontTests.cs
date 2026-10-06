using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// Color emoji, read by the engine's own TrueType reader from <c>bitmaps.ttf</c>, PNG images at 8
/// pixels to the em and no outlines, and <c>layers.ttf</c>, outlines colored by layers, which
/// <c>build/make-color-test-fonts.py</c> writes.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class ColorFontTests : IDisposable
{
    private static readonly string Bitmaps = Path.Combine(AppContext.BaseDirectory, "Api", "bitmaps.ttf");
    private static readonly string Layers = Path.Combine(AppContext.BaseDirectory, "Api", "layers.ttf");

    public ColorFontTests() => UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private static Color Pixel(byte[] rgba, int width, int x, int y)
    {
        var at = (y * width + x) * 4;
        return new Color(rgba[at], rgba[at + 1], rgba[at + 2], rgba[at + 3]);
    }

    // A glyph's pixels in a font's atlas, at a share across and down its box.
    private static Color AtlasPixel(Font font, int codepoint, float across, float down)
    {
        var glyph = font.Glyphs[codepoint];
        var x = (int)((glyph.U0 + (glyph.U1 - glyph.U0) * across) * font.Atlas.Width);
        var y = (int)((glyph.V0 + (glyph.V1 - glyph.V0) * down) * font.Atlas.Height);
        return GetImageColor(font.Atlas, x, y);
    }

    [Fact]
    public void A_Bitmap_Is_Read_As_Its_Image_At_Its_Own_Size_And_Scaled_To_Another()
    {
        var font = TrueTypeFont.Read(File.ReadAllBytes(Bitmaps))!;
        font.HasOutlines.Should().BeFalse("the font holds its glyphs as images alone");
        var glyph = font.GlyphIndex(0x1F600);
        font.HasColor(glyph).Should().BeTrue();

        // 1000 units to the em, so 8 pixels to the em is 0.008 pixels a unit, the strike's own size.
        var (rgba, width, height, left, top) = font.Color(glyph, 0.008f)!.Value;
        (width, height, left, top).Should().Be((8, 8, 0, -8), "8 pixels square standing on the baseline");
        Pixel(rgba, width, 4, 1).Should().Be(new Color(255, 0, 0, 255), "its top half is red");
        Pixel(rgba, width, 4, 6).Should().Be(new Color(0, 0, 255, 255), "and its bottom half blue");

        var twice = font.Color(glyph, 0.016f)!.Value;
        (twice.Width, twice.Height, twice.Top).Should().Be((16, 16, -16), "drawn at twice the strike's size it is scaled up");
        Pixel(twice.Rgba, 16, 8, 2).Should().Be(new Color(255, 0, 0, 255));
        Pixel(twice.Rgba, 16, 8, 13).Should().Be(new Color(0, 0, 255, 255));
    }

    [Fact]
    public void Layers_Are_Filled_In_Their_Palette_Colors_Over_Each_Other()
    {
        var font = TrueTypeFont.Read(File.ReadAllBytes(Layers))!;
        var glyph = font.GlyphIndex(0x1F600);
        font.HasColor(glyph).Should().BeTrue();
        font.HasColor(font.GlyphIndex('A')).Should().BeFalse("a glyph with no layers has no color of its own");

        // The square, 800 units across, at a tenth of a pixel a unit: 80 pixels, red then blue.
        var (rgba, width, height, left, top) = font.Color(glyph, 0.1f)!.Value;
        (width, height, left, top).Should().Be((80, 80, 10, -80));
        Pixel(rgba, width, 20, 40).Should().Be(new Color(255, 0, 0, 255), "the left layer is red");
        Pixel(rgba, width, 60, 40).Should().Be(new Color(0, 0, 255, 255), "and the right one blue");
    }

    [Fact]
    public void A_Font_Of_Bitmaps_Alone_Loads_Its_Characters_In_Color_The_First_Plane_Among_Them()
    {
        var font = LoadFontEx(Bitmaps, 8, [0x1F600, 0x2600]);

        font.IsValid.Should().BeTrue("a font of color bitmaps is baked by the engine's own reader, which the atlas builder cannot read");
        AtlasPixel(font, 0x1F600, 0.5f, 0.2f).Should().Be(new Color(255, 0, 0, 255));
        AtlasPixel(font, 0x1F600, 0.5f, 0.8f).Should().Be(new Color(0, 0, 255, 255));
        AtlasPixel(font, 0x2600, 0.5f, 0.5f).Should().Be(new Color(0, 255, 0, 255), "a character of the first plane is in color too");
        font.Glyphs[0x1F600].Advance.Should().Be(8, "1000 units wide at 8 pixels to the em");
        UnloadFont(font);
    }

    [Fact]
    public void A_Layered_Font_Draws_Its_Colored_Characters_In_Color_And_The_Rest_As_Coverage()
    {
        var font = LoadFontEx(Layers, 40, ['A', 0x1F600]);

        font.IsValid.Should().BeTrue();
        AtlasPixel(font, 0x1F600, 0.25f, 0.5f).Should().Be(new Color(255, 0, 0, 255));
        AtlasPixel(font, 0x1F600, 0.75f, 0.5f).Should().Be(new Color(0, 0, 255, 255));
        var a = AtlasPixel(font, 'A', 0.5f, 0.9f);
        (a.R, a.G, a.B).Should().Be(((byte)255, (byte)255, (byte)255), "a glyph with no color is the atlas builder's, white for the text's color to tint");
        UnloadFont(font);
    }
}
