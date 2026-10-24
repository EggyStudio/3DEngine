using FluentAssertions;

namespace Engine.Tests.Core;

/// <summary>
/// An app built with no window and stepped a frame a call through the calls a game's test reaches
/// in the package, in the E3D collection since the default plugins hold Dear ImGui's one context.
/// </summary>
[Collection("E3D")]
[Trait("Category", "Unit")]
public sealed class AppSteppingTests
{
    // A count a system below moves on each frame, and the time it has taken, as a behavior's fields.
    private struct Counter
    {
        public int Frames;
        public double Seconds;
    }

    [Fact]
    public void A_Test_Builds_An_App_With_No_Window_And_Steps_Its_Systems_A_Frame_At_A_Time()
    {
        // Through the calls a game's test reaches in the package, an app of the default plugins
        // with no window and frames a tenth of a second long, a system as a behavior's
        // registration adds one, and a frame a call.
        using var app = new App(Config.Default with { Headless = true, FrameSeconds = 0.1 }).AddPlugin(new DefaultPlugins());
        var started = 0;
        app.AddSystem(Stage.Startup, _ => started++);
        app.AddSystem(Stage.Update, world =>
        {
            var ecs = world.Resource<EcsWorld>();
            var seconds = world.Resource<Time>().DeltaSeconds;
            foreach (var (entity, _) in ecs.Query<Counter>())
            {
                ref var counter = ref ecs.GetRef<Counter>(entity);
                counter.Frames++;
                counter.Seconds += seconds;
            }
        });
        var ecs = app.World.Resource<EcsWorld>();
        var counted = ecs.Spawn();
        ecs.Add(counted, new Counter());

        for (int frame = 1; frame <= 3; frame++)
        {
            app.Frame();
            ecs.GetRef<Counter>(counted).Frames.Should().Be(frame, "each call runs one frame's systems");
            ecs.GetRef<Counter>(counted).Seconds.Should().BeApproximately(frame * 0.1, 1e-9, "each frame, the first among them, is the tenth of a second the config gives");
        }
        started.Should().Be(1, "the first frame runs Startup, once");
    }
}
