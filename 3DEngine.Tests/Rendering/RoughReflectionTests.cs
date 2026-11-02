using FluentAssertions;
using System.Numerics;
using Xunit.Abstractions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// The path-traced reference with the first face's specular share traced (gi.reference's specular),
/// read in a closed box of emissive metal walls. Metal scatters nothing diffusely, so the walls'
/// first faces show their emission and what they reflect alone, and the light inside the box
/// arrives alike from every way, the walls' emission over one less their color.
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
}
