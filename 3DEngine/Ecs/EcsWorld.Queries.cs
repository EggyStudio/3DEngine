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

        /// <summary>Only entities that got <typeparamref name="TAdded"/> since the reader last looked.</summary>
        public CopyQuery<T> Added<TAdded>() => new(_world, _filter.Added(_world.StoreOrNull<TAdded>()));

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

        /// <summary>Only entities that got <typeparamref name="TAdded"/> since the reader last looked.</summary>
        public CopyQuery<T1, T2> Added<TAdded>() => new(_world, _filter.Added(_world.StoreOrNull<TAdded>()));

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

        /// <summary>Only entities that got <typeparamref name="TAdded"/> since the reader last looked.</summary>
        public CopyQuery<T1, T2, T3> Added<TAdded>() => new(_world, _filter.Added(_world.StoreOrNull<TAdded>()));

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

    /// <summary>
    /// The entities with every one of its four component types, with a copy of each, from
    /// <see cref="EcsWorld.Query{T1,T2,T3,T4}"/>. It walks the smallest store and probes the others,
    /// and is filtered and enumerated as <see cref="CopyQuery{T}"/> is.
    /// </summary>
    public readonly struct CopyQuery<T1, T2, T3, T4> : IEnumerable<(int Entity, T1 C1, T2 C2, T3 C3, T4 C4)>
    {
        private readonly EcsWorld _world;
        private readonly QueryFilter _filter;

        internal CopyQuery(EcsWorld world, QueryFilter filter = default)
        {
            _world = world;
            _filter = filter;
        }

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public CopyQuery<T1, T2, T3, T4> With<TWith>() => new(_world, _filter.With(_world.StoreOrNull<TWith>()));

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public CopyQuery<T1, T2, T3, T4> Without<TWithout>() => new(_world, _filter.Without(_world.StoreOrNull<TWithout>()));

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public CopyQuery<T1, T2, T3, T4> Changed<TChanged>() => new(_world, _filter.Changed(_world.StoreOrNull<TChanged>()));

        /// <summary>Only entities that got <typeparamref name="TAdded"/> since the reader last looked.</summary>
        public CopyQuery<T1, T2, T3, T4> Added<TAdded>() => new(_world, _filter.Added(_world.StoreOrNull<TAdded>()));

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public Enumerator GetEnumerator() => new(_world.GetStore<T1>(create: false), _world.GetStore<T2>(create: false), _world.GetStore<T3>(create: false), _world.GetStore<T4>(create: false), _filter);

        IEnumerator<(int Entity, T1 C1, T2 C2, T3 C3, T4 C4)> IEnumerable<(int Entity, T1 C1, T2 C2, T3 C3, T4 C4)>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Walks the smallest store, skipping entities without the other types or refused by the filter.</summary>
        public struct Enumerator : IEnumerator<(int Entity, T1 C1, T2 C2, T3 C3, T4 C4)>
        {
            private readonly ComponentStore<T1>? _s1;
            private readonly ComponentStore<T2>? _s2;
            private readonly ComponentStore<T3>? _s3;
            private readonly ComponentStore<T4>? _s4;
            // Which store is walked, counted from 1, or 0 where a type has no store.
            private readonly int _driver;
            private readonly QueryFilter _filter;
            private int _index;
            private (int, T1, T2, T3, T4) _current;

            internal Enumerator(ComponentStore<T1>? s1, ComponentStore<T2>? s2, ComponentStore<T3>? s3, ComponentStore<T4>? s4, QueryFilter filter)
            {
                _s1 = s1;
                _s2 = s2;
                _s3 = s3;
                _s4 = s4;
                _driver = s1 is null || s2 is null || s3 is null || s4 is null ? 0 : Smallest([s1.Count, s2.Count, s3.Count, s4.Count]);
                _filter = filter;
                _index = -1;
                _current = default;
            }

            /// <inheritdoc />
            public readonly (int Entity, T1 C1, T2 C2, T3 C3, T4 C4) Current => _current;

            readonly object IEnumerator.Current => Current;

            /// <inheritdoc />
            public bool MoveNext()
            {
                while (++_index < DriverCount())
                {
                    int e = EntityAt(_index);
                    if (!_s1!.TryGet(e, out var c1) || !_s2!.TryGet(e, out var c2) || !_s3!.TryGet(e, out var c3) || !_s4!.TryGet(e, out var c4)) continue;
                    if (!_filter.IsEmpty && !_filter.Passes(e)) continue;
                    _current = (e, c1, c2, c3, c4);
                    return true;
                }
                return false;
            }

            private readonly int DriverCount() => _driver switch { 1 => _s1!.Count, 2 => _s2!.Count, 3 => _s3!.Count, 4 => _s4!.Count, _ => 0 };

            private readonly int EntityAt(int index) => _driver switch
            {
                1 => _s1!.EntityByDenseIndex(index),
                2 => _s2!.EntityByDenseIndex(index),
                3 => _s3!.EntityByDenseIndex(index),
                _ => _s4!.EntityByDenseIndex(index),
            };

            /// <inheritdoc />
            public void Reset() => _index = -1;

            /// <inheritdoc />
            public readonly void Dispose() { }
        }
    }

    /// <summary>
    /// The entities with every one of its five component types, with a copy of each, from
    /// <see cref="EcsWorld.Query{T1,T2,T3,T4,T5}"/>. It walks the smallest store and probes the others,
    /// and is filtered and enumerated as <see cref="CopyQuery{T}"/> is.
    /// </summary>
    public readonly struct CopyQuery<T1, T2, T3, T4, T5> : IEnumerable<(int Entity, T1 C1, T2 C2, T3 C3, T4 C4, T5 C5)>
    {
        private readonly EcsWorld _world;
        private readonly QueryFilter _filter;

        internal CopyQuery(EcsWorld world, QueryFilter filter = default)
        {
            _world = world;
            _filter = filter;
        }

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public CopyQuery<T1, T2, T3, T4, T5> With<TWith>() => new(_world, _filter.With(_world.StoreOrNull<TWith>()));

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public CopyQuery<T1, T2, T3, T4, T5> Without<TWithout>() => new(_world, _filter.Without(_world.StoreOrNull<TWithout>()));

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public CopyQuery<T1, T2, T3, T4, T5> Changed<TChanged>() => new(_world, _filter.Changed(_world.StoreOrNull<TChanged>()));

        /// <summary>Only entities that got <typeparamref name="TAdded"/> since the reader last looked.</summary>
        public CopyQuery<T1, T2, T3, T4, T5> Added<TAdded>() => new(_world, _filter.Added(_world.StoreOrNull<TAdded>()));

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public Enumerator GetEnumerator() => new(_world.GetStore<T1>(create: false), _world.GetStore<T2>(create: false), _world.GetStore<T3>(create: false), _world.GetStore<T4>(create: false), _world.GetStore<T5>(create: false), _filter);

        IEnumerator<(int Entity, T1 C1, T2 C2, T3 C3, T4 C4, T5 C5)> IEnumerable<(int Entity, T1 C1, T2 C2, T3 C3, T4 C4, T5 C5)>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Walks the smallest store, skipping entities without the other types or refused by the filter.</summary>
        public struct Enumerator : IEnumerator<(int Entity, T1 C1, T2 C2, T3 C3, T4 C4, T5 C5)>
        {
            private readonly ComponentStore<T1>? _s1;
            private readonly ComponentStore<T2>? _s2;
            private readonly ComponentStore<T3>? _s3;
            private readonly ComponentStore<T4>? _s4;
            private readonly ComponentStore<T5>? _s5;
            // Which store is walked, counted from 1, or 0 where a type has no store.
            private readonly int _driver;
            private readonly QueryFilter _filter;
            private int _index;
            private (int, T1, T2, T3, T4, T5) _current;

            internal Enumerator(ComponentStore<T1>? s1, ComponentStore<T2>? s2, ComponentStore<T3>? s3, ComponentStore<T4>? s4, ComponentStore<T5>? s5, QueryFilter filter)
            {
                _s1 = s1;
                _s2 = s2;
                _s3 = s3;
                _s4 = s4;
                _s5 = s5;
                _driver = s1 is null || s2 is null || s3 is null || s4 is null || s5 is null ? 0 : Smallest([s1.Count, s2.Count, s3.Count, s4.Count, s5.Count]);
                _filter = filter;
                _index = -1;
                _current = default;
            }

            /// <inheritdoc />
            public readonly (int Entity, T1 C1, T2 C2, T3 C3, T4 C4, T5 C5) Current => _current;

            readonly object IEnumerator.Current => Current;

            /// <inheritdoc />
            public bool MoveNext()
            {
                while (++_index < DriverCount())
                {
                    int e = EntityAt(_index);
                    if (!_s1!.TryGet(e, out var c1) || !_s2!.TryGet(e, out var c2) || !_s3!.TryGet(e, out var c3) || !_s4!.TryGet(e, out var c4) || !_s5!.TryGet(e, out var c5)) continue;
                    if (!_filter.IsEmpty && !_filter.Passes(e)) continue;
                    _current = (e, c1, c2, c3, c4, c5);
                    return true;
                }
                return false;
            }

            private readonly int DriverCount() => _driver switch { 1 => _s1!.Count, 2 => _s2!.Count, 3 => _s3!.Count, 4 => _s4!.Count, 5 => _s5!.Count, _ => 0 };

            private readonly int EntityAt(int index) => _driver switch
            {
                1 => _s1!.EntityByDenseIndex(index),
                2 => _s2!.EntityByDenseIndex(index),
                3 => _s3!.EntityByDenseIndex(index),
                4 => _s4!.EntityByDenseIndex(index),
                _ => _s5!.EntityByDenseIndex(index),
            };

            /// <inheritdoc />
            public void Reset() => _index = -1;

            /// <inheritdoc />
            public readonly void Dispose() { }
        }
    }

    /// <summary>
    /// The entities with every one of its six component types, with a copy of each, from
    /// <see cref="EcsWorld.Query{T1,T2,T3,T4,T5,T6}"/>. It walks the smallest store and probes the others,
    /// and is filtered and enumerated as <see cref="CopyQuery{T}"/> is.
    /// </summary>
    public readonly struct CopyQuery<T1, T2, T3, T4, T5, T6> : IEnumerable<(int Entity, T1 C1, T2 C2, T3 C3, T4 C4, T5 C5, T6 C6)>
    {
        private readonly EcsWorld _world;
        private readonly QueryFilter _filter;

        internal CopyQuery(EcsWorld world, QueryFilter filter = default)
        {
            _world = world;
            _filter = filter;
        }

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public CopyQuery<T1, T2, T3, T4, T5, T6> With<TWith>() => new(_world, _filter.With(_world.StoreOrNull<TWith>()));

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public CopyQuery<T1, T2, T3, T4, T5, T6> Without<TWithout>() => new(_world, _filter.Without(_world.StoreOrNull<TWithout>()));

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public CopyQuery<T1, T2, T3, T4, T5, T6> Changed<TChanged>() => new(_world, _filter.Changed(_world.StoreOrNull<TChanged>()));

        /// <summary>Only entities that got <typeparamref name="TAdded"/> since the reader last looked.</summary>
        public CopyQuery<T1, T2, T3, T4, T5, T6> Added<TAdded>() => new(_world, _filter.Added(_world.StoreOrNull<TAdded>()));

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public Enumerator GetEnumerator() => new(_world.GetStore<T1>(create: false), _world.GetStore<T2>(create: false), _world.GetStore<T3>(create: false), _world.GetStore<T4>(create: false), _world.GetStore<T5>(create: false), _world.GetStore<T6>(create: false), _filter);

        IEnumerator<(int Entity, T1 C1, T2 C2, T3 C3, T4 C4, T5 C5, T6 C6)> IEnumerable<(int Entity, T1 C1, T2 C2, T3 C3, T4 C4, T5 C5, T6 C6)>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Walks the smallest store, skipping entities without the other types or refused by the filter.</summary>
        public struct Enumerator : IEnumerator<(int Entity, T1 C1, T2 C2, T3 C3, T4 C4, T5 C5, T6 C6)>
        {
            private readonly ComponentStore<T1>? _s1;
            private readonly ComponentStore<T2>? _s2;
            private readonly ComponentStore<T3>? _s3;
            private readonly ComponentStore<T4>? _s4;
            private readonly ComponentStore<T5>? _s5;
            private readonly ComponentStore<T6>? _s6;
            // Which store is walked, counted from 1, or 0 where a type has no store.
            private readonly int _driver;
            private readonly QueryFilter _filter;
            private int _index;
            private (int, T1, T2, T3, T4, T5, T6) _current;

            internal Enumerator(ComponentStore<T1>? s1, ComponentStore<T2>? s2, ComponentStore<T3>? s3, ComponentStore<T4>? s4, ComponentStore<T5>? s5, ComponentStore<T6>? s6, QueryFilter filter)
            {
                _s1 = s1;
                _s2 = s2;
                _s3 = s3;
                _s4 = s4;
                _s5 = s5;
                _s6 = s6;
                _driver = s1 is null || s2 is null || s3 is null || s4 is null || s5 is null || s6 is null ? 0 : Smallest([s1.Count, s2.Count, s3.Count, s4.Count, s5.Count, s6.Count]);
                _filter = filter;
                _index = -1;
                _current = default;
            }

            /// <inheritdoc />
            public readonly (int Entity, T1 C1, T2 C2, T3 C3, T4 C4, T5 C5, T6 C6) Current => _current;

            readonly object IEnumerator.Current => Current;

            /// <inheritdoc />
            public bool MoveNext()
            {
                while (++_index < DriverCount())
                {
                    int e = EntityAt(_index);
                    if (!_s1!.TryGet(e, out var c1) || !_s2!.TryGet(e, out var c2) || !_s3!.TryGet(e, out var c3) || !_s4!.TryGet(e, out var c4) || !_s5!.TryGet(e, out var c5) || !_s6!.TryGet(e, out var c6)) continue;
                    if (!_filter.IsEmpty && !_filter.Passes(e)) continue;
                    _current = (e, c1, c2, c3, c4, c5, c6);
                    return true;
                }
                return false;
            }

            private readonly int DriverCount() => _driver switch { 1 => _s1!.Count, 2 => _s2!.Count, 3 => _s3!.Count, 4 => _s4!.Count, 5 => _s5!.Count, 6 => _s6!.Count, _ => 0 };

            private readonly int EntityAt(int index) => _driver switch
            {
                1 => _s1!.EntityByDenseIndex(index),
                2 => _s2!.EntityByDenseIndex(index),
                3 => _s3!.EntityByDenseIndex(index),
                4 => _s4!.EntityByDenseIndex(index),
                5 => _s5!.EntityByDenseIndex(index),
                _ => _s6!.EntityByDenseIndex(index),
            };

            /// <inheritdoc />
            public void Reset() => _index = -1;

            /// <inheritdoc />
            public readonly void Dispose() { }
        }
    }

    // Where the smallest of the counts stands, counted from 1, the first among those alike, which a
    // query of four to six types walks as the smaller queries walk the smallest of theirs.
    private static int Smallest(ReadOnlySpan<int> counts)
    {
        int at = 0;
        for (int i = 1; i < counts.Length; i++)
            if (counts[i] < counts[at]) at = i;
        return at + 1;
    }
}
