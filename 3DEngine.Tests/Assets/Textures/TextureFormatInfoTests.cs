using FluentAssertions;
using Xunit;

namespace Engine.Tests.Textures;

/// <summary>
/// Tests for <see cref="TextureFormatInfo"/> - the per-<see cref="TextureFormat"/>
/// metadata helpers used by the loader, mip generator, and renderer upload paths.
/// </summary>
[Trait("Category", "Unit")]
public class TextureFormatInfoTests
{
    [Theory]
    [InlineData(TextureFormat.R8, 1)]
    [InlineData(TextureFormat.Rg8, 2)]
    [InlineData(TextureFormat.Rgba8, 4)]
    [InlineData(TextureFormat.Rgba16F, 8)]
    [InlineData(TextureFormat.Rgba32F, 16)]
    public void BytesPerPixel_Matches_Format_Width(TextureFormat fmt, int expected)
    {
        TextureFormatInfo.BytesPerPixel(fmt).Should().Be(expected);
    }

    [Theory]
    [InlineData(TextureFormat.Bc1, 8)]
    [InlineData(TextureFormat.Bc4, 8)]
    [InlineData(TextureFormat.Bc3, 16)]
    [InlineData(TextureFormat.Bc5, 16)]
    [InlineData(TextureFormat.Bc6H, 16)]
    [InlineData(TextureFormat.Bc7, 16)]
    public void BytesPerBlock_Matches_BC_Format(TextureFormat fmt, int expected)
    {
        TextureFormatInfo.BytesPerBlock(fmt).Should().Be(expected);
    }

    [Theory]
    [InlineData(TextureFormat.Bc1, true)]
    [InlineData(TextureFormat.Bc7, true)]
    [InlineData(TextureFormat.Rgba8, false)]
    [InlineData(TextureFormat.Rgba32F, false)]
    public void IsBlockCompressed_Reflects_Family(TextureFormat fmt, bool expected)
    {
        TextureFormatInfo.IsBlockCompressed(fmt).Should().Be(expected);
    }

    [Fact]
    public void BytesPerPixel_Throws_For_Block_Compressed_Format()
    {
        var act = () => TextureFormatInfo.BytesPerPixel(TextureFormat.Bc7);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void BytesPerBlock_Throws_For_Uncompressed_Format()
    {
        var act = () => TextureFormatInfo.BytesPerBlock(TextureFormat.Rgba8);
        act.Should().Throw<ArgumentException>();
    }
}