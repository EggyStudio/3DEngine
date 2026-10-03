using FluentAssertions;
using Xunit;

namespace Engine.Tests.Textures;

/// <summary>
/// Tests for <see cref="TextureMipGenerator"/> - extents math and the per-format
/// 2x2 box filter producing a complete mip chain.
/// </summary>
[Trait("Category", "Unit")]
public class TextureMipGeneratorTests
{
    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 2, 2)]
    [InlineData(4, 4, 3)]
    [InlineData(8, 1, 4)]   // 8x1 -> 4x1 -> 2x1 -> 1x1
    [InlineData(256, 256, 9)]
    public void MipCount_Matches_Floor_Log2_MaxDim_Plus_One(int w, int h, int expected)
    {
        TextureMipGenerator.MipCount(w, h).Should().Be(expected);
    }

    [Fact]
    public void TotalBytes_Matches_Sum_Of_Mip_Levels()
    {
        // 4x4 Rgba8 -> levels: 4x4=64, 2x2=16, 1x1=4 -> 84 bytes total.
        TextureMipGenerator.TotalBytes(4, 4, bytesPerPixel: 4, mipCount: 3).Should().Be(84);
    }

    [Fact]
    public void WithMipChain_Returns_Source_When_Already_Has_Mips()
    {
        var src = new Texture
        {
            Pixels = new byte[16],
            Width = 2,
            Height = 2,
            MipCount = 2,
            Format = TextureFormat.Rgba8,
        };

        var result = TextureMipGenerator.WithMipChain(src);
        result.Should().BeSameAs(src);
    }

    [Fact]
    public void WithMipChain_Returns_Source_When_Already_OneByOne()
    {
        var src = new Texture
        {
            Pixels = new byte[4],
            Width = 1,
            Height = 1,
            MipCount = 1,
            Format = TextureFormat.Rgba8,
        };

        TextureMipGenerator.WithMipChain(src).Should().BeSameAs(src);
    }

    [Fact]
    public void WithMipChain_Throws_For_Block_Compressed_Format()
    {
        var src = new Texture
        {
            Pixels = new byte[16],
            Width = 4,
            Height = 4,
            Format = TextureFormat.Bc7,
        };

        var act = () => TextureMipGenerator.WithMipChain(src);
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void WithMipChain_Rgba8_Builds_Full_Chain_With_Box_Filter_Average()
    {
        // 2x2 RGBA: four pixels of R=(0,64,128,192), G=B=A=255 ->
        // mip 1 (1x1) box filter averages R = round((0+64+128+192+2)/4) = (384+2)>>2 = 96.
        var src = new Texture
        {
            Pixels = new byte[]
            {
                  0, 255, 255, 255,
                 64, 255, 255, 255,
                128, 255, 255, 255,
                192, 255, 255, 255,
            },
            Width = 2,
            Height = 2,
            Format = TextureFormat.Rgba8,
        };

        var dst = TextureMipGenerator.WithMipChain(src);

        dst.Should().NotBeSameAs(src);
        dst.Width.Should().Be(2);
        dst.Height.Should().Be(2);
        dst.MipCount.Should().Be(2);
        dst.Pixels.Length.Should().Be(2 * 2 * 4 + 1 * 1 * 4);

        // Level 0 verbatim copy.
        dst.Pixels[0..16].Should().BeEquivalentTo(src.Pixels);

        // Level 1 box-filtered pixel.
        dst.Pixels[16].Should().Be(96);   // R
        dst.Pixels[17].Should().Be(255);  // G
        dst.Pixels[18].Should().Be(255);  // B
        dst.Pixels[19].Should().Be(255);  // A
    }

    [Fact]
    public void WithMipChain_Preserves_ColorSpace_And_SourcePath()
    {
        var src = new Texture
        {
            Pixels = new byte[16],
            Width = 2,
            Height = 2,
            Format = TextureFormat.Rgba8,
            ColorSpace = TextureColorSpace.Srgb,
            SourcePath = "tests/foo.png",
            SourceFormat = "stb",
        };

        var dst = TextureMipGenerator.WithMipChain(src);

        dst.ColorSpace.Should().Be(TextureColorSpace.Srgb);
        dst.SourcePath.Should().Be("tests/foo.png");
        dst.SourceFormat.Should().Be("stb");
    }
}