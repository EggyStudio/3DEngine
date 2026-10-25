namespace Engine;

/// <summary>
/// Whether a mesh entity is drawn into the shadow maps alone, casting its shadow and seen nowhere,
/// as <see cref="ModelMaterial.ShadowOnly"/> draws a mesh, an entity without one drawn as any other.
/// </summary>
/// <remarks>
/// A voxel game's flood leaves out of its view the sections its eye cannot reach, and those over a
/// cave still keep the sun off its floor, so the game sets this on them where it would hide them. A
/// bool for the reason <see cref="Visible"/> is one, since the sections it holds change as the eye
/// moves. An entity with it is not met by the pointer, and one with <see cref="Visible"/> false is
/// drawn nowhere, shadows included.
/// </remarks>
[SceneComponent]
public struct ShadowOnly
{
    /// <summary>Whether the entity is drawn into the shadow maps alone.</summary>
    public bool Value;

    /// <summary>Into the shadow maps alone or as any other.</summary>
    public ShadowOnly(bool value) => Value = value;
}
