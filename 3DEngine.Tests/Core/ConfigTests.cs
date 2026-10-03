using FluentAssertions;
using Xunit;

namespace Engine.Tests.Common;

[Trait("Category", "Unit")]
public class ConfigTests
{
    [Fact]
    public void Default_Has_Expected_Values()
    {
        var cfg = Config.Default;

        cfg.WindowData.Title.Should().Be("3D Engine");
        cfg.WindowData.Width.Should().Be(600);
        cfg.WindowData.Height.Should().Be(400);
        cfg.Graphics.Should().Be(GraphicsBackend.Vulkan);
        cfg.WindowCommand.Should().Be(WindowCommand.Show);
    }

    [Fact]
    public void GetDefault_Overrides_Parameters()
    {
        var cfg = Config.Default
            .WithWindow(title: "My Game", width: 1920, height: 1080)
            .WithCommand(WindowCommand.Maximize)
            .WithGraphics(GraphicsBackend.Sdl);

        cfg.WindowData.Title.Should().Be("My Game");
        cfg.WindowData.Width.Should().Be(1920);
        cfg.WindowData.Height.Should().Be(1080);
        cfg.WindowCommand.Should().Be(WindowCommand.Maximize);
        cfg.Graphics.Should().Be(GraphicsBackend.Sdl);
    }

    [Fact]
    public void WithWindow_Returns_New_Instance_With_Updated_Window()
    {
        var original = Config.Default;
        var modified = original.WithWindow("Test", 800, 600);

        modified.Should().NotBeSameAs(original);
        modified.WindowData.Title.Should().Be("Test");
        modified.WindowData.Width.Should().Be(800);
        modified.WindowData.Height.Should().Be(600);
        // Other properties unchanged
        modified.Graphics.Should().Be(original.Graphics);
    }

    [Fact]
    public void WithCommand_Returns_New_Instance()
    {
        var original = Config.Default;
        var modified = original.WithCommand(WindowCommand.Hide);

        modified.Should().NotBeSameAs(original);
        modified.WindowCommand.Should().Be(WindowCommand.Hide);
        modified.WindowData.Should().Be(original.WindowData);
    }

    [Fact]
    public void WithGraphics_Returns_New_Instance()
    {
        var original = Config.Default;
        var modified = original.WithGraphics(GraphicsBackend.Sdl);

        modified.Should().NotBeSameAs(original);
        modified.Graphics.Should().Be(GraphicsBackend.Sdl);
    }

    [Fact]
    public void WithWindow_WindowData_Overload()
    {
        var data = new WindowData("Custom", 1280, 720);
        var cfg = Config.Default.WithWindow(data);

        cfg.WindowData.Should().Be(data);
    }

    [Fact]
    public void WindowData_Clamps_Negative_Dimensions_To_One()
    {
        var data = new WindowData("Test", -10, 0);

        data.Width.Should().Be(1);
        data.Height.Should().Be(1);
    }

    [Fact]
    public void WindowData_Blank_Title_Falls_Back_To_Default()
    {
        var data1 = new WindowData("", 100, 100);
        var data2 = new WindowData("  ", 100, 100);
        var data3 = new WindowData(null!, 100, 100);

        data1.Title.Should().Be("3D Engine");
        data2.Title.Should().Be("3D Engine");
        data3.Title.Should().Be("3D Engine");
    }

    [Fact]
    public void WindowData_Valid_Title_Is_Preserved()
    {
        var data = new WindowData("Hello World", 640, 480);

        data.Title.Should().Be("Hello World");
    }

    [Fact]
    public void Config_ToString_Contains_Key_Properties()
    {
        var cfg = Config.Default;
        var str = cfg.ToString();

        str.Should().Contain("3D Engine");
        str.Should().Contain("600");
        str.Should().Contain("400");
        str.Should().Contain("Vulkan");
    }

    [Fact]
    public void Config_Equality_Works_By_Value()
    {
        var a = Config.Default.WithWindow(title: "A", width: 100, height: 100);
        var b = Config.Default.WithWindow(title: "A", width: 100, height: 100);

        a.Should().Be(b);
    }

    [Fact]
    public void Config_Different_Values_Are_Not_Equal()
    {
        var a = Config.Default.WithWindow(title: "A", width: 100, height: 100);
        var b = Config.Default.WithWindow(title: "B", width: 100, height: 100);

        a.Should().NotBe(b);
    }
}