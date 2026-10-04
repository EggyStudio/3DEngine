namespace Engine;

/// <summary>
/// The component operations by <see cref="Entity"/> handle, which carry a generation, so a handle
/// kept across frames to an entity since despawned is refused instead of reaching the entity that
/// reused its id.
/// </summary>
/// <remarks>
/// A read answers as if the component were missing. A write throws, since writing to the wrong
/// entity, or to none, is a bug to find rather than to step past.
/// </remarks>
public sealed partial class EcsWorld
{
    /// <summary>Whether the entity is alive and has a <typeparamref name="T"/>.</summary>
    public bool Has<T>(Entity entity) => TryResolve(entity, out var id) && Has<T>(id);

    /// <summary>Reads the entity's <typeparamref name="T"/>, or answers false when it has none or is gone.</summary>
    public bool TryGet<T>(Entity entity, out T? component)
    {
        if (TryResolve(entity, out var id)) return TryGet(id, out component);
        component = default;
        return false;
    }

    /// <summary>The entity's <typeparamref name="T"/> by reference to write through, marking it changed.</summary>
    /// <exception cref="InvalidOperationException">The handle names an entity that is gone.</exception>
    /// <exception cref="KeyNotFoundException">The entity has no <typeparamref name="T"/>.</exception>
    public ref T GetRef<T>(Entity entity) => ref GetRef<T>(Alive(entity));

    /// <summary>The entity's <typeparamref name="T"/> by read-only reference, marking nothing.</summary>
    /// <exception cref="InvalidOperationException">The handle names an entity that is gone.</exception>
    /// <exception cref="KeyNotFoundException">The entity has no <typeparamref name="T"/>.</exception>
    public ref readonly T GetReadOnly<T>(Entity entity) => ref GetReadOnly<T>(Alive(entity));

    /// <summary>Adds a <typeparamref name="T"/> to the entity.</summary>
    /// <exception cref="InvalidOperationException">The handle names an entity that is gone.</exception>
    public void Add<T>(Entity entity, T component) => Add(Alive(entity), component);

    /// <summary>Replaces the entity's <typeparamref name="T"/>, marking it changed.</summary>
    /// <exception cref="InvalidOperationException">The handle names an entity that is gone.</exception>
    public void Update<T>(Entity entity, T component) => Update(Alive(entity), component);

    /// <summary>Removes the entity's <typeparamref name="T"/>, answering false when it had none or is gone.</summary>
    public bool Remove<T>(Entity entity) => TryResolve(entity, out var id) && Remove<T>(id);

    private int Alive(Entity entity) =>
        TryResolve(entity, out var id) ? id : throw new InvalidOperationException(
            entity.IsNone ? "The handle names no entity." : $"Entity {entity.Index} of generation {entity.Generation} is gone, and its id may name another entity since.");
}
