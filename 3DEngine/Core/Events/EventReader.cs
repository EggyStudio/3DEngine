namespace Engine;

/// <summary>Read-only handle for consuming events of type <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The event payload type.</typeparam>
/// <example>
/// <code>
/// var reader = EventReader&lt;DamageEvent&gt;.Get(world);
/// foreach (var evt in reader.Read())
///     ApplyDamage(evt.Target, evt.Amount);
/// </code>
/// </example>
/// <seealso cref="EventWriter{T}"/>
/// <seealso cref="Events{T}"/>
public readonly ref struct EventReader<T>
{
    private readonly Events<T> _events;

    /// <summary>Creates a reader wrapping the specified event queue.</summary>
    /// <param name="events">The underlying event queue to read from.</param>
    public EventReader(Events<T> events) => _events = events;

    /// <summary>Number of available events in the buffer.</summary>
    public int Count => _events.Count;

    /// <summary><c>true</c> when there are no events to read.</summary>
    public bool IsEmpty => _events.IsEmpty;

    /// <summary>Returns a thread-safe snapshot of all buffered events.</summary>
    /// <returns>A read-only list of event copies.</returns>
    public IReadOnlyList<T> Read() => _events.Read();

    /// <summary>Returns a snapshot of all events and clears the buffer atomically.</summary>
    /// <returns>A read-only list of the drained events.</returns>
    public IReadOnlyList<T> Drain() => _events.Drain();

    /// <summary>Creates a reader from the world's event queue for <typeparamref name="T"/>.</summary>
    /// <param name="world">The <see cref="World"/> containing the event queue resource.</param>
    /// <returns>A new <see cref="EventReader{T}"/> wrapping the queue.</returns>
    public static EventReader<T> Get(World world) => new(Events.Get<T>(world));
}
