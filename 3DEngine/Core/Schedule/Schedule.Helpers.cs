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
                Logger.FrameTrace($"  ⏭ {desc.Name} - skipped (run condition false)");
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                desc.System(world);
            }
            catch (Exception ex)
            {
                Logger.Error($"System '{desc.Name}' threw in stage {stage}", ex);
            }
            sw.Stop();
            Diagnostics.RecordSystem(stage, desc.Name, sw.Elapsed);
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
            if (batch.Count == 1)
            {
                ExecuteSystem(stage, batch[0], world);
                continue;
            }

            Parallel.ForEach(batch, desc => ExecuteSystem(stage, desc, world));
        }
    }

    /// <summary>
    /// Partitions systems into execution batches where systems within a batch have no
    /// conflicting resource access and can safely run in parallel.
    /// </summary>
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

            var placed = false;
            for (int i = 0; i < batches.Count; i++)
            {
                var batch = batches[i];
                if (batch.Count == 1 && batch[0].Affinity == ThreadAffinity.MainThread)
                    continue;

                var conflict = false;
                for (int j = 0; j < batch.Count; j++)
                {
                    if (desc.TryGetConflictReason(batch[j], out var reason))
                    {
                        conflict = true;
                        if (!batchNotes[i].Contains(reason))
                            batchNotes[i].Add(reason);
                        break;
                    }
                }

                if (conflict)
                    continue;

                batch.Add(desc);
                placed = true;
                break;
            }

            if (!placed)
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
            Logger.FrameTrace($"  ⏭ {desc.Name} - skipped (run condition false)");
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            desc.System(world);
        }
        catch (Exception ex)
        {
            Logger.Error($"System '{desc.Name}' threw in stage {stage}", ex);
        }
        sw.Stop();
        Diagnostics.RecordSystem(stage, desc.Name, sw.Elapsed);
    }
}
