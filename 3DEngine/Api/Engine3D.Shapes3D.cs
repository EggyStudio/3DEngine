using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    // -- 3D shapes, drawn through the camera BeginMode3D set.

    /// <summary>Draws a line in 3D.</summary>
    public static void DrawLine3D(Vector3 start, Vector3 end, Color color) => DrawList.Line(start, end, color);

    /// <summary>Draws a filled triangle in 3D.</summary>
    public static void DrawTriangle3D(Vector3 v1, Vector3 v2, Vector3 v3, Color color) => DrawList.Triangle(v1, v2, v3, color);

    /// <summary>Draws a box centered on <paramref name="position"/>.</summary>
    public static void DrawCube(Vector3 position, float width, float height, float length, Color color) =>
        DrawCubeV(position, new Vector3(width, height, length), color);

    /// <summary>Draws a box centered on <paramref name="position"/>.</summary>
    public static void DrawCubeV(Vector3 position, Vector3 size, Color color)
    {
        Span<Vector3> c = stackalloc Vector3[8];
        CubeCorners(position, size, c);
        DrawList.Quad(c[0], c[1], c[2], c[3], color); // -Z
        DrawList.Quad(c[4], c[5], c[6], c[7], color); // +Z
        DrawList.Quad(c[0], c[1], c[5], c[4], color); // -Y
        DrawList.Quad(c[3], c[2], c[6], c[7], color); // +Y
        DrawList.Quad(c[0], c[3], c[7], c[4], color); // -X
        DrawList.Quad(c[1], c[2], c[6], c[5], color); // +X
    }

    /// <summary>Draws a box's edges.</summary>
    public static void DrawCubeWires(Vector3 position, float width, float height, float length, Color color) =>
        DrawCubeWiresV(position, new Vector3(width, height, length), color);

    /// <summary>Draws a box's edges.</summary>
    public static void DrawCubeWiresV(Vector3 position, Vector3 size, Color color)
    {
        Span<Vector3> c = stackalloc Vector3[8];
        CubeCorners(position, size, c);
        for (int i = 0; i < 4; i++)
        {
            DrawList.Line(c[i], c[(i + 1) % 4], color);
            DrawList.Line(c[i + 4], c[(i + 1) % 4 + 4], color);
            DrawList.Line(c[i], c[i + 4], color);
        }
    }

    /// <summary>Draws a sphere of 16 rings and 16 slices.</summary>
    public static void DrawSphere(Vector3 center, float radius, Color color) => DrawSphereEx(center, radius, 16, 16, color);

    /// <summary>Draws a sphere with the given number of rings and slices.</summary>
    public static void DrawSphereEx(Vector3 center, float radius, int rings, int slices, Color color)
    {
        rings = Math.Max(2, rings);
        slices = Math.Max(3, slices);
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < slices; s++)
        {
            var a = SpherePoint(center, radius, r, s, rings, slices);
            var b = SpherePoint(center, radius, r + 1, s, rings, slices);
            var c = SpherePoint(center, radius, r + 1, s + 1, rings, slices);
            var d = SpherePoint(center, radius, r, s + 1, rings, slices);
            DrawList.Quad(a, b, c, d, color);
        }
    }

    /// <summary>Draws a sphere's rings and slices as lines, with a diagonal across each face between them, as raylib's does.</summary>
    public static void DrawSphereWires(Vector3 center, float radius, int rings, int slices, Color color)
    {
        rings = Math.Max(2, rings);
        slices = Math.Max(3, slices);
        for (int r = 0; r < rings; r++)
        for (int s = 0; s < slices; s++)
        {
            var a = SpherePoint(center, radius, r, s, rings, slices);
            DrawList.Line(a, SpherePoint(center, radius, r + 1, s, rings, slices), color);
            DrawList.Line(a, SpherePoint(center, radius, r, s + 1, rings, slices), color);
            DrawList.Line(a, SpherePoint(center, radius, r + 1, s + 1, rings, slices), color);
        }
    }

    /// <summary>Draws a point, as a short cross three lines wide, since a line of no length draws nothing.</summary>
    public static void DrawPoint3D(Vector3 position, Color color)
    {
        const float Half = 0.01f;
        DrawList.Line(position - Vector3.UnitX * Half, position + Vector3.UnitX * Half, color);
        DrawList.Line(position - Vector3.UnitY * Half, position + Vector3.UnitY * Half, color);
        DrawList.Line(position - Vector3.UnitZ * Half, position + Vector3.UnitZ * Half, color);
    }

    /// <summary>Draws a ray as a line from where it starts, a hundred units along it.</summary>
    public static void DrawRay(Ray ray, Color color)
    {
        var direction = ray.Direction.LengthSquared() > 0 ? Vector3.Normalize(ray.Direction) : Vector3.UnitZ;
        DrawList.Line(ray.Position, ray.Position + direction * 100, color);
    }

    /// <summary>Draws a circle's outline on the XY plane, turned <paramref name="rotationAngle"/> degrees about <paramref name="rotationAxis"/>.</summary>
    public static void DrawCircle3D(Vector3 center, float radius, Vector3 rotationAxis, float rotationAngle, Color color)
    {
        var turn = rotationAxis.LengthSquared() > 0
            ? Quaternion.CreateFromAxisAngle(Vector3.Normalize(rotationAxis), float.DegreesToRadians(rotationAngle))
            : Quaternion.Identity;
        const int Segments = 36;
        Vector3 Point(int i) => center + Vector3.Transform(new Vector3(MathF.Cos(MathF.Tau * i / Segments), MathF.Sin(MathF.Tau * i / Segments), 0) * radius, turn);
        for (int i = 0; i < Segments; i++) DrawList.Line(Point(i), Point(i + 1), color);
    }

    /// <summary>Draws a strip of triangles, each from three points in a row.</summary>
    public static void DrawTriangleStrip3D(ReadOnlySpan<Vector3> points, Color color)
    {
        for (int i = 2; i < points.Length; i++) DrawList.Triangle(points[i - 2], points[i - 1], points[i], color);
    }

    /// <summary>
    /// Draws an upright cylinder, or a cone with a top radius of 0, its base centered on
    /// <paramref name="position"/>, with <paramref name="slices"/> sides.
    /// </summary>
    public static void DrawCylinder(Vector3 position, float radiusTop, float radiusBottom, float height, int slices, Color color) =>
        DrawCylinderEx(position, position + new Vector3(0, height, 0), radiusBottom, radiusTop, slices, color);

    /// <summary>Draws a cylinder or cone from <paramref name="startPos"/> to <paramref name="endPos"/>, with its caps.</summary>
    public static void DrawCylinderEx(Vector3 startPos, Vector3 endPos, float startRadius, float endRadius, int sides, Color color)
    {
        var (bottom, top) = CylinderRims(startPos, endPos, startRadius, endRadius, sides);
        for (int i = 0; i < bottom.Length; i++)
        {
            var next = (i + 1) % bottom.Length;
            DrawList.Quad(bottom[i], bottom[next], top[next], top[i], color);
            if (startRadius > 0) DrawList.Triangle(startPos, bottom[next], bottom[i], color);
            if (endRadius > 0) DrawList.Triangle(endPos, top[i], top[next], color);
        }
    }

    /// <summary>Draws an upright cylinder's or cone's edges as lines.</summary>
    public static void DrawCylinderWires(Vector3 position, float radiusTop, float radiusBottom, float height, int slices, Color color) =>
        DrawCylinderWiresEx(position, position + new Vector3(0, height, 0), radiusBottom, radiusTop, slices, color);

    /// <summary>Draws a cylinder's or cone's edges as lines, from <paramref name="startPos"/> to <paramref name="endPos"/>.</summary>
    public static void DrawCylinderWiresEx(Vector3 startPos, Vector3 endPos, float startRadius, float endRadius, int sides, Color color)
    {
        var (bottom, top) = CylinderRims(startPos, endPos, startRadius, endRadius, sides);
        for (int i = 0; i < bottom.Length; i++)
        {
            var next = (i + 1) % bottom.Length;
            DrawList.Line(bottom[i], bottom[next], color);
            DrawList.Line(top[i], top[next], color);
            DrawList.Line(bottom[i], top[i], color);
        }
    }

    // The two rims of a cylinder from start to end, each a ring of points around the axis.
    private static (Vector3[] Bottom, Vector3[] Top) CylinderRims(Vector3 start, Vector3 end, float startRadius, float endRadius, int sides)
    {
        sides = Math.Max(3, sides);
        var (u, v) = Perpendiculars(end - start);
        var bottom = new Vector3[sides];
        var top = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            var angle = MathF.Tau * i / sides;
            var around = u * MathF.Cos(angle) + v * MathF.Sin(angle);
            bottom[i] = start + around * startRadius;
            top[i] = end + around * endRadius;
        }
        return (bottom, top);
    }

    // Two unit directions square to each other and to axis.
    private static (Vector3 U, Vector3 V) Perpendiculars(Vector3 axis)
    {
        var a = axis.LengthSquared() > 1e-12f ? Vector3.Normalize(axis) : Vector3.UnitY;
        var other = MathF.Abs(a.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;
        var u = Vector3.Normalize(Vector3.Cross(a, other));
        return (u, Vector3.Cross(a, u));
    }

    /// <summary>Draws a capsule: a cylinder from <paramref name="startPos"/> to <paramref name="endPos"/> with a half sphere on each end.</summary>
    public static void DrawCapsule(Vector3 startPos, Vector3 endPos, float radius, int slices, int rings, Color color)
    {
        foreach (var (a, b, c, d) in CapsuleQuads(startPos, endPos, radius, slices, rings)) DrawList.Quad(a, b, c, d, color);
    }

    /// <summary>Draws a capsule's edges as lines.</summary>
    public static void DrawCapsuleWires(Vector3 startPos, Vector3 endPos, float radius, int slices, int rings, Color color)
    {
        foreach (var (a, b, _, d) in CapsuleQuads(startPos, endPos, radius, slices, rings))
        {
            DrawList.Line(a, b, color);
            DrawList.Line(a, d, color);
        }
    }

    // The capsule's surface as quads: the half sphere at each end in rings, and the side between.
    private static IEnumerable<(Vector3, Vector3, Vector3, Vector3)> CapsuleQuads(Vector3 start, Vector3 end, float radius, int slices, int rings)
    {
        slices = Math.Max(3, slices);
        rings = Math.Max(1, rings);
        var axis = end - start;
        var a = axis.LengthSquared() > 1e-12f ? Vector3.Normalize(axis) : Vector3.UnitY;
        var (u, v) = Perpendiculars(a);
        // A point on the surface by its ring, from the start's pole (0) through the middle to the end's pole (2 rings + 1), and its slice.
        Vector3 Point(int ring, int slice)
        {
            var angle = MathF.Tau * slice / slices;
            var around = u * MathF.Cos(angle) + v * MathF.Sin(angle);
            if (ring <= rings)
            {
                var polar = MathF.PI / 2 * ring / rings;
                return start - a * MathF.Cos(polar) * radius + around * MathF.Sin(polar) * radius;
            }
            var up = MathF.PI / 2 * (ring - rings - 1) / rings;
            return end + a * MathF.Sin(up) * radius + around * MathF.Cos(up) * radius;
        }
        for (int r = 0; r <= 2 * rings; r++)
            for (int s = 0; s < slices; s++)
                yield return (Point(r, s), Point(r, s + 1), Point(r + 1, s + 1), Point(r + 1, s));
    }

    /// <summary>Draws a flat rectangle on the XZ plane, centered on <paramref name="center"/>.</summary>
    public static void DrawPlane(Vector3 center, Vector2 size, Color color)
    {
        var (hx, hz) = (size.X / 2, size.Y / 2);
        DrawList.Quad(center + new Vector3(-hx, 0, -hz), center + new Vector3(hx, 0, -hz),
            center + new Vector3(hx, 0, hz), center + new Vector3(-hx, 0, hz), color);
    }

    /// <summary>Draws a grid on the XZ plane centered on the origin, <paramref name="slices"/> cells across.</summary>
    public static void DrawGrid(int slices, float spacing)
    {
        var half = slices / 2;
        var extent = half * spacing;
        for (int i = -half; i <= half; i++)
        {
            var color = i == 0 ? new Color(128, 128, 128) : new Color(191, 191, 191);
            DrawList.Line(new Vector3(i * spacing, 0, -extent), new Vector3(i * spacing, 0, extent), color);
            DrawList.Line(new Vector3(-extent, 0, i * spacing), new Vector3(extent, 0, i * spacing), color);
        }
    }

    // Corners 0 to 3 are the -Z face counterclockwise from (-x, -y), and 4 to 7 the same on +Z.
    private static void CubeCorners(Vector3 position, Vector3 size, Span<Vector3> corners)
    {
        var h = size / 2;
        corners[0] = position + new Vector3(-h.X, -h.Y, -h.Z);
        corners[1] = position + new Vector3(h.X, -h.Y, -h.Z);
        corners[2] = position + new Vector3(h.X, h.Y, -h.Z);
        corners[3] = position + new Vector3(-h.X, h.Y, -h.Z);
        corners[4] = position + new Vector3(-h.X, -h.Y, h.Z);
        corners[5] = position + new Vector3(h.X, -h.Y, h.Z);
        corners[6] = position + new Vector3(h.X, h.Y, h.Z);
        corners[7] = position + new Vector3(-h.X, h.Y, h.Z);
    }

    private static Vector3 SpherePoint(Vector3 center, float radius, int ring, int slice, int rings, int slices)
    {
        var polar = MathF.PI * ring / rings;
        var azimuth = MathF.Tau * slice / slices;
        return center + radius * new Vector3(MathF.Sin(polar) * MathF.Cos(azimuth), MathF.Cos(polar), MathF.Sin(polar) * MathF.Sin(azimuth));
    }
}
