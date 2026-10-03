namespace Engine;

/// <summary>
/// Wraps a <see cref="SystemFn"/> with a human-readable name, optional run condition,
/// thread affinity, and resource access metadata for the parallel scheduler.
/// </summary>
/// <remarks>
/// <para>Used internally by <see cref="Schedule"/> and exposed via <see cref="ScheduleDiagnostics"/>.</para>
/// <para>
/// Resource access metadata (<see cref="Read{T}"/>/<see cref="Write{T}"/>) enables the scheduler to
/// build parallel execution batches. Systems without explicit metadata are treated conservatively
/// and serialized to prevent data races.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var desc = new SystemDescriptor(MySystem, "Physics.Integrate")
///     .Read&lt;Time&gt;()
///     .Write&lt;EcsWorld&gt;()
///     .RunIf(world => world.Resource&lt;GameState&gt;().IsPlaying);
/// </code>
/// <code>
/// app.AddSystem(Stage.Update, desc);
/// </code>
/// </example>
/// <seealso cref="Schedule"/>
/// <seealso cref="SystemFn"/>
public sealed class SystemDescriptor
{
    /// <summary>Human-readable label inferred from the delegate or set explicitly.</summary>
    public string Name { get; }

    /// <summary>The system delegate to invoke when this descriptor is executed.</summary>
    public SystemFn System { get; }

    /// <summary>Optional predicate - when set, the system only runs if this returns <c>true</c>.</summary>
    public Func<World, bool>? RunCondition { get => _runCondition; init => _runCondition = value; }
    private Func<World, bool>? _runCondition;

    /// <summary>Thread affinity for this system. Defaults to <see cref="ThreadAffinity.Any"/>.</summary>
    public ThreadAffinity Affinity { get; private set; } = ThreadAffinity.Any;

    /// <summary>
    /// Optional provenance tag identifying the source of this descriptor (e.g. <c>"Static"</c>,
    /// <c>"Dynamic"</c>, or a custom hot-reload bucket name). Used by
    /// <c>App.RemoveSystemsBySource</c> to bulk-remove a generation of systems on hot-reload.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>Resource types this system declares as read-only dependencies.</summary>
    public IReadOnlyCollection<Type> Reads => _reads;

    /// <summary>Resource types this system declares as read-write dependencies.</summary>
    public IReadOnlyCollection<Type> Writes => _writes;

    /// <summary>
    /// <c>true</c> when this system declares at least one explicit resource read/write dependency.
    /// Systems without explicit access are treated as broad writers by the parallel scheduler.
    /// </summary>
    public bool HasExplicitAccess => _reads.Count > 0 || _writes.Count > 0;

    private readonly HashSet<Type> _reads = [];
    private readonly HashSet<Type> _writes = [];

    /// <summary>Creates a new system descriptor wrapping the specified delegate.</summary>
    /// <param name="system">The system delegate to execute.</param>
    /// <param name="name">
    /// Optional human-readable name. When <c>null</c>, inferred from the delegate's
    /// declaring type and method name (e.g., <c>"MyBehavior.Update"</c>).
    /// </param>
    public SystemDescriptor(SystemFn system, string? name = null)
    {
        System = system;
        Name = name ?? InferName(system);
    }

    /// <summary>Infers a human-readable name from the delegate's method metadata.</summary>
    /// <param name="system">The system delegate to inspect.</param>
    /// <returns>A string in the format <c>"DeclaringType.MethodName"</c>.</returns>
    private static string InferName(SystemFn system)
    {
        var method = system.Method;
        var type = method.DeclaringType?.Name ?? "?";
        return $"{type}.{method.Name}";
    }

    /// <summary>Marks this system as main-thread-only, preventing it from running in parallel batches.</summary>
    /// <returns>This descriptor for fluent chaining.</returns>
    /// <seealso cref="ThreadAffinity.MainThread"/>
    public SystemDescriptor MainThreadOnly()
    {
        Affinity = ThreadAffinity.MainThread;
        return this;
    }

    /// <summary>
    /// Attaches a <c>run_if</c> condition. The system is skipped for the current frame
    /// when <paramref name="condition"/> returns <c>false</c>.
    /// </summary>
    /// <param name="condition">A predicate evaluated against the <see cref="World"/> each frame.</param>
    /// <returns>This descriptor for fluent chaining.</returns>
    public SystemDescriptor RunIf(Func<World, bool> condition)
    {
        _runCondition = condition;
        return this;
    }

    /// <summary>Declares a read-only dependency on a resource type for the parallel scheduler.</summary>
    /// <typeparam name="T">The resource type this system reads.</typeparam>
    /// <returns>This descriptor for fluent chaining.</returns>
    /// <remarks>Multiple readers of the same resource can run in parallel.</remarks>
    public SystemDescriptor Read<T>() where T : notnull
    {
        _reads.Add(typeof(T));
        return this;
    }

    /// <summary>Declares a read-write dependency on a resource type for the parallel scheduler.</summary>
    /// <typeparam name="T">The resource type this system writes.</typeparam>
    /// <returns>This descriptor for fluent chaining.</returns>
    /// <remarks>A writer conflicts with all other readers and writers of the same resource.</remarks>
    public SystemDescriptor Write<T>() where T : notnull
    {
        _writes.Add(typeof(T));
        return this;
    }

    /// <summary>
    /// Determines whether this system conflicts with <paramref name="other"/> based on
    /// declared resource access. Used by the scheduler to build safe parallel batches.
    /// </summary>
    /// <param name="other">The other system descriptor to check against.</param>
    /// <returns><c>true</c> if the systems cannot safely run in parallel; otherwise <c>false</c>.</returns>
    /// <remarks>
    /// Conservative mode: systems without explicit access metadata are treated as broad writers,
    /// so they conflict with everything to prevent data races.
    /// </remarks>
    internal bool ConflictsWith(SystemDescriptor other)
    {
        // Conservative mode: systems without explicit access metadata are treated as
        // broad writers so they don't race with annotated systems.
        if ((_reads.Count == 0 && _writes.Count == 0) || (other._reads.Count == 0 && other._writes.Count == 0))
            return true;

        if (_writes.Count > 0)
        {
            foreach (var t in _writes)
                if (other._writes.Contains(t) || other._reads.Contains(t))
                    return true;
        }

        if (_reads.Count > 0)
        {
            foreach (var t in _reads)
                if (other._writes.Contains(t))
                    return true;
        }

        return false;
    }

    /// <summary>
    /// Attempts to determine the specific conflict reason between this system and <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The other system descriptor to check against.</param>
    /// <param name="reason">When this method returns <c>true</c>, contains a human-readable conflict description.</param>
    /// <returns><c>true</c> if a conflict exists; otherwise <c>false</c>.</returns>
    internal bool TryGetConflictReason(SystemDescriptor other, out string reason)
    {
        if (!HasExplicitAccess || !other.HasExplicitAccess)
        {
            reason = "missing explicit access metadata";
            return true;
        }

        foreach (var t in _writes)
        {
            if (other._writes.Contains(t))
            {
                reason = $"write/write {t.Name}";
                return true;
            }
            if (other._reads.Contains(t))
            {
                reason = $"write/read {t.Name}";
                return true;
            }
        }

        foreach (var t in _reads)
        {
            if (other._writes.Contains(t))
            {
                reason = $"read/write {t.Name}";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }
}
