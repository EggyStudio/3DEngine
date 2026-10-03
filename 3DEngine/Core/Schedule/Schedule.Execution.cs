using System.Diagnostics;

namespace Engine;

public sealed partial class Schedule
{
    /// <summary>
    /// Runs all systems registered to the specified stage, using parallel execution if enabled.
    /// Each system is isolated - exceptions are caught, logged, and do not prevent subsequent systems from running.
    /// </summary>
    /// <param name="stage">The <see cref="Stage"/> to execute.</param>
    /// <param name="world">The <see cref="World"/> passed to each system delegate.</param>
    public void RunStage(Stage stage, World world)
    {
        List<SystemDescriptor> list;
        bool isParallel;
        bool stageMarkedParallel;

        lock (_lock)
        {
            list = _systemsByStage[stage];
            if (list.Count == 0) return;
            stageMarkedParallel = _parallelStages.Contains(stage);
            isParallel = stageMarkedParallel && list.Count > 1;
        }

        Logger.FrameTrace($"Stage {stage}: executing {list.Count} system(s) [{(isParallel ? "parallel" : "sequential")}]");

        var stageSw = Stopwatch.StartNew();

        if (isParallel)
            RunParallel(stage, list, world);
        else
            RunSequential(stage, list, world, stageMarkedParallel);

        stageSw.Stop();
        Diagnostics.RecordStage(stage, stageSw.Elapsed);
        Logger.FrameTrace($"Stage {stage}: completed in {stageSw.Elapsed.TotalMilliseconds:F2}ms");
    }

    /// <summary>Runs all stages in their fixed order (Startup → … → Cleanup).</summary>
    /// <param name="world">The <see cref="World"/> passed to each system delegate.</param>
    public void Run(World world)
    {
        foreach (var stage in StageOrder.AllInOrder())
            RunStage(stage, world);
    }
}
