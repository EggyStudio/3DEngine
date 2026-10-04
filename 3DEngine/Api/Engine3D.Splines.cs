using System.Buffers;
using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    // -- Splines, as raylib's: each curve segment cut into 24 pieces, and the whole drawn as one
    // strip thick pixels wide whose joints are mitered, so a thick curve has no gaps or overlaps.

    private const int SplineSegmentDivisions = 24;

    /// <summary>Draws lines joining each point to the next, <paramref name="thick"/> pixels wide, with their corners joined.</summary>
    public static void DrawSplineLinear(ReadOnlySpan<Vector2> points, float thick, Color color) =>
        DrawThickPath(points, thick, color);

    /// <summary>
    /// Draws a uniform B-spline through the control <paramref name="points"/>, at least four, which
    /// passes near rather than through them, <paramref name="thick"/> pixels wide.
    /// </summary>
    public static void DrawSplineBasis(ReadOnlySpan<Vector2> points, float thick, Color color) =>
        DrawSampled(points, 4, 1, thick, color, static (p, i, t) => GetSplinePointBasis(p[i], p[i + 1], p[i + 2], p[i + 3], t));

    /// <summary>
    /// Draws a Catmull-Rom spline through <paramref name="points"/>, at least four, which passes
    /// through every point but the first and last, <paramref name="thick"/> pixels wide.
    /// </summary>
    public static void DrawSplineCatmullRom(ReadOnlySpan<Vector2> points, float thick, Color color) =>
        DrawSampled(points, 4, 1, thick, color, static (p, i, t) => GetSplinePointCatmullRom(p[i], p[i + 1], p[i + 2], p[i + 3], t));

    /// <summary>
    /// Draws quadratic Bezier curves joined end to end, <paramref name="points"/> given as start,
    /// control, end, control, end and so on, at least three, <paramref name="thick"/> pixels wide.
    /// </summary>
    public static void DrawSplineBezierQuadratic(ReadOnlySpan<Vector2> points, float thick, Color color) =>
        DrawSampled(points, 3, 2, thick, color, static (p, i, t) => GetSplinePointBezierQuad(p[i], p[i + 1], p[i + 2], t));

    /// <summary>
    /// Draws cubic Bezier curves joined end to end, <paramref name="points"/> given as start, two
    /// controls, end, two controls, end and so on, at least four, <paramref name="thick"/> pixels wide.
    /// </summary>
    public static void DrawSplineBezierCubic(ReadOnlySpan<Vector2> points, float thick, Color color) =>
        DrawSampled(points, 4, 3, thick, color, static (p, i, t) => GetSplinePointBezierCubic(p[i], p[i + 1], p[i + 2], p[i + 3], t));

    /// <summary>Draws one line of a linear spline, <paramref name="thick"/> pixels wide.</summary>
    public static void DrawSplineSegmentLinear(Vector2 p1, Vector2 p2, float thick, Color color) => DrawSplineLinear([p1, p2], thick, color);

    /// <summary>Draws one segment of a B-spline, from its four control points.</summary>
    public static void DrawSplineSegmentBasis(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float thick, Color color) =>
        DrawSplineBasis([p1, p2, p3, p4], thick, color);

    /// <summary>Draws one segment of a Catmull-Rom spline, from <paramref name="p2"/> to <paramref name="p3"/>.</summary>
    public static void DrawSplineSegmentCatmullRom(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float thick, Color color) =>
        DrawSplineCatmullRom([p1, p2, p3, p4], thick, color);

    /// <summary>Draws one quadratic Bezier curve from <paramref name="p1"/> to <paramref name="p3"/>, pulled toward <paramref name="c2"/>.</summary>
    public static void DrawSplineSegmentBezierQuadratic(Vector2 p1, Vector2 c2, Vector2 p3, float thick, Color color) =>
        DrawSplineBezierQuadratic([p1, c2, p3], thick, color);

    /// <summary>Draws one cubic Bezier curve from <paramref name="p1"/> to <paramref name="p4"/>, pulled toward <paramref name="c2"/> and <paramref name="c3"/>.</summary>
    public static void DrawSplineSegmentBezierCubic(Vector2 p1, Vector2 c2, Vector2 c3, Vector2 p4, float thick, Color color) =>
        DrawSplineBezierCubic([p1, c2, c3, p4], thick, color);

    /// <summary>The point <paramref name="t"/> of the way from <paramref name="startPos"/> to <paramref name="endPos"/>, from 0 to 1.</summary>
    public static Vector2 GetSplinePointLinear(Vector2 startPos, Vector2 endPos, float t) => startPos * (1 - t) + endPos * t;

    /// <summary>The point at <paramref name="t"/>, from 0 to 1, along the B-spline segment of four control points.</summary>
    public static Vector2 GetSplinePointBasis(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float t)
    {
        var a3 = (-p1 + 3 * p2 - 3 * p3 + p4) / 6;
        var a2 = (3 * p1 - 6 * p2 + 3 * p3) / 6;
        var a1 = (-3 * p1 + 3 * p3) / 6;
        var a0 = (p1 + 4 * p2 + p3) / 6;
        return ((a3 * t + a2) * t + a1) * t + a0;
    }

    /// <summary>The point at <paramref name="t"/>, from 0 to 1, along the Catmull-Rom segment from <paramref name="p2"/> to <paramref name="p3"/>.</summary>
    public static Vector2 GetSplinePointCatmullRom(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;
        return 0.5f * (2 * p2 + (-p1 + p3) * t + (2 * p1 - 5 * p2 + 4 * p3 - p4) * t2 + (-p1 + 3 * p2 - 3 * p3 + p4) * t3);
    }

    /// <summary>The point at <paramref name="t"/>, from 0 to 1, along the quadratic Bezier curve.</summary>
    public static Vector2 GetSplinePointBezierQuad(Vector2 p1, Vector2 c2, Vector2 p3, float t)
    {
        var u = 1 - t;
        return u * u * p1 + 2 * u * t * c2 + t * t * p3;
    }

    /// <summary>The point at <paramref name="t"/>, from 0 to 1, along the cubic Bezier curve.</summary>
    public static Vector2 GetSplinePointBezierCubic(Vector2 p1, Vector2 c2, Vector2 c3, Vector2 p4, float t)
    {
        var u = 1 - t;
        return u * u * u * p1 + 3 * u * u * t * c2 + 3 * u * t * t * c3 + t * t * t * p4;
    }

    private delegate Vector2 SplinePoint(ReadOnlySpan<Vector2> points, int first, float t);

    // Samples each segment of a spline, a segment starting every step points and taking size of
    // them, and draws the samples as one path.
    private static void DrawSampled(ReadOnlySpan<Vector2> points, int size, int step, float thick, Color color, SplinePoint point)
    {
        if (points.Length < size) return;
        var segments = (points.Length - size) / step + 1;
        var count = segments * SplineSegmentDivisions + 1;
        var samples = ArrayPool<Vector2>.Shared.Rent(count);
        try
        {
            var n = 0;
            for (int s = 0; s < segments; s++)
                for (int d = s == 0 ? 0 : 1; d <= SplineSegmentDivisions; d++)
                    samples[n++] = point(points, s * step, (float)d / SplineSegmentDivisions);
            DrawThickPath(samples.AsSpan(0, n), thick, color);
        }
        finally
        {
            ArrayPool<Vector2>.Shared.Return(samples);
        }
    }

    // A path thick pixels wide as quads between its points, each point's edges set where the two
    // sides it joins meet, so corners close. A sharp corner's miter reaches a whole width from its
    // point at most, where it would otherwise reach far past the corner.
    private static void DrawThickPath(ReadOnlySpan<Vector2> points, float thick, Color color)
    {
        if (points.Length < 2) return;
        var half = thick / 2;
        var lastEdge = PathEdge(points, 0, half);
        for (int i = 1; i < points.Length; i++)
        {
            var edge = PathEdge(points, i, half);
            DrawList.Quad(new(points[i - 1] + lastEdge, 0), new(points[i] + edge, 0), new(points[i] - edge, 0), new(points[i - 1] - lastEdge, 0), color);
            lastEdge = edge;
        }
    }

    // Where point i's edges sit, half a width to either side, along the miter of the sides it joins.
    private static Vector2 PathEdge(ReadOnlySpan<Vector2> points, int i, float half)
    {
        static Vector2 Normal(Vector2 from, Vector2 to)
        {
            var d = to - from;
            return d.LengthSquared() < 1e-12f ? Vector2.Zero : Vector2.Normalize(new Vector2(-d.Y, d.X));
        }
        var before = i > 0 ? Normal(points[i - 1], points[i]) : Vector2.Zero;
        var after = i < points.Length - 1 ? Normal(points[i], points[i + 1]) : Vector2.Zero;
        if (before == Vector2.Zero) return after * half;
        if (after == Vector2.Zero) return before * half;
        var miter = before + after;
        if (miter.LengthSquared() < 1e-6f) return after * half;
        miter = Vector2.Normalize(miter);
        return miter * (half / MathF.Max(Vector2.Dot(miter, after), 0.5f));
    }
}
