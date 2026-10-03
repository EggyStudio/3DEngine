using FluentAssertions;

namespace Engine.Tests.Core;

[Trait("Category", "Unit")]
public class FixedTimeTests
{
    private static int Steps(FixedTime time)
    {
        int steps = 0;
        while (time.TryStep()) steps++;
        return steps;
    }

    [Fact]
    public void Each_Whole_Step_Of_Accumulated_Time_Is_One_Step()
    {
        var time = new FixedTime { Hz = 60 };
        time.Accumulate(3.5 / 60);

        Steps(time).Should().Be(3);
        time.Alpha.Should().BeApproximately(0.5, 1e-9);
    }

    [Fact]
    public void A_Short_Frame_Steps_Nothing_And_Carries_Its_Time()
    {
        var time = new FixedTime { Hz = 60 };
        time.Accumulate(0.6 / 60);
        Steps(time).Should().Be(0);

        time.Accumulate(0.6 / 60);
        Steps(time).Should().Be(1);
    }

    [Fact]
    public void A_Long_Frame_Stops_At_The_Cap_And_Drops_The_Backlog()
    {
        var time = new FixedTime { Hz = 60, MaxStepsPerFrame = 5 };
        time.Accumulate(1.0);

        Steps(time).Should().Be(5);
        time.Accumulator.Should().BeLessThanOrEqualTo(time.StepSeconds);
    }

    [Fact]
    public void BeginFrame_Runs_FixedUpdate_Once_Per_Step()
    {
        var app = new App();
        var fixedTime = new FixedTime { Hz = 60 };
        app.World.InsertResource(fixedTime);
        var ran = new List<Stage>();
        foreach (var stage in new[] { Stage.PreUpdate, Stage.FixedUpdate, Stage.Update })
        {
            var s = stage;
            app.AddSystem(s, new SystemDescriptor(_ => ran.Add(s), $"Record.{s}").MainThreadOnly());
        }

        fixedTime.Accumulate(2.2 / 60);
        app.BeginFrame();

        ran.Should().Equal(Stage.PreUpdate, Stage.FixedUpdate, Stage.FixedUpdate, Stage.Update);
    }
}
