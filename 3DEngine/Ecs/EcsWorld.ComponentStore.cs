using System.Runtime.CompilerServices;

namespace Engine;

public sealed partial class EcsWorld
{
    /// <summary>
    /// Typed wrapper around <see cref="SparseSet{T}"/> implementing <see cref="IComponentStore"/>
    /// for type-erased access. Provides add, remove, query, iteration, and change-tracking operations.
    /// </summary>
    /// <typeparam name="T">The component type stored.</typeparam>
    public sealed class ComponentStore<T> : IComponentStore
    {
        private readonly SparseSet<T> _set = new();

        /// <summary>Number of components currently stored.</summary>
        public int Count => _set.Count;

        /// <summary>Pre-allocates capacity in the dense and sparse arrays to reduce resizing during bulk spawns.</summary>
        /// <param name="componentCapacity">Minimum dense array capacity.</param>
        /// <param name="maxEntityIdHint">Hint for the maximum expected entity ID to size the sparse array.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reserve(int componentCapacity, int maxEntityIdHint)
            => _set.Reserve(componentCapacity, maxEntityIdHint);

        /// <summary>Adds or overwrites a component on <paramref name="entity"/> without marking it as changed.</summary>
        /// <param name="entity">The entity ID.</param>
        /// <param name="component">The component value to store.</param>
        public void Add(int entity, T component) => _set.Add(entity, component);

        /// <summary>Adds or overwrites a component on <paramref name="entity"/> and marks it as changed.</summary>
        /// <param name="entity">The entity ID.</param>
        /// <param name="component">The component value to store.</param>
        /// <param name="currentTick">The current frame tick (reserved for future use).</param>
        public void Update(int entity, T component, int currentTick) => _set.Update(entity, component);

        /// <summary>Returns <c>true</c> if <paramref name="entity"/> has this component.</summary>
        /// <param name="entity">The entity ID to check.</param>
        /// <returns><c>true</c> if present; otherwise <c>false</c>.</returns>
        public bool Has(int entity) => _set.Has(entity);

        /// <summary>Attempts to read the component value for <paramref name="entity"/>.</summary>
        /// <param name="entity">The entity ID.</param>
        /// <param name="value">When returning <c>true</c>, contains the component; otherwise <c>default</c>.</param>
        /// <returns><c>true</c> if found; otherwise <c>false</c>.</returns>
        public bool TryGet(int entity, out T value) => _set.TryGet(entity, out value!);

        /// <summary>Returns <c>true</c> if the component on <paramref name="entity"/> was modified this frame.</summary>
        /// <param name="entity">The entity ID.</param>
        /// <param name="currentTick">The current frame tick (reserved for future use).</param>
        /// <returns><c>true</c> if changed; otherwise <c>false</c>.</returns>
        public bool ChangedThisFrame(int entity, int currentTick) => _set.ChangedThisFrame(entity);

        /// <summary>Returns a zero-allocation enumerable over all (entity, component) pairs.</summary>
        /// <returns>A <see cref="ComponentEnumerable"/> for <c>foreach</c> iteration.</returns>
        public ComponentEnumerable Enumerate() => new(this);

        /// <summary>Zero-allocation enumerable over all (entity, component) pairs in a <see cref="ComponentStore{T}"/>.</summary>
        public readonly struct ComponentEnumerable
        {
            private readonly ComponentStore<T> _store;
            /// <summary>Creates a new enumerable wrapping the specified store.</summary>
            /// <param name="store">The component store to enumerate.</param>
            public ComponentEnumerable(ComponentStore<T> store) => _store = store;
            /// <summary>Returns a new enumerator positioned before the first element.</summary>
            public Enumerator GetEnumerator() => new(_store);

            /// <summary>Zero-allocation enumerator yielding (entity ID, component value) tuples.</summary>
            public struct Enumerator
            {
                private readonly ComponentStore<T> _store;
                private int _index;

                /// <summary>Creates a new enumerator for the specified store, positioned before the first element.</summary>
                /// <param name="store">The component store to iterate.</param>
                internal Enumerator(ComponentStore<T> store)
                {
                    _store = store;
                    _index = -1;
                }

                /// <summary>Gets the current (entity ID, component value) tuple.</summary>
                public (int Entity, T Component) Current =>
                    (_store._set.EntityByDenseIndex(_index), _store._set.ComponentRefByDenseIndex(_index));

                /// <summary>Advances to the next element.</summary>
                /// <returns><c>true</c> if there is a next element; <c>false</c> if enumeration is complete.</returns>
                public bool MoveNext()
                {
                    _index++;
                    return _index < _store.Count;
                }
            }
        }

        /// <summary>Returns a direct mutable reference to the component for <paramref name="entity"/>.</summary>
        /// <param name="entity">The entity ID.</param>
        /// <returns>A reference to the component in the dense array.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the entity does not have this component.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T GetRef(int entity) => ref _set.GetRef(entity);

        /// <summary>Returns the entity ID at the given dense array index.</summary>
        /// <param name="denseIndex">Zero-based index into the dense array.</param>
        /// <returns>The entity ID stored at that index.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int EntityByDenseIndex(int denseIndex) => _set.EntityByDenseIndex(denseIndex);

        /// <summary>Returns a mutable reference to the component at the given dense array index.</summary>
        /// <param name="denseIndex">Zero-based index into the dense array.</param>
        /// <returns>A reference to the component value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ComponentRefByDenseIndex(int denseIndex) => ref _set.ComponentRefByDenseIndex(denseIndex);

        /// <summary>Marks the component at <paramref name="denseIndex"/> as changed for this frame.</summary>
        /// <param name="denseIndex">Zero-based index into the dense array.</param>
        /// <param name="tick">The current frame tick (reserved for future use).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkChangedByDenseIndex(int denseIndex, int tick) => _set.MarkChangedByDenseIndex(denseIndex);

        /// <summary>Thread-safe version of <see cref="MarkChangedByDenseIndex"/>. Uses atomic operations.</summary>
        /// <param name="denseIndex">Zero-based index into the dense array.</param>
        public void MarkChangedByDenseIndexThreadSafe(int denseIndex) => _set.MarkChangedByDenseIndexThreadSafe(denseIndex);

        /// <summary>Returns a <see cref="ComponentSpan{T}"/> view of all stored components for raw iteration.</summary>
        /// <returns>A span view with parallel entity and component arrays.</returns>
        public ComponentSpan<T> AsSpan()
        {
            _set.GetSpan(out var e, out var c);
            return new ComponentSpan<T>(e, c);
        }

        /// <inheritdoc />
        public bool TryRemove(int entity, out IDisposable? disposable) => _set.TryRemove(entity, out disposable);

        /// <summary>Removes the component for <paramref name="entity"/> without tracking disposables.</summary>
        /// <param name="entity">The entity ID.</param>
        /// <returns><c>true</c> if the component was removed; <c>false</c> if not present.</returns>
        public bool Remove(int entity) => _set.Remove(entity);

        /// <inheritdoc />
        public void ClearChangedTicks() => _set.ClearChangedTicks();

        /// <summary>Returns the dense array index for <paramref name="entity"/>, or <c>-1</c> if not present.</summary>
        /// <param name="entity">The entity ID.</param>
        /// <returns>The dense index, or <c>-1</c>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal int DenseIndexOf(int entity) => _set.DenseIndexOf(entity);

        /// <summary>Returns a read-only span of entity IDs in dense order.</summary>
        internal ReadOnlySpan<int> EntitiesSpan() => _set.EntitiesSpan();

        /// <summary>Direct access to the underlying entity ID array (for parallel transforms and generated iteration).</summary>
        public int[] EntitiesArray => _set.EntitiesArray;

        /// <summary>Direct access to the underlying component array (for parallel transforms and generated iteration).</summary>
        public T[] ComponentsArray => _set.ComponentsArray;
    }

    /// <summary>
    /// Span view for high-performance, zero-allocation iteration over a component type.
    /// Provides parallel <see cref="Entities"/> and <see cref="Components"/> spans of the same length.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    public readonly ref struct ComponentSpan<T>
    {
        /// <summary>Read-only span of entity IDs, aligned with <see cref="Components"/>.</summary>
        public readonly ReadOnlySpan<int> Entities;

        /// <summary>Mutable span of component values, aligned with <see cref="Entities"/>.</summary>
        public readonly Span<T> Components;

        /// <summary><c>true</c> when this span contains at least one element.</summary>
        public bool IsValid => !Entities.IsEmpty;

        /// <summary>Creates a new <see cref="ComponentSpan{T}"/> from aligned entity and component spans.</summary>
        /// <param name="entities">The entity ID span.</param>
        /// <param name="components">The component value span.</param>
        public ComponentSpan(ReadOnlySpan<int> entities, Span<T> components)
        {
            Entities = entities;
            Components = components;
        }
    }
}
