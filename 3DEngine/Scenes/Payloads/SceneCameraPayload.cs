namespace Engine;

/// <summary>
/// Backend-agnostic camera payload mirroring <c>UsdGeomCamera</c>'s authored attributes
/// (apertures and focal length in millimeters, clip planes in stage units). Translated to
/// a renderer-side <see cref="Camera"/> by <c>SceneSpawnSystem</c> via the derived
/// <see cref="VerticalFovRadians"/>.
/// </summary>
/// <remarks>
/// <para>
/// USD authors cameras in <i>physical</i> units to match real-world DCC tools (Maya, Houdini,
/// Blender) and renderers (Karma, Cycles, Arnold). The runtime <see cref="Camera"/> only
/// needs a vertical FOV, so this payload exposes <see cref="VerticalFovRadians"/> as a
/// computed convenience while keeping the original physical inputs around for round-trip
/// to the writer and for future depth-of-field work (<see cref="FocusDistance"/>,
/// <see cref="FStop"/>).
/// </para>
/// </remarks>
public sealed class SceneCameraPayload
{
    /// <summary>Display name (typically the source camera prim's leaf name).</summary>
    public string Name { get; init; } = "Camera";

    /// <summary>Projection model (perspective vs. orthographic).</summary>
    public SceneProjection Projection { get; init; } = SceneProjection.Perspective;

    /// <summary>
    /// Horizontal aperture in millimeters, matching the USD <c>horizontalAperture</c>
    /// attribute. Together with <see cref="FocalLength"/> defines the horizontal FOV
    /// (perspective) or the orthographic width (orthographic).
    /// </summary>
    public float HorizontalAperture { get; init; } = 20.955f; // 35mm Academy default

    /// <summary>Vertical aperture in millimeters; analogous to <see cref="HorizontalAperture"/>.</summary>
    public float VerticalAperture { get; init; } = 15.2908f;  // 35mm Academy default

    /// <summary>Focal length in millimeters (used only when <see cref="Projection"/> is <see cref="SceneProjection.Perspective"/>).</summary>
    public float FocalLength { get; init; } = 50f;

    /// <summary>Near clip plane in stage units (already in source meters-per-unit).</summary>
    public float NearClip { get; init; } = 0.1f;

    /// <summary>Far clip plane in stage units (already in source meters-per-unit).</summary>
    public float FarClip { get; init; } = 1000f;

    /// <summary>Optional focus distance for depth-of-field (stage units), or <c>null</c> if not authored.</summary>
    public float? FocusDistance { get; init; }

    /// <summary>Optional aperture f-stop value for depth-of-field, or <c>null</c> if not authored.</summary>
    public float? FStop { get; init; }

    /// <summary>
    /// Convenience: vertical field-of-view in radians, derived from <see cref="VerticalAperture"/>
    /// and <see cref="FocalLength"/> using the standard perspective formula
    /// <c>2 * atan(verticalAperture / (2 * focalLength))</c>. Returns <c>0</c> for orthographic
    /// projections (caller should use <see cref="VerticalAperture"/> as the half-extent instead).
    /// </summary>
    public float VerticalFovRadians
    {
        get
        {
            if (Projection != SceneProjection.Perspective || FocalLength <= 0f) return 0f;
            return 2f * MathF.Atan(VerticalAperture / (2f * FocalLength));
        }
    }
}

/// <summary>Camera projection model.</summary>
public enum SceneProjection
{
    /// <summary>Standard perspective projection (the USD <c>perspective</c> token).</summary>
    Perspective,

    /// <summary>Orthographic projection (the USD <c>orthographic</c> token).</summary>
    Orthographic,
}