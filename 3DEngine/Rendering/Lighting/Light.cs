using System.Numerics;

namespace Engine;

/// <summary>
/// Runtime light component, the ECS counterpart of <see cref="SceneLightPayload"/>.
/// One monolithic struct (tagged-union via <see cref="Type"/>) carries all UsdLux shape
/// vocabularies the engine supports today; only the fields relevant to <see cref="Type"/>
/// (and what the source actually authored) are populated by <see cref="LightSpawnSystem"/>.
/// Optional <see cref="LightShadow"/> / <see cref="LightShaping"/> companion components
/// are attached separately when the source authored <c>UsdLuxShadowAPI</c> /
/// <c>UsdLuxShapingAPI</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Energy:</b> the effective radiant intensity is
/// <c><see cref="Color"/> * <see cref="Intensity"/> * 2^<see cref="Exposure"/></c>.
/// When <see cref="EnableColorTemperature"/> is <c>true</c>, multiply the result by the
/// blackbody color derived from <see cref="ColorTemperature"/> (Kelvin); use
/// <see cref="LightColor.ComputeEmittedColor"/> for the canonical computation.
/// </para>
/// <para>
/// <b>Texture handles</b> (<see cref="DomeTexture"/>, <see cref="RectTexture"/>) are
/// resolved at spawn time by <see cref="LightSpawnSystem"/> against the <see cref="AssetServer"/>
/// using the same sRGB / linear conventions as <c>SceneSpawner</c>'s material slots
/// (linear, mip-mapped — environment maps and area-light textures are radiometric).
/// </para>
/// </remarks>
/// <seealso cref="SceneLightPayload"/>
/// <seealso cref="LightShadow"/>
/// <seealso cref="LightShaping"/>
/// <seealso cref="RenderLight"/>
public struct Light
{
    /// <summary>UsdLux shape this light represents (sphere / rect / distant / dome / ...).</summary>
    public LightType Type;

    /// <summary>Linear-RGB color (multiplied by <see cref="Intensity"/> and 2^<see cref="Exposure"/>).</summary>
    public Vector3 Color;

    /// <summary>Scalar intensity multiplier (<c>UsdLuxLightAPI.intensity</c>, default 1).</summary>
    public float Intensity;

    /// <summary>Exposure stops applied as a power of two (<c>UsdLuxLightAPI.exposure</c>).</summary>
    public float Exposure;

    /// <summary><c>UsdLuxLightAPI.normalize</c>, <c>null</c> = unauthored (renderer default).</summary>
    public bool? Normalize;

    /// <summary><c>UsdLuxLightAPI.diffuse</c> diffuse contribution multiplier; <c>null</c> = unauthored.</summary>
    public float? Diffuse;

    /// <summary><c>UsdLuxLightAPI.specular</c> specular contribution multiplier; <c>null</c> = unauthored.</summary>
    public float? Specular;

    /// <summary><c>UsdLuxLightAPI.colorTemperature</c> (Kelvin); only honored when <see cref="EnableColorTemperature"/> is true.</summary>
    public float? ColorTemperature;

    /// <summary><c>UsdLuxLightAPI.enableColorTemperature</c>, <c>null</c> = unauthored.</summary>
    public bool? EnableColorTemperature;

    /// <summary>Sphere / disk radius in world units (<see cref="LightType.Sphere"/>, <see cref="LightType.Disk"/>).</summary>
    public float? Radius;

    /// <summary>Rect width in world units (<see cref="LightType.Rect"/>).</summary>
    public float? Width;

    /// <summary>Rect height in world units (<see cref="LightType.Rect"/>).</summary>
    public float? Height;

    /// <summary>Cylinder length in world units (<see cref="LightType.Cylinder"/>).</summary>
    public float? Length;

    /// <summary>
    /// Resolved dome texture handle (<see cref="LightType.Dome"/>); <see cref="Handle{T}.Invalid"/> when no texture.
    /// </summary>
    public Handle<Texture> DomeTexture;

    /// <summary><c>UsdLuxDomeLight.texture:format</c>; <c>null</c> = unauthored.</summary>
    public DomeTextureFormat? DomeTextureFormat;

    /// <summary><c>UsdLuxDomeLight.guideRadius</c>: viewport-only visualization radius.</summary>
    public float? DomeGuideRadius;

    /// <summary>Resolved rect-light texture handle (<see cref="LightType.Rect"/>); invalid when none.</summary>
    public Handle<Texture> RectTexture;

    /// <summary>
    /// For <see cref="LightType.Geometry"/>: source prim paths of the bound geometry
    /// (carried verbatim from <see cref="SceneLightPayload.GeometryPaths"/>; the renderer
    /// resolves these to entity references in a follow-up pass).
    /// </summary>
    public string[]? GeometryPaths;

    /// <summary>For <see cref="LightType.Dome"/>: source prim paths of <c>UsdLuxPortalLight</c> children.</summary>
    public string[]? PortalPaths;

    /// <summary>Source prim paths of linked <c>UsdLuxLightFilter</c>s; opaque to the renderer for now.</summary>
    public string[]? FilterPaths;
}

/// <summary>
/// Optional ECS companion component attached when the source <see cref="SceneLightPayload"/>
/// carried a <see cref="SceneLightShadow"/>. Mirror of <c>UsdLuxShadowAPI</c>; absent
/// component = renderer defaults (typically "casts hard shadows enabled").
/// </summary>
public struct LightShadow
{
    /// <summary><c>inputs:shadow:enable</c>: whether the light casts shadows.</summary>
    public bool? Enable;

    /// <summary><c>inputs:shadow:color</c>: linear-RGB tint applied to the shadowed region.</summary>
    public Vector3? Color;

    /// <summary><c>inputs:shadow:distance</c>: max distance shadows are traced.</summary>
    public float? Distance;

    /// <summary><c>inputs:shadow:falloff</c>: distance over which the shadow softens.</summary>
    public float? Falloff;

    /// <summary><c>inputs:shadow:falloffGamma</c>: gamma curve on the falloff.</summary>
    public float? FalloffGamma;
}

/// <summary>
/// Optional ECS companion component attached when the source <see cref="SceneLightPayload"/>
/// carried a <see cref="SceneLightShaping"/> (cone-restricted spot, focus blur, IES profile).
/// IES file <i>parsing</i> is out of scope; <see cref="IesProfilePath"/> rides through as
/// an opaque string the renderer can resolve later.
/// </summary>
public struct LightShaping
{
    /// <summary><c>inputs:shaping:cone:angle</c> in degrees (half-angle).</summary>
    public float? ConeAngle;

    /// <summary><c>inputs:shaping:cone:softness</c> 0..1 fade band.</summary>
    public float? ConeSoftness;

    /// <summary><c>inputs:shaping:focus</c> exponent on cosine fall-off.</summary>
    public float? FocusPower;

    /// <summary><c>inputs:shaping:focusTint</c> color towards the dim edge.</summary>
    public Vector3? FocusTint;

    /// <summary><c>inputs:shaping:ies:file</c>; opaque path (no candela-table parsing yet).</summary>
    public string? IesProfilePath;

    /// <summary><c>inputs:shaping:ies:angleScale</c>: rescales the IES profile.</summary>
    public float? IesAngleScale;

    /// <summary><c>inputs:shaping:ies:normalize</c>: unit-power IES emission when true.</summary>
    public bool? IesNormalize;
}

/// <summary>UsdLux shape vocabulary (Volume excluded; not yet implemented).</summary>
public enum LightType
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

    /// <summary><c>UsdLuxGeometryLight</c>: light bound to mesh geometry via the <c>geometry</c> rel.</summary>
    Geometry,

    /// <summary><c>UsdLuxPortalLight</c>: rectangular window into a parent dome for importance sampling.</summary>
    Portal,

    /// <summary><c>UsdLuxPluginLight</c>: opaque renderer-specific plugin light.</summary>
    Plugin,
}

/// <summary>Dome-texture projection format (<c>UsdLuxDomeLight.texture:format</c>).</summary>
public enum DomeTextureFormat
{
    /// <summary>Renderer chooses based on aspect ratio / extension.</summary>
    Automatic,

    /// <summary>Equirectangular latitude-longitude (the most common HDR env map layout).</summary>
    LatLong,

    /// <summary>Mirrored-ball "chrome ball" panorama.</summary>
    MirroredBall,

    /// <summary>Angular fisheye (Debevec light probe).</summary>
    Angular,

    /// <summary>Vertical-cross unfolded cube map.</summary>
    CubeMapVerticalCross,
}