using FluentAssertions;
using Xunit;

namespace Engine.Tests.Renderer;

[Trait("Category", "Unit")]
public class RenderTexturesTests
{
    [Fact]
    public void Set_And_TryGet_RoundTrips()
    {
        var textures = new RenderTextures();
        var desc = new RenderTextureDesc(1024, 768);

        textures.Set("main", desc);

        textures.TryGet("main", out var result).Should().BeTrue();
        result.Width.Should().Be(1024);
        result.Height.Should().Be(768);
    }

    [Fact]
    public void TryGet_Returns_False_When_Missing()
    {
        var textures = new RenderTextures();

        textures.TryGet("missing", out _).Should().BeFalse();
    }

    [Fact]
    public void Set_Overwrites_Existing()
    {
        var textures = new RenderTextures();
        textures.Set("rt", new RenderTextureDesc(100, 100));
        textures.Set("rt", new RenderTextureDesc(200, 200));

        textures.TryGet("rt", out var desc).Should().BeTrue();
        desc.Width.Should().Be(200);
    }

    [Fact]
    public void Remove_Deletes_Entry()
    {
        var textures = new RenderTextures();
        textures.Set("rt", new RenderTextureDesc(100, 100));

        textures.Remove("rt");

        textures.TryGet("rt", out _).Should().BeFalse();
    }
}
