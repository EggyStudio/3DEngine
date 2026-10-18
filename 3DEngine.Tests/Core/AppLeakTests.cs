using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using FluentAssertions;
using Vortice.Vulkan;
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
    // What the apps left, how many there were and how long they took, the process's resident
    // memory and the GC's live heap after the twentieth and after the last, in megabytes, and the
    // series a failure is read by on a machine no one here has, the resident memory after every
    // tenth app, the heap after every tenth where it is collected that often, the types that grew
    // from the twentieth app to the last where a census of the heap could be taken, and the threads
    // the process has after every tenth shutdown.
    private sealed record Cycled(int Apps, double Seconds, double ResidentAt20, double ResidentAtLast, double HeapAt20, double HeapAtLast,
        IReadOnlyList<(int Apps, double Heap)> HeapEveryTen, string Heaps, string Residents, double? ResidentBeforeCensus, string? Grown, string Threads, string? DeviceObjectsGrown = null,
        string? Stages = null, string? Regions = null, string? Allocations = null)
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

        // A line each, since the test page shows a message's first five lines and cuts each at its
        // width, which a series on one line with the rest ran past.
        // The resident memory's series says whether a growth is steady, an allocation each app makes
        // and its closing does not give back, or a step taken once.
        // The heap and the series after every ten apps come first, then the kinds of region and
        // what each step took, which name where a climb lies, and the private data the followed
        // apps' steps left comes last, cut so the whole fits the 4,096 characters the test page
        // keeps of a message, as the handle failure's steps are. On 1398ed24's run the regions and
        // the four apps' allocation lines standing first left the series and the heaps among the
        // lines the page leaves out (REVIEW.md, Verdict 64).
        public string Series
        {
            get
            {
                var head = Environment.NewLine +
                    $"{Apps} apps in {Seconds:0} seconds, the heap's live objects after every ten in MB, {Heaps}{Environment.NewLine}" +
                    $"{ResidentName} after every ten in MB, what the collector held committed in brackets, and on Windows what the process's heaps " +
                    $"held allocated of what they committed and how many there were, {Residents}{Environment.NewLine}" +
                    (Regions is null ? "" : $"what the process held committed by the kind of region holding it, at the twentieth app and the last, {Regions}{Environment.NewLine}") +
                    $"{ResidentName} each step of an app's life took in MB, {Stages ?? "not read"}{Environment.NewLine}" +
                    (ResidentBeforeCensus is { } before
                        ? $"the twentieth's {before:0.0} MB before its heap census and {ResidentAt20:0.0} after, the baseline{Environment.NewLine}"
                        : "") +
                    (Grown is null ? "" : $"grown from the twentieth app to the last, {Grown}{Environment.NewLine}") +
                    $"the Vulkan objects alive after the last app beside the twentieth, {DeviceObjectsGrown ?? "none more"}{Environment.NewLine}" +
                    $"the threads after every ten apps, {Threads}{Environment.NewLine}";
                if (string.IsNullOrEmpty(Allocations)) return head;
                var allocations = $"the private data each step of a followed app added, by the base of its allocation, an app a line{Environment.NewLine}{Allocations}";
                var room = Math.Max(1, 3500 - head.Length);
                return head + (allocations.Length <= room ? allocations : allocations[..(room - 1)] + "…") + Environment.NewLine;
            }
        }
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
    // ten an app, to 2527 by the 79th, which this fails near the fortieth. On Windows lavapipe's
    // build starts nine unnamed threads as the device is made and leaves the handles of four of
    // them open after they end, a handle a thread, Mesa's leak and not the engine's, and the hold
    // allows on top of this as many for each app past the twentieth as the census of the followed
    // apps finds kept. Counted before by the net of the handle counts across the driver's steps,
    // they came to 1 where the census showed 4, since the engine's own threads close their handles
    // in those steps too (REVIEW.md, Verdict 68).
    private const int HandleAllowance = 200;

    // The steps the driver opens its threads' handles at, whose handles kept of ended unnamed
    // threads the hold allows for each app past the twentieth. The device's making and its disposal
    // are each split into several steps, so the memory each step takes is named, and every one of
    // them is the driver's as the step it was split from was.
    private static readonly HashSet<string> DriverSteps =
    [
        "before the instance", "instance made", "physical device chosen", "device made", "drawn",
        "cleaned up to the device", "VkDevice destroyed", "instance destroyed", "device gone",
    ];

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
        double? residentBeforeCensus = null;
        var handlesAt20 = 0;
        var handlesEveryTen = new List<string>();
        // How long the app before took, which says on a runner where a hundred apps' time goes.
        var took = "";
        // The handles of the 21st, 31st, 41st and 51st apps at each step of their lives, which a
        // failure names, so a handle kept for each app is placed between two steps of its making or
        // its closing, read over several apps since what one app kept swung from 2 to 5 between
        // runs on Windows.
        var steps = new Dictionary<int, Steps>();
        // The memory each step of an app's life took, read for every app past the twentieth.
        var stages = new Stages();
        // The most Thread handles of ended unnamed threads, opened at the driver's steps, that a
        // followed app kept, which the hold allows for each app past the twentieth.
        var driverThreads = 0;
        HeapCensus? censusAt20 = null;
        ProcessRegions? regionsAt20 = null;
        string? regionsGrown = null;
        IReadOnlyDictionary<DeviceObjects.Kind, long>? objectsAt20 = null;
        string? grown = null;
        var heaps = new List<string>();
        var everyTen = new List<(int Apps, double Heap)>();
        var threads = new List<string>();
        var residents = new List<string>();
        var spent = Stopwatch.StartNew();
        for (int i = 1; i <= 100; i++)
        {
            // Printed as it goes, where the test's own output is shown only once it ends, so a test
            // host lost partway says from its last line which app it was at and what the process
            // held after the app before, its Vulkan objects and its handles, a count climbing
            // toward a limit showing before the death.
            Console.WriteLine($"[leak test] app {i} of at most 100, after the last {Held()}{took}");
            var clock = Stopwatch.StartNew();
            var followed = i is 21 or 31 or 41 or 51 ? steps[i] = new Steps(i, stages) : i > 20 ? new Steps(i, stages, handles: false) : null;
            followed?.Follow();
            var app = new App(config);
            (plugins ?? (a => a.AddPlugin(new DefaultPlugins())))(app);
            var made = clock.Elapsed;
            app.BeginFrame();
            app.EndFrame();
            followed?.Mark("drawn");
            var drawn = clock.Elapsed;
            app.Shutdown();
            followed?.End();
            // What a followed app's steps added that still stands once the app after it has ended.
            if (steps.TryGetValue(i - 1, out var before)) before.AfterNext();
            took = $", which took {clock.Elapsed.TotalMilliseconds:0} ms, {made.TotalMilliseconds:0} to make, with what the test draws, " +
                $"{(drawn - made).TotalMilliseconds:0} for a frame and {(clock.Elapsed - drawn).TotalMilliseconds:0} to close";
            // The handles are held from the twentieth app on, so a handle kept for each app fails
            // with its count before the process runs out of what it may hold. On Windows each
            // count, the twentieth's and every later one, is read once the app's threads have
            // ended and what they held is given back, as the census's last step waits for, since
            // read while some were still out the twentieth's stood 44 apps' kept handles above a
            // settled count and a later app's 338 above.
            if (OperatingSystem.IsWindows())
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            var handles = Handles();
            if (i == 20) handlesAt20 = handles;
            if (i % 10 == 0) handlesEveryTen.Add($"{i}: {handles}");
            if (followed is not null) driverThreads = Math.Max(driverThreads, followed.DriverThreads);
            if (i > 20)
                handles.Should().BeLessThanOrEqualTo(handlesAt20 + HandleAllowance + driverThreads * (i - 20),
                    HandlesFailure(i, handles, handlesAt20, driverThreads, handlesEveryTen, steps, threads));
            if (i % 10 != 0) continue;
            var last = i == 100 || i >= 50 && spent.Elapsed >= Budget;
            using (var process = System.Diagnostics.Process.GetCurrentProcess()) threads.Add($"{i}: {process.Threads.Count}");
            // Resident memory is read before any collection, since memory a closed app gives back
            // only to a finalizer is held until a full collection comes, which a program that
            // allocates little on the GC's heap may not see for hundreds of apps.
            var resident = Resident();
            // What the collector holds committed, read beside it, so a climb of the process's own is
            // told from the collector's regions, which it gives back over time rather than at once.
            var committed = GC.GetGCMemoryInfo().TotalCommittedBytes / 1e6;
            residents.Add($"{i}: {resident:0.0} ({committed:0.0}{ProcessMemory.Heaps()})");
            output.WriteLine($"{i,3} apps: resident {resident:0} MB, the collector committing {committed:0} MB, {GC.CollectionCount(2)} full collections");
            // The Vulkan objects every device made and none destroyed, which a closed app's device
            // should leave none of, so a native growth is told from the driver's own.
            var objects = DeviceObjects.Now();
            output.WriteLine($"{i,3} apps: alive {Held()}");
            if (i == 20) objectsAt20 = objects;
            if (!heapEveryTen && i != 20 && !last) continue;
            // The census of the twentieth app is taken before its heap is read, so what it keeps
            // is in both readings and not in what the test compares, and the last's after. The
            // resident memory is read again once it is taken, and that is the baseline the last
            // reading is held to, since dotnet-gcdump attached to the process leaves some 60 MB in
            // its working set for the rest of the run, and read before it, the baseline put the
            // census and the creep over eighty apps together, 75 to 88 MB against the bound of 50
            // on Windows' runners, where the creep alone was 20 to 35.
            if (HeapCensus.Available && i == 20)
            {
                (censusAt20, var failure) = HeapCensus.Take();
                if (failure is not null) grown = $"no census, {failure}";
                residentBeforeCensus = resident;
                resident = Resident();
            }
            // On Windows, the committed memory by the kind of region holding it, at the twentieth app
            // beside its baseline and at the last beside its reading.
            if (i == 20) regionsAt20 = ProcessRegions.Sweep();
            if (last && regionsAt20 is not null) regionsGrown = ProcessRegions.Sweep()?.GrownSince(regionsAt20);
            // The heap read as the bytes the last full blocking collection kept, its live objects.
            // GC.GetTotalMemory counts with them what the collector left on the heap where it did
            // not compact, which on macOS swung by 6 MB between collections of the same live set,
            // and on 32ce6f4e's run one trough of it among flat readings raised the floor by 6 MB
            // (REVIEW.md, Verdict 69). BevyCSharp's soak judges the same figure.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var heap = GC.GetGCMemoryInfo(GCKind.FullBlocking).PromotedBytes / 1e6;
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
                Console.WriteLine($"[leak test] {ResidentName} each step of an app's life took in MB, {stages}");
                output.WriteLine($"{i} apps in {spent.Elapsed.TotalSeconds:0} seconds");
                output.WriteLine($"{ResidentName} each step of an app's life took in MB, {stages}");
                if (regionsGrown is not null)
                {
                    Console.WriteLine($"[leak test] the memory committed by the kind of region holding it, {regionsGrown}");
                    output.WriteLine($"the memory committed by the kind of region holding it, {regionsGrown}");
                }
                return new Cycled(i, spent.Elapsed.TotalSeconds, residentAt20, resident, heapAt20, heap, everyTen, string.Join(", ", heaps), string.Join(", ", residents), residentBeforeCensus, grown,
                    string.Join(", ", threads), DeviceObjects.GrownSince(objectsAt20!), stages.ToString(), regionsGrown,
                    string.Join(Environment.NewLine, steps.Values.Select(s => s.Allocations).OfType<string>()));
            }
        }
        throw new InvalidOperationException("unreachable");
    }

    // The process's memory in megabytes, as the bound reads it. On Windows it is the private bytes
    // the process has committed, its commit charge, which GetProcessMemoryInfo answers, and not its
    // working set, which Windows trims when it likes. On a runner the drawing test's working set
    // swung 60 to 90 MB between neighboring readings with the heap flat and no Vulkan object alive,
    // and a twentieth reading that fell in a trough failed the bound with nothing kept. BevyCSharp's
    // memory guard reads the same figure for the same reason. Elsewhere it is the resident set,
    // which the system trims only when memory runs short.
    private static double Resident() => OperatingSystem.IsWindows() ? ProcessMemory.PrivateBytes() : Environment.WorkingSet / 1e6;

    // The handle bound's message. The counts after every ten apps and what each followed app kept,
    // by kind and its Thread handles by the thread each refers to, come first, then the threads, then
    // each followed app's steps, cut so the whole fits the 4,096 characters the test page keeps of a
    // message, where on 4c6630a4's run the steps standing first pushed the series past it and the
    // test's name with them (REVIEW.md, Verdict 68).
    private static string HandlesFailure(int apps, int handles, int at20, int driverThreads, List<string> everyTen,
        Dictionary<int, Steps> steps, List<string> threads)
    {
        var head = $"a closed app gives back the handles it took, {apps} apps leaving {handles} where 20 left {at20}, " +
                   $"{driverThreads} Thread handles an app allowed as the driver's{Environment.NewLine}" +
                   $"the handles after every ten apps, {string.Join(", ", everyTen)}{Environment.NewLine}" +
                   $"what each followed app kept, {string.Join("; ", steps.Values.Select(s => s.Kept).OfType<string>())}{Environment.NewLine}" +
                   $"the threads after every ten apps, {string.Join(", ", threads)}{Environment.NewLine}";
        var bySteps = $"the handles by step, {string.Join("; ", steps.Values)}";
        var room = Math.Max(1, 3500 - head.Length);
        return head + (bySteps.Length <= room ? bySteps : bySteps[..(room - 1)] + "…") + Environment.NewLine;
    }

    // What Resident reads, by its name on this system, for the series a failure is read by.
    private static string ResidentName => OperatingSystem.IsWindows() ? "the private bytes committed" : "the resident memory";

    // An app's memory at each step of its life, read where its log says the step was taken and added
    // to the stages, and with handles, its handles at each step, each step's change from the one
    // before written as it comes and kept for a failure's message.
    private sealed class Steps(int app, Stages stages, bool handles = true)
    {
        // The lines that mark a step, by how each begins, and the step's name. Between the device's
        // making and ImGui's every plugin the app builds is a step of its own, by the line that says
        // it was built, since what was read as ImGui's making, 328 handles on Windows, was every
        // plugin built after the device, the renderer's 35 shaders, the physics' workers and the
        // behaviors' compiler among them, ImGui's own context last. ImGui's font atlas, with its
        // upload's staging buffer and fence, and its pipeline are made in the first frame that
        // draws it, so they split the frame's step where the app draws a window.
        private static readonly (string Line, string Step)[] Marks =
        [
            ("Config {", "begun"), ("Step 1/6: Vulkan instance created", "instance made"), ("Step 4/6: Logical device created", "device made"),
            ("Graphics device initialized", "device ready"), ("Renderer initialized", "renderer's shaders loaded"),
            ("SdlImGuiPlugin: Creating ImGui context", "before ImGui"), ("ImGui initialized", "ImGui made"), ("Startup stage complete", "started"),
            ("ImGui font atlas uploaded", "ImGui's atlas uploaded"), ("ImGui pipeline created", "ImGui's pipeline made"),
            ("Running the Cleanup stage", "closing"), ("Graphics device disposed", "device gone"), ("Cleanup stage complete", "closed"),
            // The device's making and disposal split finer, so the loader's reading of its layers and
            // drivers as the instance is made, the driver's listing of its devices as one is chosen,
            // and the instance's destruction after the device's each have a step, and SDL's start and
            // quit where a window is made.
            ("Step 1/6: Creating Vulkan instance", "before the instance"), ("Step 3/6: Physical device selected", "physical device chosen"),
            ("Disposing graphics device", "cleaned up to the device"), ("VkDevice destroyed", "VkDevice destroyed"),
            ("VkInstance destroyed", "instance destroyed"), ("SDL3 initialized successfully", "SDL started"),
            ("SDL window destroyed and SDL subsystems shut down", "SDL quit"),
        ];

        private const string Built = "Plugin built: ";

        private readonly List<string> _taken = [];
        private int _first, _last;
        // The handles by kind as the app began and at its last step, on Windows, against which each
        // step's and those the app kept are named.
        private Dictionary<string, int> _kindsFirst = [], _kindsLast = [];
        private double _memory;
        private int _threads;
        // On Windows, the Thread handles as the app began and as its last step left them, each with
        // the thread it refers to, and the step that opened each handle seen opened.
        private Dictionary<IntPtr, HandleCensus.ThreadSeen> _threadsFirst = [], _threadsLast = [];
        private readonly Dictionary<IntPtr, string> _openedAt = [];
        // On Linux, the threads by id with their names as the last step left them.
        private Dictionary<int, string> _namesLast = [];
        // On Windows, for an app whose handles are followed, the private data by the base of each
        // allocation as the last step left it, and the allocations each step added, by their base,
        // with the step and their kilobytes, then which of them stood at the app's end and once the
        // app after it had ended.
        private Dictionary<IntPtr, double>? _data;
        private readonly Dictionary<IntPtr, (string Step, double Kilobytes)> _added = [];
        private readonly List<string> _adding = [];
        private HashSet<IntPtr>? _atEnd, _afterNext;

        // On Windows, the Thread handles the app kept of ended unnamed threads, opened at the
        // driver's steps, which lavapipe's build leaves open (HandleAllowance).
        public int DriverThreads { get; private set; }

        public void Follow()
        {
            if (handles)
            {
                _first = _last = Handles();
                (_kindsFirst, _threadsFirst) = HandleCensus.Take();
                (_kindsLast, _threadsLast) = (_kindsFirst, _threadsFirst);
                _namesLast = ProcessMemory.ThreadNames();
                _data = ProcessRegions.PrivateData();
            }
            _threads = ProcessMemory.Threads();
            _memory = Resident();
            stages.Begun(_memory);
            Logger.Heard = (_, _, message) =>
            {
                foreach (var (line, step) in Marks)
                    if (message.StartsWith(line, StringComparison.Ordinal)) Mark(step);
                if (message.StartsWith(Built, StringComparison.Ordinal))
                    Mark($"{message[Built.Length..].Split(' ')[0]} built");
            };
        }

        public void Mark(string step)
        {
            var memory = Resident();
            stages.Add(step, memory - _memory);
            _memory = memory;
            if (!handles) return;
            if (_data is not null && ProcessRegions.PrivateData() is { } data)
            {
                foreach (var (allocation, kilobytes) in data.Where(d => !_data.ContainsKey(d.Key)))
                {
                    _added[allocation] = (step, kilobytes);
                    if (!_adding.Contains(step)) _adding.Add(step);
                }
                _data = data;
            }
            var now = Handles();
            var taken = $"{step} {now - _last:+0;-0;0}";
            // On Windows, the kinds the step changed, so the step that opens a thread's handle and
            // the one that closes it, or none, are named.
            if (OperatingSystem.IsWindows())
            {
                var (kinds, threadHandles) = HandleCensus.Take();
                if (HandleCensus.Change(_kindsLast, kinds) is { Length: > 0 } change) taken += $" ({change})";
                // The threads whose handles the step opened, by name, so a step that starts threads
                // says whose they are.
                var opened = threadHandles.Where(t => !_threadsLast.ContainsKey(t.Key)).ToList();
                foreach (var (handle, _) in opened) _openedAt[handle] = step;
                if (opened.Count > 0) taken += $" [opened {Threads(opened.Select(t => t.Value))}]";
                (_kindsLast, _threadsLast) = (kinds, threadHandles);
            }
            else if (OperatingSystem.IsLinux())
            {
                // The threads the step started and ended, by name, which Linux reads without handles.
                var names = ProcessMemory.ThreadNames();
                var started = names.Where(n => !_namesLast.ContainsKey(n.Key)).Select(n => n.Value).ToList();
                var ended = _namesLast.Where(n => !names.ContainsKey(n.Key)).Select(n => n.Value).ToList();
                if (started.Count > 0) taken += $" [started {Named(started)}]";
                if (ended.Count > 0) taken += $" [ended {Named(ended)}]";
                _namesLast = names;
            }
            _taken.Add(taken);
            Console.WriteLine($"[leak test] app {app}'s handles, {taken} to {now}");
            _last = now;
        }

        // The last steps, the app shut down, then what the finalizers give back once a full
        // collection has run them, and how many it kept. Read before the finalizers, as it was,
        // the app kept 337 handles on Windows where the count after every ten apps grew by five an
        // app, the rest still held by safe handles nothing referred to.
        public void End()
        {
            Logger.Heard = null;
            Mark("shut down");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Mark("ended");
            // The app's threads waited for, half a second at most, so the last reading is taken once
            // they have ended and their stacks are given back. Read as the finalizers had run, each
            // app on Windows kept 3.2 MB where the reading after every tenth app climbed by 0.9 an
            // app, the rest given back after an app's last step and before the next app's first.
            var waited = Stopwatch.StartNew();
            while (ProcessMemory.Threads() > _threads && waited.Elapsed < ThreadWait) Thread.Sleep(10);
            if (ProcessMemory.Threads() > _threads) stages.Outlived();
            Mark("threads ended");
            stages.Ended(_memory);
            if (!handles) return;
            _atEnd = Standing();
            _taken.Add($"{_last - _first:+0;-0;0} kept");
            if (OperatingSystem.IsWindows())
            {
                var (now, threadHandles) = HandleCensus.Take();
                var kinds = HandleCensus.Change(_kindsFirst, now);
                // The Thread handles the app kept, those it holds now that it did not as it began, or
                // that refer to another thread, by the thread each refers to and the step that opened
                // it, so a handle kept after its thread ended is told from a thread still running.
                var held = threadHandles.Where(t => !_threadsFirst.TryGetValue(t.Key, out var before) || before.Id != t.Value.Id)
                    .Select(t => (Step: _openedAt.GetValueOrDefault(t.Key, "a step not seen"), Thread: t.Value))
                    .ToList();
                DriverThreads = held.Count(h => h.Thread.Ended && h.Thread.Name.Length == 0 && DriverSteps.Contains(h.Step));
                var kept = held.GroupBy(h => (h.Step, h.Thread.Named))
                    .Select(g => $"{g.Count()} {g.Key.Named}, opened at {g.Key.Step}")
                    .ToList();
                Kept = $"app {app} by kind {(kinds.Length == 0 ? "none" : kinds)}, its Thread handles {(kept.Count == 0 ? "none" : string.Join(", ", kept))}";
                _taken.Add($"by kind {(kinds.Length == 0 ? "none" : kinds)}");
                Console.WriteLine($"[leak test] {Kept}");
            }
        }

        /// <summary>On Windows, what the app kept by kind, and its Thread handles by thread and the step that opened each.</summary>
        public string? Kept { get; private set; }

        // Names counted, as "3 '.NET TP Worker', 1 'dotnet'".
        private static string Named(IEnumerable<string> names) =>
            string.Join(", ", names.GroupBy(n => n).Select(g => $"{g.Count()} '{g.Key}'"));

        // Threads by name and whether they have ended, as "3 unnamed running, 1 '.NET TP Worker' running".
        private static string Threads(IEnumerable<HandleCensus.ThreadSeen> threads) =>
            string.Join(", ", threads.GroupBy(t => t.Named).Select(g => $"{g.Count()} {g.Key}"));

        public override string ToString() => $"app {app}: {(_taken.Count == 0 ? "not read" : string.Join(", ", _taken))}";

        // The allocations the steps added that stand now.
        private HashSet<IntPtr>? Standing() =>
            ProcessRegions.PrivateData() is { } data ? _added.Keys.Where(data.ContainsKey).ToHashSet() : null;

        public void AfterNext()
        {
            _afterNext = Standing();
            if (Allocations is { } allocations) Console.WriteLine($"[leak test] the private data its steps added, {allocations}");
        }

        // By the step that added them, the allocations' count and sizes, and how many stood at the
        // app's end and once the app after it had ended, so the step whose allocations stay names
        // their owner by what it does and the sizes the allocator that made them.
        public string? Allocations
        {
            get
            {
                if (_data is null) return null;
                var standing = _afterNext ?? [];
                var kept = _added.Where(a => standing.Contains(a.Key)).ToList();
                // The steps whose allocations stood at the app's end or after the next, each named,
                // and the rest counted together, so four apps' lines fit the page.
                var stood = _adding.Where(step => _added.Any(a => a.Value.Step == step && _atEnd?.Contains(a.Key) == true)).ToList();
                var others = _added.Where(a => !stood.Contains(a.Value.Step)).ToList();
                var bySteps = stood.Select(step =>
                {
                    var added = _added.Where(a => a.Value.Step == step).ToList();
                    var sizes = string.Join(", ", added.GroupBy(a => Math.Round(a.Value.Kilobytes)).OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
                        .Select(g => $"{g.Count()} of {g.Key:#,0} KB"));
                    var atEnd = added.Count(a => _atEnd?.Contains(a.Key) == true);
                    var after = added.Where(a => standing.Contains(a.Key)).ToList();
                    return $"{step} added {added.Count} ({sizes}), {atEnd} standing at the app's end, {after.Count} once the next had ended " +
                           $"({after.Sum(a => a.Value.Kilobytes):#,0} KB)";
                });
                var rest = $"{_adding.Count - stood.Count} other steps added {others.Count} of {others.Sum(a => a.Value.Kilobytes):#,0} KB, none standing at the app's end";
                return $"app {app}, {kept.Count} allocations of {kept.Sum(a => a.Value.Kilobytes):#,0} KB standing once the next app had ended" +
                       $"{(_afterNext is null ? ", which was not read" : "")}, by the step that added them, {string.Join("; ", bySteps.Append(rest))}";
            }
        }
    }

    // What each step of an app's life took of the process's memory, as Resident reads it, summed
    // over every app past the twentieth and averaged, in the order the steps first came, so a failure
    // names the step whose memory the closing does not give back. On Windows each drawing app kept
    // some 1 MB, with the collector's memory and the heap flat, beside a held instance as without
    // one (REVIEW.md, Verdict 64). A step that took less than 0.05 MB an app either way is counted
    // among the rest, so the line names the steps that matter. What changes between one app's last
    // reading and the next app's first is a step of its own, so the steps sum to the climb of the
    // reading after every tenth app.
    private sealed class Stages
    {
        private readonly List<string> _order = [];
        private readonly Dictionary<string, double> _taken = [];
        private int _apps, _outlived;
        private double? _ended;

        public void Add(string step, double megabytes)
        {
            lock (_taken)
            {
                if (!_taken.TryGetValue(step, out var sum)) _order.Add(step);
                _taken[step] = sum + megabytes;
            }
        }

        // The memory as an app begins, against the last reading of the app before.
        public void Begun(double megabytes)
        {
            if (_ended is { } ended) Add("between apps", megabytes - ended);
        }

        public void Ended(double megabytes)
        {
            Interlocked.Increment(ref _apps);
            _ended = megabytes;
        }

        // An app whose threads had not all ended by the wait's end.
        public void Outlived() => Interlocked.Increment(ref _outlived);

        public override string ToString()
        {
            lock (_taken)
            {
                if (_apps == 0) return "not read";
                var each = _order.Select(step => (Step: step, Taken: _taken[step] / _apps)).ToList();
                var named = each.Where(e => Math.Abs(e.Taken) >= 0.05).Select(e => $"{e.Step} {e.Taken:+0.00;-0.00;0.00}");
                var rest = each.Where(e => Math.Abs(e.Taken) < 0.05).Sum(e => e.Taken);
                var outlived = _outlived == 0 ? "" : $", the threads of {_outlived} apps still running {ThreadWait.TotalSeconds:0.#} seconds after their apps ended";
                return $"averaged over the {_apps} apps after the twentieth, {string.Join(", ", named)}, the rest {rest:+0.00;-0.00;0.00}, " +
                    $"{each.Sum(e => e.Taken):+0.00;-0.00;0.00} kept{outlived}";
            }
        }
    }

    // How long an app's threads are waited for once it has ended, which a thread already told to end
    // takes far less than. On Linux the threads of two apps in eighty outlived a wait of two seconds,
    // and on Windows, where an app takes four seconds, a wait of two for each would hold fifty apps
    // past the five minutes build/test.py allows a test.
    private static readonly TimeSpan ThreadWait = TimeSpan.FromSeconds(0.5);

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
        var cycled = Draw();

        (cycled.ResidentAtLast - cycled.ResidentAt20).Should().BeLessThan(50, $"and the process gives back what each took, its pipelines included, {cycled.Series}");
    }

    // The same hundred apps beside a Vulkan instance the test holds open throughout, so the loader
    // keeps the driver it loaded for it, lavapipe with LLVM inside it, where otherwise it lets the
    // driver go with each app's instance and loads it again for the next, both with the validation
    // layer off, whose unloading with each app kept about a megabyte on Windows (Draw, below).
    // Beyond the test above, this one measures the driver kept loaded through the hundred apps, so
    // its private bytes are said and not judged, and it judges the heap's floor and the Vulkan
    // objects as that test does. On 1398ed24's run on Windows the test above held within its 50 MB,
    // and here the private bytes rose 59.7 MB from the twentieth app to the hundredth with no
    // allocation a followed app's steps added standing once the next app had ended, the growth
    // lying inside allocations that stood before, the driver's state kept across apps or the
    // readings' swing, and none of the engine's (REVIEW.md, Verdict 64).
    [NeedsVulkanFact]
    public void An_Offscreen_App_That_Draws_Made_And_Closed_A_Hundred_Times_Beside_A_Held_Instance_Leaves_Nothing_Behind()
    {
        using var held = new HeldInstance();
        var cycled = Draw();

        var said = $"beside the held driver {ResidentName} moved by {cycled.ResidentAtLast - cycled.ResidentAt20:+0.0;-0.0;0.0} MB " +
                   $"from the twentieth app to the last, said and not judged";
        Console.WriteLine($"[leak test] {said}");
        output.WriteLine($"{said}{cycled.Series}");
    }

    // A Vulkan instance made through the loader the engine's devices use, as the engine's instance
    // is made but with no surface, no debug messages and no layer, as the apps beside it are made.
    // MoltenVK on macOS is a portability driver, which the loader gives an instance only to a caller
    // that asks for portability drivers, by the extension and the flag, where the loader offers
    // them. An instance that asked for nothing was answered ErrorIncompatibleDriver there.
    private sealed unsafe class HeldInstance : IDisposable
    {
        private readonly VkInstanceApi _api;

        public HeldInstance()
        {
            Vulkan.vkInitialize().CheckResult();
            var portability = Encoding.UTF8.GetString(Vulkan.VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME).TrimEnd('\0');
            var portable = Offered(portability);
            using var extensions = new VkStringArray(portable ? [portability] : new List<string>());
            VkApplicationInfo application = new() { apiVersion = VkVersion.Version_1_3 };
            VkInstanceCreateInfo info = new()
            {
                pApplicationInfo = &application,
                enabledExtensionCount = extensions.Length,
                ppEnabledExtensionNames = extensions,
                flags = portable ? VkInstanceCreateFlags.EnumeratePortabilityKHR : VkInstanceCreateFlags.None,
            };
            Vulkan.vkCreateInstance(&info, null, out var instance).CheckResult();
            _api = new VkInstanceApi(instance);
        }

        public void Dispose() => _api.vkDestroyInstance();

        private static bool Offered(string name)
        {
            if (Vulkan.vkEnumerateInstanceExtensionProperties(out uint count) != VkResult.Success || count == 0) return false;
            var properties = new VkExtensionProperties[(int)count];
            if (Vulkan.vkEnumerateInstanceExtensionProperties(properties) != VkResult.Success) return false;
            for (int i = 0; i < (int)count; i++)
                fixed (byte* offered = properties[i].extensionName)
                    if (Marshal.PtrToStringUTF8((nint)offered) == name) return true;
            return false;
        }
    }

    // The apps are made with the validation layer off, in the Debug build as well, on every system,
    // so the test reads the engine and the driver and not the layer. The loader loads the layer with
    // each app's instance and unloads it as the instance is destroyed, and the layer's Windows build
    // at 1.4.363.0 allocates through mimalloc, which keeps what it reserved when its library goes.
    // Under it each app on Windows kept about a megabyte of private data the process took from the
    // system directly, with the heaps and the collector flat, and on 351cc461's run the test beside
    // an instance holding the layer loaded passed where this one, the layer let go with each app,
    // failed (REVIEW.md, Verdict 64). The test that requires the layer under E3D_REQUIRE_VALIDATION
    // makes devices of its own.
    private Cycled Draw()
    {
        var config = Config.Default.WithWindow("leak", 64, 64) with { Headless = true, Offscreen = true };
        GraphicsDevice.LeaveValidationOff = true;
        try
        {
            return DrawApps(config);
        }
        finally
        {
            GraphicsDevice.LeaveValidationOff = false;
        }
    }

    // The apps drawn, their heap's floor and their Vulkan objects judged, and what they came to
    // returned for the process's memory to be judged or said by the test.
    private Cycled DrawApps(Config config)
    {
        // The heap read every ten apps, as the headless test reads it, so a failure says whether it
        // grew an app's worth at a time or in one step.
        var cycled = Cycle(config, heapEveryTen: true, plugins: app =>
        {
            app.AddPlugin(new DefaultPlugins());
            // Shapes, text, a cube and a model, each of which makes its pipeline on the device.
            E3D.UseApp(app);
            var model = E3D.LoadModelFromMesh(E3D.GenMeshCube(1, 1, 1));
            for (int frame = 0; frame < 3; frame++)
            {
                E3D.BeginDrawing();
                E3D.ClearBackground(Color.RayWhite);
                E3D.DrawRectangle(4, 4, 20, 20, Color.Red);
                E3D.DrawText("leak", 4, 30, 10, Color.Black);
                E3D.BeginMode3D(new Camera3D(new System.Numerics.Vector3(3, 3, 3), System.Numerics.Vector3.Zero, System.Numerics.Vector3.UnitY, 45));
                E3D.DrawCube(System.Numerics.Vector3.Zero, 1, 1, 1, Color.Blue);
                E3D.DrawModel(model, System.Numerics.Vector3.UnitX, 1, Color.White);
                E3D.EndMode3D();
                E3D.EndDrawing();
            }
            E3D.UnloadModel(model);
            E3D.UseApp(null);
            return app;
        });

        cycled.FloorRise.Should().BeLessThan(5, $"the GC's heap holds nothing of a closed app, {cycled.Series}");
        cycled.DeviceObjectsGrown.Should().BeNull($"no Vulkan object a closed app made outlives its device, {cycled.Series}");
        return cycled;
    }
}

/// <summary>Tests that read the process's memory, run alone so no other test's allocations are in what they read.</summary>
[CollectionDefinition("Memory", DisableParallelization = true)]
public sealed class MemoryCollection;
