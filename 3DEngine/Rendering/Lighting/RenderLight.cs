using System.Numerics;

namespace Engine;

/// <summary>
/// Render-thread per-light component, populated each frame by <see cref="LightExtract"/>
/// from the main-world <see cref="Light"/> + <see cref="Transform"/> pair. Symmetric to
/// <see cref="RenderMeshInstance"/>: the world-space pose is baked, the energy is
/// premultiplied (color × intensity × 2^exposure × optional Kelvin tint), and the
/// shape parameters are flattened so render passes don't need to revisit the main world.
/// </summary>
/// <remarks>
/// Render entities carrying this component are despawned at the start of every extract
/// phase via <see cref="RenderWorld.ClearEntities"/>; treat instances as ephemeral. The
/// full per-frame array is also surfaced via the <see cref="RenderLights"/> singleton
/// for shaders that prefer a flat list to an ECS query.
/// </remarks>
/// <seealso cref="LightExtract"/>
/// <seealso cref="RenderLights"/>
public struct RenderLight
{
    /// <summary>Main-world entity ID this light was extracted from (debug / picking).</summary>
    public int MainEntityId;

    /// <summary>UsdLux shape (sphere / rect / distant / dome / ...).</summary>
    public LightType Type;

    /// <summary>World-space position (translation of the source <see cref="Transform"/>).</summary>
    public Vector3 Position;

    /// <summary>World-space forward direction. <c>-Z</c> rotated by the source rotation, matching the UsdLux convention that distant / spot lights emit along <c>-Z</c>.</summary>
    public Vector3 Direction;

    /// <summary>Premultiplied linear-RGB radiant emission (<c>Color * Intensity * 2^Exposure * KelvinTint</c>).</summary>
    public Vector3 EmittedColor;

    /// <summary>Sphere / disk radius (world units).</summary>
    public float Radius;

    /// <summary>Rect width (world units).</summary>
    public float Width;

    /// <summary>Rect height (world units).</summary>
    public float Height;

    /// <summary>Cylinder length (world units).</summary>
    public float Length;

    /// <summary>Resolved dome texture handle, or invalid when unbound.</summary>
    public Handle<Texture> DomeTexture;

    /// <summary>Resolved rect-light texture handle, or invalid when unbound.</summary>
    public Handle<Texture> RectTexture;

    // -- Flattened shadow / shaping (no nullable types on the render struct) --

    /// <summary><c>true</c> when the source had a <see cref="LightShadow"/> with shadows enabled (default true when no LightShadow component).</summary>
    public bool CastsShadows;

    /// <summary>Shadow tint when <see cref="CastsShadows"/>; black when unauthored.</summary>
    public Vector3 ShadowColor;

    /// <summary><c>true</c> when the source had a <see cref="LightShaping"/> cone authored (renderer should treat as a spot).</summary>
    public bool HasCone;

    /// <summary>Half-angle in degrees of the shaping cone, or <c>0</c> when <see cref="HasCone"/> is false.</summary>
    public float ConeAngle;

    /// <summary>Cone softness 0..1, or <c>0</c> when <see cref="HasCone"/> is false.</summary>
    public float ConeSoftness;
}

/// <summary>
/// Render-thread singleton mirroring every <see cref="RenderLight"/> extracted this frame
/// in a flat list. Convenience for shaders / passes that want an array-uniform-style
/// upload without iterating the render-world ECS. Cleared by <see cref="LightExtract"/>
/// at the start of each extract pass.
/// </summary>
public sealed class RenderLights
{
    /// <summary>All lights extracted for the current frame, in extract iteration order.</summary>
    public List<RenderLight> All { get; } = new();
}