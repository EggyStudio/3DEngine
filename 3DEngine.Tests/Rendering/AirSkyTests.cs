using System.Numerics;
using FluentAssertions;
using Xunit.Abstractions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The air's sky drawn on the GPU (<c>atmosphere_sky.slang</c>), read back from the cube the drawn
/// sky samples and from the environment's irradiance, against the Sky module's own luminance and
/// illuminance on the CPU, so the sky drawn, the light from all around and the field's sky come
/// from the model the sun's light does, and the sun's disc drawn over it.
/// </summary>
[Collection("E3D")]
public sealed class AirSkyTests(ITestOutputHelper output) : IDisposable
{
    private static readonly Sky.Atmosphere Earth = Sky.Atmosphere.Earth;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // The hour on a day of no lean at which the sun stands at an elevation, 15 degrees an hour from 6.
    private static float HourAt(float elevation) => 6 + elevation / 15;

    private static Engine.Renderer Open()
    {
        var config = Config.Default.WithWindow("air sky", 64, 48) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        return GetApp().World.Resource<Engine.Renderer>();
    }

    private static void Frames(int count, Vector3 toward)
    {
        for (int frame = 0; frame < count; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(new Camera3D(Vector3.Zero, toward, MathF.Abs(toward.Y) > 0.99f ? Vector3.UnitZ : Vector3.UnitY, 60));
            DrawSkybox();
            EndMode3D();
            EndDrawing();
        }
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Sky_Drawn_On_The_GPU_Is_The_Modules_Luminance_Along_Each_Way()
    {
        var renderer = Open();
        SetSkyDay(new SkyDay { Minutes = 0, Tilt = 0, Atmosphere = Earth });
        SetSkyTime(HourAt(30));
        Frames(4, Vector3.UnitY);

        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var cube = renderer.RenderWorld.Get<ModelRenderer>().SkyCube!;
        var faces = CubeFilter.Of(device).ReadCubeFaces(cube);
        var size = (int)cube.Size;
        var preExposure = GetApp().World.Resource<PhysicalUnits>().PreExposure;
        var sky = new Sky.SkyLight(Earth);
        var toSun = DaySky.SunToward(HourAt(30), 0);

        // Every eighth texel of each face along its own way, as probecube.slang's cubeDirection
        // gives it, away from the horizon's edge and the sun's halo, which a texel's spread over
        // the image blurs where the light changes within it.
        var (worst, count) = (0f, 0);
        for (int face = 0; face < 6; face++)
            for (int y = 4; y < size; y += 8)
                for (int x = 4; x < size; x += 8)
                {
                    var way = CubeDirection(face, (x + 0.5f) / size * 2 - 1, (y + 0.5f) / size * 2 - 1);
                    if (MathF.Abs(way.Y) < MathF.Sin(float.DegreesToRadians(3)) || Vector3.Dot(way, toSun) > MathF.Cos(float.DegreesToRadians(10))) continue;
                    var at = ((face * size + y) * size + x) * 4;
                    var drawn = new Vector3((float)faces[at], (float)faces[at + 1], (float)faces[at + 2]);
                    var expected = sky.Luminance(way, toSun) * preExposure;
                    for (int c = 0; c < 3; c++) worst = MathF.Max(worst, MathF.Abs(drawn[c] / expected[c] - 1));
                    count++;
                }
        output.WriteLine($"{count} texels, the worst channel {worst * 100:0.00}% from the module's luminance");
        count.Should().BeGreaterThan(500);
        // The bound leaves room for the tables' and the image's half floats.
        worst.Should().BeLessThan(0.01f, "the GPU draws the module's sky");
    }

    // The way a cube texel looks along for face coordinates from -1 to 1, as probecube.slang has it.
    private static Vector3 CubeDirection(int face, float u, float v) => Vector3.Normalize(face switch
    {
        0 => new Vector3(1, -v, -u),
        1 => new Vector3(-1, -v, u),
        2 => new Vector3(u, 1, v),
        3 => new Vector3(u, -1, -v),
        4 => new Vector3(u, -v, 1),
        _ => new Vector3(-u, -v, -1),
    });

    // The nine harmonics in a direction, in the model pass's order (probecube.slang).
    private static float[] Harmonics(Vector3 d) =>
    [
        0.282095f, 0.488603f * d.Y, 0.488603f * d.Z, 0.488603f * d.X, 1.092548f * d.X * d.Y, 1.092548f * d.Y * d.Z,
        0.315392f * (3 * d.Z * d.Z - 1), 1.092548f * d.X * d.Z, 0.546274f * (d.X * d.X - d.Y * d.Y),
    ];

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Light_From_All_Around_Lights_A_Level_Floor_As_The_Modules_Sky_Does()
    {
        var renderer = Open();
        SetSkyDay(new SkyDay { Minutes = 0, Tilt = 0, Atmosphere = Earth });
        SetSkyTime(HourAt(54));
        Frames(4, Vector3.UnitY);

        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var irradiance = CubeFilter.Of(device).ReadCubeIrradiance(renderer.RenderWorld.Get<ModelRenderer>().Environment!);
        var preExposure = GetApp().World.Resource<PhysicalUnits>().PreExposure;
        var sky = new Sky.SkyLight(Earth);
        var toSun = DaySky.SunToward(HourAt(54), 0);

        // The module's sky over the whole sphere projected onto the same nine harmonics, each band
        // times its share of a cosine lobe, 1, 2/3 and 1/4, so the floor's light is read as the
        // model pass reads it, nine numbers taking in a little of the ground below the horizon,
        // which on its own puts the floor over the module's own integral.
        var projected = new Vector3[9];
        const int Rows = 64, Columns = 128;
        for (int row = 0; row < Rows; row++)
        {
            var theta = (row + 0.5f) * MathF.PI / Rows;
            for (int column = 0; column < Columns; column++)
            {
                var phi = (column + 0.5f) * 2 * MathF.PI / Columns;
                var way = new Vector3(MathF.Sin(theta) * MathF.Cos(phi), MathF.Cos(theta), MathF.Sin(theta) * MathF.Sin(phi));
                var weight = MathF.Sin(theta) * (MathF.PI / Rows) * (2 * MathF.PI / Columns);
                var light = sky.Luminance(way, toSun) * preExposure * weight;
                var y = Harmonics(way);
                for (int i = 0; i < 9; i++) projected[i] += light * y[i];
            }
        }
        float[] lobe = [1, 2 / 3f, 2 / 3f, 2 / 3f, 1 / 4f, 1 / 4f, 1 / 4f, 1 / 4f, 1 / 4f];
        var up = Harmonics(Vector3.UnitY);
        var (level, expected) = (Vector3.Zero, Vector3.Zero);
        for (int i = 0; i < 9; i++)
        {
            level += irradiance[i] * up[i];
            expected += projected[i] * lobe[i] * up[i];
        }
        output.WriteLine($"level floor: drawn {level * MathF.PI / preExposure}, the module's in nine harmonics {expected * MathF.PI / preExposure}, "
            + $"its own integral {sky.Illuminance(toSun)} lux");
        for (int c = 0; c < 3; c++)
            level[c].Should().BeApproximately(expected[c], expected[c] * 0.01f, $"channel {c}");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Suns_Disc_Is_Drawn_Over_The_Air_By_Its_Luminance()
    {
        Open();
        SetSkyDay(new SkyDay { Minutes = 0, Tilt = 0, Atmosphere = Earth });
        SetSkyTime(HourAt(30));
        var toSun = DaySky.SunToward(HourAt(30), 0);
        var folder = new TestFolder("engine-air-sky-");
        try
        {
            Color Middle(Vector3 toward, string name)
            {
                var path = Path.Combine(folder.Path, name + ".png");
                for (int frame = 0; frame < 14 && !File.Exists(path); frame++)
                {
                    BeginDrawing();
                    ClearBackground(Color.Black);
                    BeginMode3D(new Camera3D(Vector3.Zero, toward, Vector3.UnitY, 10));
                    DrawSkybox();
                    EndMode3D();
                    if (frame == 4) TakeScreenshot(path);
                    EndDrawing();
                }
                File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
                return GetImageColor(LoadImage(path), 32, 24);
            }
            var sun = Middle(toSun, "sun");
            var beside = Middle(Vector3.Transform(toSun, Matrix4x4.CreateRotationY(float.DegreesToRadians(4))), "beside");
            output.WriteLine($"the disc's middle {sun}, four degrees beside it {beside}");
            Math.Min(sun.R, Math.Min(sun.G, sun.B)).Should().BeGreaterThan(240, "the disc's luminance takes the frame past white");
            Math.Max(beside.R, Math.Max(beside.G, beside.B)).Should().BeLessThan(200, "four degrees from the disc is the sky");
        }
        finally
        {
            folder.Dispose();
        }
    }
}
