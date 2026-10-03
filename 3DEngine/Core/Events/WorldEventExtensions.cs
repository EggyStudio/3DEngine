namespace Engine;

/// <summary>Convenience extensions for firing and reading events directly on <see cref="World"/>.</summary>
/// <example>
/// <code>
/// // Send directly on World
/// world.SendEvent(new DamageEvent(target, 50));
/// </code>
/// <code>
/// // Read directly on World
/// foreach (var evt in world.ReadEvents&lt;DamageEvent&gt;())
///     ApplyDamage(evt);
/// </code>
/// <code>
/// // Drain (read + clear) at end of frame
/// var events = world.DrainEvents&lt;DamageEvent&gt;();
/// </code>
/// </example>
/// <seealso cref="Events{T}"/>
public static class WorldEventExtensions
{
    /// <summary>Sends an event into the world's event queue for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The event payload type.</typeparam>
    /// <param name="world">The world containing the event queue.</param>
    /// <param name="evt">The event to send.</param>
    public static void SendEvent<T>(this World world, T evt)
        => Events.Get<T>(world).Send(evt);

    /// <summary>Returns a thread-safe snapshot of all buffered events of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The event payload type.</typeparam>
    /// <param name="world">The world containing the event queue.</param>
    /// <returns>A read-only list of event copies.</returns>
    public static IReadOnlyList<T> ReadEvents<T>(this World world)
        => Events.Get<T>(world).Read();

    /// <summary>Drains all events of type <typeparamref name="T"/>, returning a snapshot and clearing the buffer.</summary>
    /// <typeparam name="T">The event payload type.</typeparam>
    /// <param name="world">The world containing the event queue.</param>
    /// <returns>A read-only list of the drained events.</returns>
    public static IReadOnlyList<T> DrainEvents<T>(this World world)
        => Events.Get<T>(world).Drain();

    /// <summary>Clears all buffered events of type <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The event payload type.</typeparam>
    /// <param name="world">The world containing the event queue.</param>
    public static void ClearEvents<T>(this World world)
        => Events.Get<T>(world).Clear();
}
