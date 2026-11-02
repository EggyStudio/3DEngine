using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The day sky lit through the air the Sky module models (<see cref="SkyDay.Atmosphere"/>), the
/// sun's light at the ground as the module gives it at the sun's height, warming as it sinks and
/// fading as its disc sets, the moon's by night, and the light the world's directional light takes
/// for it through the camera.
/// </summary>
[Collection("E3D")]
public sealed class DaySkyAirTests : IDisposable
{
    private static readonly Sky.Atmosphere Earth = Sky.Atmosphere.Earth;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // The hour on a day of no lean at which the sun stands at an elevation, 15 degrees an hour from 6.
    private static float HourAt(float elevation) => 6 + elevation / 15;

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Sun_Through_The_Air_Is_The_Modules_At_Its_Height()
    {
        foreach (var elevation in new[] { 60f, 53.97f, 16.55f, 3 })
        {
            var (direction, light) = DaySky.AirLightAt(Earth, Sky.Sun.Outside, HourAt(elevation), 0);
            var sun = Sky.Sun.At(Earth, elevation);
            Sky.Colorimetry.Luminance(light).Should().BeApproximately(sun.Illuminance, sun.Illuminance * 2e-3f, $"at {elevation} degrees");
            Sky.Colorimetry.TemperatureOf(light).Should().BeApproximately(sun.Temperature, 5, $"at {elevation} degrees");
            direction.Y.Should().BeNegative("the light shines down from the sun");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Suns_Light_Fades_As_Its_Disc_Sets_And_The_Full_Moons_Takes_Over_Once_It_Is_Down()
    {
        var horizon = Sky.Colorimetry.Luminance(DaySky.AirLightAt(Earth, Sky.Sun.Outside, 6, 0).Light);
        var high = Sky.Colorimetry.Luminance(Sky.Sun.Outside * Earth.Transmittance(0, 0));
        horizon.Should().BeApproximately(high / 2, high * 0.01f, "half the disc is up as its middle meets the horizon");
        Sky.Colorimetry.Luminance(DaySky.AirLightAt(Earth, Sky.Sun.Outside, HourAt(-0.3f), 0).Light).Should().Be(0, "the disc has set");

        var (direction, moonlight) = DaySky.AirLightAt(Earth, Sky.Sun.Outside, 0, 0);
        direction.Y.Should().BeNegative("the moon stands high opposite the sun at midnight");
        Sky.Colorimetry.Luminance(moonlight).Should().BeApproximately(Sky.Moon.At(Earth, 90, 0).Illuminance, 1e-3f, "the full moon overhead");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Through_The_Air_The_Stars_Wait_Until_The_Sun_Is_Six_Degrees_Down_And_The_Sun_Is_The_Skys_Disc()
    {
        static void Quad(ReadOnlySpan<ImmediateVertex> corners, int texture) { }
        var sky = new DaySky { Day = new SkyDay { Minutes = 0, Tilt = 0, Atmosphere = Earth } };
        sky.Hours = HourAt(-3);
        sky.DrawStars(Quad, 0).Should().Be(0, "the sky is light in civil twilight");
        sky.Hours = HourAt(-15);
        sky.DrawStars(Quad, 0).Should().BeGreaterThan(1_000, "the stars are out past nautical twilight");
        sky.Hours = HourAt(30);
        sky.DrawSunAndMoon(Quad, 0, 1, 2).Should().Be(0, "the sky draws the sun's disc and the moon is down");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void A_Sun_Set_Outside_The_Air_Warms_Through_It_As_It_Sinks()
    {
        // The scene drawn in Unity's HDRP that the engine's sky is read against sets its sun outside
        // the air to 97,050 lux at 5,633 K with the sun 53.97 degrees high, which HDRP's Earth warms
        // to 4,924 K at the ground.
        var hue = Linear(ColorFromTemperature(5633));
        var outside = hue / Sky.Colorimetry.Luminance(hue) * 97_050;
        var high = DaySky.AirLightAt(Earth, outside, HourAt(53.97f), 0).Light;
        var low = DaySky.AirLightAt(Earth, outside, HourAt(16.55f), 0).Light;
        Sky.Colorimetry.TemperatureOf(high).Should().BeInRange(4_850, 5_050);
        Sky.Colorimetry.TemperatureOf(low).Should().BeLessThan(4_200);
        Sky.Colorimetry.Luminance(high).Should().BeInRange(80_000, 85_000, "97,050 lux times the air's share at 54 degrees");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Suns_Light_Takes_The_Airs_Lux_Through_The_Camera_And_The_Map_Is_The_Airs()
    {
        UseApp(new App(Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 }).AddPlugin(new DefaultPlugins()));
        var hour = HourAt(53.97f);
        SetSkyDay(new SkyDay { Minutes = 0, Tilt = 0, Atmosphere = Earth });
        SetSkyTime(hour);
        BeginDrawing();
        EndDrawing();

        var world = GetApp().World;
        var units = world.Resource<PhysicalUnits>();
        var ecs = world.Resource<EcsWorld>();
        ecs.TryResolve(GetSkyLightHandle().Entity, out var entity).Should().BeTrue();
        var light = ecs.GetReadOnly<Light>(entity);
        var sun = Sky.Sun.At(Earth, 53.97f);
        var luminance = Sky.Colorimetry.Luminance(light.Color);
        (light.Intensity * luminance * MathF.PI / units.PreExposure).Should().BeApproximately(sun.Illuminance, sun.Illuminance * 0.01f,
            "the light returns the sun's lux from a white surface facing it, through the camera");

        // The map is the air's sky, which the GPU draws from the module's model, and no horizon
        // color is drawn over its ground.
        var map = world.Resource<EnvironmentMap>();
        map.Air.Should().NotBeNull("the map is drawn from the air");
        map.Air!.Air.Should().Be(Earth);
        Sky.Colorimetry.Luminance(map.Air.Light).Should().BeApproximately(Sky.Sun.OutsideIlluminance * units.PreExposure, 1e-4f,
            "the sun outside the air seen through the camera");
        world.TryGetResource<SkyHorizon>(out _).Should().BeFalse();
        GetApp().World.Resource<DaySky>().SunDisc.Should().NotBeNull("the sun is up");
    }

    private static LightHandle GetSkyLightHandle() => GetApp().World.Resource<DaySky>().Light;
}
