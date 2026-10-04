using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Api;

[Collection("Engine3D")]
[Trait("Category", "Unit")]
public class Engine3DDisplayTests
{
    [Fact]
    public void Modes_That_Differ_Only_Past_Whole_Hertz_Are_Listed_Once_In_Order()
    {
        var modes = DistinctModes([(2560, 1440, 165.0f), (2560, 1440, 164.96f), (2560, 1440, 60f), (1920, 1080, 59.94f), (1920, 1080, 60f)]);

        modes.Should().Equal(new MonitorMode(2560, 1440, 165), new MonitorMode(2560, 1440, 60), new MonitorMode(1920, 1080, 60));
    }

    [Fact]
    public void A_Monitor_That_Is_Not_There_Has_No_Modes_And_No_Window_Is_Changed()
    {
        GetMonitorModes(-1).Should().BeEmpty();
        GetMonitorModes(GetMonitorCount()).Should().BeEmpty();

        // With no window these do nothing, as the rest of the window's calls do.
        SetWindowFullscreenMode(new MonitorMode(1280, 720, 60));
        SetWindowMonitor(0);
        IsWindowFullscreen().Should().BeFalse();
    }
}
