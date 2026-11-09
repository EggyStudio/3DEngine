namespace Engine;

/// <summary>A scene file has been read whole, told once after each of its components' <see cref="Loaded{T}"/>.</summary>
/// <param name="Path">The file, as it was named, or empty for a scene read from text.</param>
/// <param name="Entities">Every entity the file spawned, in its order.</param>
/// <remarks>
/// For what a game builds once for a whole level, as the paths its creatures walk between the
/// markers the file holds. Triggered for a save read back too, since a save is a scene file, and for
/// each copy a <see cref="SceneRef"/> places.
/// </remarks>
public readonly record struct SceneLoaded(string Path, IReadOnlyList<Entity> Entities);
