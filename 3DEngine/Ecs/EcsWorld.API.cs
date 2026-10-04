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

        // Refused for an id that is not alive, so despawning twice frees the id once.
        _entities.Despawn(entity);
    }

    /// <summary>Despawns the entity <paramref name="entity"/> names, if it is still that entity.</summary>
    /// <returns>Whether it was alive and is despawned.</returns>
    public bool Despawn(Entity entity)
    {
        if (!TryResolve(entity, out var id)) return false;
        Despawn(id);
        return true;
    }

    /// <summary>Whether <paramref name="entity"/> is alive.</summary>
    public bool IsAlive(int entity) => _entities.IsAlive(entity);

    /// <summary>Whether the entity <paramref name="entity"/> was taken from is still alive and has not been replaced.</summary>
    public bool IsAlive(Entity entity) =>
        !entity.IsNone && _entities.IsAlive(entity.Index) && _entities.GetGeneration(entity.Index) == entity.Generation;

    /// <summary>A handle to <paramref name="entity"/> that can be kept across frames, or <see cref="Entity.None"/> when it is not alive.</summary>
    public Entity Handle(int entity) =>
        _entities.IsAlive(entity) ? new Entity(entity, _entities.GetGeneration(entity)) : Entity.None;

    /// <summary>The id <paramref name="entity"/> names, when it is still alive and still the same entity.</summary>
    public bool TryResolve(Entity entity, out int id)
    {
        id = IsAlive(entity) ? entity.Index : 0;
        return id != 0;
    }

    /// <summary>
    /// Starts a frame, from which code outside a system counts changes. Called once at the start
    /// of each frame. A system counts changes from its own last run instead (<see cref="ChangeTicks"/>).
    /// </summary>
    public void BeginFrame()
    {
        _currentTick++;
        _frame.Start = ChangeTicks.Advance();

        // Removals are kept for RemovalFrames frames, which a system running less often than
        // that misses.
        var oldest = _frameStarts[_frameStartNext];
        _frameStarts[_frameStartNext] = _frame.Start;
        _frameStartNext = (_frameStartNext + 1) % RemovalFrames;
        if (oldest == 0) return;
        var list = _storeList;
        for (int i = 0; i < list.Count; i++) list[i].PruneRemovals(oldest);
    }

    /// <summary>How many frames a removal is kept for <see cref="Removed{T}"/>, a second at 60 frames a second.</summary>
    public const int RemovalFrames = 60;

    private readonly long[] _frameStarts = new long[RemovalFrames];
    private int _frameStartNext;

    /// <summary>The <see cref="ChangeTicks"/> tick the current frame began at.</summary>
    public long FrameStart => _frame.Start;

    // The frame's start, shared with the stores rather than the world itself, since the static
    // store cache keeps the stores reachable and a store holding the world would keep it alive.
    private readonly FrameClock _frame = new();

    /// <summary>Where a world's frame began, which its stores read.</summary>
    internal sealed class FrameClock
    {
        public long Start;
    }

    /// <summary>Returns the number of entities that currently have component <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The component type to count.</typeparam>
    /// <returns>The count of entities with this component type.</returns>
    public int Count<T>() => GetStore<T>(create: false)?.Count ?? 0;

    /// <summary>
    /// Whether any <typeparamref name="T"/> was updated or handed out by <see cref="GetRef{T}"/>
    /// since the running system last ran, or outside a system, since the frame began.
    /// </summary>
    public bool AnyChanged<T>() => GetStore<T>(create: false)?.AnyChanged() ?? false;

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

    /// <summary>
    /// Whether component <typeparamref name="T"/> on <paramref name="entity"/> was modified since
    /// the running system last ran, or outside a system, since the frame began.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="entity">The entity ID.</param>
    public bool Changed<T>(int entity)
    {
        var store = GetStore<T>(create: false);
        return store != null && store.Changed(entity);
    }

    /// <summary>
    /// Whether <paramref name="entity"/> got component <typeparamref name="T"/> since the running
    /// system last ran, or outside a system, since the frame began, not having had one before.
    /// </summary>
    public bool Added<T>(int entity)
    {
        var store = GetStore<T>(create: false);
        return store != null && store.Added(entity);
    }

    /// <summary>
    /// The entities that lost component <typeparamref name="T"/>, by <see cref="Remove{T}(int)"/> or
    /// a despawn, since the running system last ran, or outside a system, since the frame began,
    /// oldest first. An id may already name a new entity.
    /// </summary>
    /// <remarks>
    /// Removals are kept for <see cref="RemovalFrames"/> frames, so a system that runs less often
    /// than that misses the older ones.
    /// </remarks>
    public List<int> Removed<T>()
    {
        var removed = new List<int>();
        GetStore<T>(create: false)?.Removed(removed);
        return removed;
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

    /// <summary>
    /// Returns a ref to component <typeparamref name="T"/> on <paramref name="entity"/> to write
    /// through, marking it changed this frame, or throws if missing.
    /// </summary>
    /// <remarks>
    /// Marked whether or not the caller writes, as Bevy's <c>Mut</c> is on a mutable borrow, since
    /// a ref cannot tell. A <c>Changed</c> filter and transform propagation then see writes made
    /// through it. Code that only reads uses <see cref="GetReadOnly{T}"/>, which marks nothing.
    /// The bit is set atomically, so systems running in parallel can call it.
    /// </remarks>
    /// <exception cref="KeyNotFoundException">Thrown if the entity does not have the component.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef<T>(int entity)
    {
        var store = GetStore<T>(create: false) ??
                    throw new KeyNotFoundException($"Component {typeof(T).Name} store not found.");
        int index = store.DenseIndexOf(entity);
        if (index < 0) throw new KeyNotFoundException($"Entity {entity} has no {typeof(T).Name}.");
        store.MarkChangedByDenseIndexThreadSafe(index);
        return ref store.ComponentRefByDenseIndex(index);
    }

    /// <summary>
    /// Returns a read-only ref to component <typeparamref name="T"/> on <paramref name="entity"/>,
    /// which marks nothing changed, or throws if missing.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Thrown if the entity does not have the component.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T GetReadOnly<T>(int entity)
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
        return RefEnumerable<T>.FromStore(store, markOnIterate: true, this);
    }

    // The store for a filter, which reads it with the type-erased interface. Not created when it
    // does not exist, because a query must not add stores as a side effect.
    internal IComponentStore? StoreOrNull<T>() => GetStore<T>(create: false);

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
        return RefEnumerable<T1, T2>.From(s1, s2, markOnIterate: true, this);
    }

    /// <summary>Returns a zero-allocation ref-enumerable over entities that have all of <typeparamref name="T1"/>, <typeparamref name="T2"/> and <typeparamref name="T3"/>.</summary>
    /// <example>
    /// <code>
    /// foreach (var row in ecs.QueryRef&lt;Transform, Velocity, Mass&gt;().Without&lt;Frozen&gt;())
    ///     row.C1.Position += row.C2.Value / row.C3.Value * dt;
    /// </code>
    /// </example>
    public RefEnumerable<T1, T2, T3> QueryRef<T1, T2, T3>()
    {
        var s1 = GetStore<T1>(create: false);
        var s2 = GetStore<T2>(create: false);
        var s3 = GetStore<T3>(create: false);
        if (s1 == null || s2 == null || s3 == null) return RefEnumerable<T1, T2, T3>.Empty();
        return RefEnumerable<T1, T2, T3>.From(s1, s2, s3, markOnIterate: true, this);
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

    /// <summary>Every entity with a <typeparamref name="T"/>, with a copy of it, narrowed by <c>.With</c>, <c>.Without</c> and <c>.Changed</c>.</summary>
    /// <remarks>A struct, so <c>foreach</c> over it allocates nothing.</remarks>
    /// <example>
    /// <code>
    /// foreach (var (entity, pos) in ecs.Query&lt;Position&gt;().Without&lt;Frozen&gt;())
    ///     Console.WriteLine($"Entity {entity} at ({pos.X}, {pos.Y})");
    /// </code>
    /// </example>
    public CopyQuery<T> Query<T>() => new(this);

    /// <summary>Every entity with both a <typeparamref name="T1"/> and a <typeparamref name="T2"/>, with a copy of each, walking the smaller store.</summary>
    /// <example>
    /// <code>
    /// foreach (var (entity, pos, vel) in ecs.Query&lt;Position, Velocity&gt;())
    ///     ecs.Update(entity, new Position(pos.X + vel.X * dt, pos.Y + vel.Y * dt));
    /// </code>
    /// </example>
    public CopyQuery<T1, T2> Query<T1, T2>() => new(this);

    /// <summary>Every entity with a <typeparamref name="T1"/>, a <typeparamref name="T2"/> and a <typeparamref name="T3"/>, with a copy of each, walking the smallest store.</summary>
    public CopyQuery<T1, T2, T3> Query<T1, T2, T3>() => new(this);

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

    // -- Introspection

    /// <summary>How many entities are alive.</summary>
    public int EntityCount => _entities.AliveCount;

    /// <summary>Every alive entity, in id order, including those with no component.</summary>
    public IEnumerable<int> AllEntities()
    {
        for (int id = 1; id < _entities.NextEntityId; id++)
            if (_entities.IsAlive(id)) yield return id;
    }

    /// <summary>Every component type that has had a store made for it, sorted by name.</summary>
    /// <remarks>For tools such as the console. A type appears once something has added it, and stays after its last component is removed.</remarks>
    public IReadOnlyList<Type> ComponentTypes
    {
        get { lock (_stores) return _stores.Keys.OrderBy(t => t.Name, StringComparer.Ordinal).ToArray(); }
    }

    /// <summary>The types of every component <paramref name="entity"/> has, sorted by name.</summary>
    /// <remarks>Asks every store, so it costs one lookup per component type. Meant for tools, not for a system's loop.</remarks>
    public IReadOnlyList<Type> ComponentTypesOf(int entity)
    {
        lock (_stores)
            return _stores.Where(pair => pair.Value.Has(entity)).Select(pair => pair.Key).OrderBy(t => t.Name, StringComparer.Ordinal).ToArray();
    }

    /// <summary>The component of <paramref name="type"/> that <paramref name="entity"/> has, boxed, or <c>null</c>.</summary>
    /// <remarks>Boxes the value, so it is for tools such as the console rather than a system's loop.</remarks>
    public object? GetBoxed(int entity, Type type)
    {
        lock (_stores) return _stores.TryGetValue(type, out var store) ? store.GetBoxed(entity) : null;
    }

    /// <summary>Replaces <paramref name="entity"/>'s component of the value's type, and marks it changed.</summary>
    /// <returns>Whether the entity had a component of that type.</returns>
    /// <remarks>Boxes, so it is for tools such as the console rather than a system's loop.</remarks>
    public bool SetBoxed(int entity, object value)
    {
        lock (_stores) return _stores.TryGetValue(value.GetType(), out var store) && store.SetBoxed(entity, value);
    }

    /// <summary>Adds a component given as an object, whatever its type, making its store when it has none.</summary>
    /// <remarks>
    /// For tools such as the console, which know a component only at run time. The typed
    /// <see cref="Add{T}"/> is reached through reflection, once per call, so a system's loop uses
    /// that instead.
    /// </remarks>
    public void AddBoxed(int entity, object value) =>
        typeof(EcsWorld).GetMethod(nameof(AddTyped), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .MakeGenericMethod(value.GetType())
            .Invoke(this, [entity, value]);

    private void AddTyped<T>(int entity, object value) => Add(entity, (T)value);

    /// <summary>How many entities have a component of <paramref name="type"/>.</summary>
    public int CountOf(Type type)
    {
        lock (_stores) return _stores.TryGetValue(type, out var store) ? store.Count : 0;
    }

    /// <summary>Every entity with a component of <paramref name="type"/>, in storage order.</summary>
    public IReadOnlyList<int> EntitiesOf(Type type)
    {
        IComponentStore? store;
        lock (_stores) _stores.TryGetValue(type, out store);
        if (store is null) return [];
        var found = new List<int>(store.Count);
        for (int id = 1; id < _entities.NextEntityId; id++)
            if (store.Has(id)) found.Add(id);
        return found;
    }
}
