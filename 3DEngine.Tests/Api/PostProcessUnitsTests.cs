using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Api;

/// <summary>
/// The post-processing settings in the units a person types, each set as what its older form's
/// formula gave: the exposure's bounds in EV at ISO 100, the scattered bloom's spread from 0 to 1,
/// and lift, gamma and gain as three numbers.
/// </summary>
[Collection("E3D")]
public sealed class PostProcessUnitsTests : IDisposable
{
    public PostProcessUnitsTests() => UseApp(new App(Config.Default with { Headless = true }).AddPlugin(new DefaultPlugins()));

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Exposures_Bounds_In_EV_Give_The_Multipliers_Against_The_Camera_And_Follow_It()
    {
        // Sponza's camera, ISO 200 at 1/200 s and f/16, and its meter's -1 to 14, which it set as
        // 2 to the camera's EV less 14 and 2 to the camera's EV plus 1.
        SetCameraExposure(200, 1 / 200f, 16);
        SetAutoExposure(true);
        SetAutoExposureEv100(-1, 14);
        var ev = GetCameraEv100();
        var (_, min, max, _) = GetAutoExposure();
        min.Should().BeApproximately(MathF.Pow(2, ev - 14), 1e-5f);
        max.Should().BeApproximately(MathF.Pow(2, ev + 1), 1e-2f);
        GetAutoExposureEv100().Should().Be((-1f, 14f));

        // A camera set after takes the bounds with it, a sunny day's ISO 100 at 1/125 s and f/16.
        SetCameraExposure(100, 1 / 125f, 16);
        ev = GetCameraEv100();
        (_, min, max, _) = GetAutoExposure();
        min.Should().BeApproximately(MathF.Pow(2, ev - 14), 1e-5f);
        max.Should().BeApproximately(MathF.Pow(2, ev + 1), 1e-1f);

        // Multipliers given again take the bounds' place.
        SetAutoExposure(true, 0.5f, 2);
        GetAutoExposureEv100().Should().BeNull();
        GetAutoExposure().Should().Be((true, 0.5f, 2f, 2f));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Scatter_Runs_From_0_To_1_Over_The_Chains_Range_And_The_Bloom_Is_Chosen_Apart()
    {
        GetBloomKind().Should().Be(BloomKind.Added, "the engine's bloom is drawn unless the scattered one is chosen");
        var settings = GetApp().World.GetOrInsertResource(() => new BloomSettings());
        foreach (var (scatter, share) in new[] { (0f, 0.05f), (0.5f, 0.5f), (1f, 0.95f), (0.7f, 0.68f) })
        {
            SetBloomScatter(scatter);
            GetBloomScatter().Should().Be(scatter);
            settings.Scatter.Should().BeApproximately(share, 1e-6f, $"a scatter of {scatter} gives each level {share} of its place");
        }
        settings.Scattered.Should().BeFalse("a scatter alone chooses no bloom");
        SetBloomKind(BloomKind.Scattered);
        settings.Scattered.Should().BeTrue();
        GetBloomKind().Should().Be(BloomKind.Scattered);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Lift_Gamma_And_Gain_As_Three_Numbers_Are_White_Colors_Moved_By_Them()
    {
        SetLiftGammaGain(-0.1f, 0.225f, 0.3f);
        GetLiftGammaGain().Should().Be((new Vector4(1, 1, 1, -0.1f), new Vector4(1, 1, 1, 0.225f), new Vector4(1, 1, 1, 0.3f)));
    }
}
