namespace Engine;

public sealed partial class EcsWorld
{
    // Every observer of this world, made with the first, so a world with none pays a read of this
    // field where a despawn would tell them, and an add or a removal one of its store's count.
    private ObserverList? _observers;

    /// <summary>Runs <paramref name="observer"/> each time a <typeparamref name="TEvent"/> is triggered.</summary>
    /// <remarks>
    /// <para>
    /// Bevy's <c>add_observer</c>. <typeparamref name="TEvent"/> is an event the game declares,
    /// triggered with <see cref="Trigger{TEvent}(TEvent)"/>, or a change to a component,
    /// <see cref="Engine.Add{T}"/> as one is added to an entity that had none and
    /// <see cref="Engine.Remove{T}"/> once one has left, by <see cref="Remove{T}(int)"/> or with
    /// its entity by <see cref="Despawn(int)"/>. A change queued on <see cref="EcsCommands"/> tells
    /// them as the commands are applied.
    /// </para>
    /// <para>
    /// Observers run in the order they were added, inside the call that triggered the event or made
    /// the change and on its thread, once the change has been made, with the whole world to work in.
    /// An exception one throws is logged and goes no further, so the observers after it and the
    /// commands after the one that made the change still run. A store's own <c>Add</c> and
    /// <c>Remove</c>, which generated code reaches through <see cref="GetStorePublic{T}"/>, tell
    /// no observer. Disposing the returned handle stops the observer.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// ecs.Observe&lt;Remove&lt;Mine&gt;&gt;(on =&gt; index.Forget(on.Event.Entity, on.Event.Value));
    /// ecs.Observe&lt;Explode&gt;(on =&gt; Console.WriteLine($"{on.Entity} exploded"));
    /// </code>
    /// </example>
    public IDisposable Observe<TEvent>(Action<On<TEvent>> observer) => Observers().Add(this, null, observer);

    /// <summary>Runs <paramref name="observer"/> each time a <typeparamref name="TEvent"/> reaches <paramref name="entity"/>.</summary>
    /// <remarks>
    /// Bevy's <c>observe</c> on an entity, for an <see cref="IEntityEvent"/> that happened to it or
    /// reached it from a child, or a change to one of its components. The observers of every such
    /// event run before those of the entity. They are let go once the entity is despawned, after
    /// they are told of each component it took with it.
    /// </remarks>
    public IDisposable Observe<TEvent>(Entity entity, Action<On<TEvent>> observer) => Observers().Add(this, entity, observer);

    /// <summary>Runs the observers of <paramref name="value"/> now.</summary>
    /// <remarks>
    /// Bevy's <c>trigger</c>. An <see cref="IEntityEvent"/> runs the observers of every such event
    /// and then those of its entity, and an <see cref="IPropagatingEvent"/> goes on to each parent
    /// in turn the same way, until an observer stops it or it reaches the root. Any other event
    /// runs the observers of every such event.
    /// </remarks>
    public void Trigger<TEvent>(TEvent value) => _observers?.Trigger(this, value);

    private ObserverList Observers()
    {
        if (_observers is { } made) return made;
        Interlocked.CompareExchange(ref _observers, new ObserverList(), null);
        return _observers!;
    }

    /// <summary>Counts <paramref name="by"/> more observers of a <typeparamref name="T"/> being added, or removed.</summary>
    internal void CountObservers<T>(bool added, int by)
    {
        var store = GetStore<T>();
        if (added) Interlocked.Add(ref store.AddObservers, by);
        else Interlocked.Add(ref store.RemoveObservers, by);
    }

    // Tells the observers of a component added to an entity that had none, where any watch it.
    private void TellAdded<T>(int entity, T component) => _observers?.Trigger(this, new Add<T>(Handle(entity), component));

    /// <summary>
    /// Every observer of one world, those of every event of a type and those of one entity, and
    /// the triggering that runs them.
    /// </summary>
    /// <remarks>
    /// A list is copied under the lock before it runs, so an observer that adds or stops observers
    /// changes what the next trigger runs and not the one under way, and observers added on one
    /// thread while another triggers are safe.
    /// </remarks>
    private sealed class ObserverList
    {
        private static readonly ILogger Logger = Log.Category("Engine.ECS");

        private readonly Lock _lock = new();
        private readonly Dictionary<Type, List<Delegate>> _every = [];
        private readonly Dictionary<Entity, Dictionary<Type, List<Delegate>>> _at = [];

        // The counter of each type of event that is a change to a component, by the event's type.
        private readonly Dictionary<Type, IComponentEvent> _counted = [];

        public IDisposable Add<TEvent>(EcsWorld ecs, Entity? entity, Action<On<TEvent>> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            // A change to a component is counted by its store, which an add or a removal reads.
            var counted = default(TEvent) as IComponentEvent;
            List<Delegate> list;
            lock (_lock)
            {
                if (counted is not null) _counted[typeof(TEvent)] = counted;
                var byType = _every;
                if (entity is { } at && !_at.TryGetValue(at, out byType))
                    _at[at] = byType = [];
                if (!byType.TryGetValue(typeof(TEvent), out list!))
                    byType[typeof(TEvent)] = list = [];
                list.Add(observer);
            }
            counted?.Count(ecs, 1);
            return new Subscription(() =>
            {
                bool removed;
                lock (_lock) removed = list.Remove(observer);
                if (removed) counted?.Count(ecs, -1);
            });
        }

        public void Trigger<TEvent>(EcsWorld ecs, TEvent value)
        {
            var at = value is IEntityEvent happened ? happened.Entity : Entity.None;
            var on = new On<TEvent>(ecs, value, at);
            if (at.IsNone)
            {
                Run(Copy(_every, typeof(TEvent)), on);
                return;
            }

            while (true)
            {
                on.Entity = at;
                Run(Copy(_every, typeof(TEvent)), on);
                Delegate[]? own;
                lock (_lock) own = _at.TryGetValue(at, out var byType) && byType.TryGetValue(typeof(TEvent), out var list) && list.Count > 0 ? [.. list] : null;
                Run(own, on);

                if (value is not IPropagatingEvent || !on.Propagating || !ecs.TryResolve(at, out var id)) return;
                var parent = ecs.ParentOf(id);
                if (parent == 0) return;
                at = ecs.Handle(parent);
            }
        }

        /// <summary>Lets go of the observers of a despawned entity, uncounting those of its components.</summary>
        public void Forget(EcsWorld ecs, Entity entity)
        {
            // Read without the lock, as a despawn reads it each time, since a world whose observers
            // are all of every entity has none to let go. One added on another thread meanwhile is
            // of an entity alive and not this one.
            if (_at.Count == 0) return;
            List<(List<Delegate> List, IComponentEvent? Counted)>? dropped = null;
            lock (_lock)
            {
                if (!_at.Remove(entity, out var byType)) return;
                foreach (var (type, list) in byType)
                    (dropped ??= []).Add((list, _counted.GetValueOrDefault(type)));
            }
            foreach (var (list, counted) in dropped ?? [])
            {
                int count;
                lock (_lock)
                {
                    count = list.Count;
                    list.Clear();
                }
                counted?.Count(ecs, -count);
            }
        }

        private Delegate[]? Copy(Dictionary<Type, List<Delegate>> byType, Type type)
        {
            lock (_lock) return byType.TryGetValue(type, out var list) && list.Count > 0 ? [.. list] : null;
        }

        // A type as C# writes it, Add<Health> where its name is Add`1.
        private static string Named(Type type) => type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(Named))}>"
            : type.Name;

        private static void Run<TEvent>(Delegate[]? observers, On<TEvent> on)
        {
            if (observers is null) return;
            foreach (var observer in observers)
            {
                try
                {
                    ((Action<On<TEvent>>)observer)(on);
                }
                catch (Exception ex)
                {
                    Logger.Error($"An observer of {Named(typeof(TEvent))} at {on.Entity} threw", ex);
                }
            }
        }
    }

    private sealed class Subscription(Action stop) : IDisposable
    {
        private Action? _stop = stop;

        public void Dispose() => Interlocked.Exchange(ref _stop, null)?.Invoke();
    }
}
