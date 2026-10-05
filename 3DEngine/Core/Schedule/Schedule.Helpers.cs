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
    public const double SequentialBatchMilliseconds = 0.5;

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
            Logger.Error($"System '{desc.Name}' threw in stage {stage}", ex);
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
}
