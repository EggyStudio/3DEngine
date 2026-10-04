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

    /// <summary>Whether <paramref name="point"/> is inside the triangle, or on its edge.</summary>
    public static bool CheckCollisionPointTriangle(Vector2 point, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        var (d1, d2, d3) = (Side(p1, p2, point), Side(p2, p3, point), Side(p3, p1, point));
        var negative = d1 < 0 || d2 < 0 || d3 < 0;
        var positive = d1 > 0 || d2 > 0 || d3 > 0;
        return !(negative && positive);
    }

    /// <summary>Whether <paramref name="point"/> is within <paramref name="threshold"/> pixels of the segment from <paramref name="p1"/> to <paramref name="p2"/>.</summary>
    public static bool CheckCollisionPointLine(Vector2 point, Vector2 p1, Vector2 p2, int threshold) =>
        DistanceToSegment(point, p1, p2) <= threshold;

    /// <summary>Whether <paramref name="point"/> is inside the polygon whose corners are <paramref name="points"/>, in order.</summary>
    public static bool CheckCollisionPointPoly(Vector2 point, ReadOnlySpan<Vector2> points)
    {
        // Crossings of a ray to the right of the point, odd inside.
        var inside = false;
        for (int i = 0, j = points.Length - 1; i < points.Length; j = i++)
        {
            var (a, b) = (points[i], points[j]);
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Whether two segments cross, with where in <paramref name="collisionPoint"/>.</summary>
    public static bool CheckCollisionLines(Vector2 startPos1, Vector2 endPos1, Vector2 startPos2, Vector2 endPos2, out Vector2 collisionPoint)
    {
        collisionPoint = default;
        var (r, q) = (endPos1 - startPos1, endPos2 - startPos2);
        var denominator = r.X * q.Y - r.Y * q.X;
        if (MathF.Abs(denominator) < 1e-9f) return false;
        var between = startPos2 - startPos1;
        var t = (between.X * q.Y - between.Y * q.X) / denominator;
        var u = (between.X * r.Y - between.Y * r.X) / denominator;
        if (t < 0 || t > 1 || u < 0 || u > 1) return false;
        collisionPoint = startPos1 + r * t;
        return true;
    }

    /// <summary>Whether a circle touches the segment from <paramref name="p1"/> to <paramref name="p2"/>.</summary>
    public static bool CheckCollisionCircleLine(Vector2 center, float radius, Vector2 p1, Vector2 p2) =>
        DistanceToSegment(center, p1, p2) <= radius;

    private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        var along = b - a;
        var length = along.LengthSquared();
        var t = length > 0 ? Math.Clamp(Vector2.Dot(point - a, along) / length, 0, 1) : 0;
        return Vector2.Distance(point, a + along * t);
    }

    // -- 3D

    /// <summary>Whether two spheres overlap.</summary>
    public static bool CheckCollisionSpheres(Vector3 center1, float radius1, Vector3 center2, float radius2) =>
        Vector3.DistanceSquared(center1, center2) <= (radius1 + radius2) * (radius1 + radius2);

    /// <summary>Whether two boxes overlap.</summary>
    public static bool CheckCollisionBoxes(BoundingBox box1, BoundingBox box2) =>
        box1.Max.X >= box2.Min.X && box1.Min.X <= box2.Max.X
        && box1.Max.Y >= box2.Min.Y && box1.Min.Y <= box2.Max.Y
        && box1.Max.Z >= box2.Min.Z && box1.Min.Z <= box2.Max.Z;

    /// <summary>Whether a box and a sphere overlap.</summary>
    public static bool CheckCollisionBoxSphere(BoundingBox box, Vector3 center, float radius) =>
        Vector3.DistanceSquared(Vector3.Clamp(center, box.Min, box.Max), center) <= radius * radius;

    /// <summary>Where a ray first meets a sphere, from outside or within it.</summary>
    public static RayCollision GetRayCollisionSphere(Ray ray, Vector3 center, float radius)
    {
        var direction = Vector3.Normalize(ray.Direction);
        var toCenter = ray.Position - center;
        var b = Vector3.Dot(toCenter, direction);
        var c = toCenter.LengthSquared() - radius * radius;
        var discriminant = b * b - c;
        if (discriminant < 0) return default;
        var root = MathF.Sqrt(discriminant);
        var distance = -b - root;
        if (distance < 0) distance = -b + root;
        if (distance < 0) return default;
        var point = ray.Position + direction * distance;
        return new RayCollision(true, distance, point, Vector3.Normalize(point - center));
    }

    /// <summary>Where a ray first meets a box, by the slab test, from outside or within it.</summary>
    public static RayCollision GetRayCollisionBox(Ray ray, BoundingBox box)
    {
        var direction = Vector3.Normalize(ray.Direction);
        float near = float.NegativeInfinity, far = float.PositiveInfinity;
        var normal = Vector3.Zero;
        for (int axis = 0; axis < 3; axis++)
        {
            float origin = ray.Position[axis], along = direction[axis], min = box.Min[axis], max = box.Max[axis];
            if (MathF.Abs(along) < 1e-9f)
            {
                if (origin < min || origin > max) return default;
                continue;
            }
            float t1 = (min - origin) / along, t2 = (max - origin) / along;
            var sign = -1f;
            if (t1 > t2) (t1, t2, sign) = (t2, t1, 1f);
            if (t1 > near)
            {
                near = t1;
                normal = Vector3.Zero;
                normal[axis] = sign;
            }
            far = MathF.Min(far, t2);
            if (near > far) return default;
        }
        if (far < 0) return default;
        // A ray from inside meets the box where it leaves.
        var distance = near >= 0 ? near : far;
        return new RayCollision(true, distance, ray.Position + direction * distance, near >= 0 ? normal : -normal);
    }

    /// <summary>Where a ray meets a triangle, from either side, by the Möller-Trumbore test.</summary>
    public static RayCollision GetRayCollisionTriangle(Ray ray, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        var direction = Vector3.Normalize(ray.Direction);
        var (edge1, edge2) = (p2 - p1, p3 - p1);
        var h = Vector3.Cross(direction, edge2);
        var a = Vector3.Dot(edge1, h);
        if (MathF.Abs(a) < 1e-9f) return default;
        var f = 1 / a;
        var s = ray.Position - p1;
        var u = f * Vector3.Dot(s, h);
        if (u < 0 || u > 1) return default;
        var q = Vector3.Cross(s, edge1);
        var v = f * Vector3.Dot(direction, q);
        if (v < 0 || u + v > 1) return default;
        var distance = f * Vector3.Dot(edge2, q);
        if (distance < 0) return default;
        return new RayCollision(true, distance, ray.Position + direction * distance, Vector3.Normalize(Vector3.Cross(edge1, edge2)));
    }

    /// <summary>Where a ray meets a quad of corners in order around its edge.</summary>
    public static RayCollision GetRayCollisionQuad(Ray ray, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
    {
        var first = GetRayCollisionTriangle(ray, p1, p2, p4);
        return first.Hit ? first : GetRayCollisionTriangle(ray, p2, p3, p4);
    }

    /// <summary>Where a ray first meets a mesh placed by <paramref name="transform"/>, testing each of its triangles.</summary>
    public static RayCollision GetRayCollisionMesh(Ray ray, ModelMesh mesh, Matrix4x4 transform)
    {
        if (!Meshes.TryGetData(mesh.Id, out var vertices, out var indices)) return default;
        var nearest = default(RayCollision);
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            var hit = GetRayCollisionTriangle(ray,
                Vector3.Transform(vertices[indices[i]].Position, transform),
                Vector3.Transform(vertices[indices[i + 1]].Position, transform),
                Vector3.Transform(vertices[indices[i + 2]].Position, transform));
            if (hit.Hit && (!nearest.Hit || hit.Distance < nearest.Distance)) nearest = hit;
        }
        return nearest;
    }

    /// <summary>The box around a mesh's vertices, in its own space.</summary>
    public static BoundingBox GetMeshBoundingBox(ModelMesh mesh) => mesh.Bounds;
}

/// <summary>Where a ray met something, as raylib's <c>RayCollision</c> has it.</summary>
/// <param name="Hit">Whether it met it at all.</param>
/// <param name="Distance">How far along the ray, in world units.</param>
/// <param name="Point">Where, in the world.</param>
/// <param name="Normal">The surface's normal there.</param>
public readonly record struct RayCollision(bool Hit, float Distance, Vector3 Point, Vector3 Normal);
