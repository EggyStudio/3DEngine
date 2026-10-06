namespace Engine;

/// <summary>
/// The value a state machine over <typeparamref name="TState"/> moves to at the next transition
/// point, as a resource.
/// </summary>
/// <remarks>
/// The move is queued rather than made, and applied once a frame between
/// <see cref="Stage.PreUpdate"/> and <see cref="Stage.FixedUpdate"/>. Setting it twice in one frame
/// keeps the second value. Setting it to the value the machine is already in does nothing, so no
/// exit and enter run for a move that goes nowhere.
/// </remarks>
/// <typeparam name="TState">The enum the machine moves between.</typeparam>
public sealed class NextState<TState> where TState : struct, Enum
{
    /// <summary>The value queued, or null when nothing is.</summary>
    public TState? Pending { get; private set; }

    /// <summary>Queues a move to <paramref name="value"/> at the next transition point.</summary>
    public void Set(TState value) => Pending = value;

    /// <summary>Drops a queued move.</summary>
    public void Reset() => Pending = null;
}
