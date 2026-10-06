namespace Engine;

public sealed partial class EcsWorld
{
    /// <summary>
    /// Despawns <paramref name="entity"/>, with every entity below it, when the state over
    /// <typeparamref name="TState"/> leaves <paramref name="value"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// [OnEnter(Screen.Playing)]
    /// public static void Build(BehaviorContext ctx)
    /// {
    ///     var enemy = ctx.Ecs.Spawn();
    ///     ctx.Ecs.DespawnOnExit(enemy, Screen.Playing);
    /// }
    /// </code>
    /// </example>
    public void DespawnOnExit<TState>(int entity, TState value) where TState : struct, Enum
    {
        if (Has<DespawnOnExit<TState>>(entity)) Update(entity, new DespawnOnExit<TState>(value));
        else Add(entity, new DespawnOnExit<TState>(value));
    }

    /// <summary>The same for an entity by its handle, which a stale one refuses.</summary>
    /// <exception cref="InvalidOperationException">The handle names an entity that is gone.</exception>
    public void DespawnOnExit<TState>(Entity entity, TState value) where TState : struct, Enum =>
        DespawnOnExit(Alive(entity), value);

    // Despawns the entities tied to value of TState, with what is below them, once its exit
    // systems have run.
    internal void DespawnTiedTo<TState>(TState value) where TState : struct, Enum
    {
        if (Count<DespawnOnExit<TState>>() == 0) return;
        var doomed = new List<int>();
        foreach (var (entity, tie) in Query<DespawnOnExit<TState>>())
            if (EqualityComparer<TState>.Default.Equals(tie.Value, value)) doomed.Add(entity);
        // One may be below another, and gone with it by the time its turn comes.
        foreach (var entity in doomed)
            if (IsAlive(entity)) DespawnRecursive(entity);
    }
}
