using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

// The probes' pipelines made together on workers at the first frame that traces them.
public sealed partial class BounceRoomsTests
{
    [NeedsVulkanFact]
    public void The_Probes_Pipelines_Are_Made_Together_And_The_First_Frame_Is_Drawn_As_When_Made_One_After_Another()
    {
        // The Cornell box's first frame at High, its probes' pipelines made one after another and
        // then together. The first wait in the driver lets go once a second compile reaches it, so
        // made together at least two are in the driver at once, and the frame drawn from them is the
        // frame drawn from those made one after another.
        (byte[] Frame, int MostAtOnce) First(bool together)
        {
            GlobalIlluminationStages.MadeTogether = together;
            var config = Config.Default.WithWindow("together", 160, 90) with { Headless = true, Offscreen = true, Samples = 1 };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            SetSceneField(4, 0.15f);
            SetGlobalIllumination(GlobalIllumination.High);
            var rooms = new Rooms();
            var renderer = GetApp().World.Resource<Engine.Renderer>();
            var device = (GraphicsDevice)renderer.Context.Graphics!;
            var (inside, most, frames) = (0, 0, Environment.CurrentManagedThreadId);
            using var second = new ManualResetEventSlim();
            // A compile on a worker waits for a second to reach the driver beside it, and one on the
            // frame's thread, as every other pass's first pipelines are made, waits for none.
            device.WhileMakingPipeline = () =>
            {
                var now = Interlocked.Increment(ref inside);
                InterlockedMost(ref most, now);
                if (now >= 2) second.Set();
                else if (Environment.CurrentManagedThreadId != frames) second.Wait(TimeSpan.FromSeconds(30));
                Interlocked.Decrement(ref inside);
            };
            var camera = Rooms.Views[0];
            BeginDrawing();
            ClearBackground(Color.Black);
            BeginMode3D(camera);
            rooms.Draw(camera);
            EndMode3D();
            EndDrawing();
            device.WhileMakingPipeline = null;
            var frame = Srgb(device.ReadFloats(renderer.RenderWorld.TryGet<BloomRenderer>()!.Decoded!.ColorView.Image));
            rooms.Dispose();
            CloseWindow();
            UseApp(null);
            return (frame, most);
        }

        try
        {
            var (apart, oneAtATime) = First(together: false);
            var (made, together) = First(together: true);
            oneAtATime.Should().Be(1, "made one after another, one compile is in the driver at a time");
            together.Should().BeGreaterThanOrEqualTo(2, "made together, the probes' compiles are in the driver at once");
            made.Should().Equal(apart, "the first frame is drawn from the same pipelines however they were made");
        }
        finally
        {
            GlobalIlluminationStages.MadeTogether = true;
        }
    }

    private static void InterlockedMost(ref int most, int value)
    {
        for (var seen = Volatile.Read(ref most); value > seen; seen = Volatile.Read(ref most))
            if (Interlocked.CompareExchange(ref most, value, seen) == seen) return;
    }
}
