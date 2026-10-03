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
    /// Zero-allocation ref-based enumerable for iterating a single component type with direct mutable access.
    /// Returned by <see cref="EcsWorld.QueryRef{T}"/>.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <example>
    /// <code>
    /// // Heal all entities with a Health component
    /// foreach (var rc in ecs.QueryRef&lt;Health&gt;())
    ///     rc.Component.Current = Math.Min(rc.Component.Current + 1, rc.Component.Max);
    /// </code>
    /// </example>
    public readonly ref struct RefEnumerable<T>
    {
        private readonly ReadOnlySpan<int> _entities;
        private readonly Span<T> _components;
        private readonly ComponentStore<T>? _store;
        private readonly bool _markOnIterate;

        private RefEnumerable(ReadOnlySpan<int> entities, Span<T> components, ComponentStore<T>? store, bool markOnIterate)
        {
            _entities = entities;
            _components = components;
            _store = store;
            _markOnIterate = markOnIterate;
        }

        /// <summary>Creates a <see cref="RefEnumerable{T}"/> from a pre-existing <see cref="ComponentSpan{T}"/>.</summary>
        /// <param name="span">The span to iterate over.</param>
        /// <returns>A new enumerable wrapping the span (no change marking).</returns>
        public static RefEnumerable<T> From(ComponentSpan<T> span) => new(span.Entities, span.Components, null, false);

        /// <summary>Creates a <see cref="RefEnumerable{T}"/> from a component store, optionally marking iterated components as changed.</summary>
        /// <param name="store">The component store to iterate.</param>
        /// <param name="markOnIterate">When <c>true</c>, each accessed component is marked changed.</param>
        /// <returns>A new enumerable wrapping the store's data.</returns>
        internal static RefEnumerable<T> FromStore(ComponentStore<T> store, bool markOnIterate)
        {
            var span = store.AsSpan();
            return new RefEnumerable<T>(span.Entities, span.Components, store, markOnIterate);
        }

        /// <summary>Returns the enumerator for <c>foreach</c> iteration.</summary>
        /// <returns>A <see cref="RefEnumerator"/>.</returns>
        public RefEnumerator GetEnumerator() => new(_entities, _components, _store, _markOnIterate);

        /// <summary>Ref-based enumerator yielding <see cref="RefComponent{T}"/> instances with direct mutable access.</summary>
        public ref struct RefEnumerator
        {
            private ReadOnlySpan<int> _entities;
            private Span<T> _components;
            private int _index;
            private readonly ComponentStore<T>? _store;
            private readonly bool _mark;

            /// <summary>Creates a new enumerator positioned before the first element.</summary>
            internal RefEnumerator(ReadOnlySpan<int> entities, Span<T> components, ComponentStore<T>? store, bool mark)
            {
                _entities = entities;
                _components = components;
                _index = -1;
                _store = store;
                _mark = mark;
            }

            /// <summary>Gets the current <see cref="RefComponent{T}"/> with mutable component access.</summary>
            public RefComponent<T> Current
            {
                get
                {
                    if (_mark && _store != null) _store.MarkChangedByDenseIndex(_index, 0);
                    return RefComponent<T>.Create(_entities[_index], _components, _index);
                }
            }

            /// <summary>Advances to the next element.</summary>
            /// <returns><c>true</c> if there is a next element; otherwise <c>false</c>.</returns>
            public bool MoveNext()
            {
                _index++;
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
    /// Returned by <see cref="EcsWorld.QueryRef{T1,T2}"/>.
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

        private RefEnumerable(ComponentStore<T1>? a, ComponentStore<T2>? b, int which, bool markOnIterate)
        {
            _a = a;
            _b = b;
            _which = which;
            _markOnIterate = markOnIterate;
        }

        /// <summary>Returns an empty enumerable that yields no results.</summary>
        /// <returns>An empty <see cref="RefEnumerable{T1,T2}"/>.</returns>
        internal static RefEnumerable<T1, T2> Empty() => new(null, null, 0, false);

        /// <summary>Creates an enumerable that iterates the smaller of the two stores for optimal performance.</summary>
        /// <param name="a">Store for <typeparamref name="T1"/>.</param>
        /// <param name="b">Store for <typeparamref name="T2"/>.</param>
        /// <param name="markOnIterate">When <c>true</c>, each accessed component pair is marked changed.</param>
        /// <returns>A new enumerable wrapping both stores.</returns>
        internal static RefEnumerable<T1, T2> From(ComponentStore<T1> a, ComponentStore<T2> b, bool markOnIterate) =>
            new(a, b, a.Count <= b.Count ? 1 : 2, markOnIterate);

        /// <summary>Returns the enumerator for <c>foreach</c> iteration.</summary>
        /// <returns>A <see cref="RefEnumerator"/>.</returns>
        public RefEnumerator GetEnumerator() => new(_a, _b, _which, _markOnIterate);

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
            private int _i;

            /// <summary>Creates a new two-component enumerator positioned before the first element.</summary>
            internal RefEnumerator(ComponentStore<T1>? a, ComponentStore<T2>? b, int which, bool mark)
            {
                _a = a;
                _b = b;
                _which = which;
                _mark = mark;
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
                        if (_b.Has(e)) return true;
                    }
                    else
                    {
                        if (_i >= _b.Count) return false;
                        int e = _b.EntityByDenseIndex(_i);
                        if (_a.Has(e)) return true;
                    }
                } while (true);
            }
        }
    }
}
