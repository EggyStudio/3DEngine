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
    // has, the heap after every tenth app where it is collected that often, the types that grew
    // from the twentieth app to the hundredth where a census of the heap could be taken, and the
    // threads the process has after every tenth shutdown.
    private sealed record Cycled(double ResidentAt20, double ResidentAt100, double HeapAt20, double HeapAt100, IReadOnlyList<(int Apps, double Heap)> HeapEveryTen,
        string Heaps, string? Grown, string Threads, string? DeviceObjectsGrown = null)
    {
        // How far the heap's floor rose, the least of the readings from the twentieth app to the
        // fiftieth against the least from the seventieth to the hundredth. A leak raises the floor
        // as it raises every reading, where a heap that rises and falls back, as macOS's did by 6 MB
        // every thirty apps while a census found 0.25 MB more alive, leaves it where it was, and
        // the two readings it was judged by before fell on its crest and its trough by chance.
        public double FloorRise =>
            HeapEveryTen.Where(r => r.Apps >= 70).Min(r => r.Heap) - HeapEveryTen.Where(r => r.Apps is >= 20 and <= 50).Min(r => r.Heap);

        // A line each, the heap's first and the types after it, since the test page shows a
        // message's first five lines and cuts each at its width, which a series on one line with
        // the rest ran past.
        public string Series => $"{Environment.NewLine}the heap after every ten apps in MB, {Heaps}{Environment.NewLine}" +
            (Grown is null ? "" : $"grown from the twentieth app to the hundredth, {Grown}{Environment.NewLine}") +
            $"the Vulkan objects alive after the hundredth app beside the twentieth, {DeviceObjectsGrown ?? "none more"}{Environment.NewLine}" +
            $"the threads after every ten apps, {Threads}{Environment.NewLine}";
    }

    // Makes and closes an app of the given config a hundred times. The first twenty warm the pools
    // and the threads that stay for the process, which a hundred then should not add to. With
    // heapEveryTen the heap is collected and read after every tenth app, where otherwise it is
    // after the twentieth and the hundredth alone, so memory that only a finalizer gives back is
    // left to pile up between them for the resident reading to see. Where E3D_GCDUMP names
    // dotnet-gcdump, the heap's types are counted after the twentieth app and the hundredth, so a
    // failure names the types that grew.
    private Cycled Cycle(Config config, Func<App, App>? plugins = null, bool heapEveryTen = false)
    {
        double residentAt20 = 0, heapAt20 = 0;
        HeapCensus? censusAt20 = null;
        IReadOnlyDictionary<DeviceObjects.Kind, long>? objectsAt20 = null;
        string? grown = null;
        var heaps = new List<string>();
        var everyTen = new List<(int Apps, double Heap)>();
        var threads = new List<string>();
        for (int i = 1; i <= 100; i++)
        {
            // Printed as it goes, where the test's own output is shown only once it ends, so a test
            // host lost partway says from its last line which app it was at and what the process
            // held after the app before, its Vulkan objects and its handles, a count climbing
            // toward a limit showing before the death.
            Console.WriteLine($"[leak test] app {i} of 100, after the last {Held()}");
            var app = new App(config);
            (plugins ?? (a => a.AddPlugin(new DefaultPlugins())))(app);
            app.BeginFrame();
            app.EndFrame();
            app.Shutdown();
            if (i % 10 != 0) continue;
            using (var process = System.Diagnostics.Process.GetCurrentProcess()) threads.Add($"{i}: {process.Threads.Count}");
            // Resident memory is read before any collection, since memory a closed app gives back
            // only to a finalizer is held until a full collection comes, which a program that
            // allocates little on the GC's heap may not see for hundreds of apps.
            var resident = Environment.WorkingSet / 1e6;
            output.WriteLine($"{i,3} apps: resident {resident:0} MB, {GC.CollectionCount(2)} full collections");
            // The Vulkan objects every device made and none destroyed, which a closed app's device
            // should leave none of, so a native growth is told from the driver's own.
            var objects = DeviceObjects.Now();
            output.WriteLine($"{i,3} apps: alive {Held()}");
            if (i == 20) objectsAt20 = objects;
            if (!heapEveryTen && i != 20 && i != 100) continue;
            // The census of the twentieth app is taken before its heap is read, so what it keeps
            // is in both readings and not in what the test compares, and the hundredth's after.
            if (HeapCensus.Available && i == 20)
            {
                (censusAt20, var failure) = HeapCensus.Take();
                if (failure is not null) grown = $"no census, {failure}";
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var heap = GC.GetTotalMemory(forceFullCollection: true) / 1e6;
            heaps.Add($"{i}: {heap:0.00}");
            everyTen.Add((i, heap));
            output.WriteLine($"{i,3} apps: heap {heap:0.00} MB");
            if (i == 20) (residentAt20, heapAt20) = (resident, heap);
            if (censusAt20 is not null && i == 100)
            {
                var (census, failure) = HeapCensus.Take();
                grown = census is null ? $"no census, {failure}" : census.GrownSince(censusAt20, 5);
                if (census is not null) output.WriteLine($"grown from the twentieth app to the hundredth, {census.GrownSince(censusAt20, 30)}");
            }
            if (i == 100)
                return new Cycled(residentAt20, resident, heapAt20, heap, everyTen, string.Join(", ", heaps), grown, string.Join(", ", threads),
                    DeviceObjects.GrownSince(objectsAt20!));
        }
        throw new InvalidOperationException("unreachable");
    }

    // The Vulkan objects alive, the process's handles, and on Windows its GDI and USER objects,
    // which a process may hold ten thousand of each.
    private static string Held()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        var gui = OperatingSystem.IsWindows()
            ? $", {GetGuiResources(process.Handle, 0)} GDI objects, {GetGuiResources(process.Handle, 1)} USER objects"
            : "";
        return $"{string.Join(", ", DeviceObjects.Now().Select(o => $"{o.Value} {o.Key}"))}, {process.HandleCount} handles{gui}";
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);

    [Fact]
    public void A_Headless_App_Made_And_Closed_A_Hundred_Times_Leaves_Nothing_Behind()
    {
        // The heap read every ten apps, so a failure says whether it grew by a slope, an app's worth
        // at a time, or by a step the runtime took once, and the threads after each app whether a
        // closed app's threads were still alive when the heap was read.
        var cycled = Cycle(Config.Default with { Headless = true }, heapEveryTen: true);

        cycled.FloorRise.Should().BeLessThan(5, $"the GC's heap holds nothing of a closed app, {cycled.Series}");
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
        // The heap read every ten apps, as the headless test reads it, so a failure says whether it
        // grew an app's worth at a time or in one step.
        var cycled = Cycle(config, heapEveryTen: true, plugins: app =>
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

        cycled.FloorRise.Should().BeLessThan(5, $"the GC's heap holds nothing of a closed app, {cycled.Series}");
        cycled.DeviceObjectsGrown.Should().BeNull($"no Vulkan object a closed app made outlives its device, {cycled.Series}");
        (cycled.ResidentAt100 - cycled.ResidentAt20).Should().BeLessThan(50, $"and the process gives back what each took, its pipelines included, {cycled.Series}");
    }
}

/// <summary>Tests that read the process's memory, run alone so no other test's allocations are in what they read.</summary>
[CollectionDefinition("Memory", DisableParallelization = true)]
public sealed class MemoryCollection;
