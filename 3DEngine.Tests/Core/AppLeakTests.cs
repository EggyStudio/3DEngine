using System.Diagnostics;
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
    // What the apps left, how many there were and how long they took: the process's resident
    // memory and the GC's heap after the twentieth and after the last, in megabytes, and the series
    // a failure is read by on a machine no one here has, the heap after every tenth app where it is
    // collected that often, the types that grew from the twentieth app to the last where a census
    // of the heap could be taken, and the threads the process has after every tenth shutdown.
    private sealed record Cycled(int Apps, double Seconds, double ResidentAt20, double ResidentAtLast, double HeapAt20, double HeapAtLast,
        IReadOnlyList<(int Apps, double Heap)> HeapEveryTen, string Heaps, string? Grown, string Threads, string? DeviceObjectsGrown = null)
    {
        // How far the heap's floor rose, the least of the readings from the twentieth app on in the
        // first half against the least in the second, the twentieth to the fiftieth against the
        // seventieth to the hundredth where a hundred were made. A leak raises the floor as it
        // raises every reading, where a heap that rises and falls back, as macOS's did by 6 MB
        // every thirty apps while a census found 0.25 MB more alive, leaves it where it was, and
        // the two readings it was judged by before fell on its crest and its trough by chance.
        public double FloorRise
        {
            get
            {
                var readings = HeapEveryTen.Where(r => r.Apps >= 20).ToList();
                var half = readings.Count / 2;
                return readings.TakeLast(half).Min(r => r.Heap) - readings.Take(half).Min(r => r.Heap);
            }
        }

        // A line each, the heap's first and the types after it, since the test page shows a
        // message's first five lines and cuts each at its width, which a series on one line with
        // the rest ran past.
        public string Series => $"{Environment.NewLine}{Apps} apps in {Seconds:0} seconds, the heap after every ten in MB, {Heaps}{Environment.NewLine}" +
            (Grown is null ? "" : $"grown from the twentieth app to the last, {Grown}{Environment.NewLine}") +
            $"the Vulkan objects alive after the last app beside the twentieth, {DeviceObjectsGrown ?? "none more"}{Environment.NewLine}" +
            $"the threads after every ten apps, {Threads}{Environment.NewLine}";
    }

    // How long the apps are made for, past the fiftieth, before the test stops at the next tenth.
    // build/test.py holds a test to five minutes as hung, and on a Windows runner an offscreen app
    // took four seconds, whose lavapipe compiles every shader again for each device, Mesa's disk
    // cache not working on Windows and its pipeline cache keeping nothing, where on Linux an app
    // takes half a second. A hundred are made where four minutes allow, and fifty at least, which
    // the two halves the heap is compared in need.
    private static readonly TimeSpan Budget = TimeSpan.FromMinutes(4);

    // How many more handles than the twentieth app's the process may hold after a later one. On
    // Linux the count holds at the second app's to the hundredth, and on Windows it climbed by some
    // ten an app, to 2527 by the 79th, which this fails near the fortieth.
    private const int HandleAllowance = 200;

    // Makes and closes an app of the given config a hundred times, or as many tens of them past
    // fifty as the budget allows. The first twenty warm the pools and the threads that stay for the
    // process, which the rest then should not add to. With heapEveryTen the heap is collected and
    // read after every tenth app, where otherwise it is after the twentieth and the last alone, so
    // memory that only a finalizer gives back is left to pile up between them for the resident
    // reading to see. Where E3D_GCDUMP names dotnet-gcdump, the heap's types are counted after the
    // twentieth app and the last, so a failure names the types that grew.
    private Cycled Cycle(Config config, Func<App, App>? plugins = null, bool heapEveryTen = false)
    {
        double residentAt20 = 0, heapAt20 = 0;
        var handlesAt20 = 0;
        var handlesEveryTen = new List<string>();
        // How long the app before took, which says on a runner where a hundred apps' time goes.
        var took = "";
        // The twenty-first app's handles at each step of its life, which a failure names, so a
        // handle kept for each app is placed between two steps of its making or its closing.
        var steps = new Steps();
        HeapCensus? censusAt20 = null;
        IReadOnlyDictionary<DeviceObjects.Kind, long>? objectsAt20 = null;
        string? grown = null;
        var heaps = new List<string>();
        var everyTen = new List<(int Apps, double Heap)>();
        var threads = new List<string>();
        var spent = Stopwatch.StartNew();
        for (int i = 1; i <= 100; i++)
        {
            // Printed as it goes, where the test's own output is shown only once it ends, so a test
            // host lost partway says from its last line which app it was at and what the process
            // held after the app before, its Vulkan objects and its handles, a count climbing
            // toward a limit showing before the death.
            Console.WriteLine($"[leak test] app {i} of at most 100, after the last {Held()}{took}");
            var clock = Stopwatch.StartNew();
            if (i == 21) steps.Follow();
            var app = new App(config);
            (plugins ?? (a => a.AddPlugin(new DefaultPlugins())))(app);
            var made = clock.Elapsed;
            app.BeginFrame();
            app.EndFrame();
            if (i == 21) steps.Mark("drawn");
            var drawn = clock.Elapsed;
            app.Shutdown();
            if (i == 21) steps.End();
            took = $", which took {clock.Elapsed.TotalMilliseconds:0} ms, {made.TotalMilliseconds:0} to make, with what the test draws, " +
                $"{(drawn - made).TotalMilliseconds:0} for a frame and {(clock.Elapsed - drawn).TotalMilliseconds:0} to close";
            // The handles are held from the twentieth app on, so a handle kept for each app fails
            // with its count before the process runs out of what it may hold.
            var handles = Handles();
            if (i == 20) handlesAt20 = handles;
            if (i % 10 == 0) handlesEveryTen.Add($"{i}: {handles}");
            if (i > 20)
                handles.Should().BeLessThanOrEqualTo(handlesAt20 + HandleAllowance,
                    $"a closed app gives back the handles it took, {i} apps leaving {handles} where 20 left {handlesAt20}{Environment.NewLine}" +
                    $"the 21st app's handles by step, {steps}{Environment.NewLine}" +
                    $"the handles after every ten apps, {string.Join(", ", handlesEveryTen)}{Environment.NewLine}" +
                    $"the threads after every ten apps, {string.Join(", ", threads)}{Environment.NewLine}");
            if (i % 10 != 0) continue;
            var last = i == 100 || i >= 50 && spent.Elapsed >= Budget;
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
            if (!heapEveryTen && i != 20 && !last) continue;
            // The census of the twentieth app is taken before its heap is read, so what it keeps
            // is in both readings and not in what the test compares, and the last's after.
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
            if (censusAt20 is not null && last)
            {
                var (census, failure) = HeapCensus.Take();
                grown = census is null ? $"no census, {failure}" : census.GrownSince(censusAt20, 5);
                if (census is not null) output.WriteLine($"grown from the twentieth app to the last, {census.GrownSince(censusAt20, 30)}");
            }
            if (last)
            {
                Console.WriteLine($"[leak test] {i} apps in {spent.Elapsed.TotalSeconds:0} seconds{took}");
                output.WriteLine($"{i} apps in {spent.Elapsed.TotalSeconds:0} seconds");
                return new Cycled(i, spent.Elapsed.TotalSeconds, residentAt20, resident, heapAt20, heap, everyTen, string.Join(", ", heaps), grown,
                    string.Join(", ", threads), DeviceObjects.GrownSince(objectsAt20!));
            }
        }
        throw new InvalidOperationException("unreachable");
    }

    // An app's handles at each step of its life, read where its log says the step was taken, each
    // step's change from the one before written as it comes and kept for a failure's message.
    private sealed class Steps
    {
        // The lines that mark a step, by how each begins, and the step's name.
        private static readonly (string Line, string Step)[] Marks =
        [
            ("Config {", "begun"), ("Step 1/6: Vulkan instance created", "instance made"), ("Step 4/6: Logical device created", "device made"),
            ("Graphics device initialized", "device ready"), ("ImGui initialized", "ImGui made"), ("Startup stage complete", "started"),
            ("Running the Cleanup stage", "closing"), ("Graphics device disposed", "device gone"), ("Cleanup stage complete", "closed"),
        ];

        private readonly List<string> _taken = [];
        private int _first, _last;

        public void Follow()
        {
            _first = _last = Handles();
            Logger.Heard = (_, _, message) =>
            {
                foreach (var (line, step) in Marks)
                    if (message.StartsWith(line, StringComparison.Ordinal)) Mark(step);
            };
        }

        public void Mark(string step)
        {
            var now = Handles();
            var taken = $"{step} {now - _last:+0;-0;0}";
            _taken.Add(taken);
            Console.WriteLine($"[leak test] the 21st app's handles, {taken} to {now}");
            _last = now;
        }

        // The last step, the app's threads joined, and how many it kept.
        public void End()
        {
            Logger.Heard = null;
            Mark("ended");
            _taken.Add($"{_last - _first:+0;-0;0} kept");
        }

        public override string ToString() => _taken.Count == 0 ? "not read" : string.Join(", ", _taken);
    }

    private static int Handles()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return process.HandleCount;
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
        (cycled.ResidentAtLast - cycled.ResidentAt20).Should().BeLessThan(50, $"and the process gives back what each took, {cycled.Series}");
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
        (cycled.ResidentAtLast - cycled.ResidentAt20).Should().BeLessThan(50, $"and the process gives back what each took, its pipelines included, {cycled.Series}");
    }
}

/// <summary>Tests that read the process's memory, run alone so no other test's allocations are in what they read.</summary>
[CollectionDefinition("Memory", DisableParallelization = true)]
public sealed class MemoryCollection;
