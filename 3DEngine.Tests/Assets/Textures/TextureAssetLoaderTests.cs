using FluentAssertions;
using Xunit;

namespace Engine.Tests.Textures;

/// <summary>
/// Tests for <see cref="TextureAssetLoader"/>: extension dispatch into the
/// <see cref="TextureDecoderRegistry"/>, label parsing for color-space + mip hints,
/// and on-the-fly mip-chain generation when requested.
/// </summary>
[Trait("Category", "Unit")]
public class TextureAssetLoaderTests
{
    private sealed class CapturingDecoder : ITextureDecoder
    {
        public string[] Extensions { get; } = [".png"];
        public string FormatId => "fake";
        public TextureLoadSettings? LastSettings;

        public Task<Texture> DecodeAsync(AssetLoadContext context, TextureLoadSettings settings, CancellationToken ct)
        {
            LastSettings = settings;
            // Build a 2x2 RGBA so MipGenerator can run if asked.
            var t = new Texture
            {
                Pixels = new byte[2 * 2 * 4],
                Width = 2,
                Height = 2,
                Format = TextureFormat.Rgba8,
                ColorSpace = settings.ColorSpace ?? TextureColorSpace.Linear,
                SourcePath = context.Path.ToString(),
                SourceFormat = "fake",
            };
            return Task.FromResult(t);
        }
    }

    private static AssetLoadContext OpenContext(AssetPath path) =>
        new AssetLoadContext(new MemoryStream(new byte[] { 0 }), path, _ => default);

    [Fact]
    public async Task LoadAsync_Dispatches_To_Decoder_By_Extension()
    {
        var reg = new TextureDecoderRegistry();
        var dec = new CapturingDecoder();
        reg.RegisterDecoder(dec);
        var loader = new TextureAssetLoader(reg);

        using var ctx = OpenContext(new AssetPath("textures/foo.png"));
        var result = await loader.LoadAsync(ctx, CancellationToken.None);

        result.Success.Should().BeTrue(result.Error);
        result.Asset!.Width.Should().Be(2);
        result.Asset.SourceFormat.Should().Be("fake");
    }

    [Fact]
    public async Task LoadAsync_Returns_Failure_When_No_Decoder_For_Extension()
    {
        var loader = new TextureAssetLoader(new TextureDecoderRegistry());
        using var ctx = OpenContext(new AssetPath("textures/foo.xyz"));

        var result = await loader.LoadAsync(ctx, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Error.Should().Contain(".xyz");
    }

    [Fact]
    public async Task Label_Srgb_Sets_ColorSpace_Override()
    {
        var reg = new TextureDecoderRegistry();
        var dec = new CapturingDecoder();
        reg.RegisterDecoder(dec);
        var loader = new TextureAssetLoader(reg);

        using var ctx = OpenContext(new AssetPath("foo.png", "srgb"));
        var result = await loader.LoadAsync(ctx, CancellationToken.None);

        result.Success.Should().BeTrue();
        dec.LastSettings!.ColorSpace.Should().Be(TextureColorSpace.Srgb);
        result.Asset!.ColorSpace.Should().Be(TextureColorSpace.Srgb);
    }

    [Fact]
    public async Task Label_Linear_Sets_ColorSpace_Override()
    {
        var reg = new TextureDecoderRegistry();
        var dec = new CapturingDecoder();
        reg.RegisterDecoder(dec);
        var loader = new TextureAssetLoader(reg);

        using var ctx = OpenContext(new AssetPath("foo.png", "linear"));
        var result = await loader.LoadAsync(ctx, CancellationToken.None);

        result.Success.Should().BeTrue();
        dec.LastSettings!.ColorSpace.Should().Be(TextureColorSpace.Linear);
    }

    [Fact]
    public async Task Label_Mips_Triggers_Mip_Chain_Generation()
    {
        var reg = new TextureDecoderRegistry();
        var dec = new CapturingDecoder();
        reg.RegisterDecoder(dec);
        var loader = new TextureAssetLoader(reg);

        using var ctx = OpenContext(new AssetPath("foo.png", "mips"));
        var result = await loader.LoadAsync(ctx, CancellationToken.None);

        result.Success.Should().BeTrue();
        dec.LastSettings!.GenerateMips.Should().BeTrue();
        // 2x2 -> two mip levels.
        result.Asset!.MipCount.Should().Be(2);
    }

    [Fact]
    public async Task Label_Combines_Srgb_And_Mips()
    {
        var reg = new TextureDecoderRegistry();
        var dec = new CapturingDecoder();
        reg.RegisterDecoder(dec);
        var loader = new TextureAssetLoader(reg);

        using var ctx = OpenContext(new AssetPath("foo.png", "srgb|mips"));
        var result = await loader.LoadAsync(ctx, CancellationToken.None);

        result.Success.Should().BeTrue();
        dec.LastSettings!.ColorSpace.Should().Be(TextureColorSpace.Srgb);
        dec.LastSettings.GenerateMips.Should().BeTrue();
        result.Asset!.MipCount.Should().Be(2);
        result.Asset.ColorSpace.Should().Be(TextureColorSpace.Srgb);
    }

    [Fact]
    public void Extensions_Reflects_Registry_Snapshot()
    {
        var reg = new TextureDecoderRegistry();
        reg.RegisterDecoder(new CapturingDecoder());
        var loader = new TextureAssetLoader(reg);

        loader.Extensions.Should().Contain(".png");
    }

    [Fact]
    public void RefreshExtensions_Picks_Up_Newly_Registered_Backends()
    {
        var reg = new TextureDecoderRegistry();
        var loader = new TextureAssetLoader(reg);
        loader.Extensions.Should().BeEmpty();

        reg.RegisterDecoder(new CapturingDecoder());
        loader.RefreshExtensions();

        loader.Extensions.Should().Contain(".png");
    }

    [Fact]
    public void Constructor_Throws_On_Null_Registry()
    {
        var act = () => new TextureAssetLoader(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}