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

    /// <summary>Draws a sphere's rings and slices as lines.</summary>
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
        }
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
