using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    // -- 2D collision, as raylib's

    /// <summary>Whether two rectangles overlap.</summary>
    public static bool CheckCollisionRecs(Rectangle a, Rectangle b) =>
        a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;

    /// <summary>Whether two circles overlap.</summary>
    public static bool CheckCollisionCircles(Vector2 center1, float radius1, Vector2 center2, float radius2) =>
        Vector2.DistanceSquared(center1, center2) <= (radius1 + radius2) * (radius1 + radius2);

    /// <summary>Whether a circle and a rectangle overlap.</summary>
    public static bool CheckCollisionCircleRec(Vector2 center, float radius, Rectangle rec)
    {
        var nearest = new Vector2(Math.Clamp(center.X, rec.X, rec.X + rec.Width), Math.Clamp(center.Y, rec.Y, rec.Y + rec.Height));
        return Vector2.DistanceSquared(center, nearest) <= radius * radius;
    }

    /// <summary>Whether a point is inside a rectangle.</summary>
    public static bool CheckCollisionPointRec(Vector2 point, Rectangle rec) =>
        point.X >= rec.X && point.X < rec.X + rec.Width && point.Y >= rec.Y && point.Y < rec.Y + rec.Height;

    /// <summary>Whether a point is inside a circle.</summary>
    public static bool CheckCollisionPointCircle(Vector2 point, Vector2 center, float radius) =>
        Vector2.DistanceSquared(point, center) <= radius * radius;

    /// <summary>The rectangle two rectangles share, empty when they do not overlap.</summary>
    public static Rectangle GetCollisionRec(Rectangle a, Rectangle b)
    {
        var left = MathF.Max(a.X, b.X);
        var top = MathF.Max(a.Y, b.Y);
        var right = MathF.Min(a.X + a.Width, b.X + b.Width);
        var bottom = MathF.Min(a.Y + a.Height, b.Y + b.Height);
        return right > left && bottom > top ? new Rectangle(left, top, right - left, bottom - top) : default;
    }
}
