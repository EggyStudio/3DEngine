namespace Engine;

public sealed partial class Schedule
{
    /// <summary>Returns the number of systems registered to the given stage.</summary>
    /// <param name="stage">The <see cref="Stage"/> to query.</param>
    /// <returns>The count of systems in the specified stage.</returns>
    public int SystemCount(Stage stage)
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
