namespace Engine;

/// <summary>
/// Makes the convex pieces a boolean leaves into faces again, the vertices welded, the pieces in one
/// plane, one slot and one face's texture coordinates joined along the edges they share, and the
/// corners that only lie along a straight edge taken away, so a window cut from a wall is a few faces
/// and not a hundred pieces.
/// </summary>
/// <remarks>
/// <para>
/// The partition splits a polygon where a plane crosses it, so a piece's edge often runs past a
/// corner of the piece beside it, a T-junction, where the two share no whole edge. Each edge is cut
/// at every welded vertex lying along it first, so neighbors share their edges whole, which lets
/// them be joined and leaves the mesh closed with every edge shared by two faces.
/// </para>
/// <para>
/// Two pieces are joined along an edge they share where the face that makes is one loop with no
/// vertex twice, so a face round a hole, which a face of one loop cannot be, stays two or more. A
/// corner that lies between its two neighbors on a straight line in every face it is a corner of is
/// taken away from all of them at once, which keeps the edges shared, so two boxes joined into one
/// are a box of eight vertices again.
/// </para>
/// </remarks>
internal static class FaceMerge
{
    /// <summary>A face made, its corners as indices of the welded points, and what it came from.</summary>
    internal readonly record struct Made(int[] Corners, Csg.Source Source);

    /// <summary>Welds, mends, joins and tidies the pieces into faces.</summary>
    /// <param name="pieces">The convex pieces a boolean left.</param>
    /// <param name="weld">How near two points are to be one, and a point an edge to lie on it.</param>
    public static (List<Csg.Point> Points, List<Made> Faces) Faces(List<Csg.Polygon> pieces, double weld)
    {
        var points = new List<Csg.Point>();
        var cells = new Dictionary<(long, long, long), List<int>>();
        var loops = new List<(List<int> Corners, Csg.Source Source)>();

        foreach (var piece in pieces)
        {
            var corners = new List<int>(piece.Points.Length);
            foreach (var point in piece.Points)
            {
                var index = Weld(point, points, cells, weld);
                if (corners.Count == 0 || corners[^1] != index) corners.Add(index);
            }

            if (corners.Count > 1 && corners[0] == corners[^1]) corners.RemoveAt(corners.Count - 1);
            if (corners.Count >= 3) loops.Add((corners, piece.Source));
        }

        Mend(loops, points, weld);

        // Joined within each group of pieces in one plane and one slot, smoothed alike.
        var groups = new List<(Csg.Point Normal, double Offset, Csg.Source Source, List<List<int>> Loops, List<bool> Second)>();
        foreach (var (corners, source) in loops)
        {
            var normal = Newell(corners, points);
            if (normal.Length < 1e-12) continue;
            normal = normal.Unit;
            var offset = Csg.Point.Dot(normal, points[corners[0]]);

            var group = groups.FindIndex(known => known.Source.Slot == source.Slot && known.Source.Smooth == source.Smooth && known.Source.Uvs == source.Uvs
                && Csg.Point.Dot(known.Normal, normal) > 1 - 1e-6 && Math.Abs(known.Offset - offset) < weld);
            if (group < 0)
            {
                groups.Add((normal, offset, source, [], []));
                group = groups.Count - 1;
            }

            groups[group].Loops.Add(corners);
            groups[group].Second.Add(source.Second);
        }

        var faces = new List<(List<int> Corners, Csg.Source Source)>();
        foreach (var (_, _, source, members, second) in groups)
        {
            var joined = Join(members, second);
            foreach (var (corners, fromSecond) in joined) faces.Add((corners, source with { Second = fromSecond }));
        }

        Tidy(faces, points, weld);
        return (points, faces.Where(face => face.Corners.Count >= 3).Select(face => new Made([.. face.Corners], face.Source)).ToList());
    }

    /// <summary>The index of a point kept before within the weld of this one, or of this one kept new.</summary>
    private static int Weld(Csg.Point point, List<Csg.Point> points, Dictionary<(long, long, long), List<int>> cells, double weld)
    {
        var (cx, cy, cz) = ((long)Math.Floor(point.X / weld), (long)Math.Floor(point.Y / weld), (long)Math.Floor(point.Z / weld));
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
        for (int dz = -1; dz <= 1; dz++)
        {
            if (!cells.TryGetValue((cx + dx, cy + dy, cz + dz), out var near)) continue;
            foreach (var index in near)
            {
                if ((points[index] - point).Length <= weld) return index;
            }
        }

        points.Add(point);
        if (!cells.TryGetValue((cx, cy, cz), out var here)) cells[(cx, cy, cz)] = here = [];
        here.Add(points.Count - 1);
        return points.Count - 1;
    }

    /// <summary>Cuts each edge at every point lying along it, so pieces beside one another share their edges whole.</summary>
    private static void Mend(List<(List<int> Corners, Csg.Source Source)> loops, List<Csg.Point> points, double weld)
    {
        foreach (var (corners, _) in loops)
        {
            for (int i = 0; i < corners.Count; i++)
            {
                var (from, to) = (points[corners[i]], points[corners[(i + 1) % corners.Count]]);
                var along = to - from;
                var length = along.Length;
                if (length < weld) continue;
                var direction = along * (1.0 / length);

                var on = new List<(double At, int Index)>();
                for (int index = 0; index < points.Count; index++)
                {
                    if (index == corners[i] || index == corners[(i + 1) % corners.Count]) continue;

                    var offset = points[index] - from;
                    var at = Csg.Point.Dot(offset, direction);
                    if (at <= weld || at >= length - weld) continue;
                    if ((offset - direction * at).Length <= weld) on.Add((at, index));
                }

                if (on.Count == 0) continue;
                on.Sort((a, b) => a.At.CompareTo(b.At));
                corners.InsertRange(i + 1, on.Select(found => found.Index));
                i += on.Count;
            }
        }
    }

    /// <summary>
    /// Joins loops of one group along the edges they share, while the face made is one loop with no
    /// vertex twice, the first pair found joined first and the search begun again, and says for each
    /// face made whether any piece of it was the second solid's.
    /// </summary>
    private static List<(List<int> Corners, bool Second)> Join(List<List<int>> members, List<bool> second)
    {
        var loops = members.Select((corners, i) => (Corners: corners, Second: second[i], Alive: true)).ToList();
        var refused = new HashSet<(int, int)>();
        var joined = true;

        while (joined)
        {
            joined = false;
            var owner = new Dictionary<(int, int), int>();
            for (int i = 0; i < loops.Count; i++)
            {
                if (!loops[i].Alive) continue;
                var corners = loops[i].Corners;
                for (int c = 0; c < corners.Count; c++) owner.TryAdd((corners[c], corners[(c + 1) % corners.Count]), i);
            }

            for (int i = 0; i < loops.Count && !joined; i++)
            {
                if (!loops[i].Alive) continue;
                var corners = loops[i].Corners;
                for (int c = 0; c < corners.Count && !joined; c++)
                {
                    var (a, b) = (corners[c], corners[(c + 1) % corners.Count]);
                    if (!owner.TryGetValue((b, a), out var j) || j == i || !loops[j].Alive || refused.Contains((Math.Min(i, j), Math.Max(i, j)))) continue;

                    if (Joined(corners, loops[j].Corners, a, b) is { } together)
                    {
                        loops[i] = (together, loops[i].Second || loops[j].Second, true);
                        loops[j] = (loops[j].Corners, loops[j].Second, false);
                        joined = true;
                    }
                    else
                    {
                        refused.Add((Math.Min(i, j), Math.Max(i, j)));
                    }
                }
            }
        }

        return loops.Where(loop => loop.Alive).Select(loop => (loop.Corners, loop.Second)).ToList();
    }

    /// <summary>
    /// The loop two loops make joined along the edge one runs from a to b and the other back, the
    /// spikes left where they shared more than one edge folded away, or nothing where it holds a
    /// vertex twice and so is no face.
    /// </summary>
    private static List<int>? Joined(List<int> first, List<int> second, int a, int b)
    {
        var loop = new List<int>(first.Count + second.Count);
        var start = first.IndexOf(b);
        for (int k = 0; k < first.Count - 1; k++) loop.Add(first[(start + k) % first.Count]);
        start = second.IndexOf(a);
        for (int k = 0; k < second.Count - 1; k++) loop.Add(second[(start + k) % second.Count]);

        // An edge out and straight back, where the two shared a run of edges, folded away.
        var folded = true;
        while (folded && loop.Count >= 3)
        {
            folded = false;
            for (int i = 0; i < loop.Count; i++)
            {
                var before = loop[(i + loop.Count - 1) % loop.Count];
                var after = loop[(i + 1) % loop.Count];
                if (before != after) continue;

                loop.RemoveAt(i);
                loop.RemoveAt(i % loop.Count);
                folded = true;
                break;
            }
        }

        return loop.Count >= 3 && loop.Distinct().Count() == loop.Count ? loop : null;
    }

    /// <summary>Takes away each corner that lies between its neighbors on a straight line in every face it is a corner of.</summary>
    private static void Tidy(List<(List<int> Corners, Csg.Source Source)> faces, List<Csg.Point> points, double weld)
    {
        var taken = true;
        while (taken)
        {
            taken = false;
            var places = new Dictionary<int, List<int>>();
            for (int f = 0; f < faces.Count; f++)
            {
                foreach (var corner in faces[f].Corners)
                {
                    if (!places.TryGetValue(corner, out var list)) places[corner] = list = [];
                    list.Add(f);
                }
            }

            foreach (var (corner, inFaces) in places)
            {
                var straight = inFaces.All(f =>
                {
                    var corners = faces[f].Corners;
                    if (corners.Count <= 3) return false;
                    var at = corners.IndexOf(corner);
                    var (before, here, after) = (points[corners[(at + corners.Count - 1) % corners.Count]], points[corner], points[corners[(at + 1) % corners.Count]]);
                    var run = after - before;
                    return run.Length > weld
                        && Csg.Point.Cross(here - before, run).Length <= weld * run.Length
                        && Csg.Point.Dot(here - before, after - here) > 0;
                });

                if (!straight) continue;
                foreach (var f in inFaces) faces[f].Corners.Remove(corner);
                taken = true;
            }
        }
    }

    private static Csg.Point Newell(List<int> corners, List<Csg.Point> points)
    {
        double x = 0, y = 0, z = 0;
        for (int i = 0; i < corners.Count; i++)
        {
            var (a, b) = (points[corners[i]], points[corners[(i + 1) % corners.Count]]);
            x += (a.Y - b.Y) * (a.Z + b.Z);
            y += (a.Z - b.Z) * (a.X + b.X);
            z += (a.X - b.X) * (a.Y + b.Y);
        }

        return new Csg.Point(x, y, z);
    }
}
