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

    // Enough segments that no edge is longer than about four pixels, within limits.
    private static int CircleSegments(float radius) => Math.Clamp((int)(MathF.Tau * radius / 4f), 12, 128);

    private static Vector3 CirclePoint(Vector2 center, float radius, int i, int segments)
    {
        var angle = MathF.Tau * i / segments;
        return new Vector3(center.X + MathF.Cos(angle) * radius, center.Y + MathF.Sin(angle) * radius, 0);
    }
}
