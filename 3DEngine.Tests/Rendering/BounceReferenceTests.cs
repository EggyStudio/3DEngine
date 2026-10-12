using System.Globalization;
using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The path-traced reference the light that bounces is measured by (<see cref="BounceReference"/>),
/// held to a picture whose light is known: inside a closed box whose walls all give off the same
/// light and reflect the same share of what reaches them, every wall shows the light it gives off
/// over one less that share, the sum of every bounce.
/// </summary>
[Collection("E3D")]
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

    [NeedsRayQueryFact]
    public void A_Probe_Beside_A_Block_The_Bounce_Carries_As_A_Light_Reads_None_Of_Its_Glow_In_The_Reference()
    {
        // A glowing block two units on a side, which the bounce carries as a light, alone under a
        // black sky. The probe's rays leave its glow out where they meet it, since the light reaches
        // a surface as a glow light rather than through the probes, and the reference leaves it out
        // as they do, where it counted it and read the rays of the probes around four glowing
        // blocks 44 to 70% under; a reference of the window's picture still shows the block's glow.
        var config = Config.Default.WithWindow("bounce reference test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        SetAmbientLight(Color.Black, 0);
        var block = LoadModelFromMesh(GenMeshCube(2, 2, 2));
        block.Materials[0] = new ModelMaterial(new Color(188, 188, 188)) { Emissive = Color.White, EmissiveIntensity = 1 };
        var camera = new Camera3D(new Vector3(4, 2, 4), Vector3.Zero, Vector3.UnitY, 60);
        for (int frame = 0; frame < 12; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModel(block, Vector3.Zero, 1, Color.White);
            EndMode3D();
            EndDrawing();
        }
        var probe = BounceReference.MeasureProbe(GetApp().World, new Vector3(2.5f, 0, 0), 256, -1, 0, out var why);
        probe.Should().NotBeNull(why);
        probe!.Hits.Should().BeGreaterThan(0, "some of the probe's rays meet the block");
        probe.HitReference.X.Should().BeLessThan(0.01f, "nothing but the block's own glow lies along them");
        probe.HitRays.X.Should().BeLessThan(0.01f, "the probe's rays leave the glow out where they meet the block");
        var path = Path.Combine(_folder.Path, "block.png");
        BounceReference.Trace(GetApp().World, path, 16, bounces: 0).Should().StartWith("traced");
        ReadPfm(path + ".pfm").Max().Should().BeGreaterThan(0.9f, "a reference of the window's picture shows the block's glow where the eye meets it");
        UnloadModel(block);
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
        // The two read alike to a hundredth of a percent on the development machine's GPU, the
        // paths' noise the rest.
        Math.Abs(floor.Share).Should().BeLessThan(0.01f, $"the slab's top reads in the frame {floor.Frame} as in the reference {floor.Reference}");
        UnloadModel(slab);
    }

    [NeedsRayQueryFact]
    public void A_Slab_Colored_In_Its_Vertices_Reads_In_The_Frame_As_In_The_Reference()
    {
        // The slab above with its color in its vertices and a white material, as a voxel game colors
        // a section's blocks. The reference traced a copy's flat color alone, so a game's rooms of
        // white concrete read a third brighter in it than in the frame.
        var config = Config.Default.WithWindow("bounce reference test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        SetTonemap(Tonemap.None);
        CreatePointLight(new Vector3(0.5f, 2, 0.5f), new Color(255, 230, 200), 3, range: 10);
        var cube = GenMeshCube(1, 1, 1);
        GetApp().World.Resource<MeshStore>().TryGetData(cube.Id, out var vertices, out var indices).Should().BeTrue();
        var slab = LoadModelFromMesh(UploadMesh(vertices, indices, [.. vertices.Select(_ => new Color(200, 160, 120))], null));
        slab.Materials[0] = new ModelMaterial(Color.White) { Roughness = 0.4f };
        var camera = new Camera3D(new Vector3(0, 3, 4), Vector3.Zero, Vector3.UnitY, 45);
        for (int frame = 0; frame < 12; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(slab, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(4, 0.2f, 4), Color.White);
            EndMode3D();
            EndDrawing();
        }
        var path = Path.Combine(_folder.Path, "colored.png");
        BounceReference.Trace(GetApp().World, path, 256).Should().StartWith("traced");
        var regions = BounceReference.Measure(GetApp().World, path, out var why, out _);
        regions.Should().NotBeNull(why);
        var floor = regions!.Single(r => r.Name.EndsWith("floor", StringComparison.Ordinal));
        Math.Abs(floor.Share).Should().BeLessThan(0.01f, $"the slab's top reads in the frame {floor.Frame} as in the reference {floor.Reference}");
        UnloadModel(slab);
    }

    [NeedsRayQueryFact]
    public void A_Slab_Whose_Colors_Are_Written_In_Place_Bounces_Them_Onto_A_Wall_As_The_Reference_Does()
    {
        // A slab colored white in its vertices with a white wall standing on it under a lamp, then
        // its colors written red in place, as a voxel game lights a section again by its light
        // levels. The field kept the colors of the slab's last upload and the ray scene its corners',
        // so the light that bounced off the slab onto the wall stayed white, and so did the slab in
        // the reference.
        var config = Config.Default.WithWindow("bounce reference test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        SetTonemap(Tonemap.None);
        CreatePointLight(new Vector3(0.5f, 2.5f, 1.5f), Color.White, 4, range: 10);
        var cube = GenMeshCube(1, 1, 1);
        GetApp().World.Resource<MeshStore>().TryGetData(cube.Id, out var vertices, out var indices).Should().BeTrue();
        var colored = UploadMesh(vertices, indices, [.. vertices.Select(_ => Color.White)], null);
        var slab = LoadModelFromMesh(colored);
        var wall = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(new Vector3(0, 1.6f, 4), new Vector3(0, 0.7f, -1), Vector3.UnitY, 50);
        void Frames(int count)
        {
            for (int frame = 0; frame < count; frame++)
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                DrawModelEx(slab, new Vector3(0, -0.1f, 0), Vector3.UnitY, 0, new Vector3(4, 0.2f, 4), Color.White);
                DrawModelEx(wall, new Vector3(0, 1, -1.1f), Vector3.UnitY, 0, new Vector3(4, 2, 0.2f), Color.White);
                EndMode3D();
                EndDrawing();
            }
        }
        Frames(SceneFieldPlan.SettleFrames + 12);
        UpdateMeshBuffer(colored, 3, (ReadOnlySpan<Color>)[.. vertices.Select(_ => new Color(220, 40, 40))], 0);
        Frames(40);

        var path = Path.Combine(_folder.Path, "relit.png");
        BounceReference.Trace(GetApp().World, path, 512).Should().StartWith("traced");
        var regions = BounceReference.Measure(GetApp().World, path, out var why, out _);
        regions.Should().NotBeNull(why);
        var floor = regions!.Single(r => r.Name.EndsWith("floor", StringComparison.Ordinal));
        var front = regions!.Single(r => r.Name.Contains("wall facing +z", StringComparison.Ordinal));
        (floor.Reference.X / floor.Reference.Y).Should().BeGreaterThan(5, $"the reference traces the slab red, {floor.Reference}");
        Math.Abs(floor.Share).Should().BeLessThan(0.05f, $"the slab reads in the frame {floor.Frame} as in the reference {floor.Reference}");
        // The wall's red over its green read 1.230 in the frame and 1.240 in the reference on the
        // development machine's GPU, the slab's own light 3.6% from the reference's.
        float Tint(Vector3 light) => light.X / light.Y;
        Tint(front.Frame).Should().BeApproximately(Tint(front.Reference), Tint(front.Reference) * 0.02f,
            $"the wall takes the slab's red as it bounces, the frame's {front.Frame} against the reference's {front.Reference}");
        UnloadModel(slab);
        UnloadModel(wall);
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
