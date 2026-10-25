namespace Engine;

public sealed partial class EcsWorld
{
    /// <summary>
    /// The entities with a <typeparamref name="T"/> by the key it names, which the world keeps as each
    /// is added, replaced, removed or despawned, made on the first call for <typeparamref name="T"/>
    /// from the entities that have one and the same index after.
    /// </summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> has another index, and a component type has one at most.</exception>
    /// <example>
    /// <code>
    /// var fires = ecs.Index&lt;Fire, (int, int, int)&gt;();
    /// if (!fires.Contains(cell)) ecs.Add(ecs.Spawn(), new Fire { Cell = cell });
    /// </code>
    /// </example>
    public KeyIndex<T, TKey> Index<T, TKey>() where T : IKeyed<TKey> where TKey : notnull
    {
        var store = GetStore<T>();
        if (store.Index is KeyIndex<T, TKey> made) return made;
        if (store.Index is not null) throw new InvalidOperationException($"{typeof(T).Name} has another index, and a component type has one at most.");
        var index = new KeyIndex<T, TKey>(store);
        store.Index = index;
        return index;
    }

    /// <summary>
    /// The entities with a <typeparamref name="T"/> under the key it names, many to a key, which the
    /// world keeps as each is added, replaced, removed or despawned, made on the first call for
    /// <typeparamref name="T"/> from the entities that have one and the same index after.
    /// </summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="T"/> has another index, and a component type has one at most.</exception>
    /// <example>
    /// <code>
    /// foreach (var mob in ecs.Groups&lt;Mob, (int, int)&gt;().Members(column))
    ///     Push(ecs, mob);
    /// </code>
    /// </example>
    public GroupIndex<T, TKey> Groups<T, TKey>() where T : IKeyed<TKey> where TKey : notnull
    {
        var store = GetStore<T>();
        if (store.Index is GroupIndex<T, TKey> made) return made;
        if (store.Index is not null) throw new InvalidOperationException($"{typeof(T).Name} has another index, and a component type has one at most.");
        var index = new GroupIndex<T, TKey>(store);
        store.Index = index;
        return index;
    }
}
