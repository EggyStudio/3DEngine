using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The dither the window's frame takes before its eight bits, read on a slow dark ramp, which
/// falls in bands a level apart without it and in a fine grain with it.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class DitherTests : IDisposable
{
    private const int Width = 256;
    private const int Height = 64;
    private const int Black = 16;
    private readonly TestFolder _folder = new("engine-dither-");
    private int _captures;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    // A window with a light in its scene, since a scene with none is drawn as raylib draws one,
    // straight into the window without the pass that brings the frame into it.
    private static void Open()
    {
        var config = Config.Default.WithWindow("dither test", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        CreateDirectionalLight(-Vector3.UnitY, Color.White, 1);
    }

    // The ramp after a black start, from 0.03 to 0.09 as the window's colors read, fifteen levels
    // across the window, captured after the frames given.
    private Image Ramp(int frames)
    {
        var ramp = LoadShaderFromMemory($$"""
            import engine;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                float x = input.position.x;
                float v = x < {{Black}}.0 ? 0.0 : 0.03 + 0.06 * (x - {{Black}}.0) / {{Width - Black}}.0;
                return float4(v, v, v, 1.0);
            }
            """, "dark-ramp.slang");
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var path = Path.Combine(_folder.Path, $"{_captures++}.png");
        for (int frame = 0; frame < frames + 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            BeginShaderMode(ramp);
            DrawCube(new Vector3(0, 0, -50), 20000, 20000, 1, Color.White);
            EndShaderMode();
            EndMode3D();
            if (frame == frames) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        UnloadShader(ramp);
        return LoadImage(path);
    }

    // The share of pixels on the ramp whose neighbor to the right shows another level.
    private static double Changes(Image frame)
    {
        int changes = 0, pairs = 0;
        for (int y = 0; y < Height; y++)
        for (int x = Black; x < Width - 1; x++, pairs++)
            if (GetImageColor(frame, x, y).G != GetImageColor(frame, x + 1, y).G)
                changes++;
        return (double)changes / pairs;
    }

    // How far each four by four block's mean level lies from the mean of its column of blocks down
    // the window, where the ramp's light is the same, as a root mean square: the grain's part the
    // eye takes in at a few pixels rather than one.
    private static double BlockError(Image frame)
    {
        double sum = 0;
        int blocks = 0;
        for (int bx = Black; bx + 4 <= Width; bx += 4)
        {
            var means = new List<double>();
            for (int by = 0; by + 4 <= Height; by += 4)
            {
                double drawn = 0;
                for (int y = by; y < by + 4; y++)
                for (int x = bx; x < bx + 4; x++)
                    drawn += GetImageColor(frame, x, y).G;
                means.Add(drawn / 16);
            }
            var column = means.Average();
            foreach (var mean in means) { sum += (mean - column) * (mean - column); blocks++; }
        }
        return Math.Sqrt(sum / blocks);
    }

    // The most levels any column of the ramp takes down the window, where its light is the same.
    private static int MostLevels(Image frame)
    {
        var most = 0;
        for (int x = Black; x < Width; x++)
            most = Math.Max(most, Enumerable.Range(0, Height).Select(y => GetImageColor(frame, x, y).G).Distinct().Count());
        return most;
    }

    // The mean level of the ramp's pixels.
    private static double Mean(Image frame)
    {
        double sum = 0;
        for (int y = 0; y < Height; y++)
        for (int x = Black; x < Width; x++)
            sum += GetImageColor(frame, x, y).G;
        return sum / ((Width - Black) * Height);
    }

    // The pixels that differ between two captures.
    private static int Differing(Image a, Image b)
    {
        int differing = 0;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            if (GetImageColor(a, x, y) != GetImageColor(b, x, y))
                differing++;
        return differing;
    }

    [NeedsVulkanFact]
    public void A_Slow_Dark_Ramp_Falls_In_A_Fine_Grain_That_Keeps_Its_Light_And_Its_Black()
    {
        Open();
        IsDitherEnabled().Should().BeTrue("the dither is on unless set");
        GetDitherStrength().Should().Be(0.5f, "its strength is a half unless set");
        var on = Ramp(0);
        SetDitherStrength(1);
        var full = Ramp(0);
        SetDither(false);
        var off = Ramp(0);
        Changes(off).Should().BeLessThan(0.1, "fifteen levels across the ramp leave it in bands about sixteen pixels wide");
        foreach (var (grain, strength) in new[] { (on, "a half"), (full, "1") })
        {
            Changes(grain).Should().BeGreaterThan(0.3, $"at a strength of {strength} the dither breaks the bands into a grain");
            Mean(grain).Should().BeApproximately(Mean(off), 0.1, $"at a strength of {strength} the grain adds no light and takes none");
            // Blue noise spreads its grain so no clump of a few pixels stands out, which this bound on
            // blocks of four by four holds.
            BlockError(grain).Should().BeLessThan(0.065, $"at a strength of {strength} the grain is blue noise, its blocks of four by four as near their light as can be");
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Black; x++)
                GetImageColor(grain, x, y).G.Should().Be(0, "black is left as it is");
        }
        MostLevels(on).Should().Be(2, "at a half the grain is even across a level, so a column of one light takes the two levels about it");
        MostLevels(full).Should().Be(3, "at 1 the grain spans two levels, so a column of one light takes three");
    }

    [NeedsVulkanFact]
    public void The_Grain_Holds_Still_And_Moves_From_Frame_To_Frame_Only_With_Temporal_Antialiasing()
    {
        Open();
        var first = Ramp(0);
        var later = Ramp(1);
        Differing(first, later).Should().Be(0, "without temporal antialiasing a still view's frames are alike");

        SetTemporalAntialiasing(true);
        var blended = Ramp(16);
        var next = Ramp(1);
        Differing(blended, next).Should().BeGreaterThan(Width * Height / 10, "where the frames are blended the grain moves each frame, which the blend reads away");
    }
}
