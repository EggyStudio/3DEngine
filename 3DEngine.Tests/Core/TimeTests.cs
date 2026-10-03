using FluentAssertions;
using Xunit;

namespace Engine.Tests.Common;

[Trait("Category", "Unit")]
public class TimeTests
{
    [Fact]
    public void Update_Increments_FrameCount()
    {
        var time = new Time();

        time.Update(0.016, 0.016);
        time.Update(0.032, 0.016);

        time.FrameCount.Should().Be(2);
    }

    [Fact]
    public void DeltaSeconds_Is_Clamped_To_MaxDeltaSeconds()
    {
        var time = new Time { MaxDeltaSeconds = 0.1 };

        time.Update(1.0, 0.5); // raw = 0.5, max = 0.1

        time.DeltaSeconds.Should().BeApproximately(0.1, 0.001);
        time.RawDeltaSeconds.Should().BeApproximately(0.5, 0.001);
    }

    [Fact]
    public void DeltaSeconds_Not_Clamped_When_Below_Max()
    {
        var time = new Time { MaxDeltaSeconds = 0.25 };

        time.Update(0.016, 0.016);

        time.DeltaSeconds.Should().BeApproximately(0.016, 0.001);
        time.RawDeltaSeconds.Should().BeApproximately(0.016, 0.001);
    }

    [Fact]
    public void Negative_RawDelta_Is_Clamped_To_Zero()
    {
        var time = new Time();

        time.Update(0.0, -0.5);

        time.RawDeltaSeconds.Should().Be(0.0);
        time.DeltaSeconds.Should().Be(0.0);
    }

    [Fact]
    public void ElapsedSeconds_Tracks_Total_Time()
    {
        var time = new Time();

        time.Update(1.5, 0.016);

        time.ElapsedSeconds.Should().BeApproximately(1.5, 0.001);
    }

    [Fact]
    public void SmoothedFps_Is_Set_On_First_Frame()
    {
        var time = new Time();

        time.Update(0.01, 0.01); // 100 FPS

        time.SmoothedFps.Should().BeApproximately(100.0, 5.0);
    }

    [Fact]
    public void SmoothedFps_Uses_ExponentialMovingAverage()
    {
        var time = new Time();

        // First frame: 100 FPS
        time.Update(0.01, 0.01);
        double firstSmoothed = time.SmoothedFps;

        // Second frame: 50 FPS
        time.Update(0.03, 0.02);

        // EMA should be between 50 and firstSmoothed (weighted toward firstSmoothed)
        time.SmoothedFps.Should().BeLessThan(firstSmoothed);
        time.SmoothedFps.Should().BeGreaterThan(50.0);
    }

    [Fact]
    public void Fps_Returns_Zero_When_DeltaSeconds_Is_Zero()
    {
        var time = new Time();

        time.Update(0.0, 0.0);

        time.Fps.Should().Be(0.0);
    }

    [Fact]
    public void Fps_Returns_Reciprocal_Of_DeltaSeconds()
    {
        var time = new Time();

        time.Update(0.01, 0.01);

        time.Fps.Should().BeApproximately(100.0, 1.0);
    }

    [Fact]
    public void ToString_Contains_Key_Info()
    {
        var time = new Time();
        time.Update(1.0, 0.016);

        var str = time.ToString();

        str.Should().Contain("Frame=1");
        str.Should().Contain("Elapsed=");
    }
}
