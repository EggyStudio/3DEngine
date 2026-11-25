using FluentAssertions;

namespace Engine.Tests.Core;

/// <summary>What a frame allocates on the managed heap once an app has settled.</summary>
// In the collection of the logger's tests, which turn per-frame logging on for a while.
[Collection("E3D")]
[Trait("Category", "Unit")]
public class FrameAllocationTests
{
    private sealed class First;
    private sealed class Second;

    [Fact]
    public void A_Frame_Of_Systems_In_Parallel_And_In_Order_With_The_Profile_Allocates_Nothing_Once_It_Has_Run()
    {
        var logged = LogConfig.PerFrameLogging;
        LogConfig.PerFrameLogging = false;
        try
        {
            var app = new App();
            app.AddPlugin(new FrameProfilePlugin());
            var runs = new int[8];
            // A parallel stage of a reader, two writers of one resource, a system that names no
            // access and one held to the main thread, a parallel stage with one skipped, and the
            // render stage, which runs its systems in order.
            app.AddSystem(Stage.Update, new SystemDescriptor(_ => runs[0]++, "Reads.First").Read<First>());
            app.AddSystem(Stage.Update, new SystemDescriptor(_ => runs[1]++, "Writes.Second").Write<Second>());
            app.AddSystem(Stage.Update, new SystemDescriptor(_ => runs[2]++, "Writes.Second.Too").Write<Second>());
            app.AddSystem(Stage.Update, new SystemDescriptor(_ => runs[3]++, "Names.Nothing"));
            app.AddSystem(Stage.Update, new SystemDescriptor(_ => runs[4]++, "Main.Thread").MainThreadOnly());
            app.AddSystem(Stage.PreUpdate, new SystemDescriptor(_ => runs[5]++, "Skipped").Read<First>().RunIf(_ => false));
            app.AddSystem(Stage.PreUpdate, new SystemDescriptor(_ => runs[5]++, "Runs").Read<First>());
            app.AddSystem(Stage.Render, new SystemDescriptor(_ => runs[6]++, "Render.One"));
            app.AddSystem(Stage.Render, new SystemDescriptor(_ => runs[7]++, "Render.Two"));
            for (int i = 0; i < 3; i++)
            {
                app.BeginFrame();
                app.EndFrame();
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++)
            {
                app.BeginFrame();
                app.EndFrame();
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            allocated.Should().Be(0, "a stage's batches are built once, the profile's names made once and a trace not wanted is not written");
            runs.Should().Equal(103, 103, 103, 103, 103, 103, 103, 103);
            var profile = app.World.Resource<FrameProfile>();
            profile.Frames.Should().Be(103);
            profile.Report().Should().Contain("system.Update.Writes.Second", "the profile still reads each system");
            app.Schedule.Diagnostics.StageBatches[Stage.Update].Should().HaveCount(4, "the reader and the first writer share a batch");
        }
        finally
        {
            LogConfig.PerFrameLogging = logged;
        }
    }
}
