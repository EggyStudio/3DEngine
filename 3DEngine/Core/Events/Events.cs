using System.Runtime.InteropServices;

namespace Engine;

/// <summary>Static helpers to get or create typed event queues in the <see cref="World"/>.</summary>
/// <seealso cref="Events{T}"/>
/// <seealso cref="EventWriter{T}"/>
/// <seealso cref="EventReader{T}"/>
public static class Events
{
    /// <summary>Gets the event queue resource for <typeparamref name="T"/>, inserting a new one if missing.</summary>
    /// <typeparam name="T">The event payload type.</typeparam>
    /// <param name="world">The <see cref="World"/> to look up or create the queue in.</param>
    /// <returns>The existing or newly created <see cref="Events{T}"/> queue.</returns>
    public static Events<T> Get<T>(World world)
        => world.GetOrInsertResource(() => new Events<T>());
}

/// <summary>
/// Thread-safe event queue per event type <typeparamref name="T"/>, stored as a <see cref="World"/> resource.
/// Events accumulate during a frame and should be cleared once per frame
/// (typically in the <see cref="Stage.Last"/> stage) via <see cref="Clear"/>.
/// </summary>
/// <typeparam name="T">The event payload type.</typeparam>
/// <example>
/// <code>
/// // Send an event
/// Events.Get&lt;DamageEvent&gt;(world).Send(new DamageEvent(target, 25));
///
/// // Read events (snapshot copy)
/// foreach (var evt in Events.Get&lt;DamageEvent&gt;(world).Read())
///     ApplyDamage(evt);
/// </code>
/// </example>
/// <seealso cref="Events"/>
/// <seealso cref="EventWriter{T}"/>
/// <seealso cref="EventReader{T}"/>
/// <seealso cref="WorldEventExtensions"/>
public sealed class Events<T>
{
    private readonly Lock _lock = new();
    private readonly List<T> _buffer = [];

    /// <summary>Number of buffered events.</summary>
    public int Count { get { lock (_lock) return _buffer.Count; } }

    /// <summary><c>true</c> when the buffer is empty; <c>false</c> otherwise.</summary>
    public bool IsEmpty { get { lock (_lock) return _buffer.Count == 0; } }

    /// <summary>Queues a single event into the buffer.</summary>
    /// <param name="evt">The event to enqueue.</param>
    public void Send(T evt)
    {
        lock (_lock) _buffer.Add(evt);
    }

    /// <summary>Queues multiple events at once.</summary>
    /// <param name="events">A span of events to enqueue.</param>
    public void SendBatch(ReadOnlySpan<T> events)
    {
        lock (_lock)
        {
            foreach (var evt in events)
                _buffer.Add(evt);
        }
    }

    /// <summary>
    /// Returns a span view of buffered events for zero-allocation iteration.
    /// </summary>
    /// <returns>A read-only span over the internal buffer.</returns>
    /// <remarks>
    /// <b>Not safe while other threads call <see cref="Send"/>.</b>
    /// Use only from stages that do not overlap with writers (e.g., a single-threaded stage
    /// or when the queue is not being written to).
    /// </remarks>
    public ReadOnlySpan<T> AsSpan() => CollectionsMarshal.AsSpan(_buffer);

    /// <summary>Returns a thread-safe snapshot copy of all buffered events.</summary>
    /// <returns>An empty list if no events are buffered; otherwise a copy of all events.</returns>
    public IReadOnlyList<T> Read()
    {
        lock (_lock)
            return _buffer.Count == 0 ? [] : _buffer.ToArray();
    }

    /// <summary>Returns a snapshot copy of all events and clears the buffer atomically.</summary>
    /// <returns>An empty list if no events are buffered; otherwise a copy of the drained events.</returns>
    public IReadOnlyList<T> Drain()
    {
        lock (_lock)
        {
            if (_buffer.Count == 0)
                return [];

            var snapshot = _buffer.ToArray();
            _buffer.Clear();
            return snapshot;
        }
    }

    /// <summary>Removes all buffered events.</summary>
    public void Clear()
    {
        lock (_lock) _buffer.Clear();
    }
}
