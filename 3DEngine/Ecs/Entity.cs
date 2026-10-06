namespace Engine;

/// <summary>
/// An entity id with the generation it had when the handle was taken, so a handle kept past a
/// despawn is recognizably stale instead of naming whatever entity reuses the id.
/// </summary>
/// <remarks>
/// <see cref="EcsWorld"/>'s operations take the plain <see cref="int"/> id, which a system
/// iterating this frame holds. A reference kept across frames (a target, a parent, an owner) is
/// kept as an <see cref="Entity"/> from <see cref="EcsWorld.Handle"/>, and turned back into an id
/// with <see cref="EcsWorld.TryResolve"/> each time it is used.
/// </remarks>
public readonly record struct Entity(int Index, int Generation)
{
    /// <summary>A handle that names no entity.</summary>
    public static Entity None => default;

    /// <summary>Whether this is <see cref="None"/>.</summary>
    public bool IsNone => Index == 0;

    /// <inheritdoc />
    public override string ToString() => IsNone ? "Entity.None" : $"Entity({Index}v{Generation})";
}
