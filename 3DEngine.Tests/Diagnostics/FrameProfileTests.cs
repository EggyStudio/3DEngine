using FluentAssertions;

namespace Engine.Tests.Diagnostics;

[Collection("Console")]
[Trait("Category", "Unit")]
public class FrameProfileTests
{
    [Fact]
    public void A_Value_Is_Averaged_Over_About_Sixty_Frames()
    {
        var profile = new FrameProfile();

        profile.Add("frame", 10);
        profile.Average("frame").Should().Be(10, "the first measurement starts the average");
        profile.Add("frame", 70);
        profile.Average("frame").Should().BeApproximately(11, 1e-9, "a sixtieth of the difference moves it");
        profile.Average("never").Should().Be(0);
    }

    [Fact]
    public void The_Command_Reports_The_Programs_Values_And_The_Averages_And_Resets_Them()
    {
        var app = new App();
        new FrameProfilePlugin().Build(app);
        var profile = app.World.Resource<FrameProfile>();
        profile.Set("count", 1200);
        profile.Add("frame", 12.5);
        profile.Add("stage.Update", 1);
        profile.Add("stage.Last", 4);
        profile.EndFrame();

        string Run(string line)
        {
            using (ConsoleHost.Lend(app.World, app)) return ConsoleCommands.Run(line) ?? "";
        }

        var report = Run("profile");
        report.Should().StartWith("frames 1\ncount 1200\nframe 12.500 ms");
        report.Should().Contain("stage.Last 4.000 ms\nstage.Update 1.000 ms", "the largest cost of a group comes first");

        Run("profile.reset");
        profile.Frames.Should().Be(0);
        profile.Average("frame").Should().Be(0);
    }
}
