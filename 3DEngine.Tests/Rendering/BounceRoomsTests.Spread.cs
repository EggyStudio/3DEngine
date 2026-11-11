using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The coarser cascades traced in halves a frame while they lie still, against traced whole.
public sealed partial class BounceRoomsTests
{
    [NeedsVulkanFact]
    public void The_Coarser_Cascades_In_Halves_Read_As_Traced_Whole_At_Rest_And_Trace_Whole_While_A_Suns_Step_Settles()
    {
        // The open court of pillars and walls at High, settled, then the sun put out as a game puts
        // it out at night, read frame by frame with the coarser cascades in halves and traced whole.
        // At rest the halves read as the whole trace, and the cascades trace whole while the step
        // settles, so every frame after it reads as the whole trace's.
        const int width = 160, height = 90;
        var court = Rooms.Views[4];
        (byte[] Settled, List<byte[]> After, IReadOnlyList<bool> Halved, IReadOnlyList<bool> HalvedAfter) Run(bool spread)
        {
            var config = Config.Default.WithWindow("spread", width, height) with { Headless = true, Offscreen = true, Samples = 1 };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            SetSceneField(4, 0.15f);
            SetGlobalIllumination(GlobalIllumination.High);
            GetApp().World.Resource<GlobalIlluminationSettings>().SpreadOff = !spread;
            var rooms = new Rooms();
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            byte[] Frame()
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(court);
                rooms.Draw(court);
                EndMode3D();
                EndDrawing();
                return Srgb(((GraphicsDevice)renderer.Context.Graphics!).ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image));
            }
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 120; frame++)
                Frame();
            var settled = Frame();
            var halved = renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Halved.ToArray();
            SetLightColor(rooms.Sun, Color.Black, 0);
            var after = new List<byte[]>();
            var halvedAfter = new List<bool>();
            for (int frame = 0; frame < 30; frame++)
            {
                after.Add(Frame());
                halvedAfter.Add(renderer.RenderWorld.TryGet<GlobalIlluminationRenderer>()!.Halved.Any(h => h));
            }
            rooms.Dispose();
            CloseWindow();
            UseApp(null);
            return (settled, after, halved, halvedAfter);
        }

        var whole = Run(spread: false);
        var halves = Run(spread: true);
        whole.Halved.Should().AllSatisfy(h => h.Should().BeFalse(), "with the spread off every cascade traces all its probes");
        halves.Halved.Should().Equal([false, true, true, true], "the coarser cascades lying still trace in halves, the first whole");
        Apart(halves.Settled, whole.Settled).Past6.Should().Be(0, "at rest the halves read as the whole trace");
        halves.HalvedAfter.Take(15).Should().AllSatisfy(h => h.Should().BeFalse(), "the cascades trace whole while the step settles");
        halves.HalvedAfter[^1].Should().BeTrue("and in halves again once it has");
        for (int frame = 0; frame < halves.After.Count; frame++)
            Apart(halves.After[frame], whole.After[frame]).Past6.Should().Be(0, $"frame {frame + 1} after the step reads as the whole trace's");
    }

    // The frame in linear light as 8-bit sRGB, three bytes a pixel.
    private static byte[] Srgb(float[] linear)
    {
        var bytes = new byte[linear.Length / 4 * 3];
        for (int i = 0; i < linear.Length / 4; i++)
            for (int k = 0; k < 3; k++)
            {
                var v = Math.Clamp(linear[i * 4 + k], 0f, 1f);
                bytes[i * 3 + k] = (byte)MathF.Round(255 * (v <= 0.0031308f ? 12.92f * v : 1.055f * MathF.Pow(v, 1 / 2.4f) - 0.055f));
            }
        return bytes;
    }

    // The share of pixels whose most changed channel moved past 6 levels, in percent, and the mean
    // of that change.
    private static (double Past6, double Mean) Apart(byte[] x, byte[] y)
    {
        var (past, sum) = (0, 0L);
        for (int i = 0; i < x.Length; i += 3)
        {
            var d = Math.Max(Math.Abs(x[i] - y[i]), Math.Max(Math.Abs(x[i + 1] - y[i + 1]), Math.Abs(x[i + 2] - y[i + 2])));
            past += d > 6 ? 1 : 0;
            sum += d;
        }
        return (100.0 * past / (x.Length / 3), (double)sum / (x.Length / 3));
    }
}
