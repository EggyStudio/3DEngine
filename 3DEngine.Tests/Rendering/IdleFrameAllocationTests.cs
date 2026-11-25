using FluentAssertions;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// What a window's frame allocates on the managed heap once it has settled with nothing new to
/// draw: nothing, so a program at rest makes no garbage and no collection pauses its frames.
/// </summary>
// In the collection of the logger's tests, which turn per-frame logging on for a while.
[Collection("E3D")]
public sealed class IdleFrameAllocationTests : IDisposable
{
    public void Dispose()
    {
        CloseWindow();
        UseApp(null);
    }

    private static void Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            BeginDrawing();
            ClearBackground(Color.RayWhite);
            EndDrawing();
        }
    }

    [NeedsVulkanFact]
    public void A_Window_Cleared_In_Every_Frame_Allocates_Nothing_On_Its_Thread_Once_It_Has_Settled()
    {
        var logged = LogConfig.PerFrameLogging;
        LogConfig.PerFrameLogging = false;
        try
        {
            var config = Config.Default.WithWindow("idle", 320, 180) with { Headless = true, Offscreen = true, Samples = 1 };
            UseApp(new App(config).AddPlugin(new DefaultPlugins()));
            // Settled once its first frames have made what it keeps, and the pipeline cache, saved
            // once 120 frames make no pipeline, has been written.
            Frames(600);

            var before = GC.GetAllocatedBytesForCurrentThread();
            Frames(300);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            allocated.Should().Be(0, "each frame begins the objects it keeps again, sets what is unchanged once, "
                + "makes no lambda it does not run, and copies no list it only reads");
        }
        finally
        {
            LogConfig.PerFrameLogging = logged;
        }
    }
}
