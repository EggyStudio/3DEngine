namespace Engine;

public sealed partial class EcsWorld
{
    /// <summary>Zero-allocation ref wrapper for a single component, providing direct mutable access.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <example>
    /// <code>
    /// foreach (var rc in ecs.QueryRef&lt;Position&gt;())
    /// {
    ///     int entity = rc.Entity;
    ///     rc.Component.X += 1.0f; // direct mutation by ref
    /// }
    /// </code>
    /// </example>
    public readonly ref struct RefComponent<T>
    {
        /// <summary>The entity ID owning this component.</summary>
        public readonly int Entity;
        private readonly Span<T> _components;
        private readonly int _index;

        /// <summary>A mutable reference to the component value in the dense array.</summary>
        public ref T Component => ref _components[_index];

        private RefComponent(int entity, Span<T> components, int index)
        {
            Entity = entity;
            _components = components;
            _index = index;
        }

        /// <summary>Factory method creating a new <see cref="RefComponent{T}"/>.</summary>
        internal static RefComponent<T> Create(int entity, Span<T> comps, int index) => new(entity, comps, index);
    }

    /// <summary>
    /// Up to four component types an entity must have, four it must not have, and four that must
    /// have changed this frame, checked per entity by a filtered query. A struct of fixed slots, so
    /// a filtered query allocates nothing.
    /// </summary>
    public readonly struct QueryFilter
    {
        private readonly IComponentStore? _w0, _w1, _w2, _w3, _n0, _n1, _n2, _n3, _c0, _c1, _c2, _c3;
        private readonly byte _with, _without, _changed;
        private readonly bool _impossible;

        private QueryFilter(QueryFilter from, int list, IComponentStore? store)
        {
            this = from;
            // A required type that has no store yet means no entity can pass.
            if (list != 1 && store is null)
            {
                _impossible = true;
                return;
            }

            switch (list)
            {
                case 0:
                    if (_with >= 4) throw new InvalidOperationException("A query takes at most four With filters.");
                    if (_with == 0) _w0 = store; else if (_with == 1) _w1 = store; else if (_with == 2) _w2 = store; else _w3 = store;
                    _with++;
                    break;
                case 1:
                    // A forbidden type that has no store yet forbids nothing.
                    if (store is null) return;
                    if (_without >= 4) throw new InvalidOperationException("A query takes at most four Without filters.");
                    if (_without == 0) _n0 = store; else if (_without == 1) _n1 = store; else if (_without == 2) _n2 = store; else _n3 = store;
                    _without++;
                    break;
                default:
                    if (_changed >= 4) throw new InvalidOperationException("A query takes at most four Changed filters.");
                    if (_changed == 0) _c0 = store; else if (_changed == 1) _c1 = store; else if (_changed == 2) _c2 = store; else _c3 = store;
                    _changed++;
                    break;
            }
        }

        internal QueryFilter With(IComponentStore? store) => new(this, 0, store);
        internal QueryFilter Without(IComponentStore? store) => new(this, 1, store);
        internal QueryFilter Changed(IComponentStore? store) => new(this, 2, store);

        /// <summary>Whether the filter has anything to check.</summary>
        public bool IsEmpty => !_impossible && _with == 0 && _without == 0 && _changed == 0;

        /// <summary>Whether <paramref name="entity"/> passes every filter.</summary>
        public bool Passes(int entity)
        {
            if (_impossible) return false;
            if (_with > 0 && !_w0!.Has(entity)) return false;
            if (_with > 1 && !_w1!.Has(entity)) return false;
            if (_with > 2 && !_w2!.Has(entity)) return false;
            if (_with > 3 && !_w3!.Has(entity)) return false;
            if (_without > 0 && _n0!.Has(entity)) return false;
            if (_without > 1 && _n1!.Has(entity)) return false;
            if (_without > 2 && _n2!.Has(entity)) return false;
            if (_without > 3 && _n3!.Has(entity)) return false;
            if (_changed > 0 && !_c0!.Changed(entity)) return false;
            if (_changed > 1 && !_c1!.Changed(entity)) return false;
            if (_changed > 2 && !_c2!.Changed(entity)) return false;
            if (_changed > 3 && !_c3!.Changed(entity)) return false;
            return true;
        }
    }

    /// <summary>
    /// A <c>foreach</c>-able view of every component of one type, by reference, narrowed by
    /// <see cref="With{TWith}"/>, <see cref="Without{TWithout}"/> and <see cref="Changed{TChanged}"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// foreach (var row in ecs.QueryRef&lt;Velocity&gt;().With&lt;Falls&gt;().Without&lt;Grounded&gt;())
    ///     row.Component.Y -= 9.81f * dt;
    /// </code>
    /// </example>
    public readonly ref struct RefEnumerable<T>
    {
        private readonly ReadOnlySpan<int> _entities;
        private readonly Span<T> _components;
        private readonly ComponentStore<T>? _store;
        private readonly bool _markOnIterate;
        private readonly EcsWorld? _world;
        private readonly QueryFilter _filter;

        private RefEnumerable(ReadOnlySpan<int> entities, Span<T> components, ComponentStore<T>? store, bool markOnIterate,
            EcsWorld? world = null, QueryFilter filter = default)
        {
            _entities = entities;
            _components = components;
            _store = store;
            _markOnIterate = markOnIterate;
            _world = world;
            _filter = filter;
        }

        /// <summary>A view over a span of components, with no store to mark changes in.</summary>
        public static RefEnumerable<T> From(ComponentSpan<T> span) => new(span.Entities, span.Components, null, false);

        internal static RefEnumerable<T> FromStore(ComponentStore<T> store, bool markOnIterate, EcsWorld? world = null)
        {
            var span = store.AsSpan();
            return new RefEnumerable<T>(span.Entities, span.Components, store, markOnIterate, world);
        }

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public RefEnumerable<T> With<TWith>() => Filtered(_filter.With(_world?.StoreOrNull<TWith>()));

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public RefEnumerable<T> Without<TWithout>() => Filtered(_filter.Without(_world?.StoreOrNull<TWithout>()));

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public RefEnumerable<T> Changed<TChanged>() => Filtered(_filter.Changed(_world?.StoreOrNull<TChanged>()));

        private RefEnumerable<T> Filtered(QueryFilter filter) =>
            new(_entities, _components, _store, _markOnIterate, _world, filter);

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public RefEnumerator GetEnumerator() => new(_entities, _components, _store, _markOnIterate, _filter);

        /// <summary>Walks the dense array, skipping entities the filter refuses.</summary>
        public ref struct RefEnumerator
        {
            private ReadOnlySpan<int> _entities;
            private Span<T> _components;
            private int _index;
            private readonly ComponentStore<T>? _store;
            private readonly bool _mark;
            private readonly QueryFilter _filter;

            internal RefEnumerator(ReadOnlySpan<int> entities, Span<T> components, ComponentStore<T>? store, bool mark, QueryFilter filter)
            {
                _entities = entities;
                _components = components;
                _index = -1;
                _store = store;
                _mark = mark;
                _filter = filter;
            }

            /// <summary>The current entity and its component by reference. Reading it marks the component changed.</summary>
            public RefComponent<T> Current
            {
                get
                {
                    if (_mark && _store != null) _store.MarkChangedByDenseIndex(_index, 0);
                    return RefComponent<T>.Create(_entities[_index], _components, _index);
                }
            }

            /// <summary>Moves to the next entity that passes the filter.</summary>
            public bool MoveNext()
            {
                do _index++;
                while (_index < _entities.Length && !_filter.IsEmpty && !_filter.Passes(_entities[_index]));
                return _index < _entities.Length;
            }
        }
    }

    /// <summary>Zero-allocation ref wrapper for a pair of components on the same entity.</summary>
    /// <typeparam name="T1">The first component type.</typeparam>
    /// <typeparam name="T2">The second component type.</typeparam>
    /// <example>
    /// <code>
    /// foreach (var pair in ecs.QueryRef&lt;Position, Velocity&gt;())
    /// {
    ///     ref var pos = ref pair.C1;
    ///     ref var vel = ref pair.C2;
    ///     pos.X += vel.X * dt;
    /// }
    /// </code>
    /// </example>
    public readonly ref struct RefComponents<T1, T2>
    {
        /// <summary>The entity ID owning these components.</summary>
        public readonly int Entity;
        private readonly ComponentStore<T1> _s1;
        private readonly ComponentStore<T2> _s2;
        private readonly int _e;

        private RefComponents(int entity, ComponentStore<T1> s1, ComponentStore<T2> s2)
        {
            Entity = entity;
            _s1 = s1;
            _s2 = s2;
            _e = entity;
        }

        /// <summary>Creates a new <see cref="RefComponents{T1,T2}"/> for the given entity and stores.</summary>
        internal static RefComponents<T1, T2> Create(int entity, ComponentStore<T1> s1, ComponentStore<T2> s2) =>
            new(entity, s1, s2);

        /// <summary>A mutable reference to the first component.</summary>
        public ref T1 C1 => ref _s1.GetRef(_e);

        /// <summary>A mutable reference to the second component.</summary>
        public ref T2 C2 => ref _s2.GetRef(_e);
    }

    /// <summary>
    /// Zero-allocation ref-based enumerable for iterating entities that match both
    /// <typeparamref name="T1"/> and <typeparamref name="T2"/>, providing direct mutable access to both.
    /// Returned by <see cref="EcsWorld.QueryRef{T1,T2}"/>, and narrowed by
    /// <see cref="With{TWith}"/>, <see cref="Without{TWithout}"/> and <see cref="Changed{TChanged}"/>
    /// as the single-component query is.
    /// </summary>
    /// <typeparam name="T1">The first component type.</typeparam>
    /// <typeparam name="T2">The second component type.</typeparam>
    /// <example>
    /// <code>
    /// // Apply drag to all entities with Position and Velocity
    /// foreach (var pair in ecs.QueryRef&lt;Position, Velocity&gt;())
    /// {
    ///     pair.C2.X *= 0.99f;
    ///     pair.C2.Y *= 0.99f;
    ///     pair.C1.X += pair.C2.X * dt;
    ///     pair.C1.Y += pair.C2.Y * dt;
    /// }
    /// </code>
    /// </example>
    public readonly ref struct RefEnumerable<T1, T2>
    {
        private readonly ComponentStore<T1>? _a;
        private readonly ComponentStore<T2>? _b;
        private readonly int _which;
        private readonly bool _markOnIterate;
        private readonly EcsWorld? _world;
        private readonly QueryFilter _filter;

        private RefEnumerable(ComponentStore<T1>? a, ComponentStore<T2>? b, int which, bool markOnIterate,
            EcsWorld? world = null, QueryFilter filter = default)
        {
            _a = a;
            _b = b;
            _which = which;
            _markOnIterate = markOnIterate;
            _world = world;
            _filter = filter;
        }

        /// <summary>Returns an empty enumerable that yields no results.</summary>
        /// <returns>An empty <see cref="RefEnumerable{T1,T2}"/>.</returns>
        internal static RefEnumerable<T1, T2> Empty() => new(null, null, 0, false);

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public RefEnumerable<T1, T2> With<TWith>() => Filtered(_filter.With(_world?.StoreOrNull<TWith>()));

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public RefEnumerable<T1, T2> Without<TWithout>() => Filtered(_filter.Without(_world?.StoreOrNull<TWithout>()));

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public RefEnumerable<T1, T2> Changed<TChanged>() => Filtered(_filter.Changed(_world?.StoreOrNull<TChanged>()));

        private RefEnumerable<T1, T2> Filtered(QueryFilter filter) => new(_a, _b, _which, _markOnIterate, _world, filter);

        /// <summary>Creates an enumerable that iterates the smaller of the two stores for optimal performance.</summary>
        /// <param name="a">Store for <typeparamref name="T1"/>.</param>
        /// <param name="b">Store for <typeparamref name="T2"/>.</param>
        /// <param name="markOnIterate">When <c>true</c>, each accessed component pair is marked changed.</param>
        /// <returns>A new enumerable wrapping both stores.</returns>
        internal static RefEnumerable<T1, T2> From(ComponentStore<T1> a, ComponentStore<T2> b, bool markOnIterate, EcsWorld? world = null) =>
            new(a, b, a.Count <= b.Count ? 1 : 2, markOnIterate, world);

        /// <summary>Returns the enumerator for <c>foreach</c> iteration.</summary>
        /// <returns>A <see cref="RefEnumerator"/>.</returns>
        public RefEnumerator GetEnumerator() => new(_a, _b, _which, _markOnIterate, _filter);

        /// <summary>
        /// Ref-based enumerator yielding <see cref="RefComponents{T1,T2}"/> for entities that have both component types.
        /// Iterates the smaller store and probes the larger one for matching entities.
        /// </summary>
        public ref struct RefEnumerator
        {
            private readonly ComponentStore<T1>? _a;
            private readonly ComponentStore<T2>? _b;
            private readonly int _which;
            private readonly bool _mark;
            private readonly QueryFilter _filter;
            private int _i;

            /// <summary>Creates a new two-component enumerator positioned before the first element.</summary>
            internal RefEnumerator(ComponentStore<T1>? a, ComponentStore<T2>? b, int which, bool mark, QueryFilter filter)
            {
                _a = a;
                _b = b;
                _which = which;
                _mark = mark;
                _filter = filter;
                _i = -1;
            }

            /// <summary>Gets the current <see cref="RefComponents{T1,T2}"/> pair with mutable access to both components.</summary>
            public RefComponents<T1, T2> Current
            {
                get
                {
                    int e = _which == 1 ? _a!.EntityByDenseIndex(_i) : _b!.EntityByDenseIndex(_i);
                    if (_mark)
                    {
                        if (_which == 1)
                        {
                            _a!.MarkChangedByDenseIndex(_i, 0);
                            int j = _b!.DenseIndexOf(e);
                            if (j >= 0) _b.MarkChangedByDenseIndex(j, 0);
                        }
                        else
                        {
                            _b!.MarkChangedByDenseIndex(_i, 0);
                            int j = _a!.DenseIndexOf(e);
                            if (j >= 0) _a.MarkChangedByDenseIndex(j, 0);
                        }
                    }

                    return RefComponents<T1, T2>.Create(e, _a!, _b!);
                }
            }

            /// <summary>Advances to the next entity that has both component types.</summary>
            /// <returns><c>true</c> if a matching entity was found; otherwise <c>false</c>.</returns>
            public bool MoveNext()
            {
                if (_which == 0 || _a == null || _b == null) return false;
                do
                {
                    _i++;
                    if (_which == 1)
                    {
                        if (_i >= _a.Count) return false;
                        int e = _a.EntityByDenseIndex(_i);
                        if (_b.Has(e) && (_filter.IsEmpty || _filter.Passes(e))) return true;
                    }
                    else
                    {
                        if (_i >= _b.Count) return false;
                        int e = _b.EntityByDenseIndex(_i);
                        if (_a.Has(e) && (_filter.IsEmpty || _filter.Passes(e))) return true;
                    }
                } while (true);
            }
        }
    }
}
