namespace Engine;

public sealed partial class Schedule
{
    /// <summary>
    /// Thread-static "current registration source" tag. When non-null, every descriptor added
    /// through <see cref="AddSystem(Stage, SystemDescriptor)"/> (and the convenience overloads)
    /// whose own <see cref="SystemDescriptor.Source"/> is null is stamped with this value.
    /// Set/reset via <see cref="SystemRegistrationSourceScope"/>.
    /// </summary>
    [ThreadStatic] internal static string? CurrentSource;

    /// <summary>Adds a system delegate to the specified execution stage.</summary>
    /// <param name="stage">The <see cref="Stage"/> to register the system in.</param>
    /// <param name="system">The system delegate to execute when the stage runs.</param>
    /// <returns>This <see cref="Schedule"/> instance for fluent chaining.</returns>
    public Schedule AddSystem(Stage stage, SystemFn system)
    {
        var d = new SystemDescriptor(system);
        ApplyAmbientSource(d);
        lock (_lock) _systemsByStage[stage].Add(d);
        return this;
    }

    /// <summary>Adds a system with a <c>run_if</c> condition to the specified stage.</summary>
    /// <param name="stage">The <see cref="Stage"/> to register the system in.</param>
    /// <param name="system">The system delegate to execute when the stage runs.</param>
    /// <param name="runCondition">A predicate evaluated each frame; the system is skipped when <c>false</c>.</param>
    /// <returns>This <see cref="Schedule"/> instance for fluent chaining.</returns>
    public Schedule AddSystem(Stage stage, SystemFn system, Func<World, bool> runCondition)
    {
        var d = new SystemDescriptor(system) { RunCondition = runCondition };
        ApplyAmbientSource(d);
        lock (_lock) _systemsByStage[stage].Add(d);
        return this;
    }

    /// <summary>Adds a fully configured <see cref="SystemDescriptor"/> to the specified stage.</summary>
    /// <param name="stage">The <see cref="Stage"/> to register the descriptor in.</param>
    /// <param name="descriptor">A pre-configured system descriptor with name, conditions, and access metadata.</param>
    /// <returns>This <see cref="Schedule"/> instance for fluent chaining.</returns>
    public Schedule AddSystem(Stage stage, SystemDescriptor descriptor)
    {
        ApplyAmbientSource(descriptor);
        lock (_lock) _systemsByStage[stage].Add(descriptor);
        return this;
    }

    private static void ApplyAmbientSource(SystemDescriptor d)
    {
        if (d.Source is null && CurrentSource is not null)
            d.Source = CurrentSource;
    }

    /// <summary>Removes all systems matching <paramref name="predicate"/> from the specified stage.</summary>
    /// <param name="stage">The <see cref="Stage"/> to remove systems from.</param>
    /// <param name="predicate">A predicate selecting which systems to remove.</param>
    /// <returns>The number of systems removed.</returns>
    public int RemoveSystems(Stage stage, Predicate<SystemDescriptor> predicate)
    {
        lock (_lock)
            return _systemsByStage[stage].RemoveAll(predicate);
    }

    /// <summary>Removes systems tagged with the given <see cref="SystemDescriptor.Source"/> from every stage.</summary>
    /// <param name="source">The provenance tag to match (case-sensitive).</param>
    /// <returns>The total number of systems removed across all stages.</returns>
    public int RemoveSystemsBySource(string source)
    {
        int total = 0;
        lock (_lock)
        {
            foreach (var kv in _systemsByStage)
                total += kv.Value.RemoveAll(d => d.Source == source);
        }
        return total;
    }
}

/// <summary>
/// RAII scope that sets <see cref="Schedule.CurrentSource"/> for the duration of the using block.
/// Any <see cref="SystemDescriptor"/> registered through <see cref="Schedule"/> inside the scope
/// is auto-tagged with the supplied source if it does not already declare a <see cref="SystemDescriptor.Source"/>.
/// </summary>
public readonly struct SystemRegistrationSourceScope : IDisposable
{
    private readonly string? _previous;

    /// <summary>Captures the previous ambient source and overrides it with <paramref name="source"/>.</summary>
    /// <param name="source">The provenance tag to apply (e.g. <c>"Dynamic"</c>).</param>
    public SystemRegistrationSourceScope(string? source)
    {
        _previous = Schedule.CurrentSource;
        Schedule.CurrentSource = source;
    }

    /// <summary>Restores the previous ambient source.</summary>
    public void Dispose() => Schedule.CurrentSource = _previous;
}
