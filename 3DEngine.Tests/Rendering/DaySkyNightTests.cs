using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The night of a day sky with air as the Sky module models it, the moon at its place and phase
/// by the sky's clock, the bright stars at theirs, and the sky drawing them by their luminance.
/// </summary>
[Collection("E3D")]
public sealed class DaySkyNightTests : IDisposable
{
    private static readonly Sky.Atmosphere Earth = Sky.Atmosphere.Earth;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Moon_Stands_On_The_Suns_Path_Behind_It_By_The_Clocks_Elongation_Rising_Later_Each_Day()
    {
        for (int day = 0; day < 30; day += 3)
        {
            var elongation = DaySky.ClockAt(22, day).MoonPhase * 360;
            var (toMoon, phaseAngle) = DaySky.MoonAt(22, 6, day);
            // Where the sun stood as many hours before as the moon lags it, at 15 degrees an hour.
            (toMoon - DaySky.SunToward(22 - elongation / 15, 6)).Length().Should().BeLessThan(1e-5f);
            phaseAngle.Should().BeApproximately(180 - MathF.Min(elongation, 360 - elongation), 1e-3f);
        }
        // A day later the moon stands where it stood some 49 minutes earlier the day before.
        var lag = (DaySky.ClockAt(22, 1).MoonPhase - DaySky.ClockAt(22, 0).MoonPhase) * 360 / 15 * 60;
        lag.Should().BeApproximately(24 * 60 / Sky.SkyClock.SynodicMonth, 0.5f);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Stars_Stand_Where_The_Modules_Clock_Puts_Them()
    {
        foreach (var star in Sky.Stars.Catalog.Take(200))
            foreach (var hour in new[] { 0f, 7.5f, 21f })
            {
                var clock = DaySky.ClockAt(hour, 3);
                var drawn = DaySky.TowardStars(star.RightAscension, star.Declination, DaySky.SiderealTurn(hour, 3));
                (drawn - clock.TowardSky(star.RightAscension, star.Declination)).Length().Should().BeLessThan(1e-4f);
            }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Stars_Grid_Holds_Each_Star_In_Its_Cell_With_Its_Light()
    {
        var grid = DaySky.StarGrid();
        var cells = 6 * DaySky.StarCells * DaySky.StarCells;
        var head = (cells + 1 + 3) / 4 * 4;
        var count = Sky.Stars.Catalog.Count;
        grid[cells].Should().Be((uint)count);
        grid.Length.Should().Be(head + count * 8);
        float Float(int i) => BitConverter.UInt32BitsToSingle(grid[i]);
        var lux = 0.0;
        for (int cell = 0; cell < cells; cell++)
        {
            grid[cell].Should().BeLessThanOrEqualTo(grid[cell + 1]);
            for (var k = grid[cell]; k < grid[cell + 1]; k++)
            {
                var at = head + (int)k * 8;
                DaySky.CellOf(new Vector3(Float(at), Float(at + 1), Float(at + 2))).Should().Be(cell);
                lux += Float(at + 3);
            }
        }
        lux.Should().BeApproximately(Sky.Stars.Catalog.Sum(star => (double)Sky.Stars.Illuminance(star.Magnitude)), 1e-9);
    }

    // A window drawing the day sky with air at an hour of the sky's time looking along a way, held
    // at a camera's night exposure, EV 0, its frame in linear light times what a luminance is
    // multiplied by.
    private static float[] Night(float hours, Vector3 toward, float fovy, int width, int height, out float preExposure)
    {
        var config = Config.Default.WithWindow("night", width, height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSkyDay(new SkyDay { Minutes = 0, Tilt = 0, Atmosphere = Earth });
        SetSkyTime(hours);
        SetCameraExposure(100, 1, 1);
        // A bloom whose threshold nothing reaches, so the scene is decoded to linear light to read.
        SetBloom(1, 1e9f);
        var camera = new Camera3D(Vector3.Zero, toward, Math.Abs(toward.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY, fovy);
        for (int frame = 0; frame < 8; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawSkybox();
            EndMode3D();
            EndDrawing();
        }
        preExposure = GetApp().World.Resource<PhysicalUnits>().PreExposure;
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        return ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
    }

    private static float Luminance(float[] light, int width, int x, int y) =>
        0.2126f * light[(y * width + x) * 4] + 0.7152f * light[(y * width + x) * 4 + 1] + 0.0722f * light[(y * width + x) * 4 + 2];

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void Sirius_Lands_On_The_Frame_With_The_Illuminance_Its_Magnitude_Gives_Through_The_Air()
    {
        // At 21:00 of the first day, Sirius stands some 56 degrees up. Looked at straight on, the
        // light the frame holds about it over the sky behind, times each pixel's solid angle, is its
        // illuminance through the air along its way. It stands on the corner of the four middle
        // pixels, each holding a sixth of it as a Gaussian of 0.8 of a pixel gives it.
        var sirius = Sky.Stars.Catalog.MinBy(star => star.Magnitude);
        var toward = DaySky.TowardStars(sirius.RightAscension, sirius.Declination, DaySky.SiderealTurn(21, 0));
        toward.Y.Should().BeGreaterThan(0.7f, "Sirius is high");
        const int Width = 64, Height = 64;
        const float Fovy = 4;
        var light = Night(21, toward, Fovy, Width, Height, out var preExposure);
        var pixel = MathF.Pow(2 * MathF.Tan(float.DegreesToRadians(Fovy) / 2) / Height, 2);
        var background = Luminance(light, Width, 4, 4);
        double sum = 0;
        for (int y = Height / 2 - 6; y < Height / 2 + 6; y++)
            for (int x = Width / 2 - 6; x < Width / 2 + 6; x++)
                sum += Luminance(light, Width, x, y) - background;
        // Through the air each channel keeps its share at the zenith to the power of the air's
        // depth along the way, one over the sine of the star's height, as the drawn sky takes it.
        var zenith = Earth.Transmittance(0, 1);
        var through = new Vector3(MathF.Pow(zenith.X, 1 / toward.Y), MathF.Pow(zenith.Y, 1 / toward.Y), MathF.Pow(zenith.Z, 1 / toward.Y));
        var expected = Sky.Stars.Illuminance(sirius.Magnitude) * Sky.Colorimetry.Luminance(Sky.Stars.Color(sirius.ColorIndex) * through);
        ((float)(sum * pixel / preExposure)).Should().BeApproximately(expected, expected * 0.01f, "the star's light through the air, held to a hundredth");
        var corner = (Luminance(light, Width, Width / 2, Height / 2) - background) / sum;
        corner.Should().BeApproximately(0.168, 0.025, "the star is a point, its light on the few pixels about it");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Moon_Is_Drawn_Half_A_Degree_Across_At_Its_Sunlit_Luminance()
    {
        // At midnight of the 25th day the moon is a day from full near the meridian. Seen 2 degrees
        // high in 200 pixels, its disc is some 52 pixels across, and its middle reads the module's
        // sunlit luminance at its phase through the air along its way.
        var (toMoon, phaseAngle) = DaySky.MoonAt(0, 0, 24);
        const int Width = 200, Height = 200;
        const float Fovy = 2;
        var light = Night(24 * 24, toMoon, Fovy, Width, Height, out var preExposure);
        var middle = Luminance(light, Width, Width / 2, Height / 2);
        var through = Sky.Colorimetry.Luminance(Earth.Transmittance(0, toMoon.Y));
        var lit = Sky.Moon.LitLuminance(phaseAngle) * through;
        (middle / preExposure).Should().BeApproximately(lit, lit * 0.05f, "the sunlit face's luminance");
        var across = Enumerable.Range(0, Width).Count(x => Luminance(light, Width, x, Height / 2) > middle * 0.5f);
        var expected = Sky.Moon.AngularDiameter / Fovy * Height;
        across.Should().BeInRange((int)(expected * 0.9f), (int)(expected * 1.1f) + 1, "the disc spans the moon's half degree");
    }
}
