using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>The circular occluders as they draw, far from the world's origin as at it.</summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class CircularOccludersRenderTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    [NeedsVulkanFact]
    public void A_Room_Far_From_The_Origin_Is_Occluded_As_The_Same_Room_At_It()
    {
        // A room far from the origin read against the same room at it through the same frames, the
        // circular occluders' own result and the occlusion after the history and the blurs alike
        // texel for texel, since each pass works against the frame's origin and the eye.
        var (nearRaw, nearFinal) = Occlusion(Vector3.Zero);
        var (farRaw, farFinal) = Occlusion(new Vector3(4448, 24, 600));

        MeanDifference(nearRaw, farRaw).Should().BeLessThan(0.01, "the circles are read about the eye wherever the room stands");
        MeanDifference(nearFinal, farFinal).Should().BeLessThan(0.01, "and the history and the blurs keep them so");
        nearFinal.Average(v => (double)v).Should().BeLessThan(0.97, "the room's corners and the blocks on its floor close some light off");
    }

    // The room's occlusion by the circular occluders at 16 samples, raw and final, each texel's
    // share of the light from all around, after the same frames.
    private static (float[] Raw, float[] Final) Occlusion(Vector3 at)
    {
        CloseWindow();
        UseApp(null);
        var config = Config.Default.WithWindow("occluders test", 320, 180) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetAmbientOcclusion(1, 1);
        SetAmbientOcclusionMethod(AmbientOcclusionMethod.CircularOccluders, 16);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        var camera = new Camera3D(at + new Vector3(3.6f, 2.4f, 3.8f), at + new Vector3(-1, 0.6f, -1.2f), Vector3.UnitY, 65);
        for (int frame = 0; frame < 30; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            for (int x = -3; x <= 3; x++)
            for (int z = -3; z <= 3; z++)
                DrawModel(block, at + new Vector3(x, -0.5f, z), 1, Color.White);
            for (int y = 0; y < 3; y++)
            for (int i = -3; i <= 3; i++)
            {
                DrawModel(block, at + new Vector3(i, y + 0.5f, -3), 1, Color.White);
                DrawModel(block, at + new Vector3(-3, y + 0.5f, i), 1, Color.White);
            }
            DrawModel(block, at + new Vector3(0, 0.5f, 0), 1, Color.White);
            DrawModel(block, at + new Vector3(-1, 0.5f, -2), 1, Color.White);
            DrawModel(block, at + new Vector3(-1, 1.5f, -2), 1, Color.White);
            EndMode3D();
            EndDrawing();
        }
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var occlusion = renderer.RenderWorld.Get<AmbientOcclusionRenderer>();
        var raw = device.ReadPixels(occlusion.CircularPart!.Raw!);
        var final = device.ReadPixels(occlusion.WindowOcclusion!);
        UnloadModel(block);
        return (Channel(raw, 3), Channel(final, 0));
    }

    private static float[] Channel(byte[] pixels, int channel) =>
        [.. Enumerable.Range(0, pixels.Length / 4).Select(i => pixels[i * 4 + channel] / 255f)];

    private static double MeanDifference(float[] a, float[] b)
    {
        a.Length.Should().Be(b.Length);
        return Enumerable.Range(0, a.Length).Average(i => Math.Abs(a[i] - b[i]));
    }
}
