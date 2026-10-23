using System.Numerics;

namespace Engine;

/// <summary>Cuts a face of any count into triangles, the way an edit mesh is built and triangulated.</summary>
/// <remarks>
/// <para>
/// Ear clipping, which cuts a concave face right where a fan from one corner would lay triangles
/// outside it, as a fan of a staircase's side or an arch's front would. The face is laid flat
/// across the axis its normal is nearest and turned counterclockwise there, and an ear is a corner
/// whose two neighbors make a triangle that turns the same way and holds no other corner of what is
/// left, inside or on its sides.
/// </para>
/// <para>
/// The ear cut is the first in the order of the corners left, from the first, so the same face is
/// cut into the same triangles wherever it is cut, and a recipe that triangulates and then picks a
/// face by its index picks the same one in BevyCSharp, whose edit mesh cuts faces by the same rule.
/// A face left with no ear, which only one whose corners double back on themselves is, is finished
/// as a fan of what is left, so every face of n corners gives n - 2 triangles.
/// </para>
/// </remarks>
internal static class EarClipping
{
    /// <summary>The triangles of a face, three corners each, as positions in its loop, wound as it is.</summary>
    /// <param name="corners">The face's corners, in order round it.</param>
    /// <param name="normal">The way it faces, of any length.</param>
    public static int[] Cut(ReadOnlySpan<Vector3> corners, Vector3 normal)
    {
        var count = corners.Length;
        var triangles = new int[(count - 2) * 3];
        if (count == 3)
        {
            (triangles[0], triangles[1], triangles[2]) = (0, 1, 2);
            return triangles;
        }

        var flat = Flatten(corners, normal);
        var left = Enumerable.Range(0, count).ToList();
        var written = 0;
        while (left.Count > 3)
        {
            var cut = -1;
            for (int i = 0; i < left.Count && cut < 0; i++)
                if (IsEar(flat, left, i)) cut = i;

            // Nothing turns the face's way with nothing inside it, so what is left doubles back on
            // itself, and a fan of it gives the count of triangles if not their shape.
            if (cut < 0) break;

            triangles[written++] = left[(cut + left.Count - 1) % left.Count];
            triangles[written++] = left[cut];
            triangles[written++] = left[(cut + 1) % left.Count];
            left.RemoveAt(cut);
        }

        for (int i = 1; i < left.Count - 1; i++)
        {
            triangles[written++] = left[0];
            triangles[written++] = left[i];
            triangles[written++] = left[i + 1];
        }
        return triangles;
    }

    /// <summary>
    /// The face laid flat across the axis its normal is nearest, in the two other coordinates in the
    /// order that keeps it counterclockwise, so an ear is a corner that turns left.
    /// </summary>
    private static Vector2[] Flatten(ReadOnlySpan<Vector3> corners, Vector3 normal)
    {
        float x = MathF.Abs(normal.X), y = MathF.Abs(normal.Y), z = MathF.Abs(normal.Z);
        var flat = new Vector2[corners.Length];
        for (int i = 0; i < corners.Length; i++)
        {
            var point = corners[i];
            // Seen from the side the normal points to, (y, z) turns counterclockwise about +X, (z, x)
            // about +Y and (x, y) about +Z, and the pair swaps where the normal points the other way.
            flat[i] = x >= y && x >= z ? (normal.X >= 0 ? new Vector2(point.Y, point.Z) : new Vector2(point.Z, point.Y))
                : y >= z ? (normal.Y >= 0 ? new Vector2(point.Z, point.X) : new Vector2(point.X, point.Z))
                : normal.Z >= 0 ? new Vector2(point.X, point.Y) : new Vector2(point.Y, point.X);
        }
        return flat;
    }

    private static bool IsEar(Vector2[] flat, List<int> left, int at)
    {
        var a = flat[left[(at + left.Count - 1) % left.Count]];
        var b = flat[left[at]];
        var c = flat[left[(at + 1) % left.Count]];

        // A corner that turns right, or not at all, is no ear, since the triangle it would cut lies
        // outside the face or has no area.
        if (Turn(a, b, c) <= 0) return false;

        for (int i = 0; i < left.Count; i++)
        {
            var point = flat[left[i]];
            if (i == at || point.Equals(a) || point.Equals(b) || point.Equals(c)) continue;
            if (Turn(a, b, point) >= 0 && Turn(b, c, point) >= 0 && Turn(c, a, point) >= 0) return false;
        }
        return true;
    }

    /// <summary>Twice the area of a triangle, positive where it turns counterclockwise.</summary>
    private static float Turn(Vector2 a, Vector2 b, Vector2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
}
