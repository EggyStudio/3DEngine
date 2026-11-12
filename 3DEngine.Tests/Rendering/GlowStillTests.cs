using System.Numerics;
using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// A still view of a room lit by a glow light alone, read for how many frames its glow texels'
/// blend holds and how much its light changes from one frame to the next.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Render")]
public sealed class GlowStillTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    // GlowFarFromOriginTests' lantern room at the origin, its lantern the one light, held still in a
    // window of the size given at the quality given, settled and then drawn on for as many frames as
    // a still view's blend takes to hold its 32 and a few more. The mean over the room's glow texels
    // of the frames each blend holds, and the mean change of the frame's level in sRGB's 255ths
    // between the last two frames.
    private static (double Frames, double Change) Still(GlobalIllumination quality, int width, int height)
    {
        var config = Config.Default.WithWindow("glow still", width, height) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(4, 0.25f);
        SetGlobalIllumination(quality);
        SetAmbientLight(Color.Black, 0);
        var block = LoadModelFromMesh(GenMeshCube(1, 1, 1));
        Vector3[] square = [new(5, 0, 0), new(6, 0, 0), new(6, 1, 0), new(5, 1, 0)];
        var lantern = LoadModelFromMesh(UploadMesh(square, [0, 1, 2, 0, 2, 3], null, null));
        lantern.Materials[0].Emissive = new Color(200, 235, 255);
        lantern.Materials[0].EmissiveIntensity = 6;
        var blocks = GlowFarFromOriginTests.Blocks().ToArray();
        var view = new Camera3D(new Vector3(1.5f, 2.6f, 4.3f), new Vector3(8, 2.6f, 0.6f), Vector3.UnitY, 70);
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        float[] Frame() => device.ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
        float[]? first = null, second = null;
        const int Frames = SceneFieldPlan.SettleFrames + 80;
        for (int frame = 0; frame < Frames; frame++)
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(view);
            SetSceneFieldCenter(new Vector3(4, 2, 2));
            foreach (var b in blocks) DrawModel(block, b + new Vector3(0.5f), 1, new Color(210, 210, 205));
            DrawModelEx(block, new Vector3(5.5f, 0.5f, -0.51f), Vector3.UnitY, 0, new Vector3(1, 1, 0.98f), new Color(210, 210, 205));
            DrawModel(lantern, Vector3.Zero, 1, Color.White);
            EndMode3D();
            EndDrawing();
            if (frame == Frames - 2) first = Frame();
            if (frame == Frames - 1) second = Frame();
        }
        var state = device.ReadFloats(renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Screen!.Glow.State);
        var held = Enumerable.Range(0, state.Length / 4).Select(i => state[i * 4 + 1]).Where(f => f > 0).ToList();
        static float Level(float l) => 255 * Math.Clamp(l <= 0.0031308f ? 12.92f * l : 1.055f * MathF.Pow(l, 1 / 2.4f) - 0.055f, 0, 1);
        var change = Enumerable.Range(0, width * height).Average(i =>
            Enumerable.Range(0, 3).Average(c => Math.Abs(Level(first![i * 4 + c]) - Level(second![i * 4 + c]))));
        UnloadModel(block);
        UnloadModel(lantern);
        CloseWindow();
        UseApp(null);
        return (held.Count > 0 ? held.Average() : 0, change);
    }

    [NeedsVulkanTheory]
    [InlineData(GlobalIllumination.Low)]
    [InlineData(GlobalIllumination.High)]
    public void A_Still_View_Holds_Its_Glow_Lights_Light_From_Frame_To_Frame(GlobalIllumination quality)
    {
        // At 1,440 rows a glow texel spans a part of the depth's texels, and its point, found at the
        // middle of the depth texel it takes its depth from, was looked up in the frame before off
        // its texel's own place, between four texels, so the view never read as still and the blend
        // held a frame and a half's light; at 720 rows a texel is a whole count of them. The rows
        // alone set that, so each window is a strip a few tens of pixels wide, which a device that
        // draws on its CPU draws in the time a test has.
        foreach (var (width, height) in new[] { (48, 720), (48, 1440) })
        {
            var (frames, change) = Still(quality, width, height);
            frames.Should().BeGreaterThan(30, $"at {quality} and {height} rows a still view's glow texels hold their blend's 32 frames");
            change.Should().BeLessThan(0.1, $"at {quality} and {height} rows a still view's light holds from frame to frame");
        }
    }
}
