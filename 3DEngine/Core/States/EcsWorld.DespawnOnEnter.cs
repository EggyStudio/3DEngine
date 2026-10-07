namespace Engine;

public sealed partial class EcsWorld
{
    /// <summary>
    /// Despawns <paramref name="entity"/>, with every entity below it, when the state over
    /// <typeparamref name="TState"/> enters <paramref name="value"/>, before that value's enter systems.
    /// </summary>
    /// <example>
    /// <code>
    /// [OnExit(Screen.Menu)]
    /// public static void SayItWillReturn(BehaviorContext ctx)
    /// {
    ///     var notice = ctx.Ecs.Spawn();
    ///     ctx.Ecs.DespawnOnEnter(notice, Screen.Menu);
    /// }
    /// </code>
    /// </example>
    public void DespawnOnEnter<TState>(int entity, TState value) where TState : struct, Enum
    {
        if (Has<DespawnOnEnter<TState>>(entity)) Update(entity, new DespawnOnEnter<TState>(value));
        else Add(entity, new DespawnOnEnter<TState>(value));
    }

    /// <summary>The same for an entity by its handle, which a stale one refuses.</summary>
    /// <exception cref="InvalidOperationException">The handle names an entity that is gone.</exception>
    public void DespawnOnEnter<TState>(Entity entity, TState value) where TState : struct, Enum =>
        DespawnOnEnter(Alive(entity), value);

    /// <summary>
    /// Despawns <paramref name="entity"/>, with every entity below it, at the first transition of the
    /// state over <typeparamref name="TState"/> that <paramref name="rule"/> answers true for.
    /// </summary>
    /// <example>
    /// <code>
    /// ctx.Ecs.DespawnWhen&lt;Screen&gt;(hint, transition => transition.To is Screen.Paused or Screen.Menu);
    /// </code>
    /// </example>
    public void DespawnWhen<TState>(int entity, Func<StateTransition<TState>, bool> rule) where TState : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (Has<DespawnWhen<TState>>(entity)) Update(entity, new DespawnWhen<TState>(rule));
        else Add(entity, new DespawnWhen<TState>(rule));
    }

    /// <summary>The same for an entity by its handle, which a stale one refuses.</summary>
    /// <exception cref="InvalidOperationException">The handle names an entity that is gone.</exception>
    public void DespawnWhen<TState>(Entity entity, Func<StateTransition<TState>, bool> rule) where TState : struct, Enum =>
        DespawnWhen(Alive(entity), rule);

    // Despawns the entities tied to entering value of TState, with what is below them, before its
    // enter systems run.
    internal void DespawnEntering<TState>(TState value) where TState : struct, Enum
    {
        if (Count<DespawnOnEnter<TState>>() == 0) return;
        var doomed = new List<int>();
        foreach (var (entity, tie) in Query<DespawnOnEnter<TState>>())
            if (EqualityComparer<TState>.Default.Equals(tie.Value, value)) doomed.Add(entity);
        foreach (var entity in doomed)
            if (IsAlive(entity)) DespawnRecursive(entity);
    }

    // Despawns the entities whose rule answers true for a transition of TState, with what is below them.
    internal void DespawnByRule<TState>(StateTransition<TState> transition, ILogger logger) where TState : struct, Enum
    {
        if (Count<DespawnWhen<TState>>() == 0) return;
        var doomed = new List<int>();
        foreach (var (entity, when) in Query<DespawnWhen<TState>>())
        {
            try
            {
                if (when.Rule(transition)) doomed.Add(entity);
            }
            catch (Exception ex)
            {
                logger.Error($"A DespawnWhen rule of {typeof(TState).Name} threw at {transition.From?.ToString() ?? "none"} -> {transition.To?.ToString() ?? "none"}", ex);
            }
        }
        foreach (var entity in doomed)
            if (IsAlive(entity)) DespawnRecursive(entity);
    }
}
