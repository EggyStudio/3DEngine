using FluentAssertions;

namespace Engine.Tests.Platform;

/// <summary>How the window starts SDL's video, X11 asked for first on Linux and let go where it does not start.</summary>
[Trait("Category", "Unit")]
public sealed class SdlWindowTests
{
    [Fact]
    public void On_Linux_The_Window_Asks_For_X11_And_Starts_Without_It_Where_X11_Does_Not_Start()
    {
        var steps = new List<string>();
        SdlWindow.StartVideo(true, driver => steps.Add($"hint {driver}"), () => steps.Add("clear"), () => { steps.Add("start"); return true; }, out var fellBack)
            .Should().BeTrue();
        steps.Should().Equal(["hint x11", "start"], "the hint is set before SDL starts");
        fellBack.Should().BeFalse();

        steps.Clear();
        var starts = 0;
        SdlWindow.StartVideo(true, driver => steps.Add($"hint {driver}"), () => steps.Add("clear"), () => { steps.Add("start"); return ++starts > 1; }, out fellBack)
            .Should().BeTrue("the second start, the hint cleared, opens on the driver SDL finds");
        steps.Should().Equal(["hint x11", "start", "clear", "start"], "a session without XWayland still opens");
        fellBack.Should().BeTrue();
    }

    [Fact]
    public void Elsewhere_The_Window_Names_No_Driver_And_Starts_Once()
    {
        var steps = new List<string>();
        SdlWindow.StartVideo(false, driver => steps.Add($"hint {driver}"), () => steps.Add("clear"), () => { steps.Add("start"); return false; }, out var fellBack)
            .Should().BeFalse();
        steps.Should().Equal(["start"], "SDL picks the system's own driver");
        fellBack.Should().BeFalse();
    }
}
