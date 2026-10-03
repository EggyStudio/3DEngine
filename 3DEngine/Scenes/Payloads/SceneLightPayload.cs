using System.Numerics;

namespace Engine;

/// <summary>
/// Backend-agnostic light payload covering the full <c>UsdLux</c> shape vocabulary
/// (distant / sphere / rect / disk / cylinder / dome / geometry / portal / plugin) and the
/// applied APIs (<c>UsdLuxLightAPI</c>, <c>UsdLuxShadowAPI</c>, <c>UsdLuxShapingAPI</c>).
/// Carried on a <see cref="SceneNode"/> as an inert payload until a runtime <c>Light</c>
/// ECS component lands and the renderer grows light support; spawn systems may attach the
/// payload directly so future systems can pick it up without re-reading the source.
/// </summary>
/// <remarks>
/// <para>
/// Field semantics follow the <c>UsdLuxLightAPI</c> base inputs (<c>color</c>,
/// <c>intensity</c>, <c>exposure</c>, <c>diffuse</c>, <c>specular</c>, <c>normalize</c>,
/// <c>colorTemperature</c>) plus the per-shape inputs (e.g.
/// <c>UsdLuxSphereLight.radius</c>, <c>UsdLuxRectLight.width</c>/<c>height</c>) and the
/// applied <c>UsdLuxShadowAPI</c> / <c>UsdLuxShapingAPI</c> namespaces. All shape and
/// applied-API parameters are nullable so the payload type itself is shape-agnostic and
/// only the fields relevant to <see cref="Type"/> / what was authored on the prim are
/// populated by the reader.
/// </para>
/// <para>
/// <b>Energy units:</b> the effective radiant intensity is <c>color * intensity * 2^exposure</c>,
/// matching the USD luminance convention. When <see cref="EnableColorTemperature"/> is
/// <c>true</c> the renderer should multiply <see cref="Color"/> by the blackbody color
/// derived from <see cref="ColorTemperature"/> (Kelvin); USD itself does the same in
/// <c>UsdLuxLightAPI.ComputeBaseEmission</c>. Renderer-side normalization (e.g. converting
/// to nits / lumens) is the consumer's responsibility.
/// </para>
/// </remarks>
public sealed class SceneLightPayload
{
    /// <summary>Display name (typically the source light prim's leaf name).</summary>
    public string Name { get; init; } = "Light";

    /// <summary>Which UsdLux shape this light represents.</summary>
    public required SceneLightType Type { get; init; }

    /// <summary>Linear-RGB color (multiplied with <see cref="Intensity"/> and 2^<see cref="Exposure"/>).</summary>
    public Vector3 Color { get; init; } = Vector3.One;

    /// <summary>Scalar intensity multiplier (<c>UsdLuxLightAPI.intensity</c>, default 1.0).</summary>
    public float Intensity { get; init; } = 1f;

    /// <summary>Exposure stops applied as a power of two (<c>UsdLuxLightAPI.exposure</c>, default 0).</summary>
    public float Exposure { get; init; }

    /// <summary>
    /// <c>UsdLuxLightAPI.normalize</c>: when true, the light's flux is normalized so total
    /// emitted power is invariant to area. <c>null</c> = unauthored (renderer default).
    /// </summary>
    public bool? Normalize { get; init; }

    /// <summary><c>UsdLuxLightAPI.diffuse</c> diffuse contribution multiplier. <c>null</c> = unauthored.</summary>
    public float? Diffuse { get; init; }

    /// <summary><c>UsdLuxLightAPI.specular</c> specular contribution multiplier. <c>null</c> = unauthored.</summary>
    public float? Specular { get; init; }

    /// <summary>
    /// <c>UsdLuxLightAPI.colorTemperature</c> in Kelvin (default 6500 in the schema). Only
    /// honored by the renderer when <see cref="EnableColorTemperature"/> is true.
    /// </summary>
    public float? ColorTemperature { get; init; }

    /// <summary><c>UsdLuxLightAPI.enableColorTemperature</c>. <c>null</c> = unauthored.</summary>
    public bool? EnableColorTemperature { get; init; }

    /// <summary>Sphere / disk radius in stage units (<see cref="SceneLightType.Sphere"/>, <see cref="SceneLightType.Disk"/>).</summary>
    public float? Radius { get; init; }

    /// <summary>Rect / dome width in stage units (<see cref="SceneLightType.Rect"/>).</summary>
    public float? Width { get; init; }

    /// <summary>Rect / dome height in stage units (<see cref="SceneLightType.Rect"/>).</summary>
    public float? Height { get; init; }

    /// <summary>Cylinder length in stage units (<see cref="SceneLightType.Cylinder"/>).</summary>
    public float? Length { get; init; }

    /// <summary>Shaping cone angle in degrees (<c>inputs:shaping:cone:angle</c>), or <c>null</c>. Convenience shortcut to <see cref="Shaping"/>.<see cref="SceneLightShaping.ConeAngle"/>.</summary>
    public float? ConeAngle { get; init; }

    /// <summary>Shaping cone softness 0..1 (<c>inputs:shaping:cone:softness</c>), or <c>null</c>. Convenience shortcut to <see cref="Shaping"/>.<see cref="SceneLightShaping.ConeSoftness"/>.</summary>
    public float? ConeSoftness { get; init; }

    /// <summary>Path to an IES profile file for shaping (<c>inputs:shaping:ies:file</c>), or <c>null</c>. Convenience shortcut to <see cref="Shaping"/>.<see cref="SceneLightShaping.IesProfilePath"/>.</summary>
    public string? IesProfilePath { get; init; }

    /// <summary>Texture map for dome lights (<c>UsdLuxDomeLight.texture:file</c>), or <c>null</c>.</summary>
    public string? DomeTexturePath { get; init; }

    /// <summary>
    /// Dome texture format token (<c>UsdLuxDomeLight.texture:format</c>): one of
    /// <c>"automatic"</c>, <c>"latlong"</c>, <c>"mirroredBall"</c>, <c>"angular"</c>,
    /// <c>"cubeMapVerticalCross"</c>. <c>null</c> = unauthored (renderer default).
    /// </summary>
    public string? DomeTextureFormat { get; init; }

    /// <summary><c>UsdLuxDomeLight.guideRadius</c>: viewport-only visualization radius (no rendering effect).</summary>
    public float? DomeGuideRadius { get; init; }

    /// <summary>Texture map for rect lights (<c>UsdLuxRectLight.texture:file</c>), or <c>null</c>.</summary>
    public string? RectTexturePath { get; init; }

    /// <summary>
    /// For <see cref="SceneLightType.Geometry"/>: prim path(s) of the bound geometry
    /// (<c>UsdLuxGeometryLight.geometry</c> rel targets). Empty when the rel was not
    /// authored or this light is not a GeometryLight.
    /// </summary>
    public IReadOnlyList<string> GeometryPaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// For <see cref="SceneLightType.Dome"/>: prim path(s) of the
    /// <c>UsdLuxPortalLight</c> children that participate in importance sampling
    /// (<c>UsdLuxDomeLight.portals</c> rel targets).
    /// </summary>
    public IReadOnlyList<string> PortalPaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Prim paths of <c>UsdLuxLightFilter</c>s linked via <c>UsdLuxLightAPI.filters</c>.
    /// Filter shader networks themselves are not yet materialized; treat as opaque IDs the
    /// renderer can resolve to its own filter graph.
    /// </summary>
    public IReadOnlyList<string> FilterPaths { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Applied <c>UsdLuxShadowAPI</c> parameters when the prim authored any shadow input,
    /// otherwise <c>null</c>. Renderers should default to "casts hard shadows enabled" when
    /// this is null.
    /// </summary>
    public SceneLightShadow? Shadow { get; init; }

    /// <summary>
    /// Applied <c>UsdLuxShapingAPI</c> parameters (cone + focus + IES) when the prim
    /// authored any shaping input, otherwise <c>null</c>. Spawners may use the convenience
    /// shortcuts <see cref="ConeAngle"/> / <see cref="ConeSoftness"/> /
    /// <see cref="IesProfilePath"/> for the most common subset.
    /// </summary>
    public SceneLightShaping? Shaping { get; init; }
}

/// <summary>
/// Backend-agnostic mirror of <c>UsdLuxShadowAPI</c>. All fields nullable: only the
/// attributes actually authored on the source prim round-trip through the payload.
/// </summary>
/// <param name="Enable"><c>inputs:shadow:enable</c>: whether the light casts shadows.</param>
/// <param name="Color"><c>inputs:shadow:color</c>: linear-RGB tint applied to the shadowed region (artistic; default black).</param>
/// <param name="Distance"><c>inputs:shadow:distance</c>: max distance shadows are traced. Negative / unset means "use renderer default" (typically far clip).</param>
/// <param name="Falloff"><c>inputs:shadow:falloff</c>: distance from the shadow's max range over which the shadow softens.</param>
/// <param name="FalloffGamma"><c>inputs:shadow:falloffGamma</c>: gamma curve applied to the falloff transition.</param>
public sealed record SceneLightShadow(
    bool? Enable = null,
    Vector3? Color = null,
    float? Distance = null,
    float? Falloff = null,
    float? FalloffGamma = null);

/// <summary>
/// Backend-agnostic mirror of <c>UsdLuxShapingAPI</c>: cone-restricted spotlight, focus
/// blur, and IES photometric profile. All fields nullable: only authored attributes
/// round-trip.
/// </summary>
/// <param name="ConeAngle"><c>inputs:shaping:cone:angle</c> in degrees (half-angle).</param>
/// <param name="ConeSoftness"><c>inputs:shaping:cone:softness</c> 0..1 fade band.</param>
/// <param name="FocusPower"><c>inputs:shaping:focus</c> exponent on cosine fall-off.</param>
/// <param name="FocusTint"><c>inputs:shaping:focusTint</c> color towards the dim edge.</param>
/// <param name="IesProfilePath"><c>inputs:shaping:ies:file</c> path to an IES photometric profile.</param>
/// <param name="IesAngleScale"><c>inputs:shaping:ies:angleScale</c>: rescales the IES profile.</param>
/// <param name="IesNormalize"><c>inputs:shaping:ies:normalize</c>: when true, the IES profile contributes a unit-power emission.</param>
public sealed record SceneLightShaping(
    float? ConeAngle = null,
    float? ConeSoftness = null,
    float? FocusPower = null,
    Vector3? FocusTint = null,
    string? IesProfilePath = null,
    float? IesAngleScale = null,
    bool? IesNormalize = null);

/// <summary>UsdLux shape vocabulary.</summary>
public enum SceneLightType
{
    /// <summary><c>UsdLuxDistantLight</c>: parallel rays from infinity (sun-style).</summary>
    Distant,

    /// <summary><c>UsdLuxSphereLight</c>: omnidirectional spherical area light.</summary>
    Sphere,

    /// <summary><c>UsdLuxRectLight</c>: planar rectangular area light.</summary>
    Rect,

    /// <summary><c>UsdLuxDiskLight</c>: planar circular area light.</summary>
    Disk,

    /// <summary><c>UsdLuxCylinderLight</c>: capped cylindrical area light.</summary>
    Cylinder,

    /// <summary><c>UsdLuxDomeLight</c>: image-based environment light at infinity.</summary>
    Dome,

    /// <summary>
    /// <c>UsdLuxGeometryLight</c>: light whose shape is bound to a Mesh / Volume prim via
    /// the <c>geometry</c> relationship (see <see cref="SceneLightPayload.GeometryPaths"/>).
    /// </summary>
    Geometry,

    /// <summary>
    /// <c>UsdLuxPortalLight</c>: rectangular window into a parent <c>UsdLuxDomeLight</c>
    /// for importance-sampling acceleration. Width/Height come from the local extent.
    /// </summary>
    Portal,

    /// <summary>
    /// <c>UsdLuxPluginLight</c>: opaque renderer-specific plugin light. Carries the
    /// common UsdLuxLightAPI inputs only; the renderer is expected to dispatch on the
    /// prim's <c>info:id</c> shader id.
    /// </summary>
    Plugin,
}