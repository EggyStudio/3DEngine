using FluentAssertions;
using System.Numerics;
using Xunit.Abstractions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A rough surface's reflection of the light the world's probes hold along its mirror way, read in a
/// closed box of emissive metal walls, against the path-traced reference with the first face's
/// specular share traced. Metal scatters nothing diffusely, so the walls' first faces show their
/// emission and what they reflect alone, and the light inside the box arrives alike from every way,
/// the walls' emission over one less their color, which a face reflects by the share Karis's fit
/// gives a surface under light from all around.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class RoughReflectionTests(ITestOutputHelper output) : IDisposable
{
    private readonly TestFolder _folder = new("engine-rough-reflection-");

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    // The box of BounceReferenceTests with walls of metal of a roughness, three times as thick, seen
    // through a camera, its color in linear light.
    private static float MetalBox(float roughness, Camera3D camera)
    {
        var config = Config.Default.WithWindow("rough reflection test", 160, 120) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(2, 0.25f, 1);
        SetGlobalIllumination(GlobalIllumination.High);
        SetTonemap(Tonemap.None);
        var slab = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        slab.Materials[0] = new ModelMaterial(new Color(188, 188, 188)) { Emissive = Color.White, EmissiveIntensity = 1, Metallic = 1, Roughness = roughness };
        for (int frame = 0; frame < 24; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            foreach (var (at, size) in new[]
            {
                (new Vector3(0, -2.3f, 0), new Vector3(5.2f, 0.6f, 5.2f)), (new Vector3(0, 2.3f, 0), new Vector3(5.2f, 0.6f, 5.2f)),
                (new Vector3(-2.3f, 0, 0), new Vector3(0.6f, 5.2f, 5.2f)), (new Vector3(2.3f, 0, 0), new Vector3(0.6f, 5.2f, 5.2f)),
                (new Vector3(0, 0, -2.3f), new Vector3(5.2f, 5.2f, 0.6f)), (new Vector3(0, 0, 2.3f), new Vector3(5.2f, 5.2f, 0.6f)),
            })
                DrawModelEx(slab, at, Vector3.UnitY, 0, size, Color.White);
            EndMode3D();
            EndDrawing();
        }
        return MathF.Pow((188 / 255f + 0.055f) / 1.055f, 2.4f);
    }

    // The share of light from all around a face of f0 and roughness reflects seen at nv, as the
    // model pass's ambientReflected gives it by Karis's fit.
    private static float AmbientReflected(float f0, float roughness, float nv)
    {
        var r = new Vector4(-1, -0.0275f, -0.572f, 0.022f) * roughness + new Vector4(1, 0.0425f, 1.04f, -0.04f);
        var a004 = MathF.Min(r.X * r.X, MathF.Pow(2, -9.28f * nv)) * r.X + r.Y;
        return f0 * (-1.04f * a004 + r.Z) + (1.04f * a004 + r.W);
    }

    private static float Mean(string path)
    {
        var bytes = File.ReadAllBytes(path);
        // A PFM's three lines of text, then little-endian floats, three a pixel.
        var at = 0;
        for (int line = 0; line < 3; line++) at = Array.IndexOf(bytes, (byte)'\n', at) + 1;
        var floats = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(bytes.AsSpan(at));
        var sum = 0.0;
        for (int i = 0; i < floats.Length; i += 3) sum += floats[i];
        return (float)(sum / (floats.Length / 3));
    }

    [NeedsRayQueryFact]
    public void In_A_Box_Of_Metal_The_Reference_Reflects_The_Boxs_Light_Where_Its_Specular_Share_Is_Traced()
    {
        var color = MetalBox(0.6f, new Camera3D(new Vector3(0, 0, 1.5f), new Vector3(0.3f, -0.2f, 0), Vector3.UnitY, 70));
        var plain = Path.Combine(_folder.Path, "plain.png");
        BounceReference.Trace(GetApp().World, plain, 128).Should().StartWith("traced");
        Mean(plain + ".pfm").Should().BeApproximately(1, 0.02f, "the metal's first faces scatter nothing diffusely, so each shows its emission alone");

        // GGX's metal of this color reflects between a third and a half of light from all around,
        // which the box's light from every way, 1 / (1 - color), is reflected by. It read 1.815, a
        // share of 0.41, on the development machine's GPU.
        var specular = Path.Combine(_folder.Path, "specular.png");
        BounceReference.Trace(GetApp().World, specular, 512, specular: true).Should().Contain("specular share traced");
        var mean = Mean(specular + ".pfm");
        output.WriteLine($"with the specular share {mean:0.000}, a share of {(mean - 1) * (1 - color):0.000}");
        mean.Should().BeInRange(1 + 0.33f / (1 - color), 1 + 0.5f / (1 - color), "each face reflects the box's light from every way by its specular share");
    }

    [NeedsRayQueryFact]
    public void A_Rough_Metal_Wall_Reflects_The_Light_The_Probes_Hold_By_Its_Specular_Share()
    {
        // A wall seen square on from the box's middle, so each pixel's share is the fit's at an nv
        // of 1, and the probes hold the box's light alike along every way, which the model pass
        // reflected none of, with no environment in the box. The fit, Karis's for mobile, lies under
        // GGX's albedo at this roughness, so the reference reads more (RENDERING.md).
        var color = MetalBox(0.6f, new Camera3D(Vector3.Zero, new Vector3(0, 0, -1), Vector3.UnitY, 20));
        var probe = BounceReference.MeasureProbe(GetApp().World, new Vector3(1, 1, 1), 64, -1, 0, out var said);
        probe.Should().NotBeNull(said);
        var held = probe!.Each.Average(way => way.Merged.X);
        held.Should().BeApproximately(1 / (1 - color), 0.02f / (1 - color), "the probes hold the box's light along every way");

        var path = Path.Combine(_folder.Path, "wall.png");
        BounceReference.Trace(GetApp().World, path, 64, specular: true).Should().StartWith("traced");
        var regions = BounceReference.Measure(GetApp().World, path, out var why, out _);
        regions.Should().NotBeNull(why);
        var wall = regions!.MaxBy(r => r.Pixels)!;
        var expected = 1 + AmbientReflected(color, 0.6f, 1) * held;
        output.WriteLine($"the wall reads {wall.Frame.X:0.000} in the frame, {expected:0.000} expected and {wall.Reference.X:0.000} in the reference");
        // 1.675 against 1.676 on the development machine's GPU, the reference 1.832.
        wall.Frame.X.Should().BeApproximately(expected, expected * 0.005f, "the wall reflects the box's light the probes hold along its mirror ways by its specular share");
    }
}
