using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>One light as the model pass's uniform buffer holds it, four <c>float4</c>, 64 bytes.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct LightUboEntry
{
    /// <summary>xyz: world position. w: the <see cref="LightKind"/> as a number.</summary>
    public Vector4 PositionAndKind;

    /// <summary>xyz: the way the light points. w: its range, 0 for none.</summary>
    public Vector4 DirectionAndRange;

    /// <summary>xyz: color times intensity. w: 1 when it casts shadows.</summary>
    public Vector4 ColorAndShadow;

    /// <summary>x: cosine of a spot's inner angle. y: cosine of its outer angle.</summary>
    public Vector4 Cone;
}

/// <summary>
/// CPU mirror of the lighting UBO the model pass reads (<c>modelpass.slang</c>): a count, the
/// shadowed light and its cascades' world to map transforms, then a fixed-size array of
/// <see cref="LightUboEntry"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct LightingUbo
{
    /// <summary>Number of valid entries in <c>Lights</c> (<c>0..MaxLights</c>).</summary>
    public int LightCount;

    /// <summary>The index in <c>Lights</c> of the light the shadow map was drawn from, or -1 for none.</summary>
    public int ShadowLight;

    /// <summary>How many of <see cref="ShadowCascades"/> hold a cascade.</summary>
    public int CascadeCount;

    /// <summary>The index in <c>Lights</c> of the spot light drawn into the map's last tile, or -1 for none.</summary>
    public int SpotShadowCount;

    /// <summary>
    /// xyz: the width in world units one texel of each cascade covers, which the shader offsets a
    /// surface by along its normal. w: the width of a texel of the spot light's tile per unit of
    /// distance from the light.
    /// </summary>
    public Vector4 ShadowTexels;

    /// <summary>
    /// World space to each cascade's clip space, the light's view and projection, nearest first,
    /// and in the last the spot light's.
    /// </summary>
    public ShadowCascadeArray ShadowCascades;

    /// <summary>x: the environment map's intensity. y: its last mip. z: 1 when there is one.</summary>
    public Vector4 Environment;

    /// <summary>Inline fixed-size light array. Use <see cref="LightingUboPacker.WriteEntry"/> to populate by index.</summary>
    public LightUboEntryArray Lights;

    /// <summary>The width of a point light's shadow texel per unit of distance from it, in x.</summary>
    public Vector4 PointShadow;

    /// <summary>World space to each face's clip space for each shadowed point light, six to a light.</summary>
    public PointShadowFaceArray PointShadowFaces;

    /// <summary>The width of each shadowed spot light's texel per unit of distance from it, by slot.</summary>
    public Vector4 SpotShadowTexels;

    /// <summary>World space to each shadowed spot light's clip space, by slot.</summary>
    public SpotShadowArray SpotShadows;
}

/// <summary>Fixed-size storage for the shadowed spot lights' projections.</summary>
[InlineArray(ShadowFit.MaxSpotLights)]
public struct SpotShadowArray
{
    /// <summary>The first spot light's.</summary>
    public Matrix4x4 _element0;
}

/// <summary>Fixed-size storage for the faces of the shadowed point lights.</summary>
[InlineArray(ShadowFit.MaxPointLights * 6)]
public struct PointShadowFaceArray
{
    /// <summary>The first face.</summary>
    public Matrix4x4 _element0;
}

/// <summary>Inline storage for <see cref="LightingUbo.ShadowCascades"/>, four matrices.</summary>
[InlineArray(LightingUboPacker.MaxCascades)]
public struct ShadowCascadeArray
{
    /// <summary>First-element placeholder required by <see cref="InlineArrayAttribute"/>.</summary>
    public Matrix4x4 _element0;
}

/// <summary>Inline fixed-length backing storage for the <see cref="LightingUbo.Lights"/> array.</summary>
[InlineArray(LightingUboPacker.MaxLights)]
public struct LightUboEntryArray
{
    /// <summary>First-element placeholder required by <see cref="InlineArrayAttribute"/>.</summary>
    public LightUboEntry _element0;
}

/// <summary>
/// Packs a <see cref="RenderLights"/> snapshot into a <see cref="LightingUbo"/> ready
/// for upload through <see cref="DynamicBufferAllocator"/>.
/// </summary>
public static class LightingUboPacker
{
    /// <summary>
    /// Hard cap on the number of analytic lights the lighting UBO carries per frame.
    /// Matches the array size compiled into the engine-side struct, and shaders should
    /// declare a matching constant. Picked to fit comfortably within a single 16 KiB
    /// uniform buffer (16 lights of 64 bytes and a 304-byte header, about 1.3 KiB).
    /// </summary>
    public const int MaxLights = 16;

    /// <summary>The most shadow cascades the buffer carries, the tiles of the shadow map.</summary>
    public const int MaxCascades = 4;

    /// <summary>Returns the byte size of <see cref="LightingUbo"/>.</summary>
    public static int SizeBytes => Marshal.SizeOf<LightingUbo>();

    /// <summary>
    /// Builds a <see cref="LightingUbo"/> by copying up to <see cref="MaxLights"/>
    /// entries from <paramref name="lights"/>. Surplus lights are silently dropped;
    /// the count clamps to <see cref="MaxLights"/>.
    /// </summary>
    public static LightingUbo Pack(IReadOnlyList<RenderLight> lights)
    {
        var ubo = default(LightingUbo);
        int count = lights.Count < MaxLights ? lights.Count : MaxLights;
        ubo.LightCount = count;
        ubo.ShadowLight = -1;


        for (int i = 0; i < count; i++)
        {
            var l = lights[i];
            WriteEntry(ref ubo, i, in l);
        }

        return ubo;
    }

    /// <summary>
    /// Translates a single <see cref="RenderLight"/> into <c>ubo.Lights[index]</c>.
    /// Public so callers needing finer control (filtering, sorting) can pack directly.
    /// </summary>
    public static void WriteEntry(ref LightingUbo ubo, int index, in RenderLight light)
    {
        ref var e = ref ubo.Lights[index];
        e.PositionAndKind = new Vector4(light.Position, (int)light.Kind);
        e.DirectionAndRange = new Vector4(light.Direction, light.Range);
        e.ColorAndShadow = new Vector4(light.EmittedColor, light.CastsShadows ? 1f : 0f);
        e.Cone = new Vector4(light.CosInner, light.CosOuter, 0, 0);
    }
}



