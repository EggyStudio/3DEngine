using System.Numerics;
using System.Runtime.InteropServices;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The small surfaces that give off light, which the bounce carries as lights of their own
/// (<see cref="GlowLights"/>): which are carried, how they are written for the shaders, and a
/// glowing block on a floor lighting the floor around it as a path-traced reference does, with no
/// lobes and from far off as from near.
/// </summary>
[Collection("Engine3D")]
public sealed class GlowLightsTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-glow-");

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void An_Emitter_No_Longer_Than_Twice_The_First_Cascades_Spacing_Is_Carried()
    {
        var unit = new SceneFieldPlan.Box(new Vector3(-0.5f), new Vector3(0.5f));
        var glow = new Vector3(4, 2.3f, 0.75f);
        GlowLights.Carries(glow, unit, Matrix4x4.Identity, 0.25f).Should().BeTrue("a block is shorter than the four blocks of twice the spacing of eight cells of a quarter");
        GlowLights.Carries(glow, unit, Matrix4x4.CreateScale(4, 0.1f, 0.1f), 0.25f).Should().BeTrue("a strip four blocks long is no longer");
        GlowLights.Carries(glow, unit, Matrix4x4.CreateScale(4.5f, 0.1f, 0.1f), 0.25f).Should().BeFalse("a strip longer is left to the probes' rays");
        GlowLights.Carries(glow, unit, Matrix4x4.Identity, 0.05f).Should().BeFalse("at a finer cell the probes stand close enough to see the block");
        GlowLights.Carries(Vector3.Zero, unit, Matrix4x4.Identity, 0.25f).Should().BeFalse("a surface that gives off no light is no light");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void The_Lights_Written_Are_The_Nearest_The_Eye_With_Their_Boxes_And_Reach()
    {
        // Seventy blocks along x, every one carried, the eye past the last, so the 64 nearest it
        // are written and the six nearest the origin are not.
        var vertices = new ModelVertex[] { new() { Position = new Vector3(-0.5f) }, new() { Position = new Vector3(0.5f) } };
        var drawn = Enumerable.Range(0, 70).Select(i => (new SceneFieldPlan.Instance(1, vertices, Matrix4x4.CreateTranslation(i * 2, 0.5f, 0),
            false, Vector3.One, new Vector3(4, 2, 1)), false)).ToList();
        var bytes = new byte[GlowLights.Size];
        var found = GlowLights.Write(bytes, drawn, new Vector3(200, 0, 0), 0.25f, SceneFieldPlan.Box.Of, _ => 1);

        found.Should().Be(70);
        var floats = MemoryMarshal.Cast<byte, float>(bytes.AsSpan()).ToArray();
        floats[0].Should().Be(GlowLights.Most);
        var middles = Enumerable.Range(0, GlowLights.Most).Select(n => floats[4 + n * GlowLights.Bytes / 4]).ToList();
        middles.Should().Equal(Enumerable.Range(0, GlowLights.Most).Select(n => (float)(69 - n) * 2), "the nearest the eye first");
        var first = floats[4..(4 + GlowLights.Bytes / 4)];
        first[1].Should().BeApproximately(0.5f, 1e-6f);
        first[3].Should().BeApproximately(MathF.Sqrt(4 * 6 / (4 * 0.02f)), 1e-3f, "a block of six faces giving off four at its brightest reaches where it lights a surface by a fiftieth");
        first[4..8].Should().Equal(0.5f, 0, 0, 0.5f);
        first[16..20].Should().Equal(4, 2, 1, 1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Touching_Emitters_That_Give_Off_The_Same_Light_Are_One_Light()
    {
        // Four blocks stacked two by two, a fifth beside them giving off another light and a sixth
        // apart: the four are one light of the stack's box, the faces they press together left out
        // of its surface, which is then the whole of its box's.
        var vertices = new ModelVertex[] { new() { Position = new Vector3(-0.5f) }, new() { Position = new Vector3(0.5f) } };
        var (glow, other) = (new Vector3(4, 2, 1), new Vector3(1, 2, 4));
        (SceneFieldPlan.Instance, bool) Block(float x, float z, Vector3 light) =>
            (new SceneFieldPlan.Instance(1, vertices, Matrix4x4.CreateTranslation(x, 0.5f, z), false, Vector3.One, light), false);
        var lights = GlowLights.Gather([Block(0.5f, 0.5f, glow), Block(1.5f, 0.5f, glow), Block(0.5f, 1.5f, glow), Block(1.5f, 1.5f, glow),
            Block(2.5f, 0.5f, other), Block(6.5f, 0.5f, glow)], 0.25f, SceneFieldPlan.Box.Of, _ => 1);

        lights.Should().HaveCount(3);
        var stack = lights.Single(l => l.Middle == new Vector3(1, 0.5f, 1));
        (stack.X, stack.Y, stack.Z).Should().Be((new Vector3(1, 0, 0), new Vector3(0, 0.5f, 0), new Vector3(0, 0, 1)));
        stack.Share.Should().BeApproximately(1, 1e-5f, "four blocks of 6 less the four faces they press together, twice each, is the 16 of their box");
        lights.Should().Contain(l => l.Middle == new Vector3(2.5f, 0.5f, 0.5f) && l.Glow == other, "a block giving off another light stays its own");
        lights.Should().Contain(l => l.Middle == new Vector3(6.5f, 0.5f, 0.5f), "and so does one that touches none");
    }

    // The light on the floor around a block of the voxel game's glowstone, its glow 4 times
    // (255, 200, 120), lying on a gray floor of 200 at night, from 1.2 to 3.8 blocks from its middle
    // every fifth of a block, as BounceReference traced the frame with 16384 paths a pixel at 256 by
    // 256, read as the test reads the frame.
    private static readonly (float Radius, double Light)[] Traced =
    [
        (1.2f, 0.1744), (1.4f, 0.1206), (1.6f, 0.0847), (1.8f, 0.0615), (2.0f, 0.0455), (2.2f, 0.0339), (2.4f, 0.0265),
        (2.6f, 0.0207), (2.8f, 0.0165), (3.0f, 0.0134), (3.2f, 0.0110), (3.4f, 0.0091), (3.6f, 0.0077), (3.8f, 0.0065),
    ];

    [NeedsVulkanTheory]
    [Trait("Category", "Render")]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.Medium)]
    [InlineData(GlobalIllumination.High)]
    public void A_Glowing_Block_Lights_The_Floor_Around_It_As_A_Path_Traced_Reference_Does(GlobalIllumination quality)
    {
        // The world's probes stand above the floor at their spacing, so the faces the floor reads
        // see none of a block lying on it, and the block's light reached the floor through the
        // screen probes' sixteen rays alone: 0.077 two blocks out and nothing past two and a half,
        // and at High eight lobes around the ring, its eighth harmonic 0.17 of its light at two
        // blocks and 0.35 at two and a half, where the rays met the block at some angles and not
        // others. Carried as a light whose penumbra counted the floor and the block's own faces as
        // what hid it, the floor's light ran in dark rings, the falloff 0.83 to 1.02 of the
        // reference's, dipping by a tenth every block or so, as this test measured on an RTX 4070.
        // Each ring reads within 2% of the reference and the eighth harmonic 0.005.
        var light = Rings(quality, 9);
        var shares = Traced.Select(t => light[t.Radius].Mean / t.Light).ToArray();
        for (int i = 0; i < Traced.Length; i++)
            shares[i].Should().BeApproximately(1, 0.06, $"the floor {Traced[i].Radius} blocks out at {quality} reads as the reference");
        for (int i = 1; i + 1 < Traced.Length; i++)
            Math.Abs(shares[i - 1] - 2 * shares[i] + shares[i + 1]).Should().BeLessThan(0.04,
                $"the floor's light at {quality} falls off as the reference's, its shares of it {string.Join(", ", shares.Select(r => $"{r:0.000}"))} with no ring");
        light[2.6f].Lobes.Should().BeLessThan(0.05, $"at {quality} the ring two and a half blocks out has no eight lobes");
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Glowing_Blocks_Pool_Reads_The_Same_From_Forty_Blocks_Off_As_From_Five()
    {
        // From forty blocks the floor around the block lies in the cascades whose probes stand
        // eight and sixteen blocks apart and no screen probe's ray reaches it, so its pool was
        // gone from the bounce, as the voxel game found it; the model pass lights such a pixel
        // from the light itself.
        // From forty blocks a pixel is a fifth of a block wide, so the rings are read from two blocks
        // out, where the light changes little across a pixel.
        var near = Rings(GlobalIllumination.High, 5);
        var far = Rings(GlobalIllumination.High, 40);
        foreach (var (radius, _) in Traced.Where(t => t.Radius >= 2))
            far[radius].Mean.Should().BeApproximately(near[radius].Mean, near[radius].Mean * 0.1, $"the floor {radius} blocks out reads alike from forty blocks and from five");
    }

    // The floor's light, linear, around the block seen straight down from `height` blocks above,
    // the mean of 72 points on each ring and the eighth harmonic of them as a share of the mean.
    private Dictionary<float, (double Mean, double Lobes)> Rings(GlobalIllumination quality, float height)
    {
        const int Size = 256;
        var config = Config.Default.WithWindow("glow test", Size, Size) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 4);
        SetGlobalIllumination(quality);
        SetAmbientLight(Color.Black, 0);
        var floor = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var lamp = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        lamp.Materials[0].Emissive = new Color(255, 200, 120);
        lamp.Materials[0].EmissiveIntensity = 4;
        var middle = new Vector3(0.5f, 0, 0.5f);
        var camera = new Camera3D(middle + new Vector3(0, height, 0), middle, -Vector3.UnitZ, 70);
        for (int frame = 0; frame < 50; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(floor, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(128, 1, 128), new Color(200, 200, 200));
            DrawModelEx(lamp, middle + new Vector3(0, 0.5f, 0), Vector3.UnitY, 0, Vector3.One, new Color(230, 190, 110));
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var linear = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        var rings = new Dictionary<float, (double, double)>();
        foreach (var (radius, _) in Traced)
        {
            var values = Enumerable.Range(0, 72).Select(i =>
            {
                var angle = 2 * MathF.PI * i / 72;
                var at = GetWorldToScreen(middle + new Vector3(MathF.Cos(angle), 0, MathF.Sin(angle)) * radius, camera);
                double sum = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int x = Math.Clamp((int)MathF.Round(at.X) + dx, 0, Size - 1), y = Math.Clamp((int)MathF.Round(at.Y) + dy, 0, Size - 1), p = (y * Size + x) * 4;
                        sum += 0.2126 * linear[p] + 0.7152 * linear[p + 1] + 0.0722 * linear[p + 2];
                    }
                return sum / 9;
            }).ToArray();
            var mean = values.Average();
            double re = 0, im = 0;
            for (int i = 0; i < values.Length; i++)
            {
                re += values[i] * Math.Cos(2 * Math.PI * 8 * i / values.Length);
                im -= values[i] * Math.Sin(2 * Math.PI * 8 * i / values.Length);
            }
            rings[radius] = (mean, 2 * Math.Sqrt(re * re + im * im) / values.Length / Math.Max(mean, 1e-9));
        }
        UnloadModel(floor);
        UnloadModel(lamp);
        CloseWindow();
        UseApp(null);
        return rings;
    }
}
