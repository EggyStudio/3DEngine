namespace Engine;

/// <summary>Marks an entity the game spawned during play to be written whole into a <see cref="SaveGame"/>.</summary>
/// <remarks>
/// An entity a scene file spawned is saved by what play changed on it, with or without this. One the
/// game spawned itself is saved only where it carries this, as an arrow shot or a chest dropped, so
/// what the game spawns again on its own each run, its camera or its interface, is not spawned twice
/// when a save is read back.
/// </remarks>
[SceneComponent]
public struct Saved;
