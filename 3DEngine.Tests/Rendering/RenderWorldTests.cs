using FluentAssertions;
using Xunit;

namespace Engine.Tests.Renderer;

[Trait("Category", "Unit")]
public class RenderWorldTests
{
    [Fact]
    public void Set_And_Get_RoundTrips()
    {
        var rw = new RenderWorld();

        rw.Set("hello");

        rw.Get<string>().Should().Be("hello");
    }

    [Fact]
    public void Get_Throws_When_Missing()
    {
        var rw = new RenderWorld();

        var act = () => rw.Get<int>();

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void TryGet_Returns_Value_When_Present()
    {
        var rw = new RenderWorld();
        rw.Set(42);

        rw.TryGet<int>().Should().Be(42);
    }

    [Fact]
    public void TryGet_Returns_Default_When_Missing()
    {
        var rw = new RenderWorld();

        rw.TryGet<string>().Should().BeNull();
    }

    [Fact]
    public void Contains_Returns_True_When_Present()
    {
        var rw = new RenderWorld();
        rw.Set("test");

        rw.Contains<string>().Should().BeTrue();
    }

    [Fact]
    public void Contains_Returns_False_When_Missing()
    {
        var rw = new RenderWorld();

        rw.Contains<double>().Should().BeFalse();
    }

    [Fact]
    public void Remove_Returns_True_And_Removes()
    {
        var rw = new RenderWorld();
        rw.Set("value");

        rw.Remove<string>().Should().BeTrue();
        rw.Contains<string>().Should().BeFalse();
    }

    [Fact]
    public void Remove_Returns_False_When_Missing()
    {
        var rw = new RenderWorld();

        rw.Remove<int>().Should().BeFalse();
    }

    [Fact]
    public void Set_Overwrites_Existing()
    {
        var rw = new RenderWorld();
        rw.Set("first");
        rw.Set("second");

        rw.Get<string>().Should().Be("second");
    }
}
