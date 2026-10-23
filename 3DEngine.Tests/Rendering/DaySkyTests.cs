using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The day sky driven by the hour. The sun's way and light at four hours and the moon's light at
/// midnight, the hour moving on by the day's length or standing, the light turned being the sky's
/// own or the one the program names, the map painted again as the hour moves and no more often than
/// its budget, the horizon following the hour, and on the device the sky drawn in the hour's colors.
/// </summary>
[Collection("E3D")]
public sealed class DaySkyTests : IDisposable
{
    private const float Tilt = 16.7f;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private static void Headless() =>
        UseApp(new App(Config.Default with { Headless = true, HeadlessFps = 1000, FrameSeconds = 1.0 / 60 }).AddPlugin(new DefaultPlugins()));

    private static void Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            BeginDrawing();
            EndDrawing();
        }
    }

    private static EcsWorld Ecs => GetApp().World.Resource<EcsWorld>();

    private static (Light Light, Vector3 Direction) Read(LightHandle handle)
    {
        Ecs.TryResolve(handle.Entity, out var entity).Should().BeTrue("the light the sky turns is in the world");
        return (Ecs.GetReadOnly<Light>(entity), Vector3.Transform(-Vector3.UnitZ, Ecs.GetReadOnly<Transform>(entity).Rotation));
    }

    private static void Near(Vector3 actual, Vector3 expected, string because) =>
        Vector3.Distance(actual, expected).Should().BeLessThan(1e-3f, $"{because}, {actual} against {expected}");

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Sun_Rises_East_Passes_South_Of_The_Zenith_And_Sets_West_Warm_White_High_And_Orange_Low()
    {
        var (lean, rise) = (MathF.Cos(float.DegreesToRadians(Tilt)), MathF.Sin(float.DegreesToRadians(Tilt)));
        Near(DaySky.SunToward(6, Tilt), new Vector3(lean, 0, rise), "at 6 the sun is on the horizon in the east, leaning south");
        Near(DaySky.SunToward(9, Tilt), new Vector3(lean * MathF.Sqrt(0.5f), lean * MathF.Sqrt(0.5f), rise), "at 9 it is halfway up");
        Near(DaySky.SunToward(12, Tilt), new Vector3(0, lean, rise), "at noon it stands the day's lean south of the zenith");
        Near(DaySky.SunToward(18, Tilt), new Vector3(-lean, 0, rise), "at 18 it sets in the west");

        // The light travels from the sun, its color the light's hue and its strength the hue's
        // largest part, worked out by hand from the game's numbers.
        var noon = DaySky.LightAt(12, Tilt);
        Near(noon.Direction, -DaySky.SunToward(12, Tilt), "the light shines down from the sun");
        noon.Color.Should().Be(new Color(255, 228, 197), "a warm white at noon");
        noon.Intensity.Should().BeApproximately(2.515f, 0.005f);

        var morning = DaySky.LightAt(7, Tilt);
        morning.Color.Should().Be(new Color(255, 205, 161), "warmer an hour after sunrise");
        var dawn = DaySky.LightAt(6.1667f, Tilt);
        dawn.Color.Should().Be(new Color(255, 188, 134), "an orange ten minutes after sunrise");
        // The low sun's orange is strong, so it fades in over its first few degrees, from a third of
        // its strength on the horizon.
        DaySky.LightAt(6, Tilt).Intensity.Should().BeApproximately(0.787f, 0.005f, "the sun on the horizon is fading in");
        dawn.Intensity.Should().BeApproximately(2.097f, 0.005f, "and near its strength two and a half degrees up");

        var midnight = DaySky.LightAt(0, Tilt);
        Near(midnight.Direction, DaySky.SunToward(0, Tilt), "by night the light shines down from the moon, opposite the sun");
        midnight.Direction.Y.Should().BeLessThan(0);
        midnight.Color.Should().Be(new Color(225, 218, 255), "the moonlight a cool blue");
        midnight.Intensity.Should().BeApproximately(0.1397f, 0.001f, "a quarter of the sun's luminance at noon");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Sky_Is_Blue_By_Day_Orange_At_Dusk_And_Dark_Violet_By_Night_Its_Map_A_Gradient_From_The_Zenith()
    {
        var (zenith, horizon) = DaySky.ColorsAt(12, Tilt);
        (zenith, horizon).Should().Be((new Vector3(78, 134, 222), new Vector3(178, 208, 240)), "the day's colors as they are, untinted");

        var dusk = DaySky.ColorsAt(18, Tilt).Horizon;
        (dusk.X > dusk.Y && dusk.Y > dusk.Z && dusk.X > 200).Should().BeTrue($"the horizon is orange as the sun crosses it, {dusk}");

        var night = DaySky.ColorsAt(0, Tilt);
        (night.Horizon.Z > night.Horizon.X && night.Horizon.Z > night.Horizon.Y && night.Horizon.Z < 60).Should().BeTrue($"the night's horizon is a dark blue, {night.Horizon}");
        night.Zenith.Z.Should().BeLessThan(night.Horizon.Z, "darker overhead");

        var pixels = new DaySky().Paint(12, Tilt);
        Color Row(int row) => new(pixels[row * DaySky.Width * 4], pixels[row * DaySky.Width * 4 + 1], pixels[row * DaySky.Width * 4 + 2]);
        Row(0).Should().Be(new Color(78, 134, 222), "the top row is the zenith");
        Row(DaySky.Height / 2 - 1).Should().Be(new Color(173, 204, 239), "the row above the horizon nearly its color");
        Row(DaySky.Height / 2).Should().Be(new Color(178, 208, 240), "the row below it the horizon's color");
        Row(DaySky.Height - 1).Should().Be(new Color(89, 104, 120), "the bottom row half of it, lighting the world from below");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void The_Hour_Moves_On_By_The_Days_Length_Turning_The_Skys_Own_Light_And_Stands_With_A_Day_Of_None()
    {
        Headless();
        GetSkyTime().Should().Be(0, "no day sky runs until one is set");
        var lights = Ecs.Query<Light>().Count();

        SetSkyTime(9);
        SetSkyDay(new SkyDay { Minutes = 1 });
        Frames(1);
        Ecs.Query<Light>().Count().Should().Be(lights + 1, "the sky makes one light of its own");
        var light = GetApp().World.Resource<DaySky>().Light;
        Near(Read(light).Direction, -DaySky.SunToward(GetSkyTime(), Tilt), "the light shines from the sun at the hour");

        // A day of a minute moves 24 hours in 60 seconds, so 60 frames of a sixtieth move 0.4.
        var start = GetSkyTime();
        Frames(60);
        GetSkyTime().Should().BeApproximately(start + 0.4f, 1e-3f);
        Near(Read(light).Direction, -DaySky.SunToward(GetSkyTime(), Tilt), "the light follows the hour");
        Read(light).Light.Intensity.Should().BeApproximately(DaySky.LightAt(GetSkyTime() % 24, Tilt).Intensity, 1e-4f);

        SetSkyDay(new SkyDay { Minutes = 0 });
        SetSkyTime(30);
        Frames(30);
        GetSkyTime().Should().Be(30, "a day of no minutes stands at the hour set");
        GetApp().World.Resource<DaySky>().Hour.Should().Be(6, "30 hours from the first midnight is 6 on the second day");
        GetApp().World.Resource<DaySky>().Days.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void A_Light_The_Program_Names_Is_Turned_In_Place_Of_The_Skys_Own_And_Kept_When_A_Map_Ends_The_Sky()
    {
        Headless();
        SetSkyTime(12);
        var sun = CreateDirectionalLight(-Vector3.UnitY, Color.White, 1, castsShadows: true);
        var lights = Ecs.Query<Light>().Count();

        SetSkyDay(new SkyDay { Minutes = 0, Light = sun });
        Frames(1);
        Ecs.Query<Light>().Count().Should().Be(lights - 1, "the light the sky made is unloaded, so the world holds one sun");
        Near(Read(sun).Direction, -DaySky.SunToward(12, Tilt), "the program's light is turned to the hour");
        GetSkyDay().Light.Should().Be(sun);

        SetEnvironmentMap(GenImageColor(8, 4, Color.Gray));
        Frames(1);
        GetApp().World.TryGetResource<DaySky>(out _).Should().BeFalse("a map the program sets ends the day sky");
        GetSkyHorizon().Should().Be((Color.Blank, 0f), "and the horizon it set");
        Read(sun).Light.Intensity.Should().BeGreaterThan(0, "the program's light stays where the sky left it");
        GetSkyTime().Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void The_Map_Is_Painted_Again_As_The_Hour_Moves_No_More_Often_Than_Its_Budget_And_The_Horizon_Follows()
    {
        Headless();
        SetSkyTime(12);
        Frames(1);
        var world = GetApp().World;
        var map = world.Resource<EnvironmentMap>();
        GetSkyHorizon().Should().Be((new Color(178, 208, 240), DaySky.HorizonBlendDegrees), "the horizon is the hour's color, for a program's own fog to read");

        // A day of twenty minutes moves 0.05 hours in two and a half seconds, so a second leaves the map.
        Frames(60);
        world.Resource<EnvironmentMap>().Should().BeSameAs(map, "the hour has not moved far enough to paint again");

        // A day of a minute moves 0.05 hours in 7.5 frames, and the budget holds it to one painting a
        // quarter of a second, 15 frames, so 60 frames paint four times.
        SetSkyDay(new SkyDay { Minutes = 1 });
        Frames(1);
        var paintings = 0;
        for (int frame = 0; frame < 60; frame++)
        {
            map = world.Resource<EnvironmentMap>();
            Frames(1);
            if (!ReferenceEquals(map, world.Resource<EnvironmentMap>())) paintings++;
        }
        paintings.Should().BeInRange(3, 4);

        SetSkyTime(0);
        Frames(1);
        var night = GetSkyHorizon().Color;
        (night.B > night.R && night.B < 60).Should().BeTrue($"a time set paints at once, the night's dark blue, {night}");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Sky_Drawn_At_Noon_Is_The_Zenith_Overhead_And_The_Horizon_Level_And_Dark_At_Midnight()
    {
        var config = Config.Default.WithWindow("day sky test", 64, 48) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSkyDay(new SkyDay { Minutes = 0 });
        var folder = new TestFolder("engine-day-sky-");
        try
        {
            Image Look(float hour, Vector3 toward, string name)
            {
                SetSkyTime(hour);
                var path = Path.Combine(folder.Path, name + ".png");
                for (int frame = 0; frame < 14 && !File.Exists(path); frame++)
                {
                    BeginDrawing();
                    ClearBackground(Color.Black);
                    BeginMode3D(new Camera3D(Vector3.Zero, toward, toward.Y == 0 ? Vector3.UnitY : Vector3.UnitZ, 60));
                    DrawSkybox();
                    EndMode3D();
                    if (frame == 4) TakeScreenshot(path);
                    EndDrawing();
                }
                File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
                return LoadImage(path);
            }
            static int Apart(Color a, Color b) => Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

            var up = GetImageColor(Look(12, Vector3.UnitY, "noon-up"), 32, 24);
            Apart(up, new Color(78, 134, 222)).Should().BeLessThanOrEqualTo(3, $"looking up at noon shows the zenith's blue, {up}");
            var level = Look(12, -Vector3.UnitZ, "noon-level");
            Apart(GetImageColor(level, 32, 25), new Color(178, 208, 240)).Should().BeLessThanOrEqualTo(3, $"looking level shows the horizon's color, {GetImageColor(level, 32, 25)}");
            var night = GetImageColor(Look(0, Vector3.UnitY, "midnight-up"), 32, 24);
            (night.B < 40 && night.B > night.R).Should().BeTrue($"at midnight the zenith is a dark blue, {night}");
        }
        finally
        {
            folder.Dispose();
        }
    }
}
