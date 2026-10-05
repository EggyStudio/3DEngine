using System.Numerics;

namespace Engine;

/// <summary>
/// One light as the renderer reads it, made each frame by <see cref="LightExtract"/> from a
/// <see cref="Light"/> and its entity's world transform.
/// </summary>
/// <remarks>
/// Render entities carrying this are despawned at the start of every extract, through
/// <see cref="RenderWorld.ClearEntities"/>, and the frame's list is also kept in
/// <see cref="RenderLights"/>.
/// </remarks>
internal struct RenderLight
{
    /// <summary>The main-world entity the light came from.</summary>
    public int MainEntityId;

    /// <summary>What the light is.</summary>
    public LightKind Kind;

    /// <summary>Where it is in world space.</summary>
    public Vector3 Position;

    /// <summary>The way it points in world space, its entity's -Z.</summary>
    public Vector3 Direction;

    /// <summary>Its color times its intensity.</summary>
    public Vector3 EmittedColor;

    /// <summary>The distance it fades to nothing at, or 0 for none.</summary>
    public float Range;

    /// <summary>The cosine of a spot's inner angle, inside which it is full.</summary>
    public float CosInner;

    /// <summary>The cosine of a spot's outer angle, outside which it gives nothing.</summary>
    public float CosOuter;

    /// <summary>Whether it is to cast shadows.</summary>
    public bool CastsShadows;
}

/// <summary>The frame's lights, in the order they were extracted, kept on the <see cref="RenderWorld"/>.</summary>
internal sealed class RenderLights
{
    /// <summary>All lights extracted for the current frame, in extract iteration order.</summary>
    public List<RenderLight> All { get; } = new();
}
