using System.Diagnostics;

namespace Engine;

public sealed partial class Schedule
{
    /// <summary>
    /// Runs all systems registered to the specified stage, using parallel execution if enabled.
    /// Each system is isolated. Its exceptions are caught and logged, and the systems after it still run.
    /// </summary>
    /// <param name="stage">The <see cref="Stage"/> to execute.</param>
    /// <param name="world">The <see cref="World"/> passed to each system delegate.</param>
    internal void RunStage(Stage stage, World world)
    {
        List<SystemDescriptor> list;
        bool isParallel;
        bool stageMarkedParallel;
        int version;

        lock (_lock)
        {
            list = _systemsByStage[stage];
            if (list.Count == 0) return;
            stageMarkedParallel = _parallelStages.Contains(stage);
            isParallel = stageMarkedParallel && list.Count > 1;
            version = _version;
        }

        Logger.FrameTrace($"Stage {stage}: executing {list.Count} system(s) [{(isParallel ? "parallel" : "sequential")}]");

        var started = Stopwatch.GetTimestamp();

        var plan = PlanOf(stage, list, version, isParallel, stageMarkedParallel);
        Diagnostics.RecordBatches(stage, plan.Names, plan.Notes);
        if (isParallel)
            RunParallel(stage, plan.Batches, world);
        else
            RunSequential(stage, list, world);

        var elapsed = Stopwatch.GetElapsedTime(started);
        Diagnostics.RecordStage(stage, elapsed);
        Logger.FrameTrace($"Stage {stage}: completed in {elapsed.TotalMilliseconds:F2}ms");
    }

    /// <summary>Runs all stages in their fixed order (Startup → … → Cleanup).</summary>
    /// <param name="world">The <see cref="World"/> passed to each system delegate.</param>
    public void Run(World world)
    {
        foreach (var stage in StageOrder.AllInOrder())
            RunStage(stage, world);
    }
}
