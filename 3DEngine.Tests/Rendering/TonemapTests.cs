using System.Numerics;
using FluentAssertions;
using Vortice.Vulkan;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Draws SHARED.md's ramp through each of Bevy's tonemappers and holds the picture to BevyCSharp's
/// picture of the ramp and to a model of each worked out here from Bevy's own curves and tables,
/// within two levels of 255 for a curve and four for a table.
/// </summary>
/// <remarks>
/// The ramp is 1024 by 8 pixels, column x holding the linear value 2 to the power of
/// (20 x / 1023 - 12), from a 4096th to 256, and its rows gray, red, green and blue alone, cyan,
/// magenta and yellow, and (v, v/2, v/4). A shader of its own writes it with no light, exposure,
/// grading or dither in the way, and the window's eight bits hold it in sRGB. BevyCSharp's pictures
/// under <c>References/tonemapping</c> are copied as they are from its
/// <c>BevyCSharp.Tests/references/tonemapping</c> at its commit 64ec311, drawn there by Bevy 0.19.1
/// with its tables on an RTX 4070, each named as Bevy names its tonemapper.
/// </remarks>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class TonemapTests : IDisposable
{
    private const int Width = 1024, Height = 8;
    private readonly TestFolder _folder = new("engine-tonemap-");
    private readonly int _validationErrorsBefore = GraphicsDevice.ValidationErrors.Count;

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    // Bevy's eight, the curves worked out within two levels and the tables within four.
    public static TheoryData<Tonemap, int> Bevys => new()
    {
        { Tonemap.None, 2 },
        { Tonemap.Reinhard, 2 },
        { Tonemap.ReinhardLuminance, 2 },
        { Tonemap.AcesFitted, 2 },
        { Tonemap.AgX, 4 },
        { Tonemap.SomewhatBoring, 2 },
        { Tonemap.TonyMcMapface, 4 },
        { Tonemap.BlenderFilmic, 4 },
    };

    [NeedsVulkanTheory]
    [MemberData(nameof(Bevys))]
    public void The_Ramp_Through_Each_Of_Bevys_Tonemappers_Is_Bevys(Tonemap curve, int levels)
    {
        var config = Config.Default.WithWindow("tonemap test", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        var ramp = LoadShaderFromMemory("""
            import engine;
            import color;

            [shader("fragment")]
            float4 fragmentMain(VertexOutput input) : SV_Target
            {
                float v = exp2(20.0 * floor(input.position.x) / 1023.0 - 12.0);
                float3 rows[8] = { float3(1, 1, 1), float3(1, 0, 0), float3(0, 1, 0), float3(0, 0, 1),
                    float3(0, 1, 1), float3(1, 0, 1), float3(1, 1, 0), float3(1, 0.5, 0.25) };
                return float4(linearToSrgbPastWhite(v * rows[int(input.position.y)]), 1.0);
            }
            """, "ramp.slang");
        SetTonemap(curve);
        // A face far wider than the view, a view 128 times as wide as it is high and so some 6,000
        // units across at the face, so the shader covers every pixel, drawn inside BeginMode3D so
        // it is the window's scene and goes through the tonemap.
        var camera = new Camera3D(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY, 45);
        var path = Path.Combine(_folder.Path, curve + ".png");
        for (int frame = 0; frame < 10 && !File.Exists(path); frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            BeginShaderMode(ramp);
            DrawCube(new Vector3(0, 0, -50), 20000, 1000, 1, Color.White);
            EndShaderMode();
            EndMode3D();
            if (frame == 0) TakeScreenshot(path);
            EndDrawing();
        }
        File.Exists(path).Should().BeTrue("the capture is written once its frame has finished on the GPU");
        GraphicsDevice.ValidationErrors.Skip(_validationErrorsBefore).Should().BeEmpty("the validation layer, where it runs, reports nothing wrong with the frames drawn");
        var frameImage = LoadImage(path);

        var model = new BevyModel(curve);
        var worst = (Apart: 0, X: 0, Y: 0, Got: Color.Black, Expected: Color.Black);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var expected = model.Shown(Ramp(x, y));
                var got = GetImageColor(frameImage, x, y);
                var apart = Math.Max(Math.Max(Math.Abs(got.R - expected.R), Math.Abs(got.G - expected.G)), Math.Abs(got.B - expected.B));
                if (apart > worst.Apart) worst = (apart, x, y, got, expected);
            }
        worst.Apart.Should().BeLessThanOrEqualTo(levels,
            $"{curve} draws the ramp as Bevy's curve and table give it, at worst {worst.Got} where {worst.Expected} at {worst.X}, {worst.Y}");

        // BevyCSharp's own picture of the ramp, by the name Bevy gives the tonemapper.
        var bevys = Path.Combine(Api.CheatsheetTests.RepoRoot(), "3DEngine.Tests", "Rendering", "References", "tonemapping",
            (curve == Tonemap.SomewhatBoring ? "SomewhatBoringDisplayTransform" : curve.ToString()) + ".png");
        var reference = LoadImage(bevys);
        var apartFromBevy = (Apart: 0, X: 0, Y: 0);
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                Color a = GetImageColor(frameImage, x, y), b = GetImageColor(reference, x, y);
                var apart = Math.Max(Math.Max(Math.Abs(a.R - b.R), Math.Abs(a.G - b.G)), Math.Abs(a.B - b.B));
                if (apart > apartFromBevy.Apart) apartFromBevy = (apart, x, y);
            }
        apartFromBevy.Apart.Should().BeLessThanOrEqualTo(levels,
            $"{curve} draws the ramp as BevyCSharp draws it, at worst at {apartFromBevy.X}, {apartFromBevy.Y}");
        UnloadShader(ramp);
    }

    [Fact]
    public void Each_Of_Bevys_Tables_Is_A_Cube_In_Its_Format()
    {
        TonemapTables.Load(Tonemap.AgX).Should().Match<TonemapTables.Table>(t => t.Size == 32 && t.Format == VkFormat.R16G16B16A16Sfloat);
        TonemapTables.Load(Tonemap.TonyMcMapface).Should().Match<TonemapTables.Table>(t => t.Size == 48 && t.Format == VkFormat.E5B9G9R9UfloatPack32);
        TonemapTables.Load(Tonemap.BlenderFilmic).Should().Match<TonemapTables.Table>(t => t.Size == 64 && t.Format == VkFormat.R16G16B16A16Sfloat);
        TonemapTables.FileOf(Tonemap.AcesFitted).Should().BeNull("a curve worked out without a table reads none");
    }

    // The ramp's linear light at a pixel.
    private static Vector3 Ramp(int x, int y)
    {
        var v = MathF.Pow(2, 20f * x / 1023 - 12);
        Vector3[] rows = [new(1, 1, 1), new(1, 0, 0), new(0, 1, 0), new(0, 0, 1), new(0, 1, 1), new(1, 0, 1), new(1, 1, 0), new(1, 0.5f, 0.25f)];
        return v * rows[y];
    }

    /// <summary>Bevy's curves as <c>tonemapping_shared.wgsl</c> works them out, on the CPU, with its tables sampled as a linear filter samples them.</summary>
    private sealed class BevyModel
    {
        private readonly Tonemap _curve;
        private readonly int _size;
        private readonly Vector3[] _table = [];

        public BevyModel(Tonemap curve)
        {
            _curve = curve;
            if (TonemapTables.FileOf(curve) is null) return;
            var table = TonemapTables.Load(curve);
            _size = (int)table.Size;
            _table = new Vector3[_size * _size * _size];
            for (int i = 0; i < _table.Length; i++)
                _table[i] = table.Format == VkFormat.R16G16B16A16Sfloat
                    ? new Vector3((float)BitConverter.ToHalf(table.Texels, i * 8), (float)BitConverter.ToHalf(table.Texels, i * 8 + 2), (float)BitConverter.ToHalf(table.Texels, i * 8 + 4))
                    : Rgb9E5(BitConverter.ToUInt32(table.Texels, i * 4));
        }

        // The color the window shows for linear light, through the curve and sRGB's encoding.
        public Color Shown(Vector3 light)
        {
            var c = Vector3.Clamp(Curve(Vector3.Max(light, Vector3.Zero)), Vector3.Zero, Vector3.One);
            static byte Encode(float v) => (byte)Math.Round(255 * (v <= 0.0031308f ? v * 12.92f : 1.055f * MathF.Pow(v, 1 / 2.4f) - 0.055f));
            return new Color(Encode(c.X), Encode(c.Y), Encode(c.Z));
        }

        private Vector3 Curve(Vector3 c) => _curve switch
        {
            Tonemap.None => c,
            Tonemap.Reinhard => c / (Vector3.One + c),
            Tonemap.ReinhardLuminance => c / (1 + Luminance(c)),
            Tonemap.AcesFitted => AcesFitted(c),
            Tonemap.SomewhatBoring => SomewhatBoring(c),
            Tonemap.AgX => LookUp(Vector3.Clamp(Log2(new Vector3(
                Vector3.Dot(c, new(0.84247906f, 0.0784336f, 0.07922375f)), Vector3.Dot(c, new(0.04232824f, 0.87846864f, 0.07916613f)),
                Vector3.Dot(c, new(0.04237565f, 0.0784336f, 0.87914297f))), -10, 6.5f), Vector3.Zero, Vector3.One)),
            Tonemap.TonyMcMapface => Sample(Vector3.Clamp(c / (c + Vector3.One) * (47f / 48) + new Vector3(0.5f / 48), Vector3.Zero, Vector3.One)),
            Tonemap.BlenderFilmic => LookUp(Vector3.Clamp(Log2(c, -11, 12), Vector3.Zero, Vector3.One)),
            _ => throw new ArgumentOutOfRangeException(nameof(_curve)),
        };

        private static float Luminance(Vector3 c) => Vector3.Dot(c, new(0.2126f, 0.7152f, 0.0722f));

        private static Vector3 AcesFitted(Vector3 c)
        {
            var v = new Vector3(Vector3.Dot(c, new(0.59719f, 0.35458f, 0.04823f)), Vector3.Dot(c, new(0.07600f, 0.90834f, 0.01566f)),
                Vector3.Dot(c, new(0.02840f, 0.13383f, 0.83777f)));
            v = (v * (v + new Vector3(0.0245786f)) - new Vector3(0.000090537f)) / (v * (0.983729f * v + new Vector3(0.4329510f)) + new Vector3(0.238081f));
            return new Vector3(Vector3.Dot(v, new(1.60475f, -0.53108f, -0.07367f)), Vector3.Dot(v, new(-0.10208f, 1.10813f, -0.00605f)),
                Vector3.Dot(v, new(-0.00327f, -0.07276f, 1.07602f)));
        }

        private static float Boring(float v) => 1 - MathF.Exp(-v);

        private static Vector3 SomewhatBoring(Vector3 c)
        {
            var (y, cb, cr) = (Luminance(c), Vector3.Dot(c, new(-0.1146f, -0.3854f, 0.5f)), Vector3.Dot(c, new(0.5f, -0.4542f, -0.0458f)));
            var bt = Boring(MathF.Sqrt(cb * cb + cr * cr) * 2.4f);
            var desaturated = MathF.Max((bt - 0.7f) * 0.8f, 0);
            desaturated *= desaturated;
            var toward = Vector3.Lerp(c, new Vector3(y), desaturated);
            var scaled = c * MathF.Max(0, Boring(y) / MathF.Max(1e-5f, Luminance(c)));
            var alone = new Vector3(Boring(toward.X), Boring(toward.Y), Boring(toward.Z));
            return Vector3.Lerp(scaled, alone, bt * bt) * 0.97f;
        }

        private static Vector3 Log2(Vector3 c, float minimumEv, float maximumEv)
        {
            float One(float v)
            {
                v = MathF.Max(v, 0);
                if (v < 0.00003051757f) v += 0.00001525878f;
                return (Math.Clamp(MathF.Log2(v / 0.18f), minimumEv, maximumEv) - minimumEv) / (maximumEv - minimumEv);
            }
            return new Vector3(One(c.X), One(c.Y), One(c.Z));
        }

        private Vector3 LookUp(Vector3 at) => Sample(at * ((_size - 1f) / _size) + new Vector3(0.5f / _size));

        // The table at a point of the unit cube through a linear filter held at the edges.
        private Vector3 Sample(Vector3 at)
        {
            static (int Low, int High, float Share) Axis(float u, int size)
            {
                var x = u * size - 0.5f;
                var low = (int)MathF.Floor(x);
                return (Math.Clamp(low, 0, size - 1), Math.Clamp(low + 1, 0, size - 1), x - low);
            }
            var (x0, x1, fx) = Axis(at.X, _size);
            var (y0, y1, fy) = Axis(at.Y, _size);
            var (z0, z1, fz) = Axis(at.Z, _size);
            Vector3 T(int x, int y, int z) => _table[x + _size * (y + _size * z)];
            var near = Vector3.Lerp(Vector3.Lerp(T(x0, y0, z0), T(x1, y0, z0), fx), Vector3.Lerp(T(x0, y1, z0), T(x1, y1, z0), fx), fy);
            var far = Vector3.Lerp(Vector3.Lerp(T(x0, y0, z1), T(x1, y0, z1), fx), Vector3.Lerp(T(x0, y1, z1), T(x1, y1, z1), fx), fy);
            return Vector3.Lerp(near, far, fz);
        }

        // RGB9E5's three nine-bit mantissas over a shared five-bit exponent biased by 15.
        private static Vector3 Rgb9E5(uint packed)
        {
            var scale = MathF.Pow(2, (int)(packed >> 27) - 15 - 9);
            return new Vector3(packed & 0x1FF, (packed >> 9) & 0x1FF, (packed >> 18) & 0x1FF) * scale;
        }
    }
}
