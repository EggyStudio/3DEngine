using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Per-light entry in <see cref="LightingUbo"/>. Layout matches std140 alignment
/// rules so it can be uploaded verbatim into a GLSL <c>uniform LightData { ... }</c>
/// block. <see cref="LightingUboPacker.MaxLights"/> entries are reserved per frame.
/// </summary>
/// <remarks>
/// Field order is significant - it matches the <c>Light</c> struct the mesh
/// shader declares. Each <see cref="Vector4"/>
/// member is 16-byte aligned so the whole entry packs into 80 bytes with no
/// implicit padding when laid out as <c>std140</c>.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct LightUboEntry
{
    /// <summary>World-space position (<c>xyz</c>); <c>w</c> = light type (cast to int by the shader).</summary>
    public Vector4 PositionAndType;

    /// <summary>World-space forward direction (<c>xyz</c>); <c>w</c> = sphere/disk radius.</summary>
    public Vector4 DirectionAndRadius;

    /// <summary>Premultiplied linear-RGB emission (<c>rgb</c>); <c>w</c> = casts-shadows flag (0 / 1).</summary>
    public Vector4 EmittedColorAndShadow;

    /// <summary>Rect width (<c>x</c>), rect height (<c>y</c>), cylinder length (<c>z</c>), cone angle deg (<c>w</c>).</summary>
    public Vector4 ShapeParams;

    /// <summary>Cone softness (<c>x</c>); <c>yzw</c> reserved for future shaping (IES, falloff, filter intensity).</summary>
    public Vector4 ShapingParams;
}

/// <summary>
/// CPU mirror of the lighting UBO consumed by the mesh fragment shader,
/// a count followed by a fixed-size array of <see cref="LightUboEntry"/>. The size
/// matches the GLSL declaration <see cref="LightingUboPacker"/> generates / expects.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct LightingUbo
{
    /// <summary>Number of valid entries in <c>Lights</c> (<c>0..MaxLights</c>).</summary>
    public int LightCount;

    /// <summary>Padding so the array starts on a 16-byte boundary (std140 vec4 alignment).</summary>
    public int _pad0, _pad1, _pad2;

    /// <summary>Inline fixed-size light array. Use <see cref="LightingUboPacker.WriteEntry"/> to populate by index.</summary>
    public LightUboEntryArray Lights;
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
    /// Matches the array size compiled into the engine-side struct - shaders should
    /// declare a matching constant. Picked to fit comfortably within a single 16 KiB
    /// uniform buffer (16 lights × 80 bytes + header ≈ 1.3 KiB).
    /// </summary>
    public const int MaxLights = 16;

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
        e.PositionAndType         = new Vector4(light.Position, (int)light.Type);
        e.DirectionAndRadius      = new Vector4(light.Direction, light.Radius);
        e.EmittedColorAndShadow   = new Vector4(light.EmittedColor, light.CastsShadows ? 1f : 0f);
        e.ShapeParams             = new Vector4(light.Width, light.Height, light.Length, light.HasCone ? light.ConeAngle : 0f);
        e.ShapingParams           = new Vector4(light.HasCone ? light.ConeSoftness : 0f, 0, 0, 0);
    }
}



