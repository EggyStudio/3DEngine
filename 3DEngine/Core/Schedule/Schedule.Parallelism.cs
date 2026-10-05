namespace Engine;

public sealed partial class Schedule
{
    private static int _parallelBatches;

    /// <summary>
    /// Whether any schedule is running a batch of systems on several threads, the one time the
    /// engine's own code can be called from two threads at once.
    /// </summary>
    internal static bool RunningInParallel => Volatile.Read(ref _parallelBatches) > 0;

    /// <summary>Marks a stage for parallel execution (default). Pass <c>false</c> to run single-threaded.</summary>
    /// <param name="stage">The <see cref="Stage"/> to configure.</param>
    /// <param name="parallel"><c>true</c> (default) for parallel execution; <c>false</c> for sequential.</param>
    /// <returns>This <see cref="Schedule"/> instance for fluent chaining.</returns>
    internal Schedule SetParallel(Stage stage, bool parallel = true)
    {
        lock (_lock)
        {
            if (parallel) 
                _parallelStages.Add(stage); 
            else 
                _parallelStages.Remove(stage);
        }
        return this;
    }

    /// <summary>Marks a stage to run systems sequentially. Pass <c>false</c> to restore parallel execution.</summary>
    /// <param name="stage">The <see cref="Stage"/> to configure.</param>
    /// <param name="singleThreaded"><c>true</c> (default) for sequential execution; <c>false</c> for parallel.</param>
    /// <returns>This <see cref="Schedule"/> instance for fluent chaining.</returns>
    internal Schedule SetSingleThreaded(Stage stage, bool singleThreaded = true) => 
        SetParallel(stage, !singleThreaded);
}
