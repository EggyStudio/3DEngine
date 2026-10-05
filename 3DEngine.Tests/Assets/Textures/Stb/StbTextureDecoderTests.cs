using FluentAssertions;
using Xunit;

namespace Engine.Tests.Textures.Stb;

/// <summary>
/// Integration tests for <see cref="StbTextureDecoder"/>: verifies the decoder
/// produces the expected <see cref="TextureAsset"/> shape from in-memory image bytes
/// generated on the fly (no on-disk fixtures required).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Backend", "Stb")]
public class StbTextureDecoderTests
{
    /// <summary>
    /// Builds a 2x2 24-bpp BMP with four solid colors. BMP is a format
    /// <see cref="StbTextureDecoder"/> registers (and StbImageSharp ships with), and
    /// the binary headers are simple enough to author in a few lines without a
    /// third-party encoder.
    /// </summary>
    private static byte[] MakeBmp2x2()
    {
        // 2x2 24-bpp BMP: each row = width(2)*3 = 6 bytes, padded to 4-byte boundary = 8.
        // Pixel data is bottom-up and BGR-ordered.
        const int width = 2, height = 2, bpp = 24;
        int rowStride = ((width * bpp / 8 + 3) / 4) * 4;
        int pixelDataSize = rowStride * height;
        const int fileHeaderSize = 14;
        const int infoHeaderSize = 40;
        int fileSize = fileHeaderSize + infoHeaderSize + pixelDataSize;
        int pixelOffset = fileHeaderSize + infoHeaderSize;

        using var ms = new MemoryStream(fileSize);
        using var bw = new BinaryWriter(ms);

        // BITMAPFILEHEADER
        bw.Write((byte)'B'); bw.Write((byte)'M');
        bw.Write(fileSize);
        bw.Write((ushort)0); bw.Write((ushort)0);
        bw.Write(pixelOffset);

        // BITMAPINFOHEADER
        bw.Write(infoHeaderSize);
        bw.Write(width);
        bw.Write(height);
        bw.Write((ushort)1);    // planes
        bw.Write((ushort)bpp);
        bw.Write(0);            // BI_RGB
        bw.Write(pixelDataSize);
        bw.Write(2835);         // x ppm (~72 dpi)
        bw.Write(2835);         // y ppm
        bw.Write(0); bw.Write(0);

        // Pixel data (bottom-up). Row 1 (top in image): red, green. Row 0 (bottom): blue, white.
        // Bottom-up: write bottom row first.
        // BGR order.
        // Bottom row: blue, white.
        bw.Write((byte)255); bw.Write((byte)0);   bw.Write((byte)0);    // B (blue)
        bw.Write((byte)255); bw.Write((byte)255); bw.Write((byte)255);  // white
        bw.Write((byte)0);   bw.Write((byte)0);                          // padding to 8-byte stride
        // Top row: red, green.
        bw.Write((byte)0);   bw.Write((byte)0);   bw.Write((byte)255);  // red (BGR -> 0,0,255)
        bw.Write((byte)0);   bw.Write((byte)255); bw.Write((byte)0);    // green
        bw.Write((byte)0);   bw.Write((byte)0);                          // padding

        return ms.ToArray();
    }

    private static AssetLoadContext OpenContext(string path, byte[] bytes) =>
        new AssetLoadContext(new MemoryStream(bytes), new AssetPath(path), _ => default);

    [Fact]
    public void Extensions_Cover_Common_Ldr_And_Hdr_Formats()
    {
        var dec = new StbTextureDecoder();
        dec.Extensions.Should().Contain(new[] { ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".hdr", ".pic", ".ppm" });
        dec.FormatId.Should().Be("stb");
    }

    [Fact]
    public async Task DecodeAsync_Ppm_Produces_Rgba8_With_Expected_Dimensions()
    {
        var dec = new StbTextureDecoder();
        using var ctx = OpenContext("tests/inline.bmp", MakeBmp2x2());

        var tex = await dec.DecodeAsync(ctx, TextureLoadSettings.Default, CancellationToken.None);

        tex.Width.Should().Be(2);
        tex.Height.Should().Be(2);
        tex.Format.Should().Be(TextureFormat.Rgba8);
        tex.MipCount.Should().Be(1);
        tex.SourceFormat.Should().Be("stb");
        tex.SourcePath.Should().Be("tests/inline.bmp");
        // 2x2 RGBA = 16 bytes, alpha expanded to 0xFF for opaque sources.
        tex.Pixels.Length.Should().Be(16);
        tex.Pixels[3].Should().Be(255);
        tex.Pixels[7].Should().Be(255);
    }

    [Fact]
    public async Task DecodeAsync_Ppm_Honours_ColorSpace_Hint()
    {
        var dec = new StbTextureDecoder();
        using var ctx = OpenContext("tests/inline.bmp", MakeBmp2x2());

        var tex = await dec.DecodeAsync(
            ctx,
            new TextureLoadSettings { ColorSpace = TextureColorSpace.Srgb },
            CancellationToken.None);

        tex.ColorSpace.Should().Be(TextureColorSpace.Srgb);
    }

    [Fact]
    public async Task DecodeAsync_Defaults_To_Linear_When_No_Hint()
    {
        var dec = new StbTextureDecoder();
        using var ctx = OpenContext("tests/inline.bmp", MakeBmp2x2());

        var tex = await dec.DecodeAsync(ctx, TextureLoadSettings.Default, CancellationToken.None);

        tex.ColorSpace.Should().Be(TextureColorSpace.Linear);
    }

    [Fact]
    public async Task DecodeAsync_Throws_On_Garbage_Input()
    {
        var dec = new StbTextureDecoder();
        using var ctx = OpenContext("tests/garbage.png", new byte[] { 0xFF, 0xFE, 0xFD });

        var act = () => dec.DecodeAsync(ctx, TextureLoadSettings.Default, CancellationToken.None);

        // Stb may throw various decode exceptions; we just want a non-success outcome.
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task DecodeAsync_Cancels_Before_Work()
    {
        var dec = new StbTextureDecoder();
        using var ctx = OpenContext("tests/inline.bmp", MakeBmp2x2());
        var ct = new CancellationToken(canceled: true);

        var act = () => dec.DecodeAsync(ctx, TextureLoadSettings.Default, ct);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task TextureAssetLoader_Routes_Ppm_Through_Stb_End_To_End()
    {
        var reg = new TextureDecoderRegistry();
        reg.RegisterDecoder(new StbTextureDecoder());
        var loader = new TextureAssetLoader(reg);

        using var ctx = new AssetLoadContext(
            new MemoryStream(MakeBmp2x2()), new AssetPath("tests/inline.bmp", "srgb|mips"), _ => default);

        var result = await loader.LoadAsync(ctx, CancellationToken.None);

        result.Success.Should().BeTrue(result.Error);
        result.Asset!.Width.Should().Be(2);
        result.Asset.MipCount.Should().Be(2, "label asks for a mip chain");
        result.Asset.ColorSpace.Should().Be(TextureColorSpace.Srgb);
    }
}