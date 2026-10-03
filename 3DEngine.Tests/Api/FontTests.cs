using FluentAssertions;

namespace Engine.Tests.Api;

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

    [Fact]
    public void A_Font_Bakes_The_Characters_Asked_For_Beyond_Latin_1()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "3DEngine.slnx"))) directory = directory.Parent;
        var lato = Path.Combine(directory!.FullName, "3DEngine.Examples", "resources", "fonts", "Lato-Regular.ttf");
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
}
