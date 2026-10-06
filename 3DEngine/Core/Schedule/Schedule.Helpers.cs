using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Engine;

public sealed partial class Schedule
{
    /// <summary>Executes systems sequentially within a single stage, recording diagnostics for each.</summary>
    /// <param name="stage">The stage being executed.</param>
    /// <param name="systems">The list of system descriptors to run.</param>
    /// <param name="world">The shared world instance.</param>
    /// <param name="stageMarkedParallel">Whether the stage was originally marked for parallel execution.</param>
    private void RunSequential(Stage stage, List<SystemDescriptor> systems, World world, bool stageMarkedParallel)
    {
        var sequentialReason = stageMarkedParallel
            ? (systems.Count <= 1 ? "single-system-stage" : "serialized-by-conflicts")
            : "stage-configured-single-threaded";

        var batches = systems.Select(s => new List<SystemDescriptor> { s }).ToList();
        var notes = batches.Select(_ => (IReadOnlyList<string>)new[] { sequentialReason }).ToList();
        Diagnostics.RecordBatches(stage, batches);
        Diagnostics.RecordBatchNotes(stage, notes);

        var span = CollectionsMarshal.AsSpan(systems);
        for (int i = 0; i < span.Length; i++)
        {
            ref var desc = ref span[i];

            if (desc.RunCondition is { } cond && !cond(world))
            {
                Logger.FrameTrace($"  ⏭ {desc.Name} skipped (run condition false)");
                continue;
            }

            Invoke(stage, desc, world);
        }
    }

    /// <summary>
    /// Builds execution batches from resource access metadata and runs them in parallel.
    /// Main-thread systems form their own single-item batch.
    /// </summary>
    /// <param name="stage">The stage being executed.</param>
    /// <param name="systems">The list of system descriptors to partition and run.</param>
    /// <param name="world">The shared world instance.</param>
    private void RunParallel(Stage stage, List<SystemDescriptor> systems, World world)
    {
        WarnForMissingAccessMetadata(stage, systems);

        // Build execution batches where systems can safely run together.
        // Main-thread systems form their own single-item batch and flush pending parallel work.
        var batches = BuildExecutionBatches(systems, out var notes);
        Diagnostics.RecordBatches(stage, batches);
        Diagnostics.RecordBatchNotes(stage, notes);

        for (int i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            var mode = batch.Count == 1 || batch[0].Affinity == ThreadAffinity.MainThread ? "sequential" : "parallel";
            Logger.FrameTrace($"  batch {i + 1}/{batches.Count} [{mode}] => {string.Join(", ", batch.Select(d => d.Name))}");
        }

        foreach (var batch in batches)
        {
            if (batch.Count == 1 || Light(batch))
            {
                foreach (var desc in batch) ExecuteSystem(stage, desc, world);
                continue;
            }

            Interlocked.Increment(ref _parallelBatches);
            try
            {
                Parallel.ForEach(batch, desc => ExecuteSystem(stage, desc, world));
            }
            finally
            {
                Interlocked.Decrement(ref _parallelBatches);
            }
        }
    }

    /// <summary>The longest a batch's systems may have taken together last time and still run on the calling thread.</summary>
    /// <remarks>
    /// Handing a batch to the thread pool costs tens of microseconds when the pool is idle, and the
    /// calling thread waits for the tasks it queued even once it has run every system itself. While
    /// a level's files load, those tasks queued behind the loads, and a batch of five systems taking
    /// microseconds took 20 to 47 milliseconds.
    /// </remarks>
    internal const double SequentialBatchMilliseconds = 0.5;

    private static bool Light(List<SystemDescriptor> batch)
    {
        double total = 0;
        foreach (var desc in batch) total += desc.LastMilliseconds;
        return total < SequentialBatchMilliseconds;
    }

    /// <summary>
    /// Partitions systems into execution batches where systems within a batch have no
    /// conflicting resource access and can safely run in parallel.
    /// </summary>
    /// <remarks>
    /// A system joins the first batch after the last one holding a system it conflicts with and
    /// after the last main-thread system added before it, so a system that reads what an earlier
    /// one writes runs after it, and the order systems were added in holds wherever it matters.
    /// </remarks>
    /// <param name="systems">The systems to partition.</param>
    /// <param name="notes">
    /// When this method returns, contains per-batch notes describing conflict reasons
    /// or placement markers (e.g., <c>"main-thread-only"</c>).
    /// </param>
    /// <returns>A list of batches, each containing non-conflicting system descriptors.</returns>
    private static List<List<SystemDescriptor>> BuildExecutionBatches(List<SystemDescriptor> systems, out List<IReadOnlyList<string>> notes)
    {
        var batches = new List<List<SystemDescriptor>>();
        var batchNotes = new List<List<string>>();

        foreach (var desc in systems)
        {
            if (desc.Affinity == ThreadAffinity.MainThread)
            {
                batches.Add([desc]);
                batchNotes.Add(["main-thread-only"]);
                continue;
            }

            // The earliest batch it may join, past every batch it must follow.
            var earliest = 0;
            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                if (batch.Count == 1 && batch[0].Affinity == ThreadAffinity.MainThread)
                {
                    earliest = i + 1;
                    continue;
                }
                for (int j = 0; j < batch.Count; j++)
                {
                    if (desc.TryGetConflictReason(batch[j], out var reason))
                    {
                        earliest = i + 1;
                        if (!batchNotes[i].Contains(reason))
                            batchNotes[i].Add(reason);
                        break;
                    }
                }
            }

            if (earliest < batches.Count)
                batches[earliest].Add(desc);
            else
            {
                batches.Add([desc]);
                batchNotes.Add([]);
            }
        }

        notes = batchNotes.Select(n => (IReadOnlyList<string>)n.ToArray()).ToList();
        return batches;
    }

    /// <summary>
    /// Emits a one-time warning for systems that lack explicit <see cref="SystemDescriptor.Read{T}"/>/<see cref="SystemDescriptor.Write{T}"/>
    /// metadata in a parallel stage. Such systems are conservatively serialized.
    /// </summary>
    /// <param name="stage">The stage being checked.</param>
    /// <param name="systems">The systems to inspect.</param>
    private void WarnForMissingAccessMetadata(Stage stage, List<SystemDescriptor> systems)
    {
        for (int i = 0; i < systems.Count; i++)
        {
            var desc = systems[i];
            if (desc.HasExplicitAccess || desc.Affinity == ThreadAffinity.MainThread)
                continue;

            var key = $"{stage}:{desc.Name}";
            lock (_lock)
            {
                if (!_missingAccessWarnings.Add(key))
                    continue;
            }

            Logger.Warn($"System '{desc.Name}' in stage {stage} has no Read/Write metadata; scheduler is using conservative conflict mode.");
        }
    }

    /// <summary>
    /// Executes a single system with run-condition checking, timing, and exception isolation.
    /// </summary>
    /// <param name="stage">The stage context for logging.</param>
    /// <param name="desc">The system descriptor to execute.</param>
    /// <param name="world">The shared world instance.</param>
    private void ExecuteSystem(Stage stage, SystemDescriptor desc, World world)
    {
        if (desc.RunCondition is { } cond && !cond(world))
        {
            Logger.FrameTrace($"  ⏭ {desc.Name} skipped (run condition false)");
            return;
        }

        Invoke(stage, desc, world);
    }

    // Runs one system on this thread, timed, with the change tick it runs at, so a Changed filter
    // in it sees what was written since it last ran however often that is (ChangeTicks).
    private void Invoke(Stage stage, SystemDescriptor desc, World world)
    {
        var outer = ChangeTicks.Enter(desc.LastRunTick, out var tick);
        var sw = Stopwatch.StartNew();
        try
        {
            desc.System(world);
        }
        catch (Exception ex)
        {
            ReportThrown(stage, desc, ex);
        }
        finally
        {
            ChangeTicks.Leave(outer);
            desc.LastRunTick = tick;
        }
        sw.Stop();
        desc.LastMilliseconds = sw.Elapsed.TotalMilliseconds;
        Diagnostics.RecordSystem(stage, desc.Name, sw.Elapsed);
    }

    // How often each system has thrown each type of exception in each stage. A system that throws
    // in every frame wrote its trace sixty times a second, into a player's log file as into a
    // test's, so the first of each is logged whole and the rest are counted. The type is held by its
    // name, since a type a script defines would keep the script's load context from unloading.
    private readonly Dictionary<(Stage Stage, string System, string Exception), long> _thrown = [];

    // Logs an exception a system threw: whole the first time that system throws that type in that
    // stage, and after that a line at the 10th, the 100th, the 1,000th and so on. The whole line
    // names the assembly the system's code is in, so a system that reached an app it was not
    // written for, as a probe one test compiled once did, says where it came from.
    private void ReportThrown(Stage stage, SystemDescriptor desc, Exception ex)
    {
        var system = desc.Name;
        long count;
        lock (_thrown)
        {
            var key = (stage, system, ex.GetType().FullName ?? ex.GetType().Name);
            _thrown.TryGetValue(key, out count);
            _thrown[key] = ++count;
        }
        if (count == 1)
            Logger.Error($"System '{system}' from {desc.System.Method.Module.Assembly.GetName().Name} threw in stage {stage}", ex);
        else if (IsPowerOfTen(count))
            Logger.Error($"System '{system}' has thrown {ex.GetType().Name} in stage {stage} {count:N0} times, the last: {ex.Message}");
    }

    private static bool IsPowerOfTen(long count)
    {
        while (count >= 10 && count % 10 == 0) count /= 10;
        return count == 1;
    }

    /// <summary>
    /// Logs, for each system that threw the same type in a stage more than once, how many times in
    /// all, which the app does as it closes.
    /// </summary>
    internal void ReportThrownTotals()
    {
        KeyValuePair<(Stage Stage, string System, string Exception), long>[] thrown;
        lock (_thrown) thrown = [.. _thrown.Where(entry => entry.Value > 1)];
        foreach (var ((stage, system, type), count) in thrown)
            Logger.Error($"System '{system}' threw {type[(type.LastIndexOf('.') + 1)..]} in stage {stage} {count:N0} times in all");
    }
}
