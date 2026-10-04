namespace Engine;

public sealed partial class EcsWorld
{
    /// <summary>One component by read-only reference, from <see cref="QueryReadOnly{T}"/>.</summary>
    public readonly ref struct ReadOnlyComponent<T>
    {
        private readonly RefComponent<T> _inner;

        internal ReadOnlyComponent(RefComponent<T> inner) => _inner = inner;

        /// <summary>The entity owning the component.</summary>
        public int Entity => _inner.Entity;

        /// <summary>The component in the store, which cannot be written through.</summary>
        public ref readonly T Component => ref _inner.Component;
    }

    /// <summary>
    /// Every component of one type by read-only reference, which marks nothing changed, narrowed by
    /// <see cref="With{TWith}"/>, <see cref="Without{TWithout}"/> and <see cref="Changed{TChanged}"/>.
    /// For a system that reads large components without copying them and without making
    /// <c>Changed</c> filters and transform propagation see a change that did not happen.
    /// </summary>
    public readonly ref struct ReadOnlyEnumerable<T>
    {
        private readonly RefEnumerable<T> _inner;

        internal ReadOnlyEnumerable(RefEnumerable<T> inner) => _inner = inner;

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public ReadOnlyEnumerable<T> With<TWith>() => new(_inner.With<TWith>());

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public ReadOnlyEnumerable<T> Without<TWithout>() => new(_inner.Without<TWithout>());

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public ReadOnlyEnumerable<T> Changed<TChanged>() => new(_inner.Changed<TChanged>());

        /// <summary>Only entities that got <typeparamref name="TAdded"/> since the reader last looked.</summary>
        public ReadOnlyEnumerable<T> Added<TAdded>() => new(_inner.Added<TAdded>());

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public Enumerator GetEnumerator() => new(_inner.GetEnumerator());

        /// <summary>Walks the store as the by-reference query does, marking nothing.</summary>
        public ref struct Enumerator
        {
            private RefEnumerable<T>.RefEnumerator _inner;

            internal Enumerator(RefEnumerable<T>.RefEnumerator inner) => _inner = inner;

            /// <summary>The current entity and its component.</summary>
            public ReadOnlyComponent<T> Current => new(_inner.Current);

            /// <summary>Moves to the next entity the filter passes.</summary>
            public bool MoveNext() => _inner.MoveNext();
        }
    }

    /// <summary>Two components of one entity by read-only reference, from <see cref="QueryReadOnly{T1,T2}"/>.</summary>
    public readonly ref struct ReadOnlyComponents<T1, T2>
    {
        private readonly RefComponents<T1, T2> _inner;

        internal ReadOnlyComponents(RefComponents<T1, T2> inner) => _inner = inner;

        /// <summary>The entity owning the components.</summary>
        public int Entity => _inner.Entity;

        /// <summary>The first component, which cannot be written through.</summary>
        public ref readonly T1 C1 => ref _inner.C1;

        /// <summary>The second component, which cannot be written through.</summary>
        public ref readonly T2 C2 => ref _inner.C2;
    }

    /// <summary>The entities with both types by read-only reference, marking nothing, as <see cref="ReadOnlyEnumerable{T}"/> is for one.</summary>
    public readonly ref struct ReadOnlyEnumerable<T1, T2>
    {
        private readonly RefEnumerable<T1, T2> _inner;

        internal ReadOnlyEnumerable(RefEnumerable<T1, T2> inner) => _inner = inner;

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public ReadOnlyEnumerable<T1, T2> With<TWith>() => new(_inner.With<TWith>());

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public ReadOnlyEnumerable<T1, T2> Without<TWithout>() => new(_inner.Without<TWithout>());

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public ReadOnlyEnumerable<T1, T2> Changed<TChanged>() => new(_inner.Changed<TChanged>());

        /// <summary>Only entities that got <typeparamref name="TAdded"/> since the reader last looked.</summary>
        public ReadOnlyEnumerable<T1, T2> Added<TAdded>() => new(_inner.Added<TAdded>());

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public Enumerator GetEnumerator() => new(_inner.GetEnumerator());

        /// <summary>Walks the smaller store as the by-reference query does, marking nothing.</summary>
        public ref struct Enumerator
        {
            private RefEnumerable<T1, T2>.RefEnumerator _inner;

            internal Enumerator(RefEnumerable<T1, T2>.RefEnumerator inner) => _inner = inner;

            /// <summary>The current entity and its components.</summary>
            public ReadOnlyComponents<T1, T2> Current => new(_inner.Current);

            /// <summary>Moves to the next entity with both types the filter passes.</summary>
            public bool MoveNext() => _inner.MoveNext();
        }
    }

    /// <summary>Three components of one entity by read-only reference, from <see cref="QueryReadOnly{T1,T2,T3}"/>.</summary>
    public readonly ref struct ReadOnlyComponents<T1, T2, T3>
    {
        private readonly RefComponents<T1, T2, T3> _inner;

        internal ReadOnlyComponents(RefComponents<T1, T2, T3> inner) => _inner = inner;

        /// <summary>The entity owning the components.</summary>
        public int Entity => _inner.Entity;

        /// <summary>The first component, which cannot be written through.</summary>
        public ref readonly T1 C1 => ref _inner.C1;

        /// <summary>The second component, which cannot be written through.</summary>
        public ref readonly T2 C2 => ref _inner.C2;

        /// <summary>The third component, which cannot be written through.</summary>
        public ref readonly T3 C3 => ref _inner.C3;
    }

    /// <summary>The entities with all three types by read-only reference, marking nothing, as <see cref="ReadOnlyEnumerable{T}"/> is for one.</summary>
    public readonly ref struct ReadOnlyEnumerable<T1, T2, T3>
    {
        private readonly RefEnumerable<T1, T2, T3> _inner;

        internal ReadOnlyEnumerable(RefEnumerable<T1, T2, T3> inner) => _inner = inner;

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public ReadOnlyEnumerable<T1, T2, T3> With<TWith>() => new(_inner.With<TWith>());

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public ReadOnlyEnumerable<T1, T2, T3> Without<TWithout>() => new(_inner.Without<TWithout>());

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public ReadOnlyEnumerable<T1, T2, T3> Changed<TChanged>() => new(_inner.Changed<TChanged>());

        /// <summary>Only entities that got <typeparamref name="TAdded"/> since the reader last looked.</summary>
        public ReadOnlyEnumerable<T1, T2, T3> Added<TAdded>() => new(_inner.Added<TAdded>());

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public Enumerator GetEnumerator() => new(_inner.GetEnumerator());

        /// <summary>Walks the smallest store as the by-reference query does, marking nothing.</summary>
        public ref struct Enumerator
        {
            private RefEnumerable<T1, T2, T3>.RefEnumerator _inner;

            internal Enumerator(RefEnumerable<T1, T2, T3>.RefEnumerator inner) => _inner = inner;

            /// <summary>The current entity and its components.</summary>
            public ReadOnlyComponents<T1, T2, T3> Current => new(_inner.Current);

            /// <summary>Moves to the next entity with all three types the filter passes.</summary>
            public bool MoveNext() => _inner.MoveNext();
        }
    }

    /// <summary>Every <typeparamref name="T"/> by read-only reference, marking nothing changed, as a system that only reads uses.</summary>
    /// <example>
    /// <code>
    /// foreach (var row in ecs.QueryReadOnly&lt;Transform&gt;())
    ///     bounds = BoundingBox.Around(bounds, row.Component.Position);
    /// </code>
    /// </example>
    public ReadOnlyEnumerable<T> QueryReadOnly<T>()
    {
        var store = GetStore<T>(create: false);
        return new(store is null || store.Count == 0 ? RefEnumerable<T>.From(default) : RefEnumerable<T>.FromStore(store, markOnIterate: false, this));
    }

    /// <summary>The entities with both types by read-only reference, marking nothing changed.</summary>
    public ReadOnlyEnumerable<T1, T2> QueryReadOnly<T1, T2>()
    {
        var s1 = GetStore<T1>(create: false);
        var s2 = GetStore<T2>(create: false);
        return new(s1 is null || s2 is null ? RefEnumerable<T1, T2>.Empty() : RefEnumerable<T1, T2>.From(s1, s2, markOnIterate: false, this));
    }

    /// <summary>The entities with all three types by read-only reference, marking nothing changed.</summary>
    public ReadOnlyEnumerable<T1, T2, T3> QueryReadOnly<T1, T2, T3>()
    {
        var s1 = GetStore<T1>(create: false);
        var s2 = GetStore<T2>(create: false);
        var s3 = GetStore<T3>(create: false);
        return new(s1 is null || s2 is null || s3 is null ? RefEnumerable<T1, T2, T3>.Empty() : RefEnumerable<T1, T2, T3>.From(s1, s2, s3, markOnIterate: false, this));
    }
}
