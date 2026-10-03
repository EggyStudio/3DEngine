namespace Engine;

/// <summary>
/// Tracks per-stage and per-system execution timing for the most recent frame.
/// Populated by <see cref="Schedule"/> during execution and queryable for diagnostics and debugging HUDs.
/// </summary>
/// <remarks>
/// All public accessors return snapshot copies to avoid data races with the schedule's recording thread.
/// This class is thread-safe for concurrent reads and writes.
/// </remarks>
/// <seealso cref="Schedule"/>
public sealed class ScheduleDiagnostics
{
    private readonly Lock _lock = new();
    private readonly Dictionary<Stage, TimeSpan> _stageTimes = new();
    private readonly Dictionary<(Stage Stage, string System), TimeSpan> _systemTimes = new();
    private readonly Dictionary<Stage, List<IReadOnlyList<string>>> _stageBatches = new();
    private readonly Dictionary<Stage, List<IReadOnlyList<string>>> _stageBatchNotes = new();

    /// <summary>Records the total duration of a stage execution.</summary>
    /// <param name="stage">The stage that was executed.</param>
    /// <param name="elapsed">The wall-clock duration of the stage.</param>
    internal void RecordStage(Stage stage, TimeSpan elapsed)
    {
        lock (_lock) _stageTimes[stage] = elapsed;
    }

    /// <summary>Records the duration of a single system execution.</summary>
    /// <param name="stage">The stage containing the system.</param>
    /// <param name="systemName">The human-readable system name.</param>
    /// <param name="elapsed">The wall-clock duration of the system.</param>
    internal void RecordSystem(Stage stage, string systemName, TimeSpan elapsed)
    {
        lock (_lock) _systemTimes[(stage, systemName)] = elapsed;
    }

    /// <summary>Records the scheduler batch plan used for a parallel stage execution.</summary>
    /// <param name="stage">The stage whose batches are being recorded.</param>
    /// <param name="batches">The list of execution batches, each containing system descriptors.</param>
    internal void RecordBatches(Stage stage, List<List<SystemDescriptor>> batches)
    {
        lock (_lock)
        {
            _stageBatches[stage] = batches
                .Select(batch => (IReadOnlyList<string>)batch.Select(desc => desc.Name).ToArray())
                .ToList();
        }
    }

    /// <summary>Records conflict/placement notes for each computed batch.</summary>
    /// <param name="stage">The stage whose batch notes are being recorded.</param>
    /// <param name="notes">Per-batch notes describing conflict reasons or placement markers.</param>
    internal void RecordBatchNotes(Stage stage, List<IReadOnlyList<string>> notes)
    {
        lock (_lock)
            _stageBatchNotes[stage] = notes.ToList();
    }

    /// <summary>Returns the last recorded duration for the specified stage.</summary>
    /// <param name="stage">The stage to query.</param>
    /// <returns>The duration of the last execution, or <see cref="TimeSpan.Zero"/> if never recorded.</returns>
    public TimeSpan GetStageDuration(Stage stage)
    {
        lock (_lock) return _stageTimes.GetValueOrDefault(stage);
    }

    /// <summary>Returns the last recorded duration for a named system in a stage.</summary>
    /// <param name="stage">The stage containing the system.</param>
    /// <param name="systemName">The human-readable name of the system.</param>
    /// <returns>The duration of the last execution, or <see cref="TimeSpan.Zero"/> if never recorded.</returns>
    public TimeSpan GetSystemDuration(Stage stage, string systemName)
    {
        lock (_lock) return _systemTimes.GetValueOrDefault((stage, systemName));
    }

    /// <summary>Snapshot of all stage durations from the last frame.</summary>
    /// <returns>A dictionary mapping each executed <see cref="Stage"/> to its wall-clock duration.</returns>
    public IReadOnlyDictionary<Stage, TimeSpan> StageDurations
    {
        get { lock (_lock) return new Dictionary<Stage, TimeSpan>(_stageTimes); }
    }

    /// <summary>Snapshot of all per-system durations from the last frame.</summary>
    /// <returns>A dictionary mapping (stage, system name) pairs to their wall-clock durations.</returns>
    public IReadOnlyDictionary<(Stage Stage, string System), TimeSpan> SystemDurations
    {
        get { lock (_lock) return new Dictionary<(Stage, string), TimeSpan>(_systemTimes); }
    }

    /// <summary>Snapshot of stage batch composition from the most recent frame.</summary>
    /// <returns>A dictionary mapping each stage to its list of execution batches (each batch is a list of system names).</returns>
    public IReadOnlyDictionary<Stage, IReadOnlyList<IReadOnlyList<string>>> StageBatches
    {
        get
        {
            lock (_lock)
            {
                return _stageBatches.ToDictionary(
                    kv => kv.Key,
                    kv => (IReadOnlyList<IReadOnlyList<string>>)kv.Value.Select(batch => (IReadOnlyList<string>)batch.ToArray()).ToArray());
            }
        }
    }

    /// <summary>Snapshot of scheduler batch notes (conflicts/main-thread markers) from the most recent frame.</summary>
    /// <returns>A dictionary mapping each stage to per-batch note lists explaining scheduling decisions.</returns>
    public IReadOnlyDictionary<Stage, IReadOnlyList<IReadOnlyList<string>>> StageBatchNotes
    {
        get
        {
            lock (_lock)
            {
                return _stageBatchNotes.ToDictionary(
                    kv => kv.Key,
                    kv => (IReadOnlyList<IReadOnlyList<string>>)kv.Value.Select(noteList => (IReadOnlyList<string>)noteList.ToArray()).ToArray());
            }
        }
    }

    /// <summary>Clears all recorded timings, batch compositions, and notes.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _stageTimes.Clear();
            _systemTimes.Clear();
            _stageBatches.Clear();
            _stageBatchNotes.Clear();
        }
    }
}
