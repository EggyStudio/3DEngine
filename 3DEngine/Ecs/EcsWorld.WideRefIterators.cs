namespace Engine;

// The by-reference queries of four to six components, written as the three-component one beside
// the smaller ones, in a file of their own since together they passed the length a file keeps to.
public sealed partial class EcsWorld
{
    /// <summary>Zero-allocation ref wrapper for four components on the same entity.</summary>
    public readonly ref struct RefComponents<T1, T2, T3, T4>
    {
        /// <summary>The entity ID owning these components.</summary>
        public readonly int Entity;
        private readonly ComponentStore<T1> _s1;
        private readonly ComponentStore<T2> _s2;
        private readonly ComponentStore<T3> _s3;
        private readonly ComponentStore<T4> _s4;

        private RefComponents(int entity, ComponentStore<T1> s1, ComponentStore<T2> s2, ComponentStore<T3> s3, ComponentStore<T4> s4)
        {
            Entity = entity;
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
            _s4 = s4;
        }

        internal static RefComponents<T1, T2, T3, T4> Create(int entity, ComponentStore<T1> s1, ComponentStore<T2> s2, ComponentStore<T3> s3, ComponentStore<T4> s4) =>
            new(entity, s1, s2, s3, s4);

        /// <summary>A mutable reference to the first component.</summary>
        public ref T1 C1 => ref _s1.GetRef(Entity);

        /// <summary>A mutable reference to the second component.</summary>
        public ref T2 C2 => ref _s2.GetRef(Entity);

        /// <summary>A mutable reference to the third component.</summary>
        public ref T3 C3 => ref _s3.GetRef(Entity);

        /// <summary>A mutable reference to the fourth component.</summary>
        public ref T4 C4 => ref _s4.GetRef(Entity);
    }

    /// <summary>
    /// A <c>foreach</c>-able view of the entities that have all four component types, by
    /// reference, narrowed by <see cref="With{TWith}"/>, <see cref="Without{TWithout}"/> and
    /// <see cref="Changed{TChanged}"/> as the smaller queries are.
    /// </summary>
    /// <remarks>
    /// Walks the store with the fewest components and probes the others, so its cost follows the
    /// rarest of the types.
    /// </remarks>
    public readonly ref struct RefEnumerable<T1, T2, T3, T4>
    {
        private readonly ComponentStore<T1>? _s1;
        private readonly ComponentStore<T2>? _s2;
        private readonly ComponentStore<T3>? _s3;
        private readonly ComponentStore<T4>? _s4;
        private readonly bool _markOnIterate;
        private readonly EcsWorld? _world;
        private readonly QueryFilter _filter;

        private RefEnumerable(ComponentStore<T1>? s1, ComponentStore<T2>? s2, ComponentStore<T3>? s3, ComponentStore<T4>? s4, bool markOnIterate, EcsWorld? world = null, QueryFilter filter = default)
        {
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
            _s4 = s4;
            _markOnIterate = markOnIterate;
            _world = world;
            _filter = filter;
        }

        internal static RefEnumerable<T1, T2, T3, T4> Empty() => new(null, null, null, null, false);

        internal static RefEnumerable<T1, T2, T3, T4> From(ComponentStore<T1> s1, ComponentStore<T2> s2, ComponentStore<T3> s3, ComponentStore<T4> s4, bool markOnIterate, EcsWorld world) =>
            new(s1, s2, s3, s4, markOnIterate, world);

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public RefEnumerable<T1, T2, T3, T4> With<TWith>() => Filtered(_filter.With(_world?.StoreOrNull<TWith>()));

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public RefEnumerable<T1, T2, T3, T4> Without<TWithout>() => Filtered(_filter.Without(_world?.StoreOrNull<TWithout>()));

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public RefEnumerable<T1, T2, T3, T4> Changed<TChanged>() => Filtered(_filter.Changed(_world?.StoreOrNull<TChanged>()));

        /// <summary>Only entities that got <typeparamref name="TAdded"/> since the reader last looked.</summary>
        public RefEnumerable<T1, T2, T3, T4> Added<TAdded>() => Filtered(_filter.Added(_world?.StoreOrNull<TAdded>()));

        private RefEnumerable<T1, T2, T3, T4> Filtered(QueryFilter filter) => new(_s1, _s2, _s3, _s4, _markOnIterate, _world, filter);

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public RefEnumerator GetEnumerator() => new(_s1, _s2, _s3, _s4, _markOnIterate, _filter);

        /// <summary>Walks the smallest store, skipping entities missing another type or refused by the filter.</summary>
        public ref struct RefEnumerator
        {
            private readonly ComponentStore<T1>? _s1;
            private readonly ComponentStore<T2>? _s2;
            private readonly ComponentStore<T3>? _s3;
            private readonly ComponentStore<T4>? _s4;
            // Which store is walked, counted from 1, or 0 where a type has no store.
            private readonly int _driver;
            private readonly bool _mark;
            private readonly QueryFilter _filter;
            private int _i;
            private int _entity;

            internal RefEnumerator(ComponentStore<T1>? s1, ComponentStore<T2>? s2, ComponentStore<T3>? s3, ComponentStore<T4>? s4, bool mark, QueryFilter filter)
            {
                _s1 = s1;
                _s2 = s2;
                _s3 = s3;
                _s4 = s4;
                _mark = mark;
                _filter = filter;
                _i = -1;
                _entity = -1;
                _driver = s1 is null || s2 is null || s3 is null || s4 is null ? 0 : Smallest([s1.Count, s2.Count, s3.Count, s4.Count]);
            }

            /// <summary>The current entity and its components by reference. Reading it marks them all changed.</summary>
            public RefComponents<T1, T2, T3, T4> Current
            {
                get
                {
                    if (_mark)
                    {
                        Mark(_s1!, _entity);
                        Mark(_s2!, _entity);
                        Mark(_s3!, _entity);
                        Mark(_s4!, _entity);
                    }
                    return RefComponents<T1, T2, T3, T4>.Create(_entity, _s1!, _s2!, _s3!, _s4!);
                }
            }

            private static void Mark<TC>(ComponentStore<TC> store, int entity)
            {
                int index = store.DenseIndexOf(entity);
                if (index >= 0) store.MarkChangedByDenseIndex(index, 0);
            }

            /// <summary>Moves to the next entity with every type that passes the filter.</summary>
            public bool MoveNext()
            {
                if (_driver == 0) return false;
                int count = DriverCount();
                while (++_i < count)
                {
                    int e = EntityAt(_i);
                    if (_s1!.Has(e) && _s2!.Has(e) && _s3!.Has(e) && _s4!.Has(e) && (_filter.IsEmpty || _filter.Passes(e)))
                    {
                        _entity = e;
                        return true;
                    }
                }
                return false;
            }

            private readonly int DriverCount() => _driver switch { 1 => _s1!.Count, 2 => _s2!.Count, 3 => _s3!.Count, 4 => _s4!.Count, _ => 0 };

            private readonly int EntityAt(int i) => _driver switch
            {
                1 => _s1!.EntityByDenseIndex(i),
                2 => _s2!.EntityByDenseIndex(i),
                3 => _s3!.EntityByDenseIndex(i),
                _ => _s4!.EntityByDenseIndex(i),
            };
        }
    }

    /// <summary>Zero-allocation ref wrapper for five components on the same entity.</summary>
    public readonly ref struct RefComponents<T1, T2, T3, T4, T5>
    {
        /// <summary>The entity ID owning these components.</summary>
        public readonly int Entity;
        private readonly ComponentStore<T1> _s1;
        private readonly ComponentStore<T2> _s2;
        private readonly ComponentStore<T3> _s3;
        private readonly ComponentStore<T4> _s4;
        private readonly ComponentStore<T5> _s5;

        private RefComponents(int entity, ComponentStore<T1> s1, ComponentStore<T2> s2, ComponentStore<T3> s3, ComponentStore<T4> s4, ComponentStore<T5> s5)
        {
            Entity = entity;
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
            _s4 = s4;
            _s5 = s5;
        }

        internal static RefComponents<T1, T2, T3, T4, T5> Create(int entity, ComponentStore<T1> s1, ComponentStore<T2> s2, ComponentStore<T3> s3, ComponentStore<T4> s4, ComponentStore<T5> s5) =>
            new(entity, s1, s2, s3, s4, s5);

        /// <summary>A mutable reference to the first component.</summary>
        public ref T1 C1 => ref _s1.GetRef(Entity);

        /// <summary>A mutable reference to the second component.</summary>
        public ref T2 C2 => ref _s2.GetRef(Entity);

        /// <summary>A mutable reference to the third component.</summary>
        public ref T3 C3 => ref _s3.GetRef(Entity);

        /// <summary>A mutable reference to the fourth component.</summary>
        public ref T4 C4 => ref _s4.GetRef(Entity);

        /// <summary>A mutable reference to the fifth component.</summary>
        public ref T5 C5 => ref _s5.GetRef(Entity);
    }

    /// <summary>
    /// A <c>foreach</c>-able view of the entities that have all five component types, by
    /// reference, narrowed by <see cref="With{TWith}"/>, <see cref="Without{TWithout}"/> and
    /// <see cref="Changed{TChanged}"/> as the smaller queries are.
    /// </summary>
    /// <remarks>
    /// Walks the store with the fewest components and probes the others, so its cost follows the
    /// rarest of the types.
    /// </remarks>
    public readonly ref struct RefEnumerable<T1, T2, T3, T4, T5>
    {
        private readonly ComponentStore<T1>? _s1;
        private readonly ComponentStore<T2>? _s2;
        private readonly ComponentStore<T3>? _s3;
        private readonly ComponentStore<T4>? _s4;
        private readonly ComponentStore<T5>? _s5;
        private readonly bool _markOnIterate;
        private readonly EcsWorld? _world;
        private readonly QueryFilter _filter;

        private RefEnumerable(ComponentStore<T1>? s1, ComponentStore<T2>? s2, ComponentStore<T3>? s3, ComponentStore<T4>? s4, ComponentStore<T5>? s5, bool markOnIterate, EcsWorld? world = null, QueryFilter filter = default)
        {
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
            _s4 = s4;
            _s5 = s5;
            _markOnIterate = markOnIterate;
            _world = world;
            _filter = filter;
        }

        internal static RefEnumerable<T1, T2, T3, T4, T5> Empty() => new(null, null, null, null, null, false);

        internal static RefEnumerable<T1, T2, T3, T4, T5> From(ComponentStore<T1> s1, ComponentStore<T2> s2, ComponentStore<T3> s3, ComponentStore<T4> s4, ComponentStore<T5> s5, bool markOnIterate, EcsWorld world) =>
            new(s1, s2, s3, s4, s5, markOnIterate, world);

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public RefEnumerable<T1, T2, T3, T4, T5> With<TWith>() => Filtered(_filter.With(_world?.StoreOrNull<TWith>()));

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public RefEnumerable<T1, T2, T3, T4, T5> Without<TWithout>() => Filtered(_filter.Without(_world?.StoreOrNull<TWithout>()));

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public RefEnumerable<T1, T2, T3, T4, T5> Changed<TChanged>() => Filtered(_filter.Changed(_world?.StoreOrNull<TChanged>()));

        /// <summary>Only entities that got <typeparamref name="TAdded"/> since the reader last looked.</summary>
        public RefEnumerable<T1, T2, T3, T4, T5> Added<TAdded>() => Filtered(_filter.Added(_world?.StoreOrNull<TAdded>()));

        private RefEnumerable<T1, T2, T3, T4, T5> Filtered(QueryFilter filter) => new(_s1, _s2, _s3, _s4, _s5, _markOnIterate, _world, filter);

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public RefEnumerator GetEnumerator() => new(_s1, _s2, _s3, _s4, _s5, _markOnIterate, _filter);

        /// <summary>Walks the smallest store, skipping entities missing another type or refused by the filter.</summary>
        public ref struct RefEnumerator
        {
            private readonly ComponentStore<T1>? _s1;
            private readonly ComponentStore<T2>? _s2;
            private readonly ComponentStore<T3>? _s3;
            private readonly ComponentStore<T4>? _s4;
            private readonly ComponentStore<T5>? _s5;
            // Which store is walked, counted from 1, or 0 where a type has no store.
            private readonly int _driver;
            private readonly bool _mark;
            private readonly QueryFilter _filter;
            private int _i;
            private int _entity;

            internal RefEnumerator(ComponentStore<T1>? s1, ComponentStore<T2>? s2, ComponentStore<T3>? s3, ComponentStore<T4>? s4, ComponentStore<T5>? s5, bool mark, QueryFilter filter)
            {
                _s1 = s1;
                _s2 = s2;
                _s3 = s3;
                _s4 = s4;
                _s5 = s5;
                _mark = mark;
                _filter = filter;
                _i = -1;
                _entity = -1;
                _driver = s1 is null || s2 is null || s3 is null || s4 is null || s5 is null ? 0 : Smallest([s1.Count, s2.Count, s3.Count, s4.Count, s5.Count]);
            }

            /// <summary>The current entity and its components by reference. Reading it marks them all changed.</summary>
            public RefComponents<T1, T2, T3, T4, T5> Current
            {
                get
                {
                    if (_mark)
                    {
                        Mark(_s1!, _entity);
                        Mark(_s2!, _entity);
                        Mark(_s3!, _entity);
                        Mark(_s4!, _entity);
                        Mark(_s5!, _entity);
                    }
                    return RefComponents<T1, T2, T3, T4, T5>.Create(_entity, _s1!, _s2!, _s3!, _s4!, _s5!);
                }
            }

            private static void Mark<TC>(ComponentStore<TC> store, int entity)
            {
                int index = store.DenseIndexOf(entity);
                if (index >= 0) store.MarkChangedByDenseIndex(index, 0);
            }

            /// <summary>Moves to the next entity with every type that passes the filter.</summary>
            public bool MoveNext()
            {
                if (_driver == 0) return false;
                int count = DriverCount();
                while (++_i < count)
                {
                    int e = EntityAt(_i);
                    if (_s1!.Has(e) && _s2!.Has(e) && _s3!.Has(e) && _s4!.Has(e) && _s5!.Has(e) && (_filter.IsEmpty || _filter.Passes(e)))
                    {
                        _entity = e;
                        return true;
                    }
                }
                return false;
            }

            private readonly int DriverCount() => _driver switch { 1 => _s1!.Count, 2 => _s2!.Count, 3 => _s3!.Count, 4 => _s4!.Count, 5 => _s5!.Count, _ => 0 };

            private readonly int EntityAt(int i) => _driver switch
            {
                1 => _s1!.EntityByDenseIndex(i),
                2 => _s2!.EntityByDenseIndex(i),
                3 => _s3!.EntityByDenseIndex(i),
                4 => _s4!.EntityByDenseIndex(i),
                _ => _s5!.EntityByDenseIndex(i),
            };
        }
    }

    /// <summary>Zero-allocation ref wrapper for six components on the same entity.</summary>
    public readonly ref struct RefComponents<T1, T2, T3, T4, T5, T6>
    {
        /// <summary>The entity ID owning these components.</summary>
        public readonly int Entity;
        private readonly ComponentStore<T1> _s1;
        private readonly ComponentStore<T2> _s2;
        private readonly ComponentStore<T3> _s3;
        private readonly ComponentStore<T4> _s4;
        private readonly ComponentStore<T5> _s5;
        private readonly ComponentStore<T6> _s6;

        private RefComponents(int entity, ComponentStore<T1> s1, ComponentStore<T2> s2, ComponentStore<T3> s3, ComponentStore<T4> s4, ComponentStore<T5> s5, ComponentStore<T6> s6)
        {
            Entity = entity;
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
            _s4 = s4;
            _s5 = s5;
            _s6 = s6;
        }

        internal static RefComponents<T1, T2, T3, T4, T5, T6> Create(int entity, ComponentStore<T1> s1, ComponentStore<T2> s2, ComponentStore<T3> s3, ComponentStore<T4> s4, ComponentStore<T5> s5, ComponentStore<T6> s6) =>
            new(entity, s1, s2, s3, s4, s5, s6);

        /// <summary>A mutable reference to the first component.</summary>
        public ref T1 C1 => ref _s1.GetRef(Entity);

        /// <summary>A mutable reference to the second component.</summary>
        public ref T2 C2 => ref _s2.GetRef(Entity);

        /// <summary>A mutable reference to the third component.</summary>
        public ref T3 C3 => ref _s3.GetRef(Entity);

        /// <summary>A mutable reference to the fourth component.</summary>
        public ref T4 C4 => ref _s4.GetRef(Entity);

        /// <summary>A mutable reference to the fifth component.</summary>
        public ref T5 C5 => ref _s5.GetRef(Entity);

        /// <summary>A mutable reference to the sixth component.</summary>
        public ref T6 C6 => ref _s6.GetRef(Entity);
    }

    /// <summary>
    /// A <c>foreach</c>-able view of the entities that have all six component types, by
    /// reference, narrowed by <see cref="With{TWith}"/>, <see cref="Without{TWithout}"/> and
    /// <see cref="Changed{TChanged}"/> as the smaller queries are.
    /// </summary>
    /// <remarks>
    /// Walks the store with the fewest components and probes the others, so its cost follows the
    /// rarest of the types.
    /// </remarks>
    public readonly ref struct RefEnumerable<T1, T2, T3, T4, T5, T6>
    {
        private readonly ComponentStore<T1>? _s1;
        private readonly ComponentStore<T2>? _s2;
        private readonly ComponentStore<T3>? _s3;
        private readonly ComponentStore<T4>? _s4;
        private readonly ComponentStore<T5>? _s5;
        private readonly ComponentStore<T6>? _s6;
        private readonly bool _markOnIterate;
        private readonly EcsWorld? _world;
        private readonly QueryFilter _filter;

        private RefEnumerable(ComponentStore<T1>? s1, ComponentStore<T2>? s2, ComponentStore<T3>? s3, ComponentStore<T4>? s4, ComponentStore<T5>? s5, ComponentStore<T6>? s6, bool markOnIterate, EcsWorld? world = null, QueryFilter filter = default)
        {
            _s1 = s1;
            _s2 = s2;
            _s3 = s3;
            _s4 = s4;
            _s5 = s5;
            _s6 = s6;
            _markOnIterate = markOnIterate;
            _world = world;
            _filter = filter;
        }

        internal static RefEnumerable<T1, T2, T3, T4, T5, T6> Empty() => new(null, null, null, null, null, null, false);

        internal static RefEnumerable<T1, T2, T3, T4, T5, T6> From(ComponentStore<T1> s1, ComponentStore<T2> s2, ComponentStore<T3> s3, ComponentStore<T4> s4, ComponentStore<T5> s5, ComponentStore<T6> s6, bool markOnIterate, EcsWorld world) =>
            new(s1, s2, s3, s4, s5, s6, markOnIterate, world);

        /// <summary>Only entities that also have a <typeparamref name="TWith"/>.</summary>
        public RefEnumerable<T1, T2, T3, T4, T5, T6> With<TWith>() => Filtered(_filter.With(_world?.StoreOrNull<TWith>()));

        /// <summary>Only entities that do not have a <typeparamref name="TWithout"/>.</summary>
        public RefEnumerable<T1, T2, T3, T4, T5, T6> Without<TWithout>() => Filtered(_filter.Without(_world?.StoreOrNull<TWithout>()));

        /// <summary>Only entities whose <typeparamref name="TChanged"/> changed this frame.</summary>
        public RefEnumerable<T1, T2, T3, T4, T5, T6> Changed<TChanged>() => Filtered(_filter.Changed(_world?.StoreOrNull<TChanged>()));

        /// <summary>Only entities that got <typeparamref name="TAdded"/> since the reader last looked.</summary>
        public RefEnumerable<T1, T2, T3, T4, T5, T6> Added<TAdded>() => Filtered(_filter.Added(_world?.StoreOrNull<TAdded>()));

        private RefEnumerable<T1, T2, T3, T4, T5, T6> Filtered(QueryFilter filter) => new(_s1, _s2, _s3, _s4, _s5, _s6, _markOnIterate, _world, filter);

        /// <summary>The enumerator <c>foreach</c> uses.</summary>
        public RefEnumerator GetEnumerator() => new(_s1, _s2, _s3, _s4, _s5, _s6, _markOnIterate, _filter);

        /// <summary>Walks the smallest store, skipping entities missing another type or refused by the filter.</summary>
        public ref struct RefEnumerator
        {
            private readonly ComponentStore<T1>? _s1;
            private readonly ComponentStore<T2>? _s2;
            private readonly ComponentStore<T3>? _s3;
            private readonly ComponentStore<T4>? _s4;
            private readonly ComponentStore<T5>? _s5;
            private readonly ComponentStore<T6>? _s6;
            // Which store is walked, counted from 1, or 0 where a type has no store.
            private readonly int _driver;
            private readonly bool _mark;
            private readonly QueryFilter _filter;
            private int _i;
            private int _entity;

            internal RefEnumerator(ComponentStore<T1>? s1, ComponentStore<T2>? s2, ComponentStore<T3>? s3, ComponentStore<T4>? s4, ComponentStore<T5>? s5, ComponentStore<T6>? s6, bool mark, QueryFilter filter)
            {
                _s1 = s1;
                _s2 = s2;
                _s3 = s3;
                _s4 = s4;
                _s5 = s5;
                _s6 = s6;
                _mark = mark;
                _filter = filter;
                _i = -1;
                _entity = -1;
                _driver = s1 is null || s2 is null || s3 is null || s4 is null || s5 is null || s6 is null ? 0 : Smallest([s1.Count, s2.Count, s3.Count, s4.Count, s5.Count, s6.Count]);
            }

            /// <summary>The current entity and its components by reference. Reading it marks them all changed.</summary>
            public RefComponents<T1, T2, T3, T4, T5, T6> Current
            {
                get
                {
                    if (_mark)
                    {
                        Mark(_s1!, _entity);
                        Mark(_s2!, _entity);
                        Mark(_s3!, _entity);
                        Mark(_s4!, _entity);
                        Mark(_s5!, _entity);
                        Mark(_s6!, _entity);
                    }
                    return RefComponents<T1, T2, T3, T4, T5, T6>.Create(_entity, _s1!, _s2!, _s3!, _s4!, _s5!, _s6!);
                }
            }

            private static void Mark<TC>(ComponentStore<TC> store, int entity)
            {
                int index = store.DenseIndexOf(entity);
                if (index >= 0) store.MarkChangedByDenseIndex(index, 0);
            }

            /// <summary>Moves to the next entity with every type that passes the filter.</summary>
            public bool MoveNext()
            {
                if (_driver == 0) return false;
                int count = DriverCount();
                while (++_i < count)
                {
                    int e = EntityAt(_i);
                    if (_s1!.Has(e) && _s2!.Has(e) && _s3!.Has(e) && _s4!.Has(e) && _s5!.Has(e) && _s6!.Has(e) && (_filter.IsEmpty || _filter.Passes(e)))
                    {
                        _entity = e;
                        return true;
                    }
                }
                return false;
            }

            private readonly int DriverCount() => _driver switch { 1 => _s1!.Count, 2 => _s2!.Count, 3 => _s3!.Count, 4 => _s4!.Count, 5 => _s5!.Count, 6 => _s6!.Count, _ => 0 };

            private readonly int EntityAt(int i) => _driver switch
            {
                1 => _s1!.EntityByDenseIndex(i),
                2 => _s2!.EntityByDenseIndex(i),
                3 => _s3!.EntityByDenseIndex(i),
                4 => _s4!.EntityByDenseIndex(i),
                5 => _s5!.EntityByDenseIndex(i),
                _ => _s6!.EntityByDenseIndex(i),
            };
        }
    }
}
