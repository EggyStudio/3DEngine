using System.Numerics;
using FluentAssertions;
using Xunit.Abstractions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// An open-topped box 8 m a side of black walls on a white floor, under a sky of one luminance and
/// no sun. The floor's middle sees the sky through the opening alone, so its light is the sky's
/// luminance times the share of the dome the opening holds, cosine weighted, which the traced
/// reference gives and the probes are held near at each quality.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class OpenBoxTests(ITestOutputHelper output) : IDisposable
{
    private const int Size = 192, Around = 5;
    // The sky's bytes, 186 in sRGB, are 0.491 in linear light.
    private const byte SkyByte = 186;
    private readonly TestFolder _folder = new("engine-open-box-");

    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
        _folder.Dispose();
    }

    [NeedsRayQueryFact]
    public void The_Floor_In_An_Open_Box_Takes_The_Sky_By_The_Share_Of_The_Dome_It_Sees()
    {
        // The share of the dome a point under the middle of a square opening sees, cosine
        // weighted, the opening's half side a over its height c, here 4 over 8.
        double a = 4.0 / 8.0, root = Math.Sqrt(1 + a * a);
        double share = 2 / Math.PI * 2 * (a / root * Math.Atan(a / root));
        double sky = Math.Pow((SkyByte / 255.0 + 0.055) / 1.055, 2.4);
        var expected = share * sky;

        var traced = Read(GlobalIllumination.High, Path.Combine(_folder.Path, "box.png")) / expected;
        var read = new Dictionary<GlobalIllumination, double>();
        foreach (var quality in new[] { GlobalIllumination.High, GlobalIllumination.Medium, GlobalIllumination.Low })
            read[quality] = Read(quality, null) / expected;
        var said = $"the reference {traced:0.000} of the share, High {read[GlobalIllumination.High]:0.000}, "
            + $"Medium {read[GlobalIllumination.Medium]:0.000} and Low {read[GlobalIllumination.Low]:0.000}";
        output.WriteLine(said);

        traced.Should().BeApproximately(1, 0.05, $"the traced floor takes the sky by the share it sees, {said}");
        foreach (var (quality, ratio) in read)
            ratio.Should().BeInRange(0.75, 1.25, $"the floor at {quality} takes the sky near the share it sees, {said}");
    }

    // The floor's middle seen from over the box, the mean luminance in linear light over 11 by 11
    // pixels of the frame before the bloom, or of the traced reference where a path is given.
    private static double Read(GlobalIllumination quality, string? traced)
    {
        var config = Config.Default.WithWindow("open box", Size, Size) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f, 4);
        SetGlobalIllumination(quality);
        SetAmbientLight(Color.Black, 0);
        var even = new byte[64 * 32 * 4];
        for (int i = 0; i < 64 * 32; i++)
            (even[i * 4], even[i * 4 + 1], even[i * 4 + 2], even[i * 4 + 3]) = (SkyByte, SkyByte, SkyByte, (byte)255);
        SetEnvironmentMap(new Image(even, 64, 32));
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var black = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        black.Materials[0] = new ModelMaterial(Color.Black) { Roughness = 1 };
        var camera = new Camera3D(new Vector3(0, 12, 0), Vector3.Zero, -Vector3.UnitZ, 20);
        for (int frame = 0; frame < 120; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            DrawModelEx(block, new Vector3(0, -0.5f, 0), Vector3.UnitY, 0, new Vector3(10, 1, 10), Color.White);
            DrawModelEx(black, new Vector3(-4.5f, 4, 0), Vector3.UnitY, 0, new Vector3(1, 8, 10), Color.White);
            DrawModelEx(black, new Vector3(4.5f, 4, 0), Vector3.UnitY, 0, new Vector3(1, 8, 10), Color.White);
            DrawModelEx(black, new Vector3(0, 4, -4.5f), Vector3.UnitY, 0, new Vector3(8, 8, 1), Color.White);
            DrawModelEx(black, new Vector3(0, 4, 4.5f), Vector3.UnitY, 0, new Vector3(8, 8, 1), Color.White);
            EndMode3D();
            EndDrawing();
        }
        float[] light;
        if (traced is null)
        {
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var rgba = ((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            light = new float[Size * Size * 3];
            for (int i = 0; i < Size * Size; i++)
                for (int c = 0; c < 3; c++)
                    light[i * 3 + c] = rgba[i * 4 + c];
        }
        else
        {
            BounceReference.Trace(GetApp().World, traced, 4096).Should().StartWith("traced");
            light = BounceReference.ReadPfm(traced + ".pfm").Light;
        }
        double sum = 0;
        for (int dy = -Around; dy <= Around; dy++)
            for (int dx = -Around; dx <= Around; dx++)
            {
                int p = ((Size / 2 + dy) * Size + Size / 2 + dx) * 3;
                sum += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
            }
        UnloadModel(block);
        UnloadModel(black);
        CloseWindow();
        UseApp(null);
        return sum / ((2 * Around + 1) * (2 * Around + 1));
    }
}
