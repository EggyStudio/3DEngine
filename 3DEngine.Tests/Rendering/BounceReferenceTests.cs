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
        var config = Config.Default.WithWindow("bounce reference test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        // Walls of sRGB 188, half the light that reaches them in linear light, each giving off 1.
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0] = new ModelMaterial(new Color(188, 188, 188)) { Emissive = Color.White, EmissiveIntensity = 1 };
        var share = MathF.Pow((188 / 255f + 0.055f) / 1.055f, 2.4f);
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

        var path = Path.Combine(_folder.Path, "box.png");
        var said = BounceReference.Trace(GetApp().World, path, 256);
        said.Should().StartWith("traced 160 by 120", "the window's meshes are held for the device's rays at High");
        var light = ReadPfm(path + ".pfm");
        var mean = light.Average();
        var expected = 1 / (1 - share);
        mean.Should().BeApproximately(expected, expected * 0.02f, $"every bounce sums to the light given off over one less the share reflected, {expected:0.000}");
        UnloadModel(slab);
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
