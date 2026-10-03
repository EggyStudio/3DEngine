using FluentAssertions;
using Xunit;

namespace Engine.Tests.Renderer;

[Trait("Category", "Unit")]
public class RenderSurfaceInfoTests
{
    [Fact]
    public void Apply_Returns_True_On_Size_Change()
    {
        var surface = new RenderSurfaceInfo();

        surface.Apply(800, 600).Should().BeTrue();
        surface.Width.Should().Be(800);
        surface.Height.Should().Be(600);
    }

    [Fact]
    public void Apply_Returns_False_When_Same_Size()
    {
        var surface = new RenderSurfaceInfo();
        surface.Apply(800, 600);

        surface.Apply(800, 600).Should().BeFalse();
    }

    [Fact]
    public void Apply_Clamps_To_Minimum_One()
    {
        var surface = new RenderSurfaceInfo();

        surface.Apply(0, -5);

        surface.Width.Should().Be(1);
        surface.Height.Should().Be(1);
    }

    [Fact]
    public void Apply_Increments_Revision_On_Change()
    {
        var surface = new RenderSurfaceInfo();

        surface.Apply(800, 600);
        surface.Revision.Should().Be(1UL);

        surface.Apply(1920, 1080);
        surface.Revision.Should().Be(2UL);

        // Same size: no revision bump
        surface.Apply(1920, 1080);
        surface.Revision.Should().Be(2UL);
    }
}
