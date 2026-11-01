using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Api;

/// <summary>
/// Lights and the day sky given in physical units and the camera they are seen through, the
/// exposure value a camera's ISO, shutter and aperture give, a light's lux, candelas and lumens
/// taken to the engine's intensity, a plain intensity left as it is, and a black body's color.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Unit")]
public sealed class E3DPhysicalUnitsTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private static void Headless() =>
        UseApp(new App(Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 }).AddPlugin(new DefaultPlugins()));

    private static EcsWorld Ecs => GetApp().World.Resource<EcsWorld>();

    private static Light Read(LightHandle handle)
    {
        Ecs.TryResolve(handle.Entity, out var entity).Should().BeTrue();
        return Ecs.GetReadOnly<Light>(entity);
    }

    // What a candela a square meter reaches the frame as through a camera at this exposure value.
    private static float Through(float ev100) => 1 / (1.2f * MathF.Pow(2, ev100));

    [Fact]
    public void A_Camera_Exposure_Value_Is_Its_Aperture_Squared_Over_Its_Shutter_At_ISO_100()
    {
        Headless();
        GetCameraEv100().Should().Be(15, "a sunny day's camera stands until one is set");

        SetCameraExposure(200, 1 / 200f, 16);
        GetCameraEv100().Should().BeApproximately(MathF.Log2(16 * 16 * 200) - 1, 1e-4f).And.BeApproximately(14.644f, 1e-3f);

        SetCameraExposure(0, 1, 1);
        GetCameraEv100().Should().BeApproximately(14.644f, 1e-3f, "a camera of no ISO is passed over");
    }

    [Fact]
    public void A_Sun_Given_In_Lux_Reads_As_A_White_Surface_Returns_It_Through_The_Camera()
    {
        Headless();
        SetCameraExposure(200, 1 / 200f, 16);
        var sun = CreateDirectionalLight(-Vector3.UnitY, Color.White);

        SetLightIlluminance(sun, 97050);
        Read(sun).Intensity.Should().BeApproximately(97050 / MathF.PI * Through(14.644f), 1e-3f).And.BeApproximately(1.006f, 0.001f,
            "a white surface in a noon sun reads about white through a camera at EV 14.6");

        SetCameraExposure(100, 1 / 125f, 16);
        Read(sun).Intensity.Should().BeApproximately(97050 / MathF.PI * Through(GetCameraEv100()), 1e-3f, "a new camera takes the light again");
    }

    [Fact]
    public void A_Colored_Light_Takes_Its_Hue_From_Its_Color_And_Its_Luminance_From_Its_Lux()
    {
        Headless();
        var white = CreateDirectionalLight(-Vector3.UnitY, Color.White);
        var orange = CreateDirectionalLight(-Vector3.UnitY, new Color(255, 160, 60));
        SetLightIlluminance(white, 50000);
        SetLightIlluminance(orange, 50000);

        var light = Read(orange);
        var luminance = (0.2126f * light.Color.X + 0.7152f * light.Color.Y + 0.0722f * light.Color.Z) * light.Intensity;
        luminance.Should().BeApproximately(Read(white).Intensity, 1e-4f);
    }

    [Fact]
    public void A_Lamps_Lumens_Spread_Over_Its_Sphere_Or_Its_Cone_Give_Its_Candelas()
    {
        Headless();
        var point = CreatePointLight(Vector3.Zero, Color.White);
        var bulb = CreatePointLight(Vector3.Zero, Color.White);
        var spot = CreateSpotLight(Vector3.Zero, -Vector3.UnitY, Color.White, outerAngle: 30);

        SetLightLuminousFlux(point, 800);
        SetLightLuminousIntensity(bulb, 800 / (4 * MathF.PI));
        Read(point).Intensity.Should().BeApproximately(Read(bulb).Intensity, 1e-9f);
        Read(bulb).Intensity.Should().BeApproximately(800 / (4 * MathF.PI) / MathF.PI * Through(15), 1e-9f);

        SetLightLuminousFlux(spot, 800);
        Read(spot).Intensity.Should().BeApproximately(800 / (2 * MathF.PI * (1 - MathF.Cos(MathF.PI / 6))) / MathF.PI * Through(15), 1e-9f,
            "a spot spreads its lumens over its outer cone");
    }

    [Fact]
    public void A_Light_Given_A_Plain_Intensity_Again_Is_Not_Seen_Through_The_Camera()
    {
        Headless();
        var sun = CreateDirectionalLight(-Vector3.UnitY, Color.White);
        SetLightIlluminance(sun, 97050);
        SetLightColor(sun, Color.White, 2);

        SetCameraExposure(800, 1 / 30f, 2);
        Read(sun).Intensity.Should().Be(2);
    }

    [Theory]
    [InlineData(1900f)]
    [InlineData(2700f)]
    [InlineData(4000f)]
    public void A_Low_Temperature_Is_Red_Over_Green_Over_Blue(float kelvin)
    {
        var color = ColorFromTemperature(kelvin);
        color.R.Should().Be(255);
        color.R.Should().BeGreaterThan(color.G);
        color.G.Should().BeGreaterThan(color.B);
    }

    [Fact]
    public void Daylight_Is_Near_White_And_A_Blue_Sky_s_Temperature_Blue()
    {
        var day = ColorFromTemperature(6500);
        Math.Min(day.R, Math.Min(day.G, day.B)).Should().BeGreaterThan(235, $"6,500 K is near white, {day}");
        var sky = ColorFromTemperature(12000);
        sky.B.Should().Be(255);
        sky.R.Should().BeLessThan(sky.B);
    }

    [Fact]
    public void The_Day_Sky_Given_In_Lux_Gives_Its_Sun_And_A_Level_Floor_Their_Lux_Through_The_Camera()
    {
        Headless();
        SetCameraExposure(200, 1 / 200f, 16);
        SetSkyDay(new SkyDay { Minutes = 0, Tilt = 0, SunIlluminance = 97050, SkyIlluminance = 20000, SunColor = Color.White });
        SetSkyTime(12);
        BeginDrawing();
        EndDrawing();

        var light = Read(GetApp().World.Resource<DaySky>().Light);
        light.Color.Should().Be(Vector3.One, "the sun takes the color the day says");
        light.Intensity.Should().BeApproximately(97050 / MathF.PI * Through(14.644f), 0.01f, "the noon sun overhead gives its lux");

        var map = GetApp().World.Resource<EnvironmentMap>();
        (map.Intensity * DaySky.FloorLight(12, 0)).Should().BeApproximately(20000 / MathF.PI * Through(14.644f), 1e-3f,
            "the painted sky gives a level floor the sky's lux");
    }
}
