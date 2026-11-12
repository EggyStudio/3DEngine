namespace Engine;

/// <summary>
/// Whether a mesh entity is drawn into the shadow maps alone, casting its shadow and seen nowhere,
/// as <see cref="ModelMaterial.ShadowOnly"/> draws a mesh, an entity without one drawn as any other.
/// </summary>
/// <remarks>
/// A game that leaves out of its view the meshes its eye cannot reach sets this on them where it
/// would hide them, so those over a cave still keep the sun off its floor. The scene's field holds
/// the entity as any other, so global illumination keeps the sky's light off the cave as well, and
/// a mesh hidden as the eye moves stays in the field. A bool rather than a marker added and taken
/// away, since which meshes are hidden changes as the eye moves, and adding or removing a component
/// moves the entity in the stores. An entity with it is not met by the pointer, and one with
/// <see cref="Visible"/> false is drawn nowhere, shadows included.
/// </remarks>
[SceneComponent]
public struct ShadowOnly
{
    /// <summary>Whether the entity is drawn into the shadow maps alone.</summary>
    public bool Value;

    /// <summary>Into the shadow maps alone or as any other.</summary>
    public ShadowOnly(bool value) => Value = value;
}
