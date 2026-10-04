using FluentAssertions;

namespace Engine.Tests.Api;

[Collection("Engine3D")]
[Trait("Category", "Unit")]
public class FontTests
{
    [Fact]
    public void The_Default_Font_Bakes_Printable_Glyphs()
    {
        var baked = Engine3D.BakeAtlas(atlas => atlas.AddFontDefault());

        baked.Should().NotBeNull();
        var (image, size, glyphs) = baked!.Value;
        size.Should().Be(13);
        image.IsValid.Should().BeTrue();
        var a = glyphs.Should().ContainKey('A').WhoseValue;
        a.Advance.Should().BeGreaterThan(0);
        (a.X1 - a.X0).Should().BeGreaterThan(0);
        a.U1.Should().BeGreaterThan(a.U0);
        glyphs.Should().ContainKey(' ').WhoseValue.Advance.Should().Be(a.Advance, "ProggyClean is monospaced");
    }

    [Fact]
    public void Code_Points_Become_Merged_Ranges_Ending_In_Zero()
    {
        Engine3D.GlyphRanges([0x41, 0x43, 0x42, 0x45, 0x1F600, 0x41]).Should().Equal((ushort)0x41, (ushort)0x43, (ushort)0x45, (ushort)0x45, (ushort)0);
        Engine3D.LoadCodepoints("aba€").Should().Equal('a', 'b', 0x20AC);
    }

    internal static string Lato()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "3DEngine.slnx"))) directory = directory.Parent;
        return Path.Combine(directory!.FullName, "3DEngine.Examples", "resources", "fonts", "Lato-Regular.ttf");
    }

    [Fact]
    public void A_Font_Drawn_Well_Past_Its_Size_Is_Baked_Again_At_That_Size_And_Measured_In_It()
    {
        var app = new App();
        app.World.InitResource<TextureStore>();
        app.World.InitResource<DrawList>();
        Engine3D.UseApp(app);
        try
        {
            var font = Engine3D.LoadFontEx(Lato(), 16);
            var store = app.World.Resource<TextureStore>();
            var before = store.Count;

            Engine3D.DrawTextEx(font, "A", System.Numerics.Vector2.Zero, 18, 0, Color.White);
            store.Count.Should().Be(before, "a size within a quarter of the bake is drawn from it");
            Engine3D.DrawTextEx(font, "A", System.Numerics.Vector2.Zero, 62, 0, Color.White);
            store.Count.Should().Be(before + 1, "62 pixels is baked again at 64");
            Engine3D.DrawTextEx(font, "A", System.Numerics.Vector2.Zero, 63, 0, Color.White);
            store.Count.Should().Be(before + 1, "and that bake serves the next size that rounds to it");

            var drawn = app.World.Resource<DrawList>().Batches.Last().Texture;
            drawn.Should().NotBe(font.Texture.Id, "large text is drawn from the larger bake");
            var size = Engine3D.MeasureTextEx(font, "AAAA", 63, 0);
            size.X.Should().BeApproximately(4 * font.ForSize(63).Glyphs['A'].Advance * 63 / 64, 0.01f, "it is measured in the bake it is drawn from");

            Engine3D.UnloadFont(font);
            store.Count.Should().Be(before - 1, "unloading the font frees its larger bakes too");
        }
        finally
        {
            Engine3D.UseApp(null);
        }
    }

    [Fact]
    public void A_Font_From_Memory_Draws_As_One_From_Its_File_And_Lines_Take_The_Spacing_Set()
    {
        var app = new App();
        app.World.InitResource<TextureStore>();
        app.World.InitResource<DrawList>();
        Engine3D.UseApp(app);
        try
        {
            var fromFile = Engine3D.LoadFontEx(Lato(), 24);
            var fromMemory = Engine3D.LoadFontFromMemory(".ttf", File.ReadAllBytes(Lato()), 24, null);

            fromMemory.Glyphs['A'].Should().Be(fromFile.Glyphs['A'], "the same bytes bake the same glyphs");
            Engine3D.GetGlyphInfo(fromMemory, 'A').Should().Be(fromFile.Glyphs['A']);
            Engine3D.GetGlyphInfo(fromMemory, 0x4E00).Should().BeNull("Latin-1 has no CJK");
            var rec = Engine3D.GetGlyphAtlasRec(fromMemory, 'A');
            (rec.Width, rec.Height).Should().Match<(float W, float H)>(s => s.W > 0 && s.H > 0);
            Engine3D.LoadFontFromMemory(".png", [1, 2, 3], 24, null).Should().BeSameAs(Engine3D.GetFontDefault());

            var twoLines = Engine3D.MeasureTextEx(fromMemory, "A\nA", 24, 0).Y;
            Engine3D.SetTextLineSpacing(16);
            Engine3D.MeasureTextEx(fromMemory, "A\nA", 24, 0).Y.Should().BeApproximately(24 + 16 + fromMemory.LineHeight, 0.01f,
                "a line then moves down by the size and the spacing");
            Engine3D.MeasureTextEx(fromMemory, "A\nA", 24, 0).Y.Should().NotBe(twoLines);

            Engine3D.DrawTextCodepoints(fromMemory, ['H', 'i'], System.Numerics.Vector2.Zero, 24, 0, Color.White);
            Engine3D.DrawTextCodepoint(fromMemory, 'A', System.Numerics.Vector2.Zero, 24, Color.White);
            app.World.Resource<DrawList>().Vertices.Length.Should().BeGreaterThan(0);
        }
        finally
        {
            Engine3D.UseApp(null);
        }
    }

    [Fact]
    public void A_Font_Drawn_As_An_Image_Is_Read_By_Its_Key_Colored_Gaps()
    {
        var app = new App();
        app.World.InitResource<TextureStore>();
        Engine3D.UseApp(app);
        try
        {
            // Magenta all round, a gap of 2 before the glyphs and 1 above their row: an A three
            // pixels wide, a gap of 2, and a B four wide, both five high.
            var image = Engine3D.GenImageColor(16, 7, Color.Magenta);
            Engine3D.ImageDrawRectangle(ref image, 2, 1, 3, 5, Color.White);
            Engine3D.ImageDrawRectangle(ref image, 7, 1, 4, 5, Color.White);

            var font = Engine3D.LoadFontFromImage(image, Color.Magenta, 'A');

            font.BaseSize.Should().Be(5);
            font.Glyphs.Keys.Should().Equal('A', 'B');
            font.Glyphs['A'].Advance.Should().Be(3);
            font.Glyphs['B'].Advance.Should().Be(4);
            Engine3D.GetGlyphAtlasRec(font, 'B').Should().Be(new Rectangle(7, 1, 4, 5));
            Engine3D.MeasureTextEx(font, "AB", 5, 1).X.Should().Be(3 + 1 + 4 + 1);
            Engine3D.GetImageColor(font.Atlas, 0, 0).A.Should().Be(0, "the key becomes clear");
        }
        finally
        {
            Engine3D.UseApp(null);
        }
    }

    [Fact]
    public void A_Font_Bakes_The_Characters_Asked_For_Beyond_Latin_1()
    {
        var lato = Lato();
        var ranges = Engine3D.GlyphRanges(Engine3D.LoadCodepoints("A€Ωж"));

        var pin = System.Runtime.InteropServices.GCHandle.Alloc(ranges, System.Runtime.InteropServices.GCHandleType.Pinned);
        try
        {
            var address = pin.AddrOfPinnedObject();
            var baked = Engine3D.BakeAtlas(atlas => atlas.AddFontFromFileTTF(lato, 24, null, address));

            var glyphs = baked!.Value.Glyphs;
            glyphs.Should().ContainKeys('A', 0x20AC);
            glyphs.Should().NotContainKey('B', "only the characters asked for are baked");
            glyphs[0x20AC].Advance.Should().BeGreaterThan(0);
        }
        finally
        {
            pin.Free();
        }
    }

    [Fact]
    public void A_Distance_Field_Measures_From_The_Edge_Of_A_Square()
    {
        // A 9 by 9 square in the middle of 21 by 21.
        var coverage = new byte[21 * 21];
        for (int y = 6; y < 15; y++)
        for (int x = 6; x < 15; x++)
            coverage[y * 21 + x] = 255;

        var field = DistanceField.Signed(coverage, 21, 21);

        field[10 * 21 + 10].Should().Be(4.5f, "the center is four and a half pixels inside each side");
        field[10 * 21 + 6].Should().Be(0.5f);
        field[10 * 21 + 5].Should().Be(-0.5f);
        field[0].Should().BeApproximately(0.5f - MathF.Sqrt(2 * 6 * 6), 1e-4f, "a corner of the image is nearest the square's corner");
    }

    [Fact]
    public void A_Distance_Field_Matches_A_Search_Of_Every_Pixel()
    {
        var random = new Random(7);
        const int Width = 23, Height = 17;
        var coverage = new byte[Width * Height];
        for (int i = 0; i < coverage.Length; i++) coverage[i] = (byte)(random.Next(5) == 0 ? 255 : 0);

        var field = DistanceField.Signed(coverage, Width, Height);

        for (int i = 0; i < coverage.Length; i++)
        {
            var inside = coverage[i] >= 128;
            var nearest = double.MaxValue;
            for (int j = 0; j < coverage.Length; j++)
                if (coverage[j] >= 128 != inside)
                    nearest = Math.Min(nearest, Math.Sqrt(Math.Pow(i % Width - j % Width, 2) + Math.Pow(i / Width - j / Width, 2)));
            field[i].Should().BeApproximately((float)(inside ? nearest - 0.5 : 0.5 - nearest), 1e-4f, $"pixel {i}");
        }
    }

    [Fact]
    public void A_Distance_Field_Atlas_Grows_Each_Glyph_By_Its_Padding_At_The_Size_Meant()
    {
        var lato = Lato();
        var plain = Engine3D.BakeAtlas(atlas => atlas.AddFontFromFileTTF(lato, 32))!.Value;

        var (field, coverage, glyphs) = Engine3D.BakeDistanceField(lato, 32, IntPtr.Zero)!.Value;

        (coverage.Width, coverage.Height).Should().Be((field.Width, field.Height));
        var a = glyphs['A'];
        a.Advance.Should().BeApproximately(plain.Glyphs['A'].Advance, 0.5f, "the glyphs are measured at the size meant");
        (a.X1 - a.X0).Should().BeApproximately((a.U1 - a.U0) * field.Width, 1e-3f, "a pixel of the glyph is a texel of the atlas");
        (a.Y1 - a.Y0).Should().BeApproximately((a.V1 - a.V0) * field.Height, 1e-3f);
        (a.X1 - a.X0).Should().BeApproximately(plain.Glyphs['A'].X1 - plain.Glyphs['A'].X0 + 2 * Engine3D.SdfPadding, 1.5f, "each side grows by the padding");

        // The grown corner is outside the glyph, and the alpha there says how far.
        var corner = GetAlpha(field, a.U0, a.V0);
        corner.Should().BeLessThan(128);
        var b = glyphs['l'];
        var stem = GetAlpha(field, (b.U0 + b.U1) / 2, (b.V0 + b.V1) / 2);
        stem.Should().BeGreaterThan(128, "the middle of an l is inside its stroke");
        GetAlpha(coverage, (b.U0 + b.U1) / 2, (b.V0 + b.V1) / 2).Should().Be(255);
    }

    private static byte GetAlpha(Image image, float u, float v)
    {
        var x = Math.Clamp((int)(u * image.Width), 0, image.Width - 1);
        var y = Math.Clamp((int)(v * image.Height), 0, image.Height - 1);
        return image.Data[(y * image.Width + x) * 4 + 3];
    }
}
