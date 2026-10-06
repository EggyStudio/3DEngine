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
        private readonly FrameClock? _frame;

        /// <summary>A store whose readers outside a system count changes from the start of <paramref name="frame"/>.</summary>
        internal ComponentStore(FrameClock? frame = null) => _frame = frame;

        // The tick after which a write counts as changed to the reader. It is the tick the running
        // system last ran at, or outside a system the start of the world's frame.
        private long Since => ChangeTicks.Since(_frame?.Start ?? 0);

        /// <summary>Number of components currently stored.</summary>
        public int Count => _set.Count;

        /// <summary>Pre-allocates capacity in the dense and sparse arrays to reduce resizing during bulk spawns.</summary>
        /// <param name="componentCapacity">Minimum dense array capacity.</param>
        /// <param name="maxEntityIdHint">Hint for the maximum expected entity ID to size the sparse array.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Reserve(int componentCapacity, int maxEntityIdHint)
            => _set.Reserve(componentCapacity, maxEntityIdHint);

        /// <summary>Adds a component to <paramref name="entity"/>, marking it added, or overwrites the one it has, marking it changed.</summary>
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

        /// <summary>The component of <paramref name="entity"/>, boxed, or <c>null</c> when it has none.</summary>
        /// <remarks>For the console and tools, which name a component's type at run time. A system reads <see cref="TryGet"/>, which does not box.</remarks>
        public object? GetBoxed(int entity) => _set.TryGet(entity, out var value) ? value : null;

        /// <summary>Replaces the component of <paramref name="entity"/> with a boxed value, marking it changed.</summary>
        /// <returns>Whether it was replaced, which it is not when the value is not a <typeparamref name="T"/> or the entity has none.</returns>
        public bool SetBoxed(int entity, object value)
        {
            if (value is not T typed || !_set.Has(entity)) return false;
            _set.Update(entity, typed);
            return true;
        }

        /// <inheritdoc />
        void IComponentStore.AddTo(EcsWorld world, int entity, object value) => world.Add(entity, (T)value);

        /// <summary>
        /// Whether the component on <paramref name="entity"/> changed since the running system last
        /// ran, or outside a system, since the frame began.
        /// </summary>
        public bool Changed(int entity) => _set.ChangedSince(entity, Since);

        /// <summary>
        /// Whether the component on <paramref name="entity"/> changed after <paramref name="since"/>,
        /// a tick the caller took, as a system handing work to other threads, whose own runs are not
        /// systems, takes its <see cref="ChangeTicks.Since"/> before it does.
        /// </summary>
        internal bool ChangedAfter(int entity, long since) => _set.ChangedSince(entity, since);

        /// <summary>Whether <paramref name="entity"/> got the component after <paramref name="since"/>, as <see cref="ChangedAfter"/> asks of a change.</summary>
        internal bool AddedAfter(int entity, long since) => _set.AddedSince(entity, since);

        /// <summary>
        /// Whether <paramref name="entity"/> got this component since the running system last
        /// ran, or outside a system, since the frame began, not having had one before.
        /// </summary>
        public bool Added(int entity) => _set.AddedSince(entity, Since);

        /// <summary>
        /// Adds to <paramref name="into"/> every entity that lost this component since the running
        /// system last ran, or outside a system, since the frame began, oldest first.
        /// </summary>
        public void Removed(List<int> into) => _set.RemovedSince(Since, into);

        /// <inheritdoc />
        public void PruneRemovals(long before) => _set.PruneRemovals(before);

        /// <summary>Whether any component in the store changed since the running system last ran, or outside a system, since the frame began.</summary>
        public bool AnyChanged() => _set.AnyChangedSince(Since);

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

        /// <summary>Marks the component at <paramref name="denseIndex"/> as changed, with the writer's tick.</summary>
        /// <param name="denseIndex">Zero-based index into the dense array.</param>
        /// <param name="tick">The current frame tick (reserved for future use).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void MarkChangedByDenseIndex(int denseIndex, int tick) => _set.MarkChangedByDenseIndex(denseIndex);

        /// <summary>Thread-safe version of <see cref="MarkChangedByDenseIndex"/>. Uses atomic operations.</summary>
        /// <param name="denseIndex">Zero-based index into the dense array.</param>
        public void MarkChangedByDenseIndexThreadSafe(int denseIndex) => _set.MarkChangedByDenseIndexThreadSafe(denseIndex);

        /// <summary>Stamps every stored component as changed.</summary>
        public void StampAll() => _set.StampAll();

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

        /// <summary>Returns the dense array index for <paramref name="entity"/>, or <c>-1</c> if not present.</summary>
        /// <param name="entity">The entity ID.</param>
        /// <returns>The dense index, or <c>-1</c>.</returns>
        /// <remarks>Public for the code the behavior generator emits into a program, which finds a method's component parameters by it.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DenseIndexOf(int entity) => _set.DenseIndexOf(entity);

        /// <summary>Returns a read-only span of entity IDs in dense order.</summary>
        internal ReadOnlySpan<int> EntitiesSpan() => _set.EntitiesSpan();

        /// <summary>Direct access to the underlying entity ID array (for parallel transforms and generated iteration).</summary>
        public int[] EntitiesArray => _set.EntitiesArray;

        /// <summary>Direct access to the underlying component array (for parallel transforms and generated iteration).</summary>
        public T[] ComponentsArray => _set.ComponentsArray;
    }

    /// <summary>A read-only span view of a component type: its entities and their components, aligned.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    public readonly ref struct ReadOnlyComponentSpan<T>
    {
        /// <summary>The entity ids, aligned with <see cref="Components"/>.</summary>
        public readonly ReadOnlySpan<int> Entities;

        /// <summary>The component values, aligned with <see cref="Entities"/>.</summary>
        public readonly ReadOnlySpan<T> Components;

        /// <summary><c>true</c> when the span holds at least one component.</summary>
        public bool IsValid => !Entities.IsEmpty;

        /// <summary>Creates a view from aligned entity and component spans.</summary>
        public ReadOnlyComponentSpan(ReadOnlySpan<int> entities, ReadOnlySpan<T> components)
        {
            Entities = entities;
            Components = components;
        }
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
