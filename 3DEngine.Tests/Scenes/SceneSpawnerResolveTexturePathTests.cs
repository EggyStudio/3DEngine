using FluentAssertions;
using Xunit;

namespace Engine.Tests.Scenes;

/// <summary>
/// Tests for <see cref="SceneSpawner.ResolveTexturePath"/> - the helper that joins
/// scene-relative <see cref="SceneTextureRef.AssetPath"/> values against the source
/// scene's directory while letting absolute and synthetic <c>__embedded__/</c>
/// paths through verbatim.
/// </summary>
[Trait("Category", "Unit")]
public class SceneSpawnerResolveTexturePathTests
{
    [Fact]
    public void Empty_Texture_Path_Returns_Empty_String()
    {
        SceneSpawner.ResolveTexturePath("models", "").Should().BeEmpty();
    }

    [Fact]
    public void Relative_Path_Is_Joined_Against_Scene_Directory()
    {
        SceneSpawner.ResolveTexturePath("models/hero", "albedo.png")
            .Should().Be("models/hero/albedo.png");
    }

    [Fact]
    public void Backslash_Texture_Path_Is_Normalized_To_Forward_Slashes()
    {
        SceneSpawner.ResolveTexturePath("models/hero", @"sub\albedo.png")
            .Should().Be("models/hero/sub/albedo.png");
    }

    [Fact]
    public void Absolute_Texture_Path_Is_Returned_Verbatim_Stripped_Of_Leading_Slash()
    {
        SceneSpawner.ResolveTexturePath("models/hero", "/textures/global.png")
            .Should().Be("textures/global.png");
    }

    [Fact]
    public void Embedded_Synthetic_Path_Is_Returned_Verbatim()
    {
        SceneSpawner.ResolveTexturePath("models/hero", "__embedded__/model/image_0.png")
            .Should().Be("__embedded__/model/image_0.png");
    }

    [Fact]
    public void Empty_Or_Null_Scene_Directory_Returns_Texture_Path_Unchanged()
    {
        SceneSpawner.ResolveTexturePath(null, "albedo.png").Should().Be("albedo.png");
        SceneSpawner.ResolveTexturePath("", "albedo.png").Should().Be("albedo.png");
    }

    [Fact]
    public void Trailing_Slash_On_Scene_Directory_Is_Trimmed_Before_Join()
    {
        SceneSpawner.ResolveTexturePath("models/hero/", "albedo.png")
            .Should().Be("models/hero/albedo.png");
    }
}