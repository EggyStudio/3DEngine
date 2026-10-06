using FluentAssertions;
using Xunit.Abstractions;

namespace Engine.Tests.Core;

/// <summary>
/// An app made and closed again and again gives back what it took, which a test suite making
/// hundreds of them and a program that starts a level as a new app both need.
/// </summary>
/// <remarks>
/// Every app with the behaviors plugin read the runtime's assemblies, some 60 MB, as references for
/// a script compiler, scripts or none, into native memory that only a finalizer gave back. Little
/// was allocated on the GC's heap, so a full collection came once in dozens of apps, and the whole
/// suite reached 16 GB. The references are now read at the first compilation, once for the
/// process. Under lavapipe each device also starts a pool of threads, each given an arena of its
/// own by glibc, which keeps what is freed in it, and the Linux job asks for two arenas
/// (<c>MALLOC_ARENA_MAX=2</c>) so that a thousand drawing tests hold level.
/// </remarks>
[Collection("Memory")]
[Trait("Category", "Integration")]
public sealed class AppLeakTests(ITestOutputHelper output)
{
    // What a hundred apps left: the process's resident memory and the GC's heap after twenty and
    // after a hundred, in megabytes, and the series a failure is read by on a machine no one here
    // has, the heap after every tenth app where it is collected that often and the threads the
    // process has after each shutdown.
    private sealed record Cycled(double ResidentAt20, double ResidentAt100, double HeapAt20, double HeapAt100, string Heaps, string Threads)
    {
        public string Series => $"heap after every ten apps (MB): {Heaps}; threads after each app: {Threads}";
    }

    // Makes and closes an app of the given config a hundred times. The first twenty warm the pools
    // and the threads that stay for the process, which a hundred then should not add to. With
    // heapEveryTen the heap is collected and read after every tenth app, where otherwise it is
    // after the twentieth and the hundredth alone, so memory that only a finalizer gives back is
    // left to pile up between them for the resident reading to see.
    private Cycled Cycle(Config config, Func<App, App>? plugins = null, bool heapEveryTen = false)
    {
        double residentAt20 = 0, heapAt20 = 0;
        var heaps = new List<string>();
        var threads = new List<int>();
        for (int i = 1; i <= 100; i++)
        {
            var app = new App(config);
            (plugins ?? (a => a.AddPlugin(new DefaultPlugins())))(app);
            app.BeginFrame();
            app.EndFrame();
            app.Shutdown();
            using (var process = System.Diagnostics.Process.GetCurrentProcess()) threads.Add(process.Threads.Count);

            if (i % 10 != 0) continue;
            // Resident memory is read before any collection, since memory a closed app gives back
            // only to a finalizer is held until a full collection comes, which a program that
            // allocates little on the GC's heap may not see for hundreds of apps.
            var resident = Environment.WorkingSet / 1e6;
            output.WriteLine($"{i,3} apps: resident {resident:0} MB, {GC.CollectionCount(2)} full collections");
            if (!heapEveryTen && i != 20 && i != 100) continue;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var heap = GC.GetTotalMemory(forceFullCollection: true) / 1e6;
            heaps.Add($"{i}: {heap:0.00}");
            output.WriteLine($"{i,3} apps: heap {heap:0.00} MB");
            if (i == 20) (residentAt20, heapAt20) = (resident, heap);
            if (i == 100) return new Cycled(residentAt20, resident, heapAt20, heap, string.Join(", ", heaps), string.Join(" ", threads));
        }
        throw new InvalidOperationException("unreachable");
    }

    [Fact]
    public void A_Headless_App_Made_And_Closed_A_Hundred_Times_Leaves_Nothing_Behind()
    {
        // The heap read every ten apps, so a failure says whether it grew by a slope, an app's worth
        // at a time, or by a step the runtime took once, and the threads after each app whether a
        // closed app's threads were still alive when the heap was read.
        var cycled = Cycle(Config.Default with { Headless = true }, heapEveryTen: true);

        (cycled.HeapAt100 - cycled.HeapAt20).Should().BeLessThan(5, $"the GC's heap holds nothing of a closed app, {cycled.Series}");
        (cycled.ResidentAt100 - cycled.ResidentAt20).Should().BeLessThan(50, $"and the process gives back what each took, {cycled.Series}");
    }

    [Fact]
    public void A_Context_Captured_While_An_App_Lived_Does_Not_Keep_It_After()
    {
        // Made on a thread of its own, so the context it returns is the one thing that saw the app.
        (ExecutionContext? Context, WeakReference<App>? App) made = default;
        var thread = new Thread(() =>
        {
            var app = new App(Config.Default with { Headless = true });
            // As macOS's FileSystemWatcher captures the context when it starts watching and keeps it
            // until FSEvents lets go of the stream, which may be after the app has closed.
            made = (ExecutionContext.Capture(), new WeakReference<App>(app));
            app.Shutdown();
        });
        thread.Start();
        thread.Join();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        made.App!.TryGetTarget(out _).Should().BeFalse("a context that outlives an app holds no more of it than a way to ask whether it is alive");
        GC.KeepAlive(made.Context);
    }

    [NeedsVulkanFact]
    public void An_Offscreen_App_That_Draws_Made_And_Closed_A_Hundred_Times_Leaves_Nothing_Behind()
    {
        var config = Config.Default.WithWindow("leak", 64, 64) with { Headless = true, Offscreen = true };
        var cycled = Cycle(config, app =>
        {
            app.AddPlugin(new DefaultPlugins());
            // Shapes, text, a cube and a model, each of which makes its pipeline on the device.
            Engine3D.UseApp(app);
            var model = Engine3D.LoadModelFromMesh(Engine3D.GenMeshCube(1, 1, 1));
            for (int frame = 0; frame < 3; frame++)
            {
                Engine3D.BeginDrawing();
                Engine3D.ClearBackground(Color.RayWhite);
                Engine3D.DrawRectangle(4, 4, 20, 20, Color.Red);
                Engine3D.DrawText("leak", 4, 30, 10, Color.Black);
                Engine3D.BeginMode3D(new Camera3D(new System.Numerics.Vector3(3, 3, 3), System.Numerics.Vector3.Zero, System.Numerics.Vector3.UnitY, 45));
                Engine3D.DrawCube(System.Numerics.Vector3.Zero, 1, 1, 1, Color.Blue);
                Engine3D.DrawModel(model, System.Numerics.Vector3.UnitX, 1, Color.White);
                Engine3D.EndMode3D();
                Engine3D.EndDrawing();
            }
            Engine3D.UnloadModel(model);
            Engine3D.UseApp(null);
            return app;
        });

        (cycled.HeapAt100 - cycled.HeapAt20).Should().BeLessThan(5, $"the GC's heap holds nothing of a closed app, {cycled.Series}");
        (cycled.ResidentAt100 - cycled.ResidentAt20).Should().BeLessThan(50, $"and the process gives back what each took, its pipelines included, {cycled.Series}");
    }
}

/// <summary>Tests that read the process's memory, run alone so no other test's allocations are in what they read.</summary>
[CollectionDefinition("Memory", DisableParallelization = true)]
public sealed class MemoryCollection;
