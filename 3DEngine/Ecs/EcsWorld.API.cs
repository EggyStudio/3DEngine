using System.Runtime.CompilerServices;

namespace Engine;

public sealed partial class EcsWorld
{
    private int _currentTick;

    /// <summary>Spawns a new entity and returns its integer ID.</summary>
    /// <returns>The newly allocated entity ID.</returns>
    /// <example>
    /// <code>
    /// int entity = ecs.Spawn();
    /// ecs.Add(entity, new Position { X = 10, Y = 20 });
    /// </code>
    /// </example>
    public int Spawn() => _entities.Spawn();

    /// <summary>Spawns <paramref name="count"/> entities in bulk, pre-allocating capacity to avoid resize storms.</summary>
    /// <param name="count">The number of entities to spawn.</param>
    /// <param name="builder">A callback invoked for each new entity ID to attach initial components.</param>
    /// <remarks>
    /// This is significantly faster than calling <see cref="Spawn"/> in a loop because it
    /// pre-reserves entity pool and sparse-set capacity before the tight spawn loop.
    /// </remarks>
    public void SpawnBatch(int count, Action<int, EcsWorld> builder)
    {
        if (count <= 0) return;
        int maxId = _entities.NextEntityId + count;
        _entities.ReserveCapacity(maxId);
        for (int i = 0; i < count; i++)
        {
            var id = _entities.Spawn();
            builder(id, this);
        }
    }

    /// <summary>Spawns <paramref name="count"/> entities in bulk with a single component type, fully pre-allocated.</summary>
    /// <typeparam name="T">The component type to attach to each entity.</typeparam>
    /// <param name="count">The number of entities to spawn.</param>
    /// <param name="factory">A factory function receiving the entity ID and returning the component value.</param>
    public void SpawnBatch<T>(int count, Func<int, T> factory)
    {
        if (count <= 0) return;
        int maxId = _entities.NextEntityId + count;
        _entities.ReserveCapacity(maxId);
        var store = GetStore<T>();
        store.Reserve(store.Count + count, maxId);
        for (int i = 0; i < count; i++)
        {
            var id = _entities.Spawn();
            store.Add(id, factory(id));
        }
    }

    /// <summary>Spawns <paramref name="count"/> entities in bulk with a single default-constructed component, fully pre-allocated.</summary>
    /// <typeparam name="T">The component type to attach to each entity. Must be <c>new()</c>-constructible.</typeparam>
    /// <param name="count">The number of entities to spawn.</param>
    public void SpawnBatch<T>(int count) where T : new()
    {
        if (count <= 0) return;
        int maxId = _entities.NextEntityId + count;
        _entities.ReserveCapacity(maxId);
        var store = GetStore<T>();
        store.Reserve(store.Count + count, maxId);
        for (int i = 0; i < count; i++)
        {
            var id = _entities.Spawn();
            store.Add(id, new T());
        }
    }

    /// <summary>Pre-reserves capacity in the entity pool for upcoming bulk spawns.</summary>
    /// <param name="additionalCount">The number of additional entities expected to be spawned.</param>
    public void ReserveEntityCapacity(int additionalCount)
    {
        if (additionalCount <= 0) return;
        _entities.ReserveCapacity(_entities.NextEntityId + additionalCount);
    }

    /// <summary>Returns the current generation for an entity ID (0 if never allocated).</summary>
    /// <param name="entityId">The entity ID to query.</param>
    /// <returns>The generation counter, or <c>0</c> if the ID was never used.</returns>
    public int GetGeneration(int entityId) => _entities.GetGeneration(entityId);

    /// <summary>Removes an entity and all of its components, disposing <see cref="IDisposable"/> components.</summary>
    /// <param name="entity">The entity ID to remove.</param>
    public void Despawn(int entity)
    {
        var list = _storeList;
        for (int i = 0; i < list.Count; i++)
            if (list[i].TryRemove(entity, out var disposable) && disposable is not null)
                try { disposable.Dispose(); } catch { }

        _entities.Despawn(entity);
    }

    /// <summary>
    /// Advances the frame tick counter and clears all per-frame change-tracking bits.
    /// Called once at the start of each frame.
    /// </summary>
    public void BeginFrame()
    {
        _currentTick++;
        var list = _storeList;
        for (int i = 0; i < list.Count; i++)
            list[i].ClearChangedTicks();
    }

    /// <summary>Returns the number of entities that currently have component <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The component type to count.</typeparam>
    /// <returns>The count of entities with this component type.</returns>
    public int Count<T>() => GetStore<T>(create: false)?.Count ?? 0;

    /// <summary>Removes component <typeparamref name="T"/> from an entity if present.</summary>
    /// <typeparam name="T">The component type to remove.</typeparam>
    /// <param name="entity">The entity ID.</param>
    /// <returns><c>true</c> if the component was removed; <c>false</c> if it was not present.</returns>
    public bool Remove<T>(int entity)
    {
        var store = GetStore<T>(create: false);
        return store != null && store.Remove(entity);
    }

    /// <summary>Returns a span of entity IDs that currently have component <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The component type to query.</typeparam>
    /// <returns>A read-only span of entity IDs. Empty if no entities have this component.</returns>
    public ReadOnlySpan<int> EntitiesWith<T>()
    {
        var store = GetStore<T>(create: false);
        return store == null ? ReadOnlySpan<int>.Empty : store.EntitiesSpan();
    }

    /// <summary>Pre-reserves capacity for component <typeparamref name="T"/> to reduce resizing during bulk spawns.</summary>
    /// <typeparam name="T">The component type to reserve storage for.</typeparam>
    /// <param name="componentCapacity">The number of component slots to pre-allocate in the dense array.</param>
    /// <param name="maxEntityIdHint">Optional hint for the maximum expected entity ID to size the sparse array.</param>
    public void Reserve<T>(int componentCapacity, int maxEntityIdHint = 0)
    {
        var store = GetStore<T>();
        store.Reserve(componentCapacity, maxEntityIdHint);
    }

    /// <summary>Adds a component to an entity (overwrites if already present) without marking it as changed.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity ID.</param>
    /// <param name="component">The component value.</param>
    public void Add<T>(int entity, T component) => GetStore<T>().Add(entity, component);

    /// <summary>Updates an existing component (or adds if missing) and marks it as changed for this frame.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity ID.</param>
    /// <param name="component">The new component value.</param>
    public void Update<T>(int entity, T component) => GetStore<T>().Update(entity, component, _currentTick);

    /// <summary>Updates an existing component via a transformer function and marks it as changed; no-op if the component is missing.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity ID.</param>
    /// <param name="mutate">A function that receives the current value and returns the updated value.</param>
    public void Mutate<T>(int entity, Func<T, T> mutate)
    {
        var store = GetStore<T>(create: false);
        if (store is null) return;
        if (store.TryGet(entity, out var value))
        {
            var next = mutate(value);
            store.Update(entity, next, _currentTick);
        }
    }

    /// <summary>Checks whether an entity has component <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity ID.</param>
    /// <returns><c>true</c> if the entity has the component; otherwise <c>false</c>.</returns>
    public bool Has<T>(int entity)
    {
        var store = GetStore<T>(create: false);
        return store != null && store.Has(entity);
    }

    /// <summary>Checks whether component <typeparamref name="T"/> on <paramref name="entity"/> was modified this frame.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity ID.</param>
    /// <returns><c>true</c> if the component was modified this frame; otherwise <c>false</c>.</returns>
    public bool Changed<T>(int entity)
    {
        var store = GetStore<T>(create: false);
        return store != null && store.ChangedThisFrame(entity, _currentTick);
    }

    /// <summary>Attempts to read component <typeparamref name="T"/> from an entity.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity ID.</param>
    /// <param name="component">When returning <c>true</c>, contains the component value; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if the component was found; otherwise <c>false</c>.</returns>
    /// <example>
    /// <code>
    /// if (ecs.TryGet&lt;Health&gt;(entity, out var hp))
    ///     Console.WriteLine($"HP: {hp.Current}/{hp.Max}");
    /// </code>
    /// </example>
    public bool TryGet<T>(int entity, out T? component)
    {
        var store = GetStore<T>(create: false);
        if (store != null && store.TryGet(entity, out var value))
        {
            component = value;
            return true;
        }

        component = default;
        return false;
    }

    /// <summary>Returns a ref to component <typeparamref name="T"/> on <paramref name="entity"/>, or throws if missing.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity ID.</param>
    /// <returns>A reference to the component value in the dense array.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the entity does not have the component.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>(int entity)
    {
        var store = GetStore<T>(create: false) ??
                    throw new KeyNotFoundException($"Component {typeof(T).Name} store not found.");
        return ref store.GetRef(entity);
    }

    /// <summary>Applies a transform function to every component of type <typeparamref name="T"/>, marking each as changed.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="transform">A function receiving the entity ID and current value, returning the new value.</param>
    /// <example>
    /// <code>
    /// // Apply gravity to all velocities
    /// ecs.TransformEach&lt;Velocity&gt;((entity, vel) =>
    ///     vel with { Y = vel.Y - 9.81f * dt });
    /// </code>
    /// </example>
    public void TransformEach<T>(Func<int, T, T> transform)
    {
        var store = GetStore<T>(create: false);
        if (store == null) return;
        for (int i = 0; i < store.Count; i++)
        {
            ref var comp = ref store.ComponentRefByDenseIndex(i);
            var newVal = transform(store.EntityByDenseIndex(i), comp);
            comp = newVal;
            store.MarkChangedByDenseIndex(i, _currentTick);
        }
    }

    /// <summary>Parallel version of <see cref="TransformEach{T}"/>. Suitable for large component counts with no cross-entity dependencies.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="transform">A function receiving the entity ID and current value, returning the new value.</param>
    public void ParallelTransformEach<T>(Func<int, T, T> transform)
    {
        var store = GetStore<T>(create: false);
        if (store == null || store.Count == 0) return;
        var count = store.Count;
        var ents = store.EntitiesArray;
        var comps = store.ComponentsArray;
        System.Threading.Tasks.Parallel.For(0, count, i =>
        {
            var newVal = transform(ents[i], comps[i]);
            comps[i] = newVal;
            store.MarkChangedByDenseIndexThreadSafe(i);
        });
    }

    /// <summary>Returns a zero-allocation ref-enumerable over components of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>A <see cref="RefEnumerable{T}"/> for <c>foreach</c>-based ref iteration.</returns>
    /// <example>
    /// <code>
    /// // Mutate positions in-place with zero allocation
    /// foreach (var rc in ecs.QueryRef&lt;Position&gt;())
    ///     rc.Component.X += velocity * dt;
    /// </code>
    /// </example>
    public RefEnumerable<T> QueryRef<T>()
    {
        var store = GetStore<T>(create: false);
        if (store == null || store.Count == 0) return RefEnumerable<T>.From(default);
        return RefEnumerable<T>.FromStore(store, markOnIterate: true);
    }

    /// <summary>Returns a zero-allocation ref-enumerable over entities matching both <typeparamref name="T1"/> and <typeparamref name="T2"/>.</summary>
    /// <typeparam name="T1">The first component type.</typeparam>
    /// <typeparam name="T2">The second component type.</typeparam>
    /// <returns>A <see cref="RefEnumerable{T1,T2}"/> for <c>foreach</c>-based ref iteration.</returns>
    /// <example>
    /// <code>
    /// // Integrate velocity into position, both mutated by ref
    /// foreach (var pair in ecs.QueryRef&lt;Position, Velocity&gt;())
    /// {
    ///     ref var pos = ref pair.C1;
    ///     ref var vel = ref pair.C2;
    ///     pos.X += vel.X * dt;
    ///     pos.Y += vel.Y * dt;
    /// }
    /// </code>
    /// </example>
    public RefEnumerable<T1, T2> QueryRef<T1, T2>()
    {
        var s1 = GetStore<T1>(create: false);
        var s2 = GetStore<T2>(create: false);
        if (s1 == null || s2 == null) return RefEnumerable<T1, T2>.Empty();
        return RefEnumerable<T1, T2>.From(s1, s2, markOnIterate: true);
    }

    /// <summary>Returns a span view of all components of type <typeparamref name="T"/> for raw iteration.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>A <see cref="ComponentSpan{T}"/> containing parallel entity ID and component spans.</returns>
    public ComponentSpan<T> GetSpan<T>()
    {
        var store = GetStore<T>(create: false);
        if (store == null || store.Count == 0) return default;
        return store.AsSpan();
    }

    /// <summary>Enumerates all (entity, component) pairs of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>An enumerable of (entity ID, component value) tuples.</returns>
    /// <example>
    /// <code>
    /// foreach (var (entity, pos) in ecs.Query&lt;Position&gt;())
    ///     Console.WriteLine($"Entity {entity} at ({pos.X}, {pos.Y})");
    /// </code>
    /// </example>
    public IEnumerable<(int Entity, T Component)> Query<T>()
    {
        var store = GetStore<T>(create: false);
        if (store == null) yield break;
        foreach (var item in store.Enumerate())
            yield return item;
    }

    /// <summary>Enumerates entities that have both <typeparamref name="T1"/> and <typeparamref name="T2"/>, iterating the smaller store first.</summary>
    /// <typeparam name="T1">The first component type.</typeparam>
    /// <typeparam name="T2">The second component type.</typeparam>
    /// <returns>An enumerable of (entity ID, C1, C2) tuples.</returns>
    /// <example>
    /// <code>
    /// foreach (var (entity, pos, vel) in ecs.Query&lt;Position, Velocity&gt;())
    ///     ecs.Update(entity, new Position(pos.X + vel.X * dt, pos.Y + vel.Y * dt));
    /// </code>
    /// </example>
    public IEnumerable<(int Entity, T1 C1, T2 C2)> Query<T1, T2>()
    {
        var s1 = GetStore<T1>(create: false);
        var s2 = GetStore<T2>(create: false);
        if (s1 == null || s2 == null) yield break;
        if (s1.Count <= s2.Count)
        {
            foreach (var (e, c1) in s1.Enumerate())
                if (s2.TryGet(e, out var c2))
                    yield return (e, c1, c2);
        }
        else
        {
            foreach (var (e, c2) in s2.Enumerate())
                if (s1.TryGet(e, out var c1))
                    yield return (e, c1, c2);
        }
    }

    /// <summary>Enumerates entities that have <typeparamref name="T1"/>, <typeparamref name="T2"/>, and <typeparamref name="T3"/>.</summary>
    /// <typeparam name="T1">The first component type.</typeparam>
    /// <typeparam name="T2">The second component type.</typeparam>
    /// <typeparam name="T3">The third component type.</typeparam>
    /// <returns>An enumerable of (entity ID, C1, C2, C3) tuples.</returns>
    public IEnumerable<(int Entity, T1 C1, T2 C2, T3 C3)> Query<T1, T2, T3>()
    {
        var s1 = GetStore<T1>(create: false);
        var s2 = GetStore<T2>(create: false);
        var s3 = GetStore<T3>(create: false);
        if (s1 == null || s2 == null || s3 == null) yield break;
        var c1 = s1.Count;
        var c2 = s2.Count;
        var c3 = s3.Count;
        int smallestCase = (c1 <= c2 && c1 <= c3) ? 1 : (c2 <= c1 && c2 <= c3) ? 2 : 3;
        if (smallestCase == 1)
        {
            foreach (var (e, comp1) in s1.Enumerate())
            {
                if (!s2.TryGet(e, out var comp2) || !s3.TryGet(e, out var comp3)) continue;
                yield return (e, comp1, comp2, comp3);
            }
        }
        else if (smallestCase == 2)
        {
            foreach (var (e, comp2) in s2.Enumerate())
            {
                if (!s1.TryGet(e, out var comp1) || !s3.TryGet(e, out var comp3)) continue;
                yield return (e, comp1, comp2, comp3);
            }
        }
        else
        {
            foreach (var (e, comp3) in s3.Enumerate())
            {
                if (!s1.TryGet(e, out var comp1) || !s2.TryGet(e, out var comp2)) continue;
                yield return (e, comp1, comp2, comp3);
            }
        }
    }

    /// <summary>Enumerates components of type <typeparamref name="T"/> that match a predicate.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="predicate">A filter function that must return <c>true</c> for the entity to be included.</param>
    /// <returns>An enumerable of (entity ID, component value) tuples matching the predicate.</returns>
    public IEnumerable<(int Entity, T Component)> QueryWhere<T>(Func<T, bool> predicate)
    {
        foreach (var (entity, comp) in Query<T>())
            if (predicate(comp))
                yield return (entity, comp);
    }

    /// <summary>
    /// Provides raw span access to all components of type <typeparamref name="T"/> for bulk/SIMD processing.
    /// The processor receives parallel mutable component and read-only entity spans.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="processor">
    /// A callback receiving the component data as a mutable span and entity IDs as a read-only span,
    /// both of the same length. The caller may use <see cref="SimdMath"/> or manual vectorization.
    /// </param>
    /// <example>
    /// <code>
    /// // SIMD-accelerate gravity on all Velocity.Y values
    /// ecs.BulkProcess&lt;Velocity&gt;((comps, entities) =>
    /// {
    ///     var ySpan = MemoryMarshal.Cast&lt;Velocity, float&gt;(comps);
    ///     // Process Y fields with SIMD...
    /// });
    /// </code>
    /// </example>
    public void BulkProcess<T>(BulkProcessAction<T> processor)
    {
        var store = GetStore<T>(create: false);
        if (store == null || store.Count == 0) return;
        var span = store.AsSpan();
        processor(span.Components, span.Entities);
    }

    /// <summary>Delegate for <see cref="BulkProcess{T}"/> providing raw span access to component data.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="components">Mutable span of component values in dense order.</param>
    /// <param name="entities">Read-only span of entity IDs, aligned with <paramref name="components"/>.</param>
    public delegate void BulkProcessAction<T>(Span<T> components, ReadOnlySpan<int> entities);
}
