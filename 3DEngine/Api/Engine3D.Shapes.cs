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
        DrawList.Quad(new(x0, y0, 0), new(x0, y1, 0), new(x1, y1, 0), new(x1, y0, 0), color);
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
        var segments = CirclePieces;
        var c = new Vector3(center, 0);
        for (int i = 0; i < segments; i++)
            DrawList.Triangle(c, CirclePoint(center, radius, i + 1, segments), CirclePoint(center, radius, i, segments), color);
    }

    /// <summary>Draws a circle's outline.</summary>
    public static void DrawCircleLines(int centerX, int centerY, float radius, Color color)
    {
        var center = new Vector2(centerX, centerY);
        var segments = CirclePieces;
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

    /// <summary>Draws a line in dashes <paramref name="dashSize"/> pixels long with gaps of <paramref name="spaceSize"/> between them.</summary>
    public static void DrawLineDashed(Vector2 startPos, Vector2 endPos, int dashSize, int spaceSize, Color color)
    {
        var length = Vector2.Distance(startPos, endPos);
        if (length < 1e-6f || dashSize <= 0) return;
        var direction = (endPos - startPos) / length;
        for (float at = 0; at < length; at += dashSize + Math.Max(0, spaceSize))
            DrawLineV(startPos + direction * at, startPos + direction * MathF.Min(length, at + dashSize), color);
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
        DrawList.Quad(Corner(0, 0), Corner(0, rec.Height), Corner(rec.Width, rec.Height), Corner(rec.Width, 0), color);
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

    /// <summary>Draws a rectangle's outline <paramref name="lineThick"/> pixels wide, inside its edge, or outside it for a negative width.</summary>
    public static void DrawRectangleLinesEx(Rectangle rec, float lineThick, Color color)
    {
        if (lineThick < 0)
        {
            var o = -lineThick;
            DrawRectangleRec(new Rectangle(rec.X - o, rec.Y - o, rec.Width + 2 * o, o), color);
            DrawRectangleRec(new Rectangle(rec.X - o, rec.Y + rec.Height, rec.Width + 2 * o, o), color);
            DrawRectangleRec(new Rectangle(rec.X - o, rec.Y, o, rec.Height), color);
            DrawRectangleRec(new Rectangle(rec.X + rec.Width, rec.Y, o, rec.Height), color);
            return;
        }
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
            DrawList.Triangle(middle, new Vector3(outline[(i + 1) % outline.Length], 0), new Vector3(outline[i], 0), color);
    }

    /// <summary>Draws the outline of a rectangle with rounded corners, as <see cref="DrawRectangleRounded"/> shapes it.</summary>
    public static void DrawRectangleRoundedLines(Rectangle rec, float roundness, int segments, Color color)
    {
        var outline = RoundedOutline(rec, RoundedRadius(rec, roundness), segments);
        for (int i = 0; i < outline.Length; i++)
            DrawList.Line(new Vector3(outline[i], 0), new Vector3(outline[(i + 1) % outline.Length], 0), color);
    }

    /// <summary>
    /// Draws a rounded rectangle's outline <paramref name="lineThick"/> pixels wide, inside its edge,
    /// or outside it for a negative width, as raylib's does.
    /// </summary>
    public static void DrawRectangleRoundedLinesEx(Rectangle rec, float roundness, int segments, float lineThick, Color color)
    {
        if (roundness <= 0)
        {
            DrawRectangleLinesEx(rec, lineThick, color);
            return;
        }
        var radius = RoundedRadius(rec, roundness);
        if (radius <= 0) return;
        // The band between the outer edge, the rectangle's own or one as much larger for a negative
        // width, and the inner one as much smaller, whose corners keep their pieces with a radius
        // of what is left, none past the width, so each piece of the one edge and of the other make
        // a quad and no part of the band is laid on twice.
        var thick = MathF.Abs(lineThick);
        var outer = lineThick < 0 ? new Rectangle(rec.X - thick, rec.Y - thick, rec.Width + 2 * thick, rec.Height + 2 * thick) : rec;
        var outerRadius = lineThick < 0 ? radius + thick : radius;
        var pieces = CornerPieces(lineThick < 0 ? radius : outerRadius, segments);
        var outside = RoundedEdge(outer, outerRadius, pieces);
        var inside = RoundedEdge(new Rectangle(outer.X + thick, outer.Y + thick, outer.Width - 2 * thick, outer.Height - 2 * thick),
            MathF.Max(0, outerRadius - thick), pieces);
        for (int i = 0; i < outside.Length; i++)
        {
            var next = (i + 1) % outside.Length;
            DrawList.Quad(new(inside[next], 0), new(outside[next], 0), new(outside[i], 0), new(inside[i], 0), color);
        }
    }

    private static float RoundedRadius(Rectangle rec, float roundness) =>
        Math.Clamp(roundness, 0, 1) * MathF.Min(rec.Width, rec.Height) / 2;

    // The edge of a rounded rectangle, clockwise from the top left corner's arc.
    private static Vector2[] RoundedOutline(Rectangle rec, float radius, int segments)
    {
        if (radius <= 0) return [new(rec.X, rec.Y), new(rec.X + rec.Width, rec.Y), new(rec.X + rec.Width, rec.Y + rec.Height), new(rec.X, rec.Y + rec.Height)];
        return RoundedEdge(rec, radius, CornerPieces(radius, segments));
    }

    // The same with every corner of so many pieces, which a radius of 0 gathers at the corner.
    private static Vector2[] RoundedEdge(Rectangle rec, float radius, int pieces)
    {
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

    /// <summary>Draws a filled triangle with a color at each corner, blended across it.</summary>
    public static void DrawTriangleGradient(Vector2 v1, Vector2 v2, Vector2 v3, Color c1, Color c2, Color c3) =>
        DrawList.Triangle(new Vector3(v1, 0), c1, new Vector3(v2, 0), c2, new Vector3(v3, 0), c3);

    /// <summary>Draws a triangle's outline <paramref name="thick"/> pixels wide, inside its edges, or outside them for a negative width.</summary>
    public static void DrawTriangleLinesEx(Vector2 v1, Vector2 v2, Vector2 v3, float thick, Color color)
    {
        // The outline lies inside the triangle, its outer edge the triangle's own and its inner one
        // the triangle made smaller about its incenter, which is as far from every side, so the three
        // sides meet without overlapping, as raylib's do, and a color half clear is laid on once. A
        // thickness past the inradius fills the triangle, and a negative one lies outside it.
        float e1 = Vector2.Distance(v2, v3), e2 = Vector2.Distance(v3, v1), e3 = Vector2.Distance(v1, v2);
        var perimeter = e1 + e2 + e3;
        if (perimeter <= 0) return;
        var half = perimeter / 2;
        var incenter = (e1 * v1 + e2 * v2 + e3 * v3) / perimeter;
        var inradius = MathF.Sqrt(MathF.Max(0, (half - e1) * (half - e2) * (half - e3) / half));
        var scale = inradius > 0 ? 1 - thick / inradius : 0;
        if (scale <= 0)
        {
            DrawTriangle(v1, v2, v3, color);
            return;
        }
        Vector2 v4 = incenter + (v1 - incenter) * scale, v5 = incenter + (v2 - incenter) * scale, v6 = incenter + (v3 - incenter) * scale;
        if (thick < 0) (v1, v4, v2, v5, v3, v6) = (v4, v1, v5, v2, v6, v3);
        DrawList.Quad(new(v1, 0), new(v2, 0), new(v5, 0), new(v4, 0), color);
        DrawList.Quad(new(v2, 0), new(v3, 0), new(v6, 0), new(v5, 0), color);
        DrawList.Quad(new(v3, 0), new(v1, 0), new(v4, 0), new(v6, 0), color);
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

    /// <summary>
    /// Draws a strip of triangles, each from three points in a row, every other one taken in the
    /// opposite order so that all face the way the first does, as raylib's are.
    /// </summary>
    public static void DrawTriangleStrip(ReadOnlySpan<Vector2> points, Color color)
    {
        for (int i = 2; i < points.Length; i++)
        {
            if (i % 2 == 0) DrawTriangle(points[i - 2], points[i - 1], points[i], color);
            else DrawTriangle(points[i - 1], points[i - 2], points[i], color);
        }
    }

    /// <summary>Draws a filled regular polygon of <paramref name="sides"/> sides, turned <paramref name="rotation"/> degrees.</summary>
    public static void DrawPoly(Vector2 center, int sides, float radius, float rotation, Color color)
    {
        sides = Math.Max(3, sides);
        var c = new Vector3(center, 0);
        for (int i = 0; i < sides; i++)
            DrawList.Triangle(c, PolyPoint(center, radius, rotation, i + 1, sides), PolyPoint(center, radius, rotation, i, sides), color);
    }

    /// <summary>Draws a regular polygon's outline.</summary>
    public static void DrawPolyLines(Vector2 center, int sides, float radius, float rotation, Color color)
    {
        sides = Math.Max(3, sides);
        for (int i = 0; i < sides; i++)
            DrawList.Line(PolyPoint(center, radius, rotation, i, sides), PolyPoint(center, radius, rotation, i + 1, sides), color);
    }

    /// <summary>
    /// Draws a regular polygon's outline <paramref name="lineThick"/> pixels wide across each side,
    /// inside its edge, or outside it for a negative width.
    /// </summary>
    public static void DrawPolyLinesEx(Vector2 center, int sides, float radius, float rotation, float lineThick, Color color)
    {
        sides = Math.Max(3, sides);
        // Measured across a side rather than at a corner, the corners lying further from the middle
        // than the sides in the ratio of the radius to the distance from the middle to a side.
        var across = lineThick / MathF.Cos(MathF.PI / sides);
        float outer = radius, inner = MathF.Max(0, radius - across);
        if (lineThick < 0) (outer, inner) = (radius - across, radius);
        for (int i = 0; i < sides; i++)
        {
            Vector3 a = PolyPoint(center, outer, rotation, i, sides), b = PolyPoint(center, outer, rotation, i + 1, sides);
            Vector3 c = PolyPoint(center, inner, rotation, i + 1, sides), d = PolyPoint(center, inner, rotation, i, sides);
            DrawList.Quad(d, c, b, a, color);
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

    /// <summary>
    /// Draws a slice of a circle's outline <paramref name="thick"/> pixels wide, with its two radii,
    /// inside its edge, or outside it for a negative width.
    /// </summary>
    /// <remarks>
    /// The pieces meet without overlapping, as raylib's do, so a color that is partly clear shows
    /// one shade throughout. The arc is a band as wide as asked across each straight piece of the
    /// polygon it is drawn as, each radius is a strip along it that ends at the band, and the two
    /// strips meet on the line halfway between the radii. A width that leaves no hole draws the
    /// slice filled, and a whole turn keeps the radius it starts at only when drawn inside.
    /// </remarks>
    public static void DrawCircleSectorLinesEx(Vector2 center, float radius, float startAngle, float endAngle, int segments, float thick, Color color)
    {
        if (startAngle == endAngle || radius <= 0 || thick == 0) return;
        if (endAngle < startAngle) (startAngle, endAngle) = (endAngle, startAngle);
        var radii = true;
        if (endAngle - startAngle >= 360)
        {
            radii = thick > 0;
            endAngle = startAngle + 360;
        }
        var pieces = Arc(center, radius, startAngle, endAngle, segments).Length - 1;
        var across = BandWidth(thick, (endAngle - startAngle) / pieces);
        float near = radius - across, far = radius;
        if (thick < 0) (near, far) = (far, near);
        if (thick >= near)
        {
            DrawCircleSector(center, radius, startAngle, endAngle, pieces, color);
            return;
        }
        DrawRing(center, near, far, startAngle, endAngle, pieces, color);
        if (!radii) return;

        Vector2 first = Direction(startAngle), last = Direction(endAngle);
        // Toward the inside of the slice from each radius.
        Vector2 intoFirst = new(-first.Y, first.X), intoLast = new(last.Y, -last.X);
        var (firstSide, lastSide) = Halves(center, startAngle, endAngle);
        if (thick > 0)
        {
            // Each strip covers the part of the polygon inside the band that lies within the width
            // of its radius, on its half of the slice.
            var inner = Arc(center, near, startAngle, endAngle, pieces);
            for (int i = 0; i < pieces; i++)
            {
                ReadOnlySpan<Vector2> piece = [center, inner[i + 1], inner[i]];
                FillClipped(piece, [Keep(center + intoFirst * thick, intoFirst), firstSide], color);
                FillClipped(piece, [Keep(center + intoLast * thick, intoLast), lastSide], color);
            }
            return;
        }
        // Outside, each strip runs back past the middle to the line halfway between the radii,
        // which it meets less far behind than the width over the sine of half the slice.
        var behind = -thick / MathF.Max(MathF.Sin(float.DegreesToRadians(endAngle - startAngle) / 2), 1e-3f);
        var outer = Arc(center, far, startAngle, endAngle, pieces);
        FillEnd(center, center - first * behind, outer[0], outer[1], intoFirst, thick, firstSide, color);
        FillEnd(center, center - last * behind, outer[^1], outer[^2], intoLast, thick, lastSide, color);
    }

    /// <summary>Draws a circle's outline <paramref name="thick"/> pixels wide, inside its edge, or outside it for a negative width.</summary>
    public static void DrawCircleLinesEx(Vector2 center, float radius, float thick, Color color) =>
        DrawRing(center, radius - thick, radius, 0, 360, CirclePieces, color);

    /// <summary>Draws a filled circle blending from <paramref name="inner"/> at its middle to <paramref name="outer"/> at its edge.</summary>
    public static void DrawCircleGradient(Vector2 center, float radius, Color inner, Color outer)
    {
        var segments = CirclePieces;
        var c = new Vector3(center, 0);
        for (int i = 0; i < segments; i++)
            DrawList.Triangle(c, inner, CirclePoint(center, radius, i + 1, segments), outer, CirclePoint(center, radius, i, segments), outer);
    }

    /// <summary>Draws a circle's outline.</summary>
    public static void DrawCircleLinesV(Vector2 center, float radius, Color color) => DrawEllipseLines(center, radius, radius, color);

    /// <summary>Draws a filled ellipse.</summary>
    public static void DrawEllipse(int centerX, int centerY, float radiusH, float radiusV, Color color)
    {
        var center = new Vector2(centerX, centerY);
        var segments = CirclePieces;
        var c = new Vector3(center, 0);
        for (int i = 0; i < segments; i++)
            DrawList.Triangle(c, EllipsePoint(center, radiusH, radiusV, i + 1, segments), EllipsePoint(center, radiusH, radiusV, i, segments), color);
    }

    /// <summary>Draws an ellipse's outline.</summary>
    public static void DrawEllipseLines(int centerX, int centerY, float radiusH, float radiusV, Color color) =>
        DrawEllipseLines(new Vector2(centerX, centerY), radiusH, radiusV, color);

    /// <summary>Draws a filled ellipse around a point.</summary>
    public static void DrawEllipseV(Vector2 center, float radiusH, float radiusV, Color color)
    {
        var segments = CirclePieces;
        var c = new Vector3(center, 0);
        for (int i = 0; i < segments; i++)
            DrawList.Triangle(c, EllipsePoint(center, radiusH, radiusV, i + 1, segments), EllipsePoint(center, radiusH, radiusV, i, segments), color);
    }

    /// <summary>Draws an ellipse's outline around a point.</summary>
    public static void DrawEllipseLinesV(Vector2 center, float radiusH, float radiusV, Color color) =>
        DrawEllipseLines(center, radiusH, radiusV, color);

    /// <summary>
    /// Draws an ellipse's outline <paramref name="thick"/> pixels wide, inside its edge, or outside
    /// it for a negative width, filled where the width leaves no hole.
    /// </summary>
    public static void DrawEllipseLinesEx(Vector2 center, float radiusH, float radiusV, float thick, Color color)
    {
        var segments = CirclePieces;
        float innerH = radiusH - thick, innerV = radiusV - thick;
        if (thick >= 0 && (innerH <= 0 || innerV <= 0))
        {
            DrawEllipseV(center, radiusH, radiusV, color);
            return;
        }
        if (thick < 0) (innerH, radiusH, innerV, radiusV) = (radiusH, innerH, radiusV, innerV);
        for (int i = 0; i < segments; i++)
            DrawList.Quad(EllipsePoint(center, innerH, innerV, i + 1, segments), EllipsePoint(center, radiusH, radiusV, i + 1, segments),
                EllipsePoint(center, radiusH, radiusV, i, segments), EllipsePoint(center, innerH, innerV, i, segments), color);
    }

    private static void DrawEllipseLines(Vector2 center, float radiusH, float radiusV, Color color)
    {
        var segments = CirclePieces;
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
    /// screen from right, in <paramref name="segments"/> pieces, or where that is fewer than one a
    /// quarter turn, in as many as keep it within half a pixel of the circle, as raylib draws it.
    /// </summary>
    public static void DrawRing(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, Color color)
    {
        if (startAngle == endAngle) return;
        if (outerRadius < innerRadius) (innerRadius, outerRadius) = (outerRadius, innerRadius);
        if (outerRadius <= 0) return;
        // Swept from the smaller angle, as raylib's is, so each piece runs counterclockwise on the
        // screen whichever way the angles were given, and no more than once around.
        if (endAngle < startAngle) (startAngle, endAngle) = (endAngle, startAngle);
        endAngle = MathF.Min(endAngle, startAngle + 360);
        var outer = Arc(center, outerRadius, startAngle, endAngle, segments);
        var inner = Arc(center, innerRadius, startAngle, endAngle, outer.Length - 1);
        for (int i = 1; i < outer.Length; i++)
        {
            if (innerRadius <= 0) DrawList.Triangle(new(center, 0), new(outer[i], 0), new(outer[i - 1], 0), color);
            else DrawList.Quad(new(inner[i], 0), new(outer[i], 0), new(outer[i - 1], 0), new(inner[i - 1], 0), color);
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

    /// <summary>
    /// Draws a ring's outline <paramref name="thick"/> pixels wide, with its two ends, inside its
    /// edges, or outside them for a negative width.
    /// </summary>
    /// <remarks>
    /// The pieces meet without overlapping, as raylib's do, so a color that is partly clear shows
    /// one shade throughout. Each arc is a band as wide as asked across each straight piece of its
    /// polygon. Inside, each end fills the ring between the bands as far along them as the width,
    /// measured along each, and the ends meet halfway when they would cross. Outside, each end is a
    /// strip beyond its edge as long as the ring is wide with both bands. A width that leaves no
    /// hole draws the ring filled, a ring with no hole is drawn as a slice's outline, and a whole
    /// turn keeps the end it starts at only when drawn inside.
    /// </remarks>
    public static void DrawRingLinesEx(Vector2 center, float innerRadius, float outerRadius, float startAngle, float endAngle, int segments, float thick, Color color)
    {
        if (startAngle == endAngle || thick == 0) return;
        if (outerRadius < innerRadius) (innerRadius, outerRadius) = (outerRadius, innerRadius);
        if (outerRadius <= 0) return;
        if (endAngle < startAngle) (startAngle, endAngle) = (endAngle, startAngle);
        var ends = true;
        if (endAngle - startAngle >= 360)
        {
            ends = thick > 0;
            endAngle = startAngle + 360;
        }
        var pieces = Arc(center, outerRadius, startAngle, endAngle, segments).Length - 1;
        var across = BandWidth(thick, (endAngle - startAngle) / pieces);
        Vector2 first = Direction(startAngle), last = Direction(endAngle);
        Vector2 intoFirst = new(-first.Y, first.X), intoLast = new(last.Y, -last.X);
        var (firstSide, lastSide) = Halves(center, startAngle, endAngle);

        if (thick < 0)
        {
            if (innerRadius <= 0)
            {
                DrawCircleSectorLinesEx(center, outerRadius, startAngle, endAngle, pieces, thick, color);
                return;
            }
            float lowest = MathF.Max(0, innerRadius + across), highest = outerRadius - across;
            DrawRing(center, lowest, innerRadius, startAngle, endAngle, pieces, color);
            DrawRing(center, outerRadius, highest, startAngle, endAngle, pieces, color);
            if (!ends) return;
            var outer = Arc(center, highest, startAngle, endAngle, pieces);
            FillEnd(center, center + first * lowest, outer[0], outer[1], intoFirst, thick, firstSide, color);
            FillEnd(center, center + last * lowest, outer[^1], outer[^2], intoLast, thick, lastSide, color);
            return;
        }

        innerRadius = MathF.Max(0, innerRadius);
        if (across > (outerRadius - innerRadius) / 2)
        {
            DrawRing(center, innerRadius, outerRadius, startAngle, endAngle, pieces, color);
            return;
        }
        float lower = innerRadius + across, upper = outerRadius - across;
        var span = endAngle - startAngle;
        float alongLower = float.RadiansToDegrees(thick / lower), alongUpper = float.RadiansToDegrees(thick / upper);
        if (ends && span < alongUpper * 2)
        {
            DrawRing(center, innerRadius, outerRadius, startAngle, endAngle, pieces, color);
            return;
        }
        DrawRing(center, innerRadius, lower, startAngle, endAngle, pieces, color);
        DrawRing(center, upper, outerRadius, startAngle, endAngle, pieces, color);
        if (!ends) return;
        var low = Arc(center, lower, startAngle, endAngle, pieces);
        var high = Arc(center, upper, startAngle, endAngle, pieces);
        // Each end stops at the line from its reach along the lower band to its reach along the
        // upper, kept on the side of its own edge.
        var middle = (lower + upper) / 2;
        var firstEnd = Beyond(Crossing(center, low, startAngle, span, alongLower), Crossing(center, high, startAngle, span, alongUpper), center + first * middle);
        var lastEnd = Beyond(Crossing(center, low, startAngle, span, span - alongLower), Crossing(center, high, startAngle, span, span - alongUpper), center + last * middle);
        for (int i = 0; i < pieces; i++)
        {
            ReadOnlySpan<Vector2> piece = [low[i + 1], high[i + 1], high[i], low[i]];
            FillClipped(piece, [firstEnd, firstSide], color);
            FillClipped(piece, [lastEnd, lastSide], color);
        }
    }

    // The width a band of a polygon is drawn at, so that it is as wide as asked across each of the
    // polygon's straight pieces rather than at its corners.
    private static float BandWidth(float thick, float step) => thick / MathF.Cos(float.DegreesToRadians(step / 2));

    private static Vector2 Direction(float degrees)
    {
        var angle = float.DegreesToRadians(degrees);
        return new Vector2(MathF.Cos(angle), MathF.Sin(angle));
    }

    // The side of the line through a point, across a direction, that the direction points away from.
    private static (Vector2 Normal, float Most) Keep(Vector2 point, Vector2 normal) => (normal, Vector2.Dot(point, normal));

    // The side of the line through two points that holds the third.
    private static (Vector2 Normal, float Most) Beyond(Vector2 a, Vector2 b, Vector2 kept)
    {
        var normal = new Vector2(b.Y - a.Y, a.X - b.X);
        if (Vector2.Dot(kept - a, normal) > 0) normal = -normal;
        return Keep(a, normal);
    }

    // The halves of the plane either side of the line halfway between a slice's two edges, the
    // first edge's and the last's, on which each edge's piece of an outline is kept.
    private static ((Vector2, float) First, (Vector2, float) Last) Halves(Vector2 center, float startAngle, float endAngle)
    {
        var middle = Direction((startAngle + endAngle) / 2);
        var towardLast = new Vector2(-middle.Y, middle.X);
        return (Keep(center, towardLast), Keep(center, -towardLast));
    }

    // Where the ray from the middle at an angle, in degrees past the first of an arc's points,
    // crosses the polygon the points make.
    private static Vector2 Crossing(Vector2 center, Vector2[] arc, float startAngle, float span, float angle)
    {
        var at = Math.Clamp((int)(angle / (span / (arc.Length - 1))), 0, arc.Length - 2);
        Vector2 a = arc[at], b = arc[at + 1], ray = Direction(startAngle + angle);
        var t = Cross(ray, center - a) / Cross(ray, b - a);
        return a + (b - a) * t;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    // An outline's end drawn outside its edge: a strip along the edge from a point on it out to the
    // line of the arc's last piece, the edge's corner on the arc and the point before it on the arc
    // giving that line, kept on the edge's half of the slice.
    private static void FillEnd(Vector2 center, Vector2 from, Vector2 corner, Vector2 before, Vector2 inward, float thick,
        (Vector2, float) side, Color color)
    {
        var depth = Vector2.Dot(before - center, inward);
        var reach = corner + (corner - before) * (-thick / depth);
        ReadOnlySpan<Vector2> strip = [from, corner, reach, reach - (corner - from)];
        FillClipped(strip, [side], color);
    }

    // The part of a convex polygon on the kept side of each line, as a fan of triangles wound
    // counterclockwise on the screen, as raylib winds its shapes.
    private static void FillClipped(ReadOnlySpan<Vector2> polygon, ReadOnlySpan<(Vector2 Normal, float Most)> keep, Color color)
    {
        Span<Vector2> points = stackalloc Vector2[polygon.Length + keep.Length];
        Span<Vector2> next = stackalloc Vector2[polygon.Length + keep.Length];
        polygon.CopyTo(points);
        var count = polygon.Length;
        foreach (var (normal, most) in keep)
        {
            var kept = 0;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = points[i], q = points[(i + 1) % count];
                float dp = Vector2.Dot(p, normal) - most, dq = Vector2.Dot(q, normal) - most;
                if (dp <= 0) next[kept++] = p;
                if ((dp <= 0) != (dq <= 0)) next[kept++] = p + (q - p) * (dp / (dp - dq));
            }
            count = kept;
            next[..count].CopyTo(points);
            if (count < 3) return;
        }
        var area = 0f;
        for (int i = 0; i < count; i++) area += Cross(points[i], points[(i + 1) % count]);
        if (MathF.Abs(area) < 1e-6f) return;
        for (int i = 1; i + 1 < count; i++)
        {
            Vector3 a = new(points[0], 0), b = new(points[i], 0), c = new(points[i + 1], 0);
            if (area < 0) DrawList.Triangle(a, b, c, color);
            else DrawList.Triangle(a, c, b, color);
        }
    }

    // The points along an arc, from one angle to the other in degrees, one more than its pieces.
    private static Vector2[] Arc(Vector2 center, float radius, float startAngle, float endAngle, int segments)
    {
        var sweep = endAngle - startAngle;
        segments = ArcPieces(radius, sweep, segments);
        var points = new Vector2[segments + 1];
        for (int i = 0; i <= segments; i++)
        {
            var angle = float.DegreesToRadians(startAngle + sweep * i / segments);
            points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
        return points;
    }

    // The pieces raylib draws every whole circle and ellipse in, one each ten degrees, whatever its size.
    private const int CirclePieces = 36;

    // The pieces of an arc: as many as asked, or where that is fewer than one a quarter turn, as many
    // as keep the arc within half a pixel of the circle, as raylib picks them.
    private static int ArcPieces(float radius, float sweep, int segments)
    {
        var least = (int)MathF.Ceiling(MathF.Abs(sweep) / 90);
        if (segments >= least) return Math.Max(segments, 1);
        var smooth = SmoothPieces(radius, MathF.Abs(sweep));
        return smooth > 0 ? smooth : Math.Max(least, 1);
    }

    // The pieces of each corner of a rounded rectangle: as many as asked, or at none, as many as keep
    // the corner within half a pixel of the circle, as raylib picks them.
    private static int CornerPieces(float radius, int segments)
    {
        if (segments >= 1) return segments;
        var smooth = SmoothPieces(radius, 90);
        return smooth > 0 ? smooth : 4;
    }

    // raylib's count of the pieces that keep an arc of so many degrees within half a pixel of its
    // circle, or 0 where the radius is too small to give one.
    private static int SmoothPieces(float radius, float sweep)
    {
        var step = MathF.Acos(2 * MathF.Pow(1 - 0.5f / radius, 2) - 1);
        var pieces = MathF.Ceiling(sweep * (MathF.Tau / step) / 360);
        return pieces > 0 && pieces < int.MaxValue ? (int)pieces : 0;
    }

    private static Vector3 CirclePoint(Vector2 center, float radius, int i, int segments)
    {
        var angle = MathF.Tau * i / segments;
        return new Vector3(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius, 0);
    }
}
