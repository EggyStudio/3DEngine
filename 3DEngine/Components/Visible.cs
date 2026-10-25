namespace Engine;

/// <summary>
/// Whether a mesh entity is drawn at all, through any camera or into the shadow maps, an entity
/// without one drawn as if it held true.
/// </summary>
/// <remarks>
/// A bool rather than a marker added and taken away, so a game that works out each frame what its
/// camera can see, as a voxel game's flood through its sections' open faces does, writes it where it
/// is, since adding or removing a component moves the entity in the stores and has every chunk of
/// mesh entities gathered again. An entity not drawn keeps its mesh while it holds it, so it shows
/// again with nothing uploaded, and the pointer passes through it. <see cref="ShadowOnly"/> keeps an
/// entity in the shadow maps alone.
/// </remarks>
[SceneComponent]
public struct Visible
{
    /// <summary>Whether the entity is drawn.</summary>
    public bool Value;

    /// <summary>Drawn, the value a scene file's entity starts from.</summary>
    public static Visible Default => new(true);

    /// <summary>Drawn or not.</summary>
    public Visible(bool value) => Value = value;
}
