using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// Packed sparse set mapping entity IDs to dense component arrays with O(1) operations and per-index change tracking.
/// </summary>
/// <remarks>
/// <para>
/// The sparse set maintains two parallel dense arrays (<c>_denseEntities</c> and <c>_denseComponents</c>)
/// plus a sparse-to-dense index map (<c>_sparse</c>). This gives O(1) add, remove, has, and lookup while
/// keeping components packed for cache-friendly sequential iteration.
/// </para>
/// <para>
/// Change tracking keeps a tick per dense position (<c>_changedTicks</c>), the
/// <see cref="ChangeTicks"/> tick of the last write marked there, 0 for none, and the largest
/// of them, so whether anything changed after a tick is one comparison.
/// </para>
/// </remarks>
/// <typeparam name="T">The component type stored in the dense array.</typeparam>
/// <seealso cref="EcsWorld"/>
internal sealed class SparseSet<T>
{
    private int[] _denseEntities = Array.Empty<int>();
    private T[] _denseComponents = Array.Empty<T>();
    private long[] _changedTicks = Array.Empty<long>();
    private long _latestChange;
    private int[] _sparse = Array.Empty<int>();
    private int _count;

    /// <summary>Number of components currently stored in the dense array.</summary>
    public int Count => _count;

    /// <summary>Ensures the change ticks have room for the given dense capacity.</summary>
    /// <param name="denseCapacity">The minimum dense array capacity to support.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureTickCapacity(int denseCapacity)
    {
        if (_changedTicks.Length < denseCapacity)
            Array.Resize(ref _changedTicks, denseCapacity);
    }

    /// <summary>Stamps the component at dense <paramref name="index"/> with the tick of the write.</summary>
    /// <param name="index">Dense array index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Stamp(int index)
    {
        var tick = ChangeTicks.ForWrite;
        _changedTicks[index] = tick;
        // The largest, raised atomically since systems in parallel mark the same store.
        long latest;
        while (tick > (latest = Volatile.Read(ref _latestChange))
               && Interlocked.CompareExchange(ref _latestChange, tick, latest) != latest) { }
    }

    /// <summary>Forgets the change at dense <paramref name="index"/>.</summary>
    /// <param name="index">Dense array index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Unstamp(int index)
    {
        _changedTicks[index] = 0;
    }

    /// <summary>Whether the component at dense <paramref name="index"/> changed after <paramref name="since"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ChangedAfter(int index, long since)
    {
        return _changedTicks[index] > since;
    }

    /// <summary>Ensures the sparse array can map <paramref name="entity"/>. Grows and fills new slots with <c>-1</c>.</summary>
    /// <param name="entity">The entity ID that must be indexable.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureSparseCapacity(int entity)
    {
        if (entity < _sparse.Length) return;
        int newSize = _sparse.Length == 0 ? Math.Max(entity + 1, 128) : _sparse.Length;
        while (entity >= newSize) newSize *= 2;
        int oldLen = _sparse.Length;
        Array.Resize(ref _sparse, newSize);
        Array.Fill(_sparse, -1, oldLen, newSize - oldLen);
    }

    /// <summary>Ensures the dense arrays have room for at least one more element.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureDenseCapacity()
    {
        if (_count < _denseEntities.Length) return;
        int newCap = _count == 0 ? 128 : _count * 2;
        Array.Resize(ref _denseEntities, newCap);
        Array.Resize(ref _denseComponents, newCap);
        EnsureTickCapacity(newCap);
    }

    /// <summary>Pre-allocates capacity in both dense and sparse arrays to minimize resizing during bulk inserts.</summary>
    /// <param name="componentCapacity">Minimum number of dense slots.</param>
    /// <param name="maxEntityIdHint">Maximum entity ID expected, used to size the sparse array.</param>
    public void Reserve(int componentCapacity, int maxEntityIdHint)
    {
        if (componentCapacity > _denseEntities.Length)
        {
            Array.Resize(ref _denseEntities, componentCapacity);
            Array.Resize(ref _denseComponents, componentCapacity);
            EnsureTickCapacity(componentCapacity);
        }

        if (maxEntityIdHint > _sparse.Length)
        {
            int oldLen = _sparse.Length;
            Array.Resize(ref _sparse, maxEntityIdHint + 1);
            Array.Fill(_sparse, -1, oldLen, _sparse.Length - oldLen);
        }
    }

    /// <summary>Adds or overwrites a component without marking it changed.</summary>
    /// <param name="entity">The entity ID.</param>
    /// <param name="component">The component value to store.</param>
    public void Add(int entity, T component)
    {
        EnsureSparseCapacity(entity);
        int idx = _sparse[entity];
        if (idx >= 0)
        {
            _denseComponents[idx] = component!;
            return;
        }

        EnsureDenseCapacity();
        idx = _count++;
        _denseEntities[idx] = entity;
        _denseComponents[idx] = component!;
        Unstamp(idx);
        _sparse[entity] = idx;
    }

    /// <summary>Updates (or adds) a component and marks it changed for the current frame.</summary>
    /// <param name="entity">The entity ID.</param>
    /// <param name="component">The component value to store.</param>
    public void Update(int entity, T component)
    {
        EnsureSparseCapacity(entity);
        int idx = _sparse[entity];
        if (idx >= 0)
        {
            _denseComponents[idx] = component!;
            Stamp(idx);
            return;
        }

        EnsureDenseCapacity();
        idx = _count++;
        _denseEntities[idx] = entity;
        _denseComponents[idx] = component!;
        Stamp(idx);
        _sparse[entity] = idx;
    }

    /// <summary>Checks whether <paramref name="entity"/> has a component in this set.</summary>
    /// <param name="entity">The entity ID to check.</param>
    /// <returns><c>true</c> if the entity is mapped; otherwise <c>false</c>.</returns>
    public bool Has(int entity) => entity < _sparse.Length && _sparse[entity] >= 0;

    /// <summary>Attempts to read the component value for <paramref name="entity"/>.</summary>
    /// <param name="entity">The entity ID.</param>
    /// <param name="value">When returning <c>true</c>, contains the component value; otherwise <c>default</c>.</param>
    /// <returns><c>true</c> if the entity has a component; otherwise <c>false</c>.</returns>
    public bool TryGet(int entity, out T value)
    {
        if (entity < _sparse.Length)
        {
            int idx = _sparse[entity];
            if (idx >= 0)
            {
                value = _denseComponents[idx];
                return true;
            }
        }

        value = default!;
        return false;
    }

    /// <summary>Whether the component for <paramref name="entity"/> was marked changed after tick <paramref name="since"/>.</summary>
    /// <param name="entity">The entity ID.</param>
    /// <param name="since">The tick a change has to come after.</param>
    public bool ChangedSince(int entity, long since) => entity < _sparse.Length && _sparse[entity] >= 0 && ChangedAfter(_sparse[entity], since);

    /// <summary>Zero-allocation enumerable over all (entity, component) pairs in the sparse set.</summary>
    public readonly struct ComponentEnumerable
    {
        private readonly SparseSet<T> _set;
        /// <summary>Creates a new enumerable wrapping the specified sparse set.</summary>
        /// <param name="set">The sparse set to enumerate.</param>
        public ComponentEnumerable(SparseSet<T> set) => _set = set;
        /// <summary>Returns a new enumerator positioned before the first element.</summary>
        public Enumerator GetEnumerator() => new(_set);

        /// <summary>Zero-allocation enumerator yielding (entity ID, component value) tuples from the dense array.</summary>
        public struct Enumerator
        {
            private readonly SparseSet<T> _set;
            private int _index;
            /// <summary>Creates a new enumerator positioned before the first element.</summary>
            /// <param name="set">The sparse set to iterate.</param>
            internal Enumerator(SparseSet<T> set) { _set = set; _index = -1; }

            /// <summary>Gets the current (entity ID, component value) tuple.</summary>
            public (int Entity, T Component) Current => (_set._denseEntities[_index], _set._denseComponents[_index]);

            /// <summary>Advances to the next dense element.</summary>
            /// <returns><c>true</c> if there is a next element; otherwise <c>false</c>.</returns>
            public bool MoveNext() { _index++; return _index < _set._count; }
        }
    }

    /// <summary>Returns a zero-allocation enumerable over all (entity, component) pairs.</summary>
    /// <returns>A <see cref="ComponentEnumerable"/> for <c>foreach</c> iteration.</returns>
    public ComponentEnumerable Enumerate() => new(this);

    /// <summary>Returns a direct mutable reference to the component for <paramref name="entity"/>.</summary>
    /// <param name="entity">The entity ID.</param>
    /// <returns>A reference to the component in the dense array.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the entity does not have a component in this set.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetRef(int entity)
    {
        if (entity >= _sparse.Length)
            throw new KeyNotFoundException($"Entity {entity} does not have component {typeof(T).Name}.");
        int idx = _sparse[entity];
        if (idx < 0) throw new KeyNotFoundException($"Entity {entity} does not have component {typeof(T).Name}.");
        return ref _denseComponents[idx];
    }

    /// <summary>Returns the entity ID stored at the given dense index.</summary>
    /// <param name="denseIndex">Zero-based index into the dense entity array.</param>
    /// <returns>The entity ID at that position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int EntityByDenseIndex(int denseIndex) => _denseEntities[denseIndex];

    /// <summary>Returns a mutable reference to the component stored at the given dense index.</summary>
    /// <param name="denseIndex">Zero-based index into the dense component array.</param>
    /// <returns>A reference to the component value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T ComponentRefByDenseIndex(int denseIndex) => ref _denseComponents[denseIndex];

    /// <summary>Stamps the component at <paramref name="denseIndex"/> as changed, with the writer's tick.</summary>
    /// <param name="denseIndex">Zero-based index into the dense array.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MarkChangedByDenseIndex(int denseIndex) => Stamp(denseIndex);

    /// <summary>Whether any component was marked changed after tick <paramref name="since"/>.</summary>
    /// <remarks>A removed component's change still counts, which can only cause a needless look.</remarks>
    public bool AnyChangedSince(long since) => Volatile.Read(ref _latestChange) > since;

    /// <summary>
    /// <see cref="MarkChangedByDenseIndex"/> for use inside <c>Parallel.For</c> loops. A tick is
    /// written whole, and the largest is raised atomically, so the two are the same.
    /// </summary>
    /// <param name="denseIndex">Zero-based index into the dense array.</param>
    public void MarkChangedByDenseIndexThreadSafe(int denseIndex) => Stamp(denseIndex);

    /// <summary>Returns parallel entity and component spans over the packed dense arrays.</summary>
    /// <param name="entities">A read-only span of entity IDs.</param>
    /// <param name="components">A mutable span of component values.</param>
    public void GetSpan(out ReadOnlySpan<int> entities, out Span<T> components)
    {
        entities = _denseEntities.AsSpan(0, _count);
        components = _denseComponents.AsSpan(0, _count);
    }

    /// <summary>
    /// Removes the component for <paramref name="entity"/> using swap-remove on the dense arrays.
    /// If the component implements <see cref="IDisposable"/>, the disposable reference is returned
    /// via <paramref name="disposable"/>.
    /// </summary>
    /// <param name="entity">The entity ID to remove.</param>
    /// <param name="disposable">Set to the component's <see cref="IDisposable"/> implementation if applicable; otherwise <c>null</c>.</param>
    /// <returns><c>true</c> if the component was removed; <c>false</c> if not present.</returns>
    public bool TryRemove(int entity, out IDisposable? disposable)
    {
        disposable = null;
        if (entity >= _sparse.Length) return false;
        int idx = _sparse[entity];
        if (idx < 0) return false;
        var comp = _denseComponents[idx];
        if (comp is IDisposable d) disposable = d;
        int lastIdx = _count - 1;
        if (idx != lastIdx)
        {
            _denseComponents[idx] = _denseComponents[lastIdx];
            _denseEntities[idx] = _denseEntities[lastIdx];
            _changedTicks[idx] = _changedTicks[lastIdx];
            Unstamp(lastIdx);
            _sparse[_denseEntities[idx]] = idx;
        }

        _sparse[entity] = -1;
        _count--;
        return true;
    }

    /// <summary>
    /// Removes the component for <paramref name="entity"/> using swap-remove on the dense arrays.
    /// Does not track disposables; use <see cref="TryRemove"/> when disposal is needed.
    /// </summary>
    /// <param name="entity">The entity ID to remove.</param>
    /// <returns><c>true</c> if the component was removed; <c>false</c> if not present.</returns>
    public bool Remove(int entity)
    {
        if (entity >= _sparse.Length) return false;
        int idx = _sparse[entity];
        if (idx < 0) return false;
        int lastIdx = _count - 1;
        if (idx != lastIdx)
        {
            _denseComponents[idx] = _denseComponents[lastIdx];
            _denseEntities[idx] = _denseEntities[lastIdx];
            _changedTicks[idx] = _changedTicks[lastIdx];
            Unstamp(lastIdx);
            _sparse[_denseEntities[idx]] = idx;
        }

        _sparse[entity] = -1;
        _count--;
        return true;
    }

    /// <summary>Returns the dense array index for <paramref name="entity"/>, or <c>-1</c> if not present.</summary>
    /// <param name="entity">The entity ID.</param>
    /// <returns>The dense index, or <c>-1</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int DenseIndexOf(int entity)
    {
        if ((uint)entity >= (uint)_sparse.Length) return -1;
        return _sparse[entity];
    }

    /// <summary>Direct access to the underlying dense entity array. Use with <see cref="Count"/> for bounds.</summary>
    public int[] EntitiesArray => _denseEntities;

    /// <summary>Direct access to the underlying dense component array. Use with <see cref="Count"/> for bounds.</summary>
    public T[] ComponentsArray => _denseComponents;

    /// <summary>Returns a read-only span of entity IDs over the packed portion of the dense array.</summary>
    /// <returns>A span of length <see cref="Count"/>.</returns>
    public ReadOnlySpan<int> EntitiesSpan() => _denseEntities.AsSpan(0, _count);
}
