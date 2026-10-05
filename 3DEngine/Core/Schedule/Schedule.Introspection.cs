namespace Engine;

public sealed partial class Schedule
{
    /// <summary>The names of the systems registered to <paramref name="stage"/>, in the order they run.</summary>
    internal IReadOnlyList<string> SystemNames(Stage stage)
    {
        lock (_lock)
            return _systemsByStage[stage].Select(system => system.Name).ToArray();
    }

    /// <summary>Returns the number of systems registered to the given stage.</summary>
    /// <param name="stage">The <see cref="Stage"/> to query.</param>
    /// <returns>The count of systems in the specified stage.</returns>
    internal int SystemCount(Stage stage)
    {
        lock (_lock)
            return _systemsByStage[stage].Count;
    }

    /// <summary>Total number of systems across all stages.</summary>
    public int TotalSystemCount
    {
        get
        {
            lock (_lock)
            {
                int count = 0;
                foreach (var kv in _systemsByStage)
                    count += kv.Value.Count;
                return count;
            }
        }
    }
}
