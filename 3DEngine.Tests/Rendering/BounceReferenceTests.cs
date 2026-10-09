using System.Globalization;
using System.Numerics;
using FluentAssertions;
using static Engine.Engine3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The path-traced reference the light that bounces is measured by (<see cref="BounceReference"/>),
/// held to a picture whose light is known: inside a closed box whose walls all give off the same
/// light and reflect the same share of what reaches them, every wall shows the light it gives off
/// over one less that share, the sum of every bounce.
/// </summary>
[Collection("Engine3D")]
[Trait("Category", "Render")]
public sealed class BounceReferenceTests : IDisposable
{
    private readonly TestFolder _folder = new("engine-bounce-reference-");

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [NeedsRayQueryFact]
    public void Inside_A_Closed_Glowing_Box_The_Reference_Shows_Its_Light_Over_One_Less_Its_Color()
    {
        var share = GlowingBox();
        var path = Path.Combine(_folder.Path, "box.png");
        var said = BounceReference.Trace(GetApp().World, path, 256);
        said.Should().StartWith("traced 160 by 120", "the window's meshes are held for the device's rays at High");
        var mean = ReadPfm(path + ".pfm").Average();
        var expected = 1 / (1 - share);
        mean.Should().BeApproximately(expected, expected * 0.02f, $"every bounce sums to the light given off over one less the share reflected, {expected:0.000}");
    }

    [NeedsRayQueryFact]
    public void In_The_Glowing_Box_Light_That_Never_Bounces_Shows_The_Walls_Own_And_Light_That_Bounces_Once_Adds_Their_Color()
    {
        var share = GlowingBox();
        var never = Path.Combine(_folder.Path, "never.png");
        BounceReference.Trace(GetApp().World, never, 16, bounces: 0).Should().StartWith("traced");
        ReadPfm(never + ".pfm").Average().Should().BeApproximately(1, 0.01f, "each wall shows the light it gives off and nothing that reached it");
        var once = Path.Combine(_folder.Path, "once.png");
        BounceReference.Trace(GetApp().World, once, 256, bounces: 1).Should().Contain("light bouncing once");
        ReadPfm(once + ".pfm").Average().Should().BeApproximately(1 + share, (1 + share) * 0.02f, "and the walls' light, reflected once, on top");
    }

    [NeedsRayQueryFact]
    public void A_Probe_Inside_The_Glowing_Box_Has_Its_Light_Over_One_Less_Its_Color_Arriving_From_Every_Way()
    {
        var share = GlowingBox();
        var probe = BounceReference.MeasureProbe(GetApp().World, Vector3.Zero, 256, -1, 0, out var why);
        probe.Should().NotBeNull(why);
        var expected = 1 / (1 - share);
        probe!.Ways.Should().Be(64, "High's first cascade has eight texels along each side of its octahedron");
        probe.Hits.Should().BeGreaterThan(48, "nearly every way meets a wall within the first cascade's reach, a far corner's past it");
        probe.Reference.X.Should().BeApproximately(expected, expected * 0.03f, $"the light from every way is that of every wall, {expected:0.000}");
        probe.HitReference.X.Should().BeApproximately(expected, expected * 0.03f);
        probe.Middle.Length().Should().BeLessThan(2, "the probe is the one nearest the box's middle");
    }

    // A closed box of walls of sRGB 188, which reflect half the light that reaches them in linear
    // light, each giving off 1, drawn until light bouncing at High has settled, the share returned.
    private static float GlowingBox()
    {
        var config = Config.Default.WithWindow("bounce reference test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0] = new ModelMaterial(new Color(188, 188, 188)) { Emissive = Color.White, EmissiveIntensity = 1 };
        var camera = new Camera3D(new Vector3(0, 0, 1.5f), new Vector3(0.3f, -0.2f, 0), Vector3.UnitY, 70);
        for (int frame = 0; frame < 12; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            foreach (var (at, size) in new[]
            {
                (new Vector3(0, -2.1f, 0), new Vector3(4.4f, 0.2f, 4.4f)), (new Vector3(0, 2.1f, 0), new Vector3(4.4f, 0.2f, 4.4f)),
                (new Vector3(-2.1f, 0, 0), new Vector3(0.2f, 4.4f, 4.4f)), (new Vector3(2.1f, 0, 0), new Vector3(0.2f, 4.4f, 4.4f)),
                (new Vector3(0, 0, -2.1f), new Vector3(4.4f, 4.4f, 0.2f)), (new Vector3(0, 0, 2.1f), new Vector3(4.4f, 4.4f, 0.2f)),
            })
                DrawModelEx(slab, at, Vector3.UnitY, 0, size, Color.White);
            EndMode3D();
            EndDrawing();
        }
        return MathF.Pow((188 / 255f + 0.055f) / 1.055f, 2.4f);
    }

    [NeedsRayQueryFact]
    public void A_Lone_Slab_Under_A_Lamp_Reads_In_The_Frame_As_In_The_Reference()
    {
        // Nothing for light to bounce from and a black sky, so the frame's light is the model
        // pass's own, which the reference's first face is lit as, specular and all.
        var config = Config.Default.WithWindow("bounce reference test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        SetTonemap(Tonemap.None);
        CreatePointLight(new Vector3(0.5f, 2, 0.5f), new Color(255, 230, 200), 3, range: 10);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0] = new ModelMaterial(new Color(200, 160, 120)) { Roughness = 0.4f };
        var camera = new Camera3D(new Vector3(0, 3, 4), Vector3.Zero, Vector3.UnitY, 45);
        void Frames()
        {
            for (int frame = 0; frame < 12; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModelEx(slab, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(4, 0.2f, 4), Color.White);
                EndMode3D();
                EndDrawing();
            }
        }
        Frames();
        var path = Path.Combine(_folder.Path, "slab.png");
        BounceReference.Trace(GetApp().World, path, 256).Should().StartWith("traced");
        var regions = BounceReference.Measure(GetApp().World, path, out var why, out _);
        regions.Should().NotBeNull(why);
        var floor = regions!.Single(r => r.Name.EndsWith("floor", StringComparison.Ordinal));
        floor.Pixels.Should().BeGreaterThan(5000, "the slab's top fills most of the view");
        // The two read alike to a hundredth of a percent on an RTX 4070, the paths' noise the rest.
        Math.Abs(floor.Share).Should().BeLessThan(0.01f, $"the slab's top reads in the frame {floor.Frame} as in the reference {floor.Reference}");
        UnloadModel(slab);
    }

    // A PFM file's floats, whatever their order.
    private static float[] ReadPfm(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var (lines, at) = (0, 0);
        while (lines < 3) if (bytes[at++] == '\n') lines++;
        var size = System.Text.Encoding.ASCII.GetString(bytes, 0, at).Split('\n')[1].Split(' ');
        var count = int.Parse(size[0], CultureInfo.InvariantCulture) * int.Parse(size[1], CultureInfo.InvariantCulture) * 3;
        return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(bytes.AsSpan(at, count * 4)).ToArray();
    }
}
