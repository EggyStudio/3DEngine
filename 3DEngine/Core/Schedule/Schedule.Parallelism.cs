namespace Engine;

public sealed partial class Schedule
{
    /// <summary>Marks a stage for parallel execution (default). Pass <c>false</c> to run single-threaded.</summary>
    /// <param name="stage">The <see cref="Stage"/> to configure.</param>
    /// <param name="parallel"><c>true</c> (default) for parallel execution; <c>false</c> for sequential.</param>
    /// <returns>This <see cref="Schedule"/> instance for fluent chaining.</returns>
    public Schedule SetParallel(Stage stage, bool parallel = true)
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
    public Schedule SetSingleThreaded(Stage stage, bool singleThreaded = true) => 
        SetParallel(stage, !singleThreaded);
}
