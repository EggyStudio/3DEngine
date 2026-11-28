using System.Globalization;
using FluentAssertions;
using RadianceCascades;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// A lamp switched on in the split cascades, its reach given as a box the frame changed, with their
// ways starting again from the frame once their frames disagree with the light they keep, as
// gi.split_agree asks, and with them keeping the mean of their last rays.
public sealed partial class BounceRoomsTests
{
    [NeedsGpuFact]
    [Trait("Category", "Render")]
    public void A_Lamp_Switched_On_Under_The_Split_Settles_Alike_Its_Ways_Starting_Again_Or_Keeping_Their_Mean()
    {
        // The first room at High at 320 by 180 under the split layout, its lamp out and settled,
        // then the lamp switched on, the probes' indirect light read by 32 parts of the frame, 8
        // across and 4 up, against the room's settled with the lamp lit from the start, the mean
        // of 16 frames. A frame lies as far off as its part furthest from that light by the log of
        // their ratio, over the parts holding more than a fiftieth of the brightest's. At this
        // size each way takes few rays a frame. The lamp's reach is given as a box the frame
        // changed, so each way whose rays may cross it lets go what it kept, and 32 frames after
        // the switch the room lies within 0.05 of its settled light, the ways starting again or
        // keeping their mean, the world probes' bounce the rays' hits are shaded with taking those
        // frames to settle. A device that draws on its CPU takes past the time the build gives a
        // test over those frames, and the test is skipped there.
        const int Wide = 320, Tall = 180, Across = 8, Up = 4, Kept = 16, After = 32;
        double[][] Run(bool lit, bool agree, bool switched, int read)
        {
            var config = Config.Default.WithWindow("bounce rooms", Wide, Tall) with { Headless = true, Offscreen = true, Samples = 1 };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            SetSceneField(3, 0.15f, 2);
            SetGlobalIllumination(GlobalIllumination.High);
            var settings = GetApp().World.Resource<GlobalIlluminationSettings>();
            settings.Layout = ProbeLayout.Split;
            settings.SplitAgree = agree;
            settings.Term = 1;
            var rooms = new Rooms();
            var camera = Rooms.Views[0];
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var device = (GraphicsDevice)renderer.Context.Graphics!;
            void Frame()
            {
                BeginDrawing();
                ClearBackground(Color.Black);
                BeginMode3D(camera);
                rooms.Draw(camera);
                EndMode3D();
                EndDrawing();
            }
            void Switch(bool on) => SetLightColor(rooms.Lamp, on ? Rooms.LampColor : Color.Black, on ? Rooms.LampIntensity : 0);
            Switch(lit);
            for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 240; frame++)
                Frame();
            if (switched) Switch(!lit);
            var frames = new double[read][];
            for (int f = 0; f < read; f++)
            {
                Frame();
                var light = device.ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
                var parts = new double[Across * Up];
                for (int y = 0; y < Tall; y++)
                    for (int x = 0; x < Wide; x++)
                    {
                        var p = (y * Wide + x) * 4;
                        parts[y * Up / Tall * Across + x * Across / Wide] += 0.2126 * light[p] + 0.7152 * light[p + 1] + 0.0722 * light[p + 2];
                    }
                frames[f] = parts;
            }
            rooms.Dispose();
            CloseWindow();
            UseApp(null);
            return frames;
        }

        var settled = Run(lit: true, agree: false, switched: false, Kept);
        var target = Enumerable.Range(0, Across * Up).Select(i => settled.Average(f => f[i])).ToArray();
        var shown = Enumerable.Range(0, target.Length).Where(i => target[i] > 0.02 * target.Max()).ToArray();
        double Off(double[] parts) => shown.Max(i => Math.Abs(Math.Log(Math.Max(parts[i], 1e-9) / target[i])));
        var again = Off(Run(lit: false, agree: true, switched: true, After)[^1]);
        var kept = Off(Run(lit: false, agree: false, switched: true, After)[^1]);
        var said = string.Create(CultureInfo.InvariantCulture,
            $"{After} frames after the lamp is switched on the room lies {again:0.000} off its settled light with the ways starting again and {kept:0.000} with them keeping their mean, the settled frames {settled.Max(Off):0.000} off at most");
        output.WriteLine(said);
        again.Should().BeLessThan(0.05, said);
        kept.Should().BeLessThan(0.05, said);
    }
}
