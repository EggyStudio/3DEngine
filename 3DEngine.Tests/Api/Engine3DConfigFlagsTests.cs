using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

[Collection("Engine3D")]
[Trait("Category", "Unit")]
public sealed class Engine3DConfigFlagsTests : IDisposable
{
    // CloseWindow forgets the flags, with or without a window.
    public void Dispose() => CloseWindow();

    [Fact]
    public void With_No_Flags_A_Window_Is_Multisampled_Fixed_In_Size_And_Shown()
    {
        var config = ConfigFor(800, 450, "plain");

        config.Samples.Should().Be(4);
        (config.Resizable, config.Vsync, config.Undecorated, config.Fullscreen).Should().Be((false, false, false, false),
            "a raylib window is resized only when FLAG_WINDOW_RESIZABLE asks");
        config.WindowData.Should().Be(new WindowData("plain", 800, 450));
    }

    [Fact]
    public void Flags_Add_Up_And_Shape_The_Next_Window()
    {
        SetConfigFlags(ConfigFlags.VsyncHint | ConfigFlags.WindowUndecorated);
        SetConfigFlags(ConfigFlags.WindowTopmost | ConfigFlags.WindowMaximized | ConfigFlags.WindowResizable);
        SetConfigSamples(1);
        var config = ConfigFor(640, 360, "flags");

        (config.Vsync, config.Undecorated, config.Topmost, config.Resizable).Should().Be((true, true, true, true), "flags asked twice add up");
        config.WindowCommand.Should().Be(WindowCommand.Maximize);
        config.Samples.Should().Be(1, "multisampling was turned off");

        CloseWindow();
        ConfigFor(640, 360, "after").Vsync.Should().BeFalse("CloseWindow forgets the flags");
    }
}
