namespace Engine;

/// <summary>Write-only handle for sending events of type <typeparamref name="T"/>.</summary>
/// <typeparam name="T">The event payload type.</typeparam>
/// <example>
/// <code>
/// var writer = EventWriter&lt;DamageEvent&gt;.Get(world);
/// writer.Send(new DamageEvent(target, 25));
/// </code>
/// </example>
/// <seealso cref="EventReader{T}"/>
/// <seealso cref="Events{T}"/>
public readonly ref struct EventWriter<T>
{
    private readonly Events<T> _events;

    /// <summary>Creates a writer wrapping the specified event queue.</summary>
    /// <param name="events">The underlying event queue to write into.</param>
    public EventWriter(Events<T> events) => _events = events;

    /// <summary>Queues a single event.</summary>
    /// <param name="evt">The event to enqueue.</param>
    public void Send(T evt) => _events.Send(evt);

    /// <summary>Queues multiple events at once.</summary>
    /// <param name="events">A span of events to enqueue.</param>
    public void SendBatch(ReadOnlySpan<T> events) => _events.SendBatch(events);

    /// <summary>Creates a writer from the world's event queue for <typeparamref name="T"/>.</summary>
    /// <param name="world">The <see cref="World"/> containing the event queue resource.</param>
    /// <returns>A new <see cref="EventWriter{T}"/> wrapping the queue.</returns>
    public static EventWriter<T> Get(World world) => new(Events.Get<T>(world));
}
