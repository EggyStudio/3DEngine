using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// SIMD-accelerated bulk math operations for float spans.
/// Automatically uses the widest available vector register via <see cref="Vector{T}"/>
/// (e.g., AVX2 = 256-bit, SSE2 = 128-bit) with a scalar fallback for remainders.
/// </summary>
/// <remarks>
/// Designed for use with <see cref="EcsWorld.BulkProcess{T}"/> or raw <c>ComponentSpan&lt;T&gt;</c> data.
/// For struct components, use <see cref="MemoryMarshal.Cast{TFrom,TTo}(Span{TFrom})"/> to reinterpret
/// as a flat float span before calling these methods.
/// </remarks>
/// <example>
/// <code>
/// // Apply gravity to all Velocity components using SIMD
/// var span = ecs.GetSpan&lt;Velocity&gt;();
/// // Assuming Velocity is { float X, float Y } - cast to flat floats, stride 2
/// var floats = MemoryMarshal.Cast&lt;Velocity, float&gt;(span.Components);
/// // Add -9.81*dt to every Y (odd indices): use ScaleAndOffset on a strided view,
/// // or iterate in bulk with the span APIs.
///
/// // Simpler: scale all floats in a component by a factor
/// SimdMath.Scale(floats, 0.99f); // damping
/// </code>
/// </example>
public static class SimdMath
{
    /// <summary>Adds <paramref name="value"/> to every element: <c>data[i] += value</c>.</summary>
    /// <param name="data">The float span to modify in-place.</param>
    /// <param name="value">The scalar value to add.</param>
    public static void Add(Span<float> data, float value)
    {
        if (Vector.IsHardwareAccelerated && data.Length >= Vector<float>.Count)
        {
            var vecs = MemoryMarshal.Cast<float, Vector<float>>(data);
            var addVec = new Vector<float>(value);
            for (int i = 0; i < vecs.Length; i++)
                vecs[i] += addVec;
            int processed = vecs.Length * Vector<float>.Count;
            for (int i = processed; i < data.Length; i++)
                data[i] += value;
        }
        else
        {
            for (int i = 0; i < data.Length; i++)
                data[i] += value;
        }
    }

    /// <summary>Multiplies every element by <paramref name="scale"/>: <c>data[i] *= scale</c>.</summary>
    /// <param name="data">The float span to modify in-place.</param>
    /// <param name="scale">The scalar multiplier.</param>
    public static void Scale(Span<float> data, float scale)
    {
        if (Vector.IsHardwareAccelerated && data.Length >= Vector<float>.Count)
        {
            var vecs = MemoryMarshal.Cast<float, Vector<float>>(data);
            var scaleVec = new Vector<float>(scale);
            for (int i = 0; i < vecs.Length; i++)
                vecs[i] *= scaleVec;
            int processed = vecs.Length * Vector<float>.Count;
            for (int i = processed; i < data.Length; i++)
                data[i] *= scale;
        }
        else
        {
            for (int i = 0; i < data.Length; i++)
                data[i] *= scale;
        }
    }

    /// <summary>Fused multiply-add: <c>data[i] = data[i] * scale + offset</c>.</summary>
    /// <param name="data">The float span to modify in-place.</param>
    /// <param name="scale">The scalar multiplier applied first.</param>
    /// <param name="offset">The scalar addend applied after multiplication.</param>
    public static void ScaleAndOffset(Span<float> data, float scale, float offset)
    {
        if (Vector.IsHardwareAccelerated && data.Length >= Vector<float>.Count)
        {
            var vecs = MemoryMarshal.Cast<float, Vector<float>>(data);
            var scaleVec = new Vector<float>(scale);
            var offsetVec = new Vector<float>(offset);
            for (int i = 0; i < vecs.Length; i++)
                vecs[i] = vecs[i] * scaleVec + offsetVec;
            int processed = vecs.Length * Vector<float>.Count;
            for (int i = processed; i < data.Length; i++)
                data[i] = data[i] * scale + offset;
        }
        else
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = data[i] * scale + offset;
        }
    }

    /// <summary>Clamps every element to <c>[min, max]</c>.</summary>
    /// <param name="data">The float span to modify in-place.</param>
    /// <param name="min">The minimum allowed value.</param>
    /// <param name="max">The maximum allowed value.</param>
    public static void Clamp(Span<float> data, float min, float max)
    {
        if (Vector.IsHardwareAccelerated && data.Length >= Vector<float>.Count)
        {
            var vecs = MemoryMarshal.Cast<float, Vector<float>>(data);
            var minVec = new Vector<float>(min);
            var maxVec = new Vector<float>(max);
            for (int i = 0; i < vecs.Length; i++)
                vecs[i] = Vector.Max(Vector.Min(vecs[i], maxVec), minVec);
            int processed = vecs.Length * Vector<float>.Count;
            for (int i = processed; i < data.Length; i++)
                data[i] = MathF.Max(min, MathF.Min(max, data[i]));
        }
        else
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = MathF.Max(min, MathF.Min(max, data[i]));
        }
    }

    /// <summary>Element-wise addition: <c>dst[i] += src[i]</c>.</summary>
    /// <param name="dst">The destination span to modify in-place.</param>
    /// <param name="src">The source span to add from. Must be at least as long as <paramref name="dst"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="src"/> is shorter than <paramref name="dst"/>.</exception>
    public static void AddSpans(Span<float> dst, ReadOnlySpan<float> src)
    {
        if (src.Length < dst.Length)
            throw new ArgumentException("Source span must be at least as long as destination.", nameof(src));

        if (Vector.IsHardwareAccelerated && dst.Length >= Vector<float>.Count)
        {
            var dstVecs = MemoryMarshal.Cast<float, Vector<float>>(dst);
            var srcVecs = MemoryMarshal.Cast<float, Vector<float>>(src);
            for (int i = 0; i < dstVecs.Length; i++)
                dstVecs[i] += srcVecs[i];
            int processed = dstVecs.Length * Vector<float>.Count;
            for (int i = processed; i < dst.Length; i++)
                dst[i] += src[i];
        }
        else
        {
            for (int i = 0; i < dst.Length; i++)
                dst[i] += src[i];
        }
    }

    /// <summary>Linear interpolation: <c>a[i] = a[i] + (b[i] - a[i]) * t</c>.</summary>
    /// <param name="a">The span to interpolate in-place (also serves as the "from" values).</param>
    /// <param name="b">The "to" values. Must be at least as long as <paramref name="a"/>.</param>
    /// <param name="t">Interpolation factor, typically in <c>[0, 1]</c>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="b"/> is shorter than <paramref name="a"/>.</exception>
    public static void Lerp(Span<float> a, ReadOnlySpan<float> b, float t)
    {
        if (b.Length < a.Length)
            throw new ArgumentException("Source span must be at least as long as destination.", nameof(b));

        if (Vector.IsHardwareAccelerated && a.Length >= Vector<float>.Count)
        {
            var aVecs = MemoryMarshal.Cast<float, Vector<float>>(a);
            var bVecs = MemoryMarshal.Cast<float, Vector<float>>(b);
            var tVec = new Vector<float>(t);
            for (int i = 0; i < aVecs.Length; i++)
                aVecs[i] += (bVecs[i] - aVecs[i]) * tVec;
            int processed = aVecs.Length * Vector<float>.Count;
            for (int i = processed; i < a.Length; i++)
                a[i] += (b[i] - a[i]) * t;
        }
        else
        {
            for (int i = 0; i < a.Length; i++)
                a[i] += (b[i] - a[i]) * t;
        }
    }
}


