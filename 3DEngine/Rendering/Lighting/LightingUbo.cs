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
/// CPU mirror of the lighting UBO the model pass reads (<c>modelpass.slang</c>),
/// a count followed by a fixed-size array of <see cref="LightUboEntry"/>. The size
/// matches the shader declaration <see cref="LightingUboPacker"/> generates / expects.
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
    /// uniform buffer (16 lights of 64 bytes and a 16-byte header, about 1 KiB).
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
        e.PositionAndKind = new Vector4(light.Position, (int)light.Kind);
        e.DirectionAndRange = new Vector4(light.Direction, light.Range);
        e.ColorAndShadow = new Vector4(light.EmittedColor, light.CastsShadows ? 1f : 0f);
        e.Cone = new Vector4(light.CosInner, light.CosOuter, 0, 0);
    }
}



