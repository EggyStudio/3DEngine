using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    // -- 2D shapes, in pixels from the top left corner. Inside BeginMode3D they are drawn in the
    // camera's world instead, on the plane z = 0.

    /// <summary>Draws a line.</summary>
    public static void DrawLine(int startX, int startY, int endX, int endY, Color color) =>
        DrawList.Line(new Vector3(startX, startY, 0), new Vector3(endX, endY, 0), color);

    /// <summary>Draws a line.</summary>
    public static void DrawLineV(Vector2 start, Vector2 end, Color color) =>
        DrawList.Line(new Vector3(start, 0), new Vector3(end, 0), color);

    /// <summary>Draws a filled triangle.</summary>
    public static void DrawTriangle(Vector2 v1, Vector2 v2, Vector2 v3, Color color) =>
        DrawList.Triangle(new Vector3(v1, 0), new Vector3(v2, 0), new Vector3(v3, 0), color);

    /// <summary>Draws a filled rectangle.</summary>
    public static void DrawRectangle(int x, int y, int width, int height, Color color) =>
        DrawRectangleV(new Vector2(x, y), new Vector2(width, height), color);

    /// <summary>Draws a filled rectangle.</summary>
    public static void DrawRectangleV(Vector2 position, Vector2 size, Color color)
    {
        var (x0, y0, x1, y1) = (position.X, position.Y, position.X + size.X, position.Y + size.Y);
        DrawList.Quad(new(x0, y0, 0), new(x1, y0, 0), new(x1, y1, 0), new(x0, y1, 0), color);
    }

    /// <summary>Draws a rectangle's outline.</summary>
    public static void DrawRectangleLines(int x, int y, int width, int height, Color color)
    {
        // Half a pixel in, so each line covers the pixel row it names rather than straddling two.
        float x0 = x + 0.5f, y0 = y + 0.5f, x1 = x + width - 0.5f, y1 = y + height - 0.5f;
        DrawList.Line(new(x0, y0, 0), new(x1, y0, 0), color);
        DrawList.Line(new(x1, y0, 0), new(x1, y1, 0), color);
        DrawList.Line(new(x1, y1, 0), new(x0, y1, 0), color);
        DrawList.Line(new(x0, y1, 0), new(x0, y0, 0), color);
    }

    /// <summary>Draws a filled circle.</summary>
    public static void DrawCircle(int centerX, int centerY, float radius, Color color) =>
        DrawCircleV(new Vector2(centerX, centerY), radius, color);

    /// <summary>Draws a filled circle.</summary>
    public static void DrawCircleV(Vector2 center, float radius, Color color)
    {
        var segments = CircleSegments(radius);
        var c = new Vector3(center, 0);
        for (int i = 0; i < segments; i++)
            DrawList.Triangle(c, CirclePoint(center, radius, i, segments), CirclePoint(center, radius, i + 1, segments), color);
    }

    /// <summary>Draws a circle's outline.</summary>
    public static void DrawCircleLines(int centerX, int centerY, float radius, Color color)
    {
        var center = new Vector2(centerX, centerY);
        var segments = CircleSegments(radius);
        for (int i = 0; i < segments; i++)
            DrawList.Line(CirclePoint(center, radius, i, segments), CirclePoint(center, radius, i + 1, segments), color);
    }

    /// <summary>Draws a pixel.</summary>
    public static void DrawPixel(int x, int y, Color color) => DrawRectangleV(new Vector2(x, y), Vector2.One, color);

    /// <summary>Draws a pixel.</summary>
    public static void DrawPixelV(Vector2 position, Color color) => DrawRectangleV(position, Vector2.One, color);

    /// <summary>Draws a line <paramref name="thick"/> pixels wide.</summary>
    public static void DrawLineEx(Vector2 start, Vector2 end, float thick, Color color)
    {
        var along = end - start;
        if (along.LengthSquared() < 1e-12f) return;
        var side = Vector2.Normalize(new Vector2(-along.Y, along.X)) * (thick / 2);
        DrawList.Quad(new(start + side, 0), new(end + side, 0), new(end - side, 0), new(start - side, 0), color);
    }

    /// <summary>Draws lines joining each point to the next.</summary>
    public static void DrawLineStrip(ReadOnlySpan<Vector2> points, Color color)
    {
        for (int i = 1; i < points.Length; i++) DrawLineV(points[i - 1], points[i], color);
    }

    /// <summary>Draws a curve from <paramref name="start"/> to <paramref name="end"/>, easing in and out, <paramref name="thick"/> pixels wide.</summary>
    public static void DrawLineBezier(Vector2 start, Vector2 end, float thick, Color color)
    {
        // raylib's ease in and out of a cubic, across x, with y following it.
        const int Segments = 24;
        var previous = start;
        for (int i = 1; i <= Segments; i++)
        {
            var t = (float)i / Segments;
            var eased = t < 0.5f ? 4 * t * t * t : 1 - MathF.Pow(-2 * t + 2, 3) / 2;
            var point = new Vector2(start.X + (end.X - start.X) * t, start.Y + (end.Y - start.Y) * eased);
            DrawLineEx(previous, point, thick, color);
            previous = point;
        }
    }

    /// <summary>Draws a filled rectangle.</summary>
    public static void DrawRectangleRec(Rectangle rec, Color color) =>
        DrawRectangleV(new Vector2(rec.X, rec.Y), new Vector2(rec.Width, rec.Height), color);

    /// <summary>Draws a filled rectangle turned <paramref name="rotation"/> degrees about <paramref name="origin"/>, a point measured from its top left corner, which lands at the rectangle's x and y.</summary>
    public static void DrawRectanglePro(Rectangle rec, Vector2 origin, float rotation, Color color)
    {
        var turn = Matrix3x2.CreateRotation(float.DegreesToRadians(rotation));
        var at = new Vector2(rec.X, rec.Y);
        Vector3 Corner(float x, float y) => new(at + Vector2.Transform(new Vector2(x, y) - origin, turn), 0);
        DrawList.Quad(Corner(0, 0), Corner(rec.Width, 0), Corner(rec.Width, rec.Height), Corner(0, rec.Height), color);
    }

    /// <summary>Draws a filled rectangle blending from <paramref name="top"/> to <paramref name="bottom"/>.</summary>
    public static void DrawRectangleGradientV(int x, int y, int width, int height, Color top, Color bottom) =>
        DrawRectangleGradientEx(new Rectangle(x, y, width, height), top, bottom, bottom, top);

    /// <summary>Draws a filled rectangle blending from <paramref name="left"/> to <paramref name="right"/>.</summary>
    public static void DrawRectangleGradientH(int x, int y, int width, int height, Color left, Color right) =>
        DrawRectangleGradientEx(new Rectangle(x, y, width, height), left, left, right, right);

    /// <summary>Draws a filled rectangle blending between a color at each corner, given counterclockwise from the top left.</summary>
    public static void DrawRectangleGradientEx(Rectangle rec, Color topLeft, Color bottomLeft, Color bottomRight, Color topRight)
    {
        var (x0, y0, x1, y1) = (rec.X, rec.Y, rec.X + rec.Width, rec.Y + rec.Height);
        Vector3 tl = new(x0, y0, 0), bl = new(x0, y1, 0), br = new(x1, y1, 0), tr = new(x1, y0, 0);
        DrawList.Triangle(tl, topLeft, bl, bottomLeft, br, bottomRight);
        DrawList.Triangle(tl, topLeft, br, bottomRight, tr, topRight);
    }

    /// <summary>Draws a rectangle's outline <paramref name="lineThick"/> pixels wide, inside its edge.</summary>
    public static void DrawRectangleLinesEx(Rectangle rec, float lineThick, Color color)
    {
        var t = MathF.Min(lineThick, MathF.Min(rec.Width, rec.Height) / 2);
        DrawRectangleRec(new Rectangle(rec.X, rec.Y, rec.Width, t), color);
        DrawRectangleRec(new Rectangle(rec.X, rec.Y + rec.Height - t, rec.Width, t), color);
        DrawRectangleRec(new Rectangle(rec.X, rec.Y + t, t, rec.Height - 2 * t), color);
        DrawRectangleRec(new Rectangle(rec.X + rec.Width - t, rec.Y + t, t, rec.Height - 2 * t), color);
    }

    /// <summary>
    /// Draws a filled rectangle with rounded corners, <paramref name="roundness"/> from 0 (square)
    /// to 1 (corners as round as the shorter side allows), each corner in <paramref name="segments"/>
    /// pieces, or as many as its size needs at 0.
    /// </summary>
    public static void DrawRectangleRounded(Rectangle rec, float roundness, int segments, Color color)
    {
        var radius = RoundedRadius(rec, roundness);
        if (radius <= 0)
        {
            DrawRectangleRec(rec, color);
            return;
        }
        var outline = RoundedOutline(rec, radius, segments);
        var middle = new Vector3(rec.X + rec.Width / 2, rec.Y + rec.Height / 2, 0);
        for (int i = 0; i < outline.Length; i++)
            DrawList.Triangle(middle, new Vector3(outline[i], 0), new Vector3(outline[(i + 1) % outline.Length], 0), color);
    }

    /// <summary>Draws the outline of a rectangle with rounded corners, as <see cref="DrawRectangleRounded"/> shapes it.</summary>
    public static void DrawRectangleRoundedLines(Rectangle rec, float roundness, int segments, Color color)
    {
        var outline = RoundedOutline(rec, RoundedRadius(rec, roundness), segments);
        for (int i = 0; i < outline.Length; i++)
            DrawList.Line(new Vector3(outline[i], 0), new Vector3(outline[(i + 1) % outline.Length], 0), color);
    }

    private static float RoundedRadius(Rectangle rec, float roundness) =>
        Math.Clamp(roundness, 0, 1) * MathF.Min(rec.Width, rec.Height) / 2;

    // The edge of a rounded rectangle, clockwise from the top left corner's arc.
    private static Vector2[] RoundedOutline(Rectangle rec, float radius, int segments)
    {
        if (radius <= 0) return [new(rec.X, rec.Y), new(rec.X + rec.Width, rec.Y), new(rec.X + rec.Width, rec.Y + rec.Height), new(rec.X, rec.Y + rec.Height)];
        var pieces = segments > 0 ? segments : Math.Max(2, CircleSegments(radius) / 4);
        (Vector2 Center, float From)[] corners =
        [
            (new(rec.X + radius, rec.Y + radius), MathF.PI),
            (new(rec.X + rec.Width - radius, rec.Y + radius), MathF.PI * 1.5f),
            (new(rec.X + rec.Width - radius, rec.Y + rec.Height - radius), 0),
            (new(rec.X + radius, rec.Y + rec.Height - radius), MathF.PI * 0.5f),
        ];
        var outline = new Vector2[4 * (pieces + 1)];
        int n = 0;
        foreach (var (center, from) in corners)
            for (int i = 0; i <= pieces; i++)
            {
                var angle = from + MathF.PI / 2 * i / pieces;
                outline[n++] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
            }
        return outline;
    }

    /// <summary>Draws a triangle's outline.</summary>
    public static void DrawTriangleLines(Vector2 v1, Vector2 v2, Vector2 v3, Color color)
    {
        DrawLineV(v1, v2, color);
        DrawLineV(v2, v3, color);
        DrawLineV(v3, v1, color);
    }

    /// <summary>Draws triangles fanning out from the first point through each pair of the rest.</summary>
    public static void DrawTriangleFan(ReadOnlySpan<Vector2> points, Color color)
    {
        for (int i = 2; i < points.Length; i++) DrawTriangle(points[0], points[i - 1], points[i], color);
    }

    /// <summary>Draws a strip of triangles, each from three points in a row.</summary>
    public static void DrawTriangleStrip(ReadOnlySpan<Vector2> points, Color color)
    {
        for (int i = 2; i < points.Length; i++) DrawTriangle(points[i - 2], points[i - 1], points[i], color);
    }

    /// <summary>Draws a filled regular polygon of <paramref name="sides"/> sides, turned <paramref name="rotation"/> degrees.</summary>
    public static void DrawPoly(Vector2 center, int sides, float radius, float rotation, Color color)
    {
        sides = Math.Max(3, sides);
        var c = new Vector3(center, 0);
        for (int i = 0; i < sides; i++)
            DrawList.Triangle(c, PolyPoint(center, radius, rotation, i, sides), PolyPoint(center, radius, rotation, i + 1, sides), color);
    }

    /// <summary>Draws a regular polygon's outline.</summary>
    public static void DrawPolyLines(Vector2 center, int sides, float radius, float rotation, Color color)
    {
        sides = Math.Max(3, sides);
        for (int i = 0; i < sides; i++)
            DrawList.Line(PolyPoint(center, radius, rotation, i, sides), PolyPoint(center, radius, rotation, i + 1, sides), color);
    }

    /// <summary>Draws a regular polygon's outline <paramref name="lineThick"/> pixels wide, inside its edge.</summary>
    public static void DrawPolyLinesEx(Vector2 center, int sides, float radius, float rotation, float lineThick, Color color)
    {
        sides = Math.Max(3, sides);
        var inner = MathF.Max(0, radius - lineThick);
        for (int i = 0; i < sides; i++)
        {
            Vector3 a = PolyPoint(center, radius, rotation, i, sides), b = PolyPoint(center, radius, rotation, i + 1, sides);
            Vector3 c = PolyPoint(center, inner, rotation, i + 1, sides), d = PolyPoint(center, inner, rotation, i, sides);
            DrawList.Quad(a, b, c, d, color);
        }
    }

    private static Vector3 PolyPoint(Vector2 center, float radius, float rotation, int i, int sides)
    {
        var angle = float.DegreesToRadians(rotation) + MathF.Tau * i / sides;
        return new Vector3(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius, 0);
    }

    /// <summary>Draws a filled slice of a circle from <paramref name="startAngle"/> to <paramref name="endAngle"/> degrees, clockwise on the screen from right.</summary>
    public static void DrawCircleSector(Vector2 center, float radius, float startAngle, float endAngle, int segments, Color color) =>
        DrawRing(center, 0, radius, startAngle, endAngle, segments, color);

    /// <summary>Draws a slice of a circle's outline, with its two radii.</summary>
    public static void DrawCircleSectorLines(Vector2 center, float radius, float startAngle, float endAngle, int segments, Color color)
    {
        var arc = Arc(center, radius, startAngle, endAngle, segments);
        DrawLineV(center, arc[0], color);
        DrawLineStrip(arc, color);
        DrawLineV(arc[^1], center, color);
    }

    /// <summary>Draws a filled circle blending from <paramref name="inner"/> at its middle to <paramref name="outer"/> at its edge.</summary>
    public static void DrawCircleGradient(int centerX, int centerY, float radius, Color inner, Color outer)
    {
        var center = new Vector2(centerX, centerY);
        var segments = CircleSegments(radius);
        var c = new Vector3(center, 0);
        for (int i = 0; i < segments; i++)
            DrawList.Triangle(c, inner, CirclePoint(center, radius, i, segments), outer, CirclePoint(center, radius, i + 1, segments), outer);
    }

    /// <summary>Draws a circle's outline.</summary>
    public static void DrawCircleLinesV(Vector2 center, float radius, Color color) => DrawEllipseLines(center, radius, radius, color);

    /// <summary>Draws a filled ellipse.</summary>
    public static void DrawEllipse(int centerX, int centerY, float radiusH, float radiusV, Color color)
    {
        var center = new Vector2(centerX, centerY);
        var segments = CircleSegments(MathF.Max(radiusH, radiusV));
        var c = new Vector3(center, 0);
        for (int i = 0; i < segments; i++)
            DrawList.Triangle(c, EllipsePoint(center, radiusH, radiusV, i, segments), EllipsePoint(center, radiusH, radiusV, i + 1, segments), color);
    }

    /// <summary>Draws an ellipse's outline.</summary>
    public static void DrawEllipseLines(int centerX, int centerY, float radiusH, float radiusV, Color color) =>
        DrawEllipseLines(new Vector2(centerX, centerY), radiusH, radiusV, color);

    private static void DrawEllipseLines(Vector2 center, float radiusH, float radiusV, Color color)
    {
        var segments = CircleSegments(MathF.Max(radiusH, radiusV));
        for (int i = 0; i < segments; i++)
            DrawList.Line(EllipsePoint(center, radiusH, radiusV, i, segments), EllipsePoint(center, radiusH, radiusV, i + 1, segments), color);
    }

    private static Vector3 EllipsePoint(Vector2 center, float radiusH, float radiusV, int i, int segments)
    {
        var angle = MathF.Tau * i / segments;
        return new Vector3(center.X + MathF.Cos(angle) * radiusH, center.Y + MathF.Sin(angle) * radiusV, 0);
    }

    /// <summary>
    /// Draws a filled ring between <paramref name="innerRadius"/> and <paramref name="outerRadius"/>,
    /// from <paramref name="startAngle"/> to <paramref name="endAngle"/> degrees, clockwise on the
    /// screen from right, in <paramref name="segments"/> pieces, or as many as its size needs at 0.
    /// </summary>
    public static void DrawRing(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, Color color)
    {
        var outer = Arc(center, outerRadius, startAngle, endAngle, segments);
        var inner = Arc(center, innerRadius, startAngle, endAngle, outer.Length - 1);
        for (int i = 1; i < outer.Length; i++)
        {
            if (innerRadius <= 0) DrawList.Triangle(new(center, 0), new(outer[i - 1], 0), new(outer[i], 0), color);
            else DrawList.Quad(new(inner[i - 1], 0), new(outer[i - 1], 0), new(outer[i], 0), new(inner[i], 0), color);
        }
    }

    /// <summary>Draws a ring's outline, its two arcs and, for less than a whole turn, its two ends.</summary>
    public static void DrawRingLines(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, Color color)
    {
        var outer = Arc(center, outerRadius, startAngle, endAngle, segments);
        var inner = Arc(center, innerRadius, startAngle, endAngle, outer.Length - 1);
        DrawLineStrip(outer, color);
        if (innerRadius > 0) DrawLineStrip(inner, color);
        if (MathF.Abs(endAngle - startAngle) < 360)
        {
            DrawLineV(inner[0], outer[0], color);
            DrawLineV(inner[^1], outer[^1], color);
        }
    }

    // The points along an arc, from one angle to the other in degrees, one more than its pieces.
    private static Vector2[] Arc(Vector2 center, float radius, float startAngle, float endAngle, int segments)
    {
        var sweep = endAngle - startAngle;
        if (segments <= 0) segments = Math.Max(1, (int)MathF.Ceiling(CircleSegments(radius) * MathF.Abs(sweep) / 360));
        var points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            var angle = float.DegreesToRadians(startAngle + sweep * i / segments);
            points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
        return points;
    }

    // Enough segments that no edge is longer than about four pixels, within limits.
    private static int CircleSegments(float radius) => Math.Clamp((int)(MathF.Tau * radius / 4f), 12, 128);

    private static Vector3 CirclePoint(Vector2 center, float radius, int i, int segments)
    {
        var angle = MathF.Tau * i / segments;
        return new Vector3(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius, 0);
    }
}
