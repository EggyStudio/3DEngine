using FluentAssertions;

namespace Engine.Tests.DiagnosticsHost;

// In the E3D collection with every test that lends the console, since the world it lends is the
// process's, and one test putting its lend back while another's command runs leaves that one none.
[Collection("E3D")]
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
    public void A_Value_Set_Over_A_Window_Shows_Its_Mean_Over_The_Seconds_Before()
    {
        var profile = new FrameProfile();
        double Shown() => profile.Snapshot().Values.Single(v => v.Name == "splashes").Value;

        // Set each sixty-fourth of a second, a time a double holds exactly, 0 and 100 by turns, as
        // a count that leaps from frame to frame.
        for (int frame = 0; frame < 128; frame++) profile.Set("splashes", frame % 2 * 100, seconds: 1, now: frame / 64.0);
        Shown().Should().Be(50, "the last second holds sixty-four of them, half of each");

        profile.Set("splashes", 20, seconds: 1, now: 3);
        Shown().Should().Be(20, "the values set more than a second before are dropped");
        profile.Set("splashes", 7);
        Shown().Should().Be(7, "set with no window, a value shows as it is");
    }

    [Fact]
    public void The_Slowest_Frame_Is_Kept_Whole_And_Forgotten_Once_Read()
    {
        var profile = new FrameProfile();
        void Frame(double frame, double present)
        {
            profile.Add("frame", frame);
            profile.Add("render.present", present);
            profile.Add("stage.Update", 1);
            profile.EndFrame();
        }

        Frame(16, 0.5);
        Frame(250, 800);
        Frame(17, 0.4);
        var slowest = profile.Slowest();
        slowest.Should().StartWith("frame 250.000 ms", "the stall's frame is the one kept");
        slowest.Should().Contain("render.present 800.000 ms", "with what held it, which the averages would have smoothed away");
        profile.Slowest().Should().Be("no frame measured since the last call");

        Frame(20, 1);
        profile.Slowest().Should().StartWith("frame 20.000 ms", "the frames after the last call are the ones looked at");
    }

    [Fact]
    public void The_Frame_After_A_Stall_Measures_Its_Values_Without_Allocating()
    {
        // The first frame measures its time alone and is the slowest, the frames after measure
        // forty systems' times, and a stall then trades the slowest frame's values for the frame's,
        // so the frame after fills the values the first frame held.
        var profile = new FrameProfile();
        var systems = Enumerable.Range(0, 40).Select(i => $"system.Update.{i}").ToArray();
        void Frame(double frame)
        {
            profile.Add("frame", frame);
            foreach (var system in systems) profile.Add(system, 0.1);
            profile.EndFrame();
        }

        profile.Add("frame", 100);
        profile.EndFrame();
        Frame(16);
        Frame(16);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Frame(250);
        Frame(16);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.Should().Be(0, "each of the frame's values has room in the values it is measured into, which a frame that is the slowest trades");
        profile.Slowest().Should().StartWith("frame 250.000 ms");
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
            using (ConsoleHost.Lend(app.World, app)) return ConsoleHost.Run(line).Text;
        }

        var report = Run("profile");
        report.Should().StartWith("frames 1\ncount 1200\nframe 12.500 ms");
        report.Should().Contain("stage.Last 4.000 ms\nstage.Update 1.000 ms", "the largest cost of a group comes first");

        Run("profile.reset");
        profile.Frames.Should().Be(0);
        profile.Average("frame").Should().Be(0);
    }
}
