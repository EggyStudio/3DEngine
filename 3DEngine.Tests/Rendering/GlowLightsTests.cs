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

    // The voxel game's end rod, 2 by 15 by 2 sixteenths of a block, and the floor's light around it
    // standing in the block's place, as BounceReference traced the frame with 16384 paths a pixel at
    // 256 by 256 from five blocks up, read as the test reads the frame.
    private static readonly Vector3 Rod = new(2f / 16, 15f / 16, 2f / 16);

    private static readonly (float Radius, double Light)[] RodTraced =
        [(1.0f, 0.018642), (1.2f, 0.012486), (1.4f, 0.008741), (1.6f, 0.006212), (1.8f, 0.004641)];

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Glowing_Rods_Light_On_The_Floor_Falls_With_No_Step()
    {
        // The rod's light stepped by 7 to 9% on a curve around it where its faces' closed form gave
        // way to points at four of its half diagonals, a face fifteen sixteenths long read as one
        // point 11% over the closed form there, the bend of the light's log 0.062 at 1.8 blocks.
        // Cut into points as long as the face is wide, the floor reads the reference within 2% to
        // 1.6 blocks, past which the light fades by its reach, and bends nowhere over 0.021.
        float[] radii = [.. Enumerable.Range(0, 41).Select(i => 1.0f + i * 0.05f)];
        var light = Rings(GlobalIllumination.High, 5, rod: true, radii);
        double At(float radius) => light[radii.First(r => MathF.Abs(r - radius) < 1e-4f)].Mean;
        foreach (var (radius, traced) in RodTraced)
            (At(radius) / traced).Should().BeApproximately(1, 0.05, $"the floor {radius} blocks from the rod reads as the reference");
        var logs = radii.Select(r => Math.Log(light[r].Mean)).ToArray();
        for (int i = 1; i + 1 < logs.Length; i++)
            Math.Abs(logs[i - 1] - 2 * logs[i] + logs[i + 1]).Should().BeLessThan(0.03, $"the rod's light bends nowhere, as it did {radii[i]:0.00} blocks out");
    }

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
        // reference's, dipping by a tenth every block or so, as this test measured on the
        // development machine's GPU. Each ring reads within 2% of the reference and the eighth
        // harmonic 0.005.
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

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Glowing_Block_Shut_In_A_Room_And_Seen_From_Outside_Keeps_The_Device()
    {
        // The voxel game's case: a glowstone set in a closed room of white blocks, eleven by six by
        // eleven inside, the eye twenty blocks off outside, lost the device on the development
        // machine's GPU within a few frames in three runs of five, an MMU fault through the
        // constant cache (Xid 31), while the world probes' rays met the walls and marched through
        // them to the lamp with a loop nested in the march's own; read once after the march, the
        // device holds.
        var config = Config.Default.WithWindow("glow room", 640, 360) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 4);
        SetGlobalIllumination(GlobalIllumination.High);
        CreateDirectionalLight(new Vector3(-0.4f, -0.8f, -0.3f), Color.White, 3, castsShadows: true);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var lamp = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        lamp.Materials[0].Emissive = new Color(255, 200, 120);
        lamp.Materials[0].EmissiveIntensity = 4;
        var camera = new Camera3D(new Vector3(0.5f, 5.6f, 0.5f), new Vector3(16.5f, 7.5f, -15.5f), Vector3.UnitY, 70);
        var white = new Color(220, 220, 220);
        void Frames(int count, bool lit)
        {
            for (int frame = 0; frame < count; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.SkyBlue);
                BeginMode3D(camera);
                DrawModelEx(block, new Vector3(10, 3.5f, -10), Vector3.UnitY, 0, new Vector3(120, 1, 120), new Color(110, 150, 90));
                // The room's floor and roof, and its four walls around eleven by six by eleven.
                DrawModelEx(block, new Vector3(16.5f, 4.5f, -15.5f), Vector3.UnitY, 0, new Vector3(13, 1, 13), white);
                DrawModelEx(block, new Vector3(16.5f, 11.5f, -15.5f), Vector3.UnitY, 0, new Vector3(13, 1, 13), white);
                DrawModelEx(block, new Vector3(10.5f, 8, -15.5f), Vector3.UnitY, 0, new Vector3(1, 6, 11), white);
                DrawModelEx(block, new Vector3(22.5f, 8, -15.5f), Vector3.UnitY, 0, new Vector3(1, 6, 11), white);
                DrawModelEx(block, new Vector3(16.5f, 8, -21.5f), Vector3.UnitY, 0, new Vector3(13, 6, 1), white);
                DrawModelEx(block, new Vector3(16.5f, 8, -9.5f), Vector3.UnitY, 0, new Vector3(13, 6, 1), white);
                if (lit)
                    DrawModelEx(lamp, new Vector3(13.5f, 5.5f, -18.5f), Vector3.UnitY, 0, Vector3.One, new Color(230, 190, 110));
                EndMode3D();
                EndDrawing();
            }
        }
        Frames(120, false);
        Frames(240, true);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var linear = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        linear.Should().Contain(value => value > 0.1f, "the frame was drawn, which reading it back from a lost device would not give");
        UnloadModel(block);
        UnloadModel(lamp);
    }

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Row_Of_Lamps_Lights_The_Floor_Under_A_Block_Floating_Below_It_As_Beside_It()
    {
        // The voxel game's room: three glowing blocks touching in a row under the ceiling, one light,
        // and a white block floating two under the middle one, the floor three under the block, a
        // path-traced reference reading the floor under the block 0.98 of the floor beside it. The
        // row's face was marched to two points on its middle line, both behind the block from the
        // floor under the whole row, and a march passing inside the block's side was taken as deep
        // as the block is tall, so the floor under it read 0.68 of the floor beside it, and reads
        // 0.92 with the longer side halved and the way out taken across the march.
        var config = Config.Default.WithWindow("lamp row", 320, 180) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var lamp = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        lamp.Materials[0].Emissive = new Color(255, 200, 120);
        lamp.Materials[0].EmissiveIntensity = 4;
        var white = new Color(212, 216, 217);
        var eye = new Vector3(16.5f, 8.6f, -11.5f);
        var down = 35 * MathF.PI / 180;
        var camera = new Camera3D(eye, eye + new Vector3(0, -MathF.Sin(down), -MathF.Cos(down)), Vector3.UnitY, 70);
        void Box(Vector3 from, Vector3 to, Model model, Color color) =>
            DrawModelEx(model, (from + to) / 2, Vector3.UnitY, 0, to - from, color);
        for (int frame = 0; frame < 120; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            // The room's floor, ceiling and walls, its inside from 11 to 22, 5 to 11 and -21 to -10.
            Box(new(10, 4, -22), new(23, 5, -9), block, white);
            Box(new(10, 11, -22), new(23, 12, -9), block, white);
            Box(new(10, 5, -22), new(11, 11, -9), block, white);
            Box(new(22, 5, -22), new(23, 11, -9), block, white);
            Box(new(11, 5, -22), new(22, 11, -21), block, white);
            Box(new(11, 5, -10), new(22, 11, -9), block, white);
            for (int x = 15; x <= 17; x++)
                Box(new(x, 10, -16), new(x + 1, 11, -15), lamp, new Color(230, 190, 110));
            Box(new(16, 8, -16), new(17, 9, -15), block, white);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var linear = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        UnloadModel(block);
        UnloadModel(lamp);

        // The floor's mean light over five by five pixels around a point.
        double Floor(Vector3 at)
        {
            var pixel = GetWorldToScreenEx(at, camera, 320, 180);
            double sum = 0;
            for (int y = (int)pixel.Y - 2; y <= (int)pixel.Y + 2; y++)
                for (int x = (int)pixel.X - 2; x <= (int)pixel.X + 2; x++)
                {
                    int p = (y * 320 + x) * 4;
                    sum += 0.2126 * linear[p] + 0.7152 * linear[p + 1] + 0.0722 * linear[p + 2];
                }
            return sum / 25;
        }
        var share = Floor(new Vector3(16.5f, 5, -15.5f)) / Floor(new Vector3(19.5f, 5, -15.5f));
        share.Should().BeGreaterThan(0.8, "the floor under the block takes the row's light past it, as the reference's does");
    }

    // The share of four glowing blocks' light, two by two on a floor and one light, that a pale wall
    // two blocks south sees past a pillar from floor to ceiling a block east of them, at points of the
    // wall's face along x at a height of two blocks, everything but the wall nearly black so little
    // light bounces, as BounceReference traced the frame with 4096 paths a pixel at 640 by 360.
    private static readonly (float X, double Share)[] PillarTraced =
        [(7.5f, 0.814), (8.5f, 0.541), (9.5f, 0.386), (10.5f, 0.304), (11.5f, 0.265)];

    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void A_Wall_Sees_Four_Joined_Lamps_Past_A_Pillar_As_A_Path_Traced_Reference_Does()
    {
        // The voxel game's room where a pillar's shadow drew a seam. The faces of a light's box
        // were weighed by the box's half size along each face's normal where their area belongs,
        // alike for a cube, so for the four blocks two by two the side the pillar hides weighed a
        // quarter of its due against the face it leaves open, and the wall read 0.07 to 0.19 more
        // of the light past the pillar than the reference, 0.893, 0.679, 0.572, 0.478 and 0.388;
        // weighed by area, within 0.03.
        var eye = new Vector3(9.5f, 56.62f, -35.4f);
        var heading = 185 * MathF.PI / 180;
        var pitch = -3 * MathF.PI / 180;
        var camera = new Camera3D(eye, eye + new Vector3(MathF.Sin(heading) * MathF.Cos(pitch), MathF.Sin(pitch), -MathF.Cos(heading) * MathF.Cos(pitch)), Vector3.UnitY, 70);
        const int Width = 320, Height = 180;
        float[] Wall(bool pillar)
        {
            var config = Config.Default.WithWindow("pillar", Width, Height) with { Headless = true, Offscreen = true, Samples = 1 };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            SetSceneField(4, 0.25f, 2);
            SetGlobalIllumination(GlobalIllumination.High);
            SetAmbientLight(Color.Black, 0);
            var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            var lamp = LoadModelFromMesh(GenMeshCube(1, 1, 1));
            lamp.Materials[0].Emissive = new Color(255, 200, 120);
            lamp.Materials[0].EmissiveIntensity = 4;
            var dark = new Color(6, 6, 6);
            void Box(Vector3 from, Vector3 to, Model model, Color color) => DrawModelEx(model, (from + to) / 2, Vector3.UnitY, 0, to - from, color);
            for (int frame = 0; frame < 90; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                // The room's floor, ceiling and walls around 16 by 4 by 9, the wall to the south pale.
                Box(new(-2, 54, -42), new(16, 55, -32), block, dark);
                Box(new(-2, 59, -42), new(16, 60, -32), block, dark);
                Box(new(-2, 55, -42), new(-1, 59, -32), block, dark);
                Box(new(15, 55, -42), new(16, 59, -32), block, dark);
                Box(new(-1, 55, -42), new(15, 59, -41), block, dark);
                Box(new(-1, 55, -33), new(15, 59, -32), block, new Color(212, 216, 217));
                for (int y = 55; y <= 56; y++)
                    for (int z = -37; z <= -36; z++)
                        Box(new(4, y, z), new(5, y + 1, z + 1), lamp, new Color(230, 190, 110));
                if (pillar)
                    Box(new(6, 55, -37), new(7, 59, -35), block, dark);
                EndMode3D();
                EndDrawing();
            }
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var linear = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            UnloadModel(block);
            UnloadModel(lamp);
            CloseWindow();
            UseApp(null);
            return [.. PillarTraced.Select(point =>
            {
                var at = GetWorldToScreenEx(new Vector3(point.X, 57, -33), camera, Width, Height);
                float sum = 0;
                for (int y = (int)at.Y - 2; y <= (int)at.Y + 2; y++)
                    for (int x = (int)at.X - 2; x <= (int)at.X + 2; x++)
                    {
                        int p = (y * Width + x) * 4;
                        sum += 0.2126f * linear[p] + 0.7152f * linear[p + 1] + 0.0722f * linear[p + 2];
                    }
                return sum;
            })];
        }
        var hidden = Wall(true);
        var open = Wall(false);
        for (int i = 0; i < PillarTraced.Length; i++)
            ((double)hidden[i] / open[i]).Should().BeApproximately(PillarTraced[i].Share, 0.05, $"the wall {PillarTraced[i].X} blocks east sees as much past the pillar as the reference");
    }

    // The floor's light, linear, around the block, or where `rod` is set a rod standing in its
    // place, seen straight down from `height` blocks above, the mean of 72 points on each ring of
    // `radii`, the reference's rings by default, and the eighth harmonic of them as a share of the mean.
    private Dictionary<float, (double Mean, double Lobes)> Rings(GlobalIllumination quality, float height, bool rod = false, float[]? radii = null)
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
            if (rod)
                DrawModelEx(lamp, middle + new Vector3(0, Rod.Y / 2, 0), Vector3.UnitY, 0, Rod, new Color(230, 190, 110));
            else
                DrawModelEx(lamp, middle + new Vector3(0, 0.5f, 0), Vector3.UnitY, 0, Vector3.One, new Color(230, 190, 110));
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var linear = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        var rings = new Dictionary<float, (double, double)>();
        foreach (var radius in radii ?? [.. Traced.Select(t => t.Radius)])
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
