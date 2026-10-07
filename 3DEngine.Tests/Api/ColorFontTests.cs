using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

/// <summary>
/// Color emoji, read by the engine's own TrueType reader from <c>bitmaps.ttf</c>, PNG images at 8
/// pixels to the em and no outlines, <c>layers.ttf</c>, outlines colored by layers, and
/// <c>paints.ttf</c>, outlines colored by paints, which <c>build/make-color-test-fonts.py</c> writes.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Integration")]
public sealed class ColorFontTests : IDisposable
{
    private static readonly string Bitmaps = Path.Combine(AppContext.BaseDirectory, "Api", "bitmaps.ttf");
    private static readonly string Layers = Path.Combine(AppContext.BaseDirectory, "Api", "layers.ttf");
    private static readonly string Paints = Path.Combine(AppContext.BaseDirectory, "Api", "paints.ttf");
    private static readonly string Collection = Path.Combine(AppContext.BaseDirectory, "Api", "layers.ttc");

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
    public void Paints_Fill_Outlines_With_A_Gradient_And_A_Color_Moved_By_A_Transform_Inside_The_Clip_Box()
    {
        var font = TrueTypeFont.Read(File.ReadAllBytes(Paints))!;
        var glyph = font.GlyphIndex(0x1F600);
        font.HasColor(glyph).Should().BeTrue();

        // At a tenth of a pixel a unit, the clip box from 100 to 900 across and 0 to 800 up.
        var (rgba, width, height, left, top) = font.Color(glyph, 0.1f)!.Value;
        (width, height, left, top).Should().Be((80, 80, 10, -80));
        var red = Pixel(rgba, width, 1, 40);
        (red.R, red.B).Should().Match<(byte R, byte B)>(c => c.R > 245 && c.B < 10, "the gradient starts red at the square's left");
        var blue = Pixel(rgba, width, 78, 40);
        (blue.R, blue.B).Should().Match<(byte R, byte B)>(c => c.R < 10 && c.B > 245, "and ends blue at its right");
        var middle = Pixel(rgba, width, 25, 70);
        // Unit 352.5 across, 0.316 of the way from red to blue.
        ((int)middle.R).Should().BeCloseTo(174, 2);
        ((int)middle.B).Should().BeCloseTo(81, 2);
        Pixel(rgba, width, 60, 40).Should().Be(new Color(0, 255, 0, 255), "the small square is green, moved from 400 to 600 across to 600 to 800");
        Pixel(rgba, width, 35, 40).G.Should().Be(0, "where it was before the move is the gradient's");
    }

    [Fact]
    public void A_Font_With_No_Character_Map_Of_Unicode_Is_Refused_Rather_Than_Stopping_The_Program()
    {
        // layers.ttf with its one character map's encoding made Microsoft's Symbol, as Marlett's is,
        // which the atlas builder reads none of and asserts on.
        var data = File.ReadAllBytes(Layers);
        int tables = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4)), cmap = 0;
        for (int i = 0; i < tables; i++)
            if (System.Text.Encoding.ASCII.GetString(data, 12 + i * 16, 4) == "cmap")
                cmap = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12 + i * 16 + 8));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(cmap + 4 + 2), 0);

        FontProblem(data).Should().Be("no character map of Unicode, only of a symbol or an older encoding");
        LoadFontFromMemory(".ttf", data, 24, ['A']).Should().BeSameAs(GetFontDefault(), "the font is refused for the default one");
    }

    // A table's offset in a lone font's directory.
    private static int TableOffset(byte[] data, string tag)
    {
        int tables = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
        for (int i = 0; i < tables; i++)
            if (System.Text.Encoding.ASCII.GetString(data, 12 + i * 16, 4) == tag)
                return (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(12 + i * 16 + 8));
        return -1;
    }

    [Fact]
    public void A_Font_With_None_Of_Latin_1_Is_Baked_With_The_First_Character_It_Has()
    {
        // layers.ttf with its 'A' moved to the Greek omega, as a font of one script other than Latin
        // has, of whose characters the atlas builder would find none in Latin-1 and assert.
        var data = File.ReadAllBytes(Layers);
        var group = TableOffset(data, "cmap") + 12 + 16;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(group), 0x3A9);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(group + 4), 0x3A9);

        var font = LoadFontFromMemory(".ttf", data, 24, null);
        font.Should().NotBeSameAs(GetFontDefault());
        font.Glyphs.Should().ContainKey(0x3A9);
        UnloadFont(font);
    }

    [Fact]
    public void A_Font_Of_CFF2_Outlines_Alone_Is_Refused()
    {
        // layers.ttf with its glyf table named CFF2, as a variable OpenType font holds its outlines,
        // which the atlas builder cannot parse and asserts on.
        var data = File.ReadAllBytes(Layers);
        int tables = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(4));
        for (int i = 0; i < tables; i++)
            if (System.Text.Encoding.ASCII.GetString(data, 12 + i * 16, 4) == "glyf")
                System.Text.Encoding.ASCII.GetBytes("CFF2").CopyTo(data, 12 + i * 16);

        FontProblem(data).Should().Be("outlines of CFF2 alone, as a variable OpenType font holds them, which the atlas builder does not read");
    }

    [Fact]
    public void A_Font_Of_Color_Bitmaps_Alone_Asked_For_A_Distance_Field_Gives_The_Default_Font()
    {
        LoadFontEx(Bitmaps, 24, [0x1F600], FontType.Sdf).Should().BeSameAs(GetFontDefault(), "it has no outlines to measure distances from");
    }

    [Fact]
    public void A_Collections_First_Font_Draws_Its_Colored_Characters_In_Color()
    {
        var reader = TrueTypeFont.Read(File.ReadAllBytes(Collection));
        reader.Should().NotBeNull("a collection's first font is read, as the atlas builder reads it");
        reader!.HasColor(reader.GlyphIndex(0x1F600)).Should().BeTrue();

        var font = LoadFontEx(Collection, 40, ['A', 0x1F600]);
        AtlasPixel(font, 0x1F600, 0.25f, 0.5f).Should().Be(new Color(255, 0, 0, 255));
        UnloadFont(font);
    }

    [Fact]
    public void A_Font_Of_Paints_Loads_Its_Characters_In_Color()
    {
        var font = LoadFontEx(Paints, 40, [0x1F600]);

        font.IsValid.Should().BeTrue();
        AtlasPixel(font, 0x1F600, 0.75f, 0.5f).Should().Be(new Color(0, 255, 0, 255));
        UnloadFont(font);
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
    public void A_Sequence_The_Font_Joins_Is_Drawn_As_The_One_Glyph_It_Joins_Into()
    {
        // The face, the zero width joiner and the sun, which the font's ligature joins into its
        // yellow glyph 3, as an emoji font joins a family from its people.
        const string Joined = "\U0001F600\u200D\u2600";
        var font = LoadFontEx(Bitmaps, 8, LoadCodepoints(Joined));

        TextKeys(font, Joined).Should().Equal(JoinedKey(3));
        AtlasPixel(font, JoinedKey(3), 0.5f, 0.5f).Should().Be(new Color(255, 255, 0, 255), "the glyph joined into is baked with the characters asked for");
        MeasureTextEx(font, Joined, 8, 0).X.Should().Be(8, "one glyph 8 wide, where the face and the sun apart are 16");
        var image = ImageTextEx(font, Joined, 8, 0, Color.White);
        GetImageColor(image, 4, 4).Should().Be(new Color(255, 255, 0, 255));
        UnloadImage(image);

        TextKeys(font, "\U0001F600\u2600").Should().Equal([0x1F600, 0x2600], "without the joiner they are two characters");
        TextKeys(font, "\U0001F600\u200D\U0001F600").Should().Equal([0x1F600, 0x1F600],
            "a sequence the font has no ligature for is its characters, the joiner left between them hidden as a shaper hides it");
        MeasureTextEx(font, "\U0001F600\u200D\U0001F600", 8, 0).X.Should().Be(16);
        UnloadFont(font);
    }

    [Fact]
    public void A_Chained_Context_Chooses_A_Glyph_By_The_Character_After_It()
    {
        // The sun before U+FE0F is turned into glyph 3 by a rule that looks ahead, as Segoe UI
        // Emoji chooses a glyph by the selector after it.
        var font = LoadFontEx(Bitmaps, 8, LoadCodepoints("\u2600\uFE0F"));

        TextKeys(font, "\u2600\uFE0F").Should().Equal([JoinedKey(3)], "the selector chose the glyph and is not drawn");
        TextKeys(font, "\u2600").Should().Equal([0x2600], "with nothing after it the sun keeps its own glyph");
        TextKeys(font, "a\u2600\uFE0F b").Should().Equal('a', JoinedKey(3), ' ', 'b');
        UnloadFont(font);
    }

    [Fact]
    public void A_Font_With_No_Substitutions_Draws_Each_Character_As_Itself()
    {
        var font = LoadFontEx(Layers, 40, ['A', 0x1F600, 0x200D]);

        font.Joining.Should().BeNull("the font has no GSUB table");
        TextKeys(font, "\U0001F600\u200D\U0001F600").Should().Equal(0x1F600, 0x200D, 0x1F600);
        UnloadFont(font);
    }

    [Fact]
    public void A_Distance_Field_Font_Holds_A_Character_Past_U_FFFF_From_Its_Outline()
    {
        // U+1F600's outline in layers.ttf is a square, from 100 to 900 across and 0 to 800 up.
        var font = LoadFontEx(Layers, 32, ['A', 0x1F600], FontType.Sdf);

        font.Type.Should().Be(FontType.Sdf);
        font.Glyphs.Should().ContainKey(0x1F600, "the reader rasterizes it into the bake the distances are measured in");
        AtlasPixel(font, 0x1F600, 0.5f, 0.5f).A.Should().BeGreaterThan(200, "the square's middle is well inside it");
        var corner = AtlasPixel(font, 0x1F600, 0.02f, 0.02f);
        corner.A.Should().BeLessThan(128, "the glyph's box is grown past the outline to hold the distances outside it");
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
