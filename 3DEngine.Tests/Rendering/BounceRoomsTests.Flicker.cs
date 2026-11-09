using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The screen's probes holding their light still while the view is, which the places their rays
// leave from in turn would set flickering at surfaces whose places see light far apart.
public sealed partial class BounceRoomsTests
{
    [NeedsVulkanFact]
    [Trait("Category", "Render")]
    public void The_Strip_Rooms_Light_Holds_Still_While_The_View_Does()
    {
        // The strip's room at High at 320 by 180, settled, its frames' light read 48 frames held
        // still. The running mean alone, its rays' sixteen places held over sixteen frames, left
        // many pixels spreading past a fiftieth of their light, and with the last whole cycle of
        // places standing in for it none, so the bound is a twentieth of what the running mean
        // alone leaves.
        const int Wide = 320, Tall = 180, Read = 48;
        var config = Config.Default.WithWindow("bounce rooms", Wide, Tall) with { Headless = true, Offscreen = true, Samples = 1 };
        UseApp(new App(config).AddPlugin(new DefaultPlugins()));
        SetSceneField(3, 0.15f, 2);
        SetGlobalIllumination(GlobalIllumination.High);
        using var rooms = new Rooms();
        var camera = Rooms.Views[5];
        void Frame()
        {
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            rooms.Draw(camera);
            EndMode3D();
            EndDrawing();
        }
        for (int frame = 0; frame < SceneFieldPlan.SettleFrames + 120; frame++)
            Frame();
        var renderer = GetApp().World.Resource<Engine.Renderer>();
        var device = (GraphicsDevice)renderer.Context.Graphics!;
        var sums = new double[Wide * Tall];
        var squares = new double[Wide * Tall];
        for (int frame = 0; frame < Read; frame++)
        {
            Frame();
            var light = device.ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image);
            for (int i = 0; i < Wide * Tall; i++)
            {
                var y = 0.2126 * light[i * 4] + 0.7152 * light[i * 4 + 1] + 0.0722 * light[i * 4 + 2];
                sums[i] += y;
                squares[i] += y * y;
            }
        }
        // A pixel flickers where its light's spread over the frames is past a fiftieth of its mean.
        int flickering = 0;
        for (int i = 0; i < Wide * Tall; i++)
        {
            var mean = sums[i] / Read;
            var spread = Math.Sqrt(Math.Max(squares[i] / Read - mean * mean, 0));
            if (mean > 1e-4 && spread > 0.02 * mean)
                flickering++;
        }
        flickering.Should().BeLessThan(80, $"the screen's probes hold their light still while the view does, {flickering} of {Wide * Tall} pixels flickering");
    }
}
