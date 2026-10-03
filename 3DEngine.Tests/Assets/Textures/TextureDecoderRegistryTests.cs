using FluentAssertions;
using Xunit;

namespace Engine.Tests.Textures;

/// <summary>
/// Tests for <see cref="TextureDecoderRegistry"/> - extension/format dispatch and
/// last-write-wins semantics.
/// </summary>
[Trait("Category", "Unit")]
public class TextureDecoderRegistryTests
{
    private sealed class FakeDecoder : ITextureDecoder
    {
        public string[] Extensions { get; }
        public string FormatId { get; }
        public FakeDecoder(string formatId, params string[] extensions)
        {
            FormatId = formatId;
            Extensions = extensions;
        }
        public Task<Texture> DecodeAsync(AssetLoadContext context, TextureLoadSettings settings, CancellationToken ct) =>
            throw new NotImplementedException();
    }

    [Fact]
    public void RegisterDecoder_Indexes_By_All_Extensions_And_FormatId()
    {
        var reg = new TextureDecoderRegistry();
        var dec = new FakeDecoder("fake", ".png", ".jpg");

        reg.RegisterDecoder(dec);

        reg.FindDecoderByExtension(".png").Should().BeSameAs(dec);
        reg.FindDecoderByExtension(".jpg").Should().BeSameAs(dec);
        reg.FindDecoderByFormat("fake").Should().BeSameAs(dec);
        reg.Extensions.Should().BeEquivalentTo(new[] { ".png", ".jpg" });
        reg.Decoders.Should().ContainSingle().Which.Should().BeSameAs(dec);
    }

    [Fact]
    public void RegisterDecoder_Last_Write_Wins_Per_Extension()
    {
        var reg = new TextureDecoderRegistry();
        var stb = new FakeDecoder("stb", ".png");
        var ktx = new FakeDecoder("ktx2", ".png");

        reg.RegisterDecoder(stb);
        reg.RegisterDecoder(ktx);

        reg.FindDecoderByExtension(".png").Should().BeSameAs(ktx);
        // Both format-id entries are still reachable.
        reg.FindDecoderByFormat("stb").Should().BeSameAs(stb);
        reg.FindDecoderByFormat("ktx2").Should().BeSameAs(ktx);
    }

    [Fact]
    public void Find_Returns_Null_For_Unknown_Extension()
    {
        var reg = new TextureDecoderRegistry();
        reg.FindDecoderByExtension(".xyz").Should().BeNull();
        reg.FindDecoderByFormat("nope").Should().BeNull();
    }

    [Fact]
    public void RegisterDecoder_Throws_On_Null()
    {
        var reg = new TextureDecoderRegistry();
        var act = () => reg.RegisterDecoder(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Extension_Lookup_Is_Case_Insensitive()
    {
        var reg = new TextureDecoderRegistry();
        var dec = new FakeDecoder("fake", ".png");
        reg.RegisterDecoder(dec);

        reg.FindDecoderByExtension(".PNG").Should().BeSameAs(dec);
    }
}