using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>
    /// The format the positions of a mesh uploaded from them alone take where every one of them lies
    /// on a grid 16-bit integers hold (<see cref="PositionPacking"/>), three integers where the device
    /// reads them as a vertex, four where it reads those alone, or null for none, every position then
    /// three floats, chosen when the device is made.
    /// </summary>
    internal VertexFormat? PackedPositions { get; private set; }

    // The three-component format where the device reads it from a vertex buffer, the four-component
    // one where it reads that alone, and neither where it reads no 16-bit integers as floats.
    private void ChoosePackedPositions()
    {
        PackedPositions = null;
        foreach (var (format, vkFormat) in new[] { (VertexFormat.SScaled16x3, VkFormat.R16G16B16Sscaled), (VertexFormat.SScaled16x4, VkFormat.R16G16B16A16Sscaled) })
        {
            _instanceApi.vkGetPhysicalDeviceFormatProperties(_physicalDevice, vkFormat, out var properties);
            if ((properties.bufferFeatures & VkFormatFeatureFlags.VertexBuffer) == 0) continue;
            PackedPositions = format;
            return;
        }
    }

    // How the positions on a grid are kept, as the device's line says it.
    private string PackedPositionsLine => PackedPositions switch
    {
        VertexFormat.SScaled16x3 => "positions on a grid in six bytes (R16G16B16_SSCALED)",
        VertexFormat.SScaled16x4 => "positions on a grid in eight bytes (R16G16B16A16_SSCALED), the device reading no three 16-bit integers as a vertex's",
        _ => "positions as floats, the device reading no 16-bit integers as a vertex's",
    };
}

/// <summary>
/// The positions of a mesh uploaded from them alone, as 16-bit integers times a power of two, where
/// every one of them is a whole multiple of it.
/// </summary>
/// <remarks>
/// The power of two, the quantum, is the smallest whose 32,767 multiples reach every position, so
/// the integers are the positions themselves divided by it, exactly, and the quantum is folded into
/// the three columns of each instance's world matrix that turn a position (<c>ModelRenderer</c>),
/// which a power of two scales exactly. The vertex stage's sum of the integers through those rows
/// and the translation is then the float the floats would give, bit for bit, so two meshes' shared
/// corners agree, and the same vertex stage reads either. A mesh with a position off its grid keeps
/// three floats a vertex.
/// </remarks>
internal static class PositionPacking
{
    /// <summary>The quantum every position of <paramref name="vertices"/> is a whole multiple of within 16 bits, or 0 where none holds them.</summary>
    public static float Quantum(ReadOnlySpan<ModelVertex> vertices)
    {
        var most = 0f;
        foreach (ref readonly var vertex in vertices)
        {
            var p = vertex.Position;
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || !float.IsFinite(p.Z)) return 0;
            most = MathF.Max(most, MathF.Max(MathF.Abs(p.X), MathF.Max(MathF.Abs(p.Y), MathF.Abs(p.Z))));
        }
        if (most == 0) return 1;
        // The smallest power of two holding the reach in 32,767 of it, from a guess a little under.
        var quantum = MathF.Pow(2, MathF.Floor(MathF.Log2(most / 32767f)) - 1);
        while (most / quantum > 32767f) quantum *= 2;
        if (quantum < float.Epsilon * 65536) return 0;
        foreach (ref readonly var vertex in vertices)
        {
            var p = vertex.Position / quantum;
            if (p.X != MathF.Round(p.X) || p.Y != MathF.Round(p.Y) || p.Z != MathF.Round(p.Z)) return 0;
        }
        return quantum;
    }

    /// <summary>The positions of <paramref name="vertices"/> as integers of <paramref name="quantum"/>, <paramref name="components"/> 16-bit integers a vertex, the fourth 0.</summary>
    public static byte[] Pack(ReadOnlySpan<ModelVertex> vertices, float quantum, int components)
    {
        var bytes = new byte[vertices.Length * components * 2];
        var integers = MemoryMarshal.Cast<byte, short>(bytes.AsSpan());
        for (int i = 0; i < vertices.Length; i++)
        {
            var p = vertices[i].Position / quantum;
            (integers[i * components], integers[i * components + 1], integers[i * components + 2]) = ((short)p.X, (short)p.Y, (short)p.Z);
        }
        return bytes;
    }

    /// <summary>The bytes a vertex of positions alone takes in <paramref name="format"/>.</summary>
    public static uint Stride(VertexFormat format) => format switch
    {
        VertexFormat.SScaled16x3 => 6,
        VertexFormat.SScaled16x4 => 8,
        _ => 12,
    };
}
