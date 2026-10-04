using System.Collections;

namespace Engine;

public sealed partial class EcsWorld
{
    /// <summary>
    /// The entities with a <typeparamref name="T"/> and a copy of it each, from
    /// <see cref="EcsWorld.Query{T}"/>, narrowed by <see cref="With{TWith}"/>,
    /// <see cref="Without{TWithout}"/> and <see cref="Changed{TChanged}"/>.
    /// </summary>
    /// <remarks>
    /// A struct whose <c>foreach</c> allocates nothing, which LINQ can still take as an
    /// <see cref="IEnumerable{T}"/>, boxed. The store is looked up when enumeration starts, so a
    /// query made before its first component was added still finds it.
    /// </remarks>
    public readonly struct CopyQuery<T> : IEnumerable<(int Entity, T Component)>
    {
        private readonly EcsWorld _world;
        private readonly QueryFilter _filter;

        internal CopyQuery(EcsWorld world, QueryFilter filter = default)
        {
            _world = world;
            _filter = filter;
        }

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public CopyQuery<T> With<TWith>() => new(_world, _filter.With(_world.StoreOrNull<TWith>()));

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public CopyQuery<T> Without<TWithout>() => new(_world, _filter.Without(_world.StoreOrNull<TWithout>()));

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public CopyQuery<T> Changed<TChanged>() => new(_world, _filter.Changed(_world.StoreOrNull<TChanged>()));

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public Enumerator GetEnumerator() => new(_world.GetStore<T>(create: false), _filter);

        IEnumerator<(int Entity, T Component)> IEnumerable<(int Entity, T Component)>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Walks the store's dense array, skipping entities the filter refuses.</summary>
        public struct Enumerator : IEnumerator<(int Entity, T Component)>
        {
            private readonly ComponentStore<T>? _store;
            private readonly QueryFilter _filter;
            private int _index;

            internal Enumerator(ComponentStore<T>? store, QueryFilter filter)
            {
                _store = store;
                _filter = filter;
                _index = -1;
            }

            /// <inheritdoc />
            public readonly (int Entity, T Component) Current =>
                (_store!.EntityByDenseIndex(_index), _store.ComponentRefByDenseIndex(_index));

            readonly object IEnumerator.Current => Current;

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (_store is null) return false;
                while (++_index < _store.Count)
                    if (_filter.IsEmpty || _filter.Passes(_store.EntityByDenseIndex(_index))) return true;
                return false;
            }

            /// <inheritdoc />
            public void Reset() => _index = -1;

            /// <inheritdoc />
            public readonly void Dispose() { }
        }
    }

    /// <summary>
    /// The entities with both a <typeparamref name="T1"/> and a <typeparamref name="T2"/>, with a copy
    /// of each, from <see cref="EcsWorld.Query{T1,T2}"/>. It walks the smaller store and probes the
    /// other, and is filtered and enumerated as <see cref="CopyQuery{T}"/> is.
    /// </summary>
    public readonly struct CopyQuery<T1, T2> : IEnumerable<(int Entity, T1 C1, T2 C2)>
    {
        private readonly EcsWorld _world;
        private readonly QueryFilter _filter;

        internal CopyQuery(EcsWorld world, QueryFilter filter = default)
        {
            _world = world;
            _filter = filter;
        }

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public CopyQuery<T1, T2> With<TWith>() => new(_world, _filter.With(_world.StoreOrNull<TWith>()));

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public CopyQuery<T1, T2> Without<TWithout>() => new(_world, _filter.Without(_world.StoreOrNull<TWithout>()));

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public CopyQuery<T1, T2> Changed<TChanged>() => new(_world, _filter.Changed(_world.StoreOrNull<TChanged>()));

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public Enumerator GetEnumerator() => new(_world.GetStore<T1>(create: false), _world.GetStore<T2>(create: false), _filter);

        IEnumerator<(int Entity, T1 C1, T2 C2)> IEnumerable<(int Entity, T1 C1, T2 C2)>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Walks the smaller store, skipping entities without the other type or refused by the filter.</summary>
        public struct Enumerator : IEnumerator<(int Entity, T1 C1, T2 C2)>
        {
            private readonly ComponentStore<T1>? _a;
            private readonly ComponentStore<T2>? _b;
            private readonly bool _walkA;
            private readonly QueryFilter _filter;
            private int _index;
            private (int, T1, T2) _current;

            internal Enumerator(ComponentStore<T1>? a, ComponentStore<T2>? b, QueryFilter filter)
            {
                _a = a;
                _b = b;
                _walkA = a is not null && b is not null && a.Count <= b.Count;
                _filter = filter;
                _index = -1;
                _current = default;
            }

            /// <inheritdoc />
            public readonly (int Entity, T1 C1, T2 C2) Current => _current;

            readonly object IEnumerator.Current => Current;

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (_a is null || _b is null) return false;
                int count = _walkA ? _a.Count : _b.Count;
                while (++_index < count)
                {
                    int e = _walkA ? _a.EntityByDenseIndex(_index) : _b.EntityByDenseIndex(_index);
                    if (!_a.TryGet(e, out var c1) || !_b.TryGet(e, out var c2)) continue;
                    if (!_filter.IsEmpty && !_filter.Passes(e)) continue;
                    _current = (e, c1, c2);
                    return true;
                }
                return false;
            }

            /// <inheritdoc />
            public void Reset() => _index = -1;

            /// <inheritdoc />
            public readonly void Dispose() { }
        }
    }

    /// <summary>
    /// The entities with a <typeparamref name="T1"/>, a <typeparamref name="T2"/> and a
    /// <typeparamref name="T3"/>, with a copy of each, from <see cref="EcsWorld.Query{T1,T2,T3}"/>.
    /// It walks the smallest store and probes the others, and is filtered and enumerated as
    /// <see cref="CopyQuery{T}"/> is.
    /// </summary>
    public readonly struct CopyQuery<T1, T2, T3> : IEnumerable<(int Entity, T1 C1, T2 C2, T3 C3)>
    {
        private readonly EcsWorld _world;
        private readonly QueryFilter _filter;

        internal CopyQuery(EcsWorld world, QueryFilter filter = default)
        {
            _world = world;
            _filter = filter;
        }

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public CopyQuery<T1, T2, T3> With<TWith>() => new(_world, _filter.With(_world.StoreOrNull<TWith>()));

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public CopyQuery<T1, T2, T3> Without<TWithout>() => new(_world, _filter.Without(_world.StoreOrNull<TWithout>()));

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public CopyQuery<T1, T2, T3> Changed<TChanged>() => new(_world, _filter.Changed(_world.StoreOrNull<TChanged>()));

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public Enumerator GetEnumerator() =>
            new(_world.GetStore<T1>(create: false), _world.GetStore<T2>(create: false), _world.GetStore<T3>(create: false), _filter);

        IEnumerator<(int Entity, T1 C1, T2 C2, T3 C3)> IEnumerable<(int Entity, T1 C1, T2 C2, T3 C3)>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Walks the smallest store, skipping entities without the other types or refused by the filter.</summary>
        public struct Enumerator : IEnumerator<(int Entity, T1 C1, T2 C2, T3 C3)>
        {
            private readonly ComponentStore<T1>? _a;
            private readonly ComponentStore<T2>? _b;
            private readonly ComponentStore<T3>? _c;
            private readonly IComponentStore? _driver;
            private readonly QueryFilter _filter;
            private int _index;
            private (int, T1, T2, T3) _current;

            internal Enumerator(ComponentStore<T1>? a, ComponentStore<T2>? b, ComponentStore<T3>? c, QueryFilter filter)
            {
                _a = a;
                _b = b;
                _c = c;
                _driver = a is null || b is null || c is null ? null
                    : a.Count <= b.Count && a.Count <= c.Count ? a
                    : b.Count <= c.Count ? b
                    : c;
                _filter = filter;
                _index = -1;
                _current = default;
            }

            /// <inheritdoc />
            public readonly (int Entity, T1 C1, T2 C2, T3 C3) Current => _current;

            readonly object IEnumerator.Current => Current;

            /// <inheritdoc />
            public bool MoveNext()
            {
                if (_driver is null) return false;
                while (++_index < _driver.Count)
                {
                    int e = ReferenceEquals(_driver, _a) ? _a!.EntityByDenseIndex(_index)
                        : ReferenceEquals(_driver, _b) ? _b!.EntityByDenseIndex(_index)
                        : _c!.EntityByDenseIndex(_index);
                    if (!_a!.TryGet(e, out var c1) || !_b!.TryGet(e, out var c2) || !_c!.TryGet(e, out var c3)) continue;
                    if (!_filter.IsEmpty && !_filter.Passes(e)) continue;
                    _current = (e, c1, c2, c3);
                    return true;
                }
                return false;
            }

            /// <inheritdoc />
            public void Reset() => _index = -1;

            /// <inheritdoc />
            public readonly void Dispose() { }
        }
    }
}
