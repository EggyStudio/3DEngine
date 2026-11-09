using System.Numerics;
using System.Runtime.InteropServices;
using StbImageSharp;

namespace Engine;

/// <summary>An axis-aligned box, by its two opposite corners.</summary>
public readonly record struct BoundingBox(Vector3 Min, Vector3 Max)
{
    /// <summary>The box around every point in <paramref name="points"/>.</summary>
    public static BoundingBox Around(ReadOnlySpan<Vector3> points)
    {
        if (points.IsEmpty) return default;
        var (min, max) = (points[0], points[0]);
        foreach (var p in points)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        return new BoundingBox(min, max);
    }
}
