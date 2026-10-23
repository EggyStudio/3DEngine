using System.Numerics;

namespace Engine;

// The bevel, which cuts the selected edges back into bands of faces with a round profile.
internal sealed partial class EditMeshData
{
    /// <summary>Cuts each selected edge back by a width into a band of faces, round across in a number of segments, and closes each corner with one face.</summary>
    /// <remarks>
    /// <para>
    /// An edge is beveled where both its ends are selected and two faces run along it in opposite
    /// directions. Each face beside a beveled edge is cut back by the width from it, a corner between
    /// two beveled edges moving in from both as an inset's does, and an edge that is not beveled at
    /// a corner that is gives up the end of it the bevel needs, its far end where it was.
    /// </para>
    /// <para>
    /// Across each edge the band follows the arc tangent to both faces at the width from the edge,
    /// so a box's edges are rounded with a quarter circle of that radius, and one segment is a flat
    /// chamfer. Where three or more beveled edges meet, as at a box's corner, the corner is closed
    /// with one face. The bands and the corners are selected after. A width wider than half a face
    /// turns its cut faces inside out, which the operation leaves as asked.
    /// </para>
    /// </remarks>
    public void Bevel(float width, int segments)
    {
        if (!(width > 0) || !float.IsFinite(width)) throw new ArgumentOutOfRangeException(nameof(width), width, "A bevel's width is a finite number above zero.");
        ArgumentOutOfRangeException.ThrowIfLessThan(segments, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(segments, 64);

        var beveled = EdgeUses().Where(edge => edge.Value is { Forward: 1, Backward: 1 }
            && _selectedVertices.Contains(edge.Key.Low) && _selectedVertices.Contains(edge.Key.High)).Select(edge => edge.Key).ToHashSet();
        var made = new List<int>();
        if (beveled.Count > 0) made = Beveled(beveled, width, segments);

        _selectedFaces.Clear();
        _selectedFaces.UnionWith(made);
        _selectedVertices.Clear();
        foreach (var face in made) _selectedVertices.UnionWith(_faces[face].Corners);
        Compact([]);
        Did($"bevel {F(width)} {segments}");
    }

    // Bevels the edges given, each shared by two faces, and answers the faces it made.
    private List<int> Beveled(HashSet<(int Low, int High)> beveled, float width, int segments)
    {
        bool IsBeveled(int a, int b) => beveled.Contains(a < b ? (a, b) : (b, a));
        var touched = new HashSet<int>();
        foreach (var (low, high) in beveled) touched.UnionWith([low, high]);

        // The vertex each new one stands for, so the hole left round a beveled vertex is found.
        var origin = new Dictionary<int, int>();
        int Add(Vector3 point, int of)
        {
            var vertex = AddVertex(point);
            origin[vertex] = of;
            return vertex;
        }

        // How far along each edge that is not beveled its end at a beveled vertex slides, from each
        // face beside it whose other edge at that corner is beveled, which is the distance at which
        // the line the width in from that edge crosses it.
        var slides = new Dictionary<(int At, int Toward), List<float>>();
        foreach (var face in _faces)
        {
            var corners = face.Corners;
            var normal = Unit(Newell(corners));
            for (int k = 0; k < corners.Length; k++)
            {
                var (before, at, after) = (corners[(k + corners.Length - 1) % corners.Length], corners[k], corners[(k + 1) % corners.Length]);
                if (!touched.Contains(at)) continue;
                var (inBeveled, outBeveled) = (IsBeveled(before, at), IsBeveled(at, after));
                if (inBeveled == outBeveled) continue;

                // Inward of an edge running counterclockwise about the normal is the normal crossed with it.
                var inward = inBeveled ? Unit(Vector3.Cross(normal, _positions[at] - _positions[before])) : Unit(Vector3.Cross(normal, _positions[after] - _positions[at]));
                var toward = inBeveled ? after : before;
                var along = Unit(_positions[toward] - _positions[at]);
                var meet = Vector3.Dot(along, inward);
                if (meet <= 1e-4f) continue;
                if (!slides.TryGetValue((at, toward), out var found)) slides[(at, toward)] = found = [];
                found.Add(width / meet);
            }
        }

        var slid = new Dictionary<(int At, int Toward), int>();
        foreach (var (low, high) in EdgeUses().Keys)
        {
            if (beveled.Contains((low, high))) continue;
            foreach (var (at, toward) in new[] { (low, high), (high, low) })
            {
                if (!touched.Contains(at)) continue;
                var distance = slides.TryGetValue((at, toward), out var found) ? found.Average() : width;
                slid[(at, toward)] = Add(_positions[at] + Unit(_positions[toward] - _positions[at]) * distance, at);
            }
        }

        // The two faces along each beveled edge, the first running from its lower end to its higher,
        // with their normals, read before the corners they share are replaced.
        var sides = beveled.ToDictionary(edge => edge, edge =>
        {
            var first = _faces.First(face => Runs(face, edge.Low, edge.High));
            var second = _faces.First(face => Runs(face, edge.High, edge.Low));
            return (first.Slot, first.Smooth, First: Unit(Newell(first.Corners)), Second: Unit(Newell(second.Corners)));
        });

        // Each face's corners at beveled vertices replaced, and the point each face gives a beveled
        // edge at each of its ends, the rail the band runs along.
        var rails = new Dictionary<(int From, int To, int At), int>();
        var slots = new Dictionary<int, (int Slot, float Smooth)>();
        foreach (var face in _faces)
        {
            var corners = face.Corners;
            var normal = Unit(Newell(corners));
            var replaced = new List<int>(corners.Length + 4);
            for (int k = 0; k < corners.Length; k++)
            {
                var (before, at, after) = (corners[(k + corners.Length - 1) % corners.Length], corners[k], corners[(k + 1) % corners.Length]);
                if (!touched.Contains(at))
                {
                    replaced.Add(at);
                    continue;
                }

                slots.TryAdd(at, (face.Slot, face.Smooth));
                var (inBeveled, outBeveled) = (IsBeveled(before, at), IsBeveled(at, after));
                if (inBeveled && outBeveled)
                {
                    // In from both edges by the width, as an inset's corner moves.
                    var fromBefore = Unit(Vector3.Cross(normal, _positions[at] - _positions[before]));
                    var fromAfter = Unit(Vector3.Cross(normal, _positions[after] - _positions[at]));
                    var meet = 1 + Vector3.Dot(fromBefore, fromAfter);
                    var offset = meet > 1e-4f ? (fromBefore + fromAfter) * (width / meet) : fromBefore * width;
                    var corner = Add(_positions[at] + offset, at);
                    replaced.Add(corner);
                    rails[(before, at, at)] = corner;
                    rails[(at, after, at)] = corner;
                }
                else if (inBeveled)
                {
                    replaced.Add(slid[(at, after)]);
                    rails[(before, at, at)] = slid[(at, after)];
                }
                else if (outBeveled)
                {
                    replaced.Add(slid[(at, before)]);
                    rails[(at, after, at)] = slid[(at, before)];
                }
                else
                {
                    replaced.Add(slid[(at, before)]);
                    replaced.Add(slid[(at, after)]);
                }
            }
            Recorner(face, [.. replaced.Where((corner, k) => corner != replaced[(k + 1) % replaced.Count])]);
        }

        // A band across each edge, from the rail of the face running along it from its lower end to
        // the rail of the face running back, its rows joined end to end.
        var made = new List<int>();
        foreach (var (a, b) in beveled)
        {
            var (slot, smooth, first, second) = sides[(a, b)];
            var along = _positions[b] - _positions[a];
            var atA = Profile(a, rails[(a, b, a)], rails[(b, a, a)], first, second, along, width, segments, Add);
            var atB = Profile(b, rails[(a, b, b)], rails[(b, a, b)], first, second, along, width, segments, Add);
            for (int k = 0; k < segments; k++)
                made.Add(AddFace([atA[k + 1], atB[k + 1], atB[k], atA[k]], slot, smooth));
        }

        // What is left open round each beveled vertex, edges between its new vertices that no face
        // runs along the other way, closed by a face running round it the other way.
        var runs = new HashSet<(int, int)>();
        foreach (var face in _faces)
            for (int k = 0; k < face.Corners.Length; k++) runs.Add((face.Corners[k], face.Corners[(k + 1) % face.Corners.Length]));
        var next = new Dictionary<int, int>();
        foreach (var (from, to) in runs)
            if (!runs.Contains((to, from)) && origin.TryGetValue(from, out var of) && origin.TryGetValue(to, out var other) && of == other)
                next.TryAdd(to, from);
        while (next.Count > 0)
        {
            var start = next.Keys.Min();
            var loop = new List<int> { start };
            var at = start;
            var closed = false;
            while (next.Remove(at, out var step))
            {
                if (step == start)
                {
                    closed = true;
                    break;
                }
                loop.Add(step);
                at = step;
            }
            if (!closed || loop.Count < 3) continue;
            var (slot, smooth) = slots[origin[start]];
            made.Add(AddFace([.. loop], slot, smooth));
        }

        Changed();
        return made;
    }

    // Whether a face runs along an edge from one vertex to the other.
    private static bool Runs(Face face, int from, int to)
    {
        var corners = face.Corners;
        for (int k = 0; k < corners.Length; k++)
            if (corners[k] == from && corners[(k + 1) % corners.Length] == to) return true;
        return false;
    }

    /// <summary>
    /// The points across a beveled edge at one of its ends, from the first face's rail to the
    /// second's, on the arc tangent to both faces, the two rails first and last and the points
    /// between them new.
    /// </summary>
    /// <param name="at">The end of the edge, which the new points stand for.</param>
    /// <param name="firstRail">The rail of the face that runs along the edge from its lower end.</param>
    /// <param name="secondRail">The rail of the face that runs back along it.</param>
    /// <param name="first">The first face's normal.</param>
    /// <param name="second">The second face's normal.</param>
    /// <param name="along">The edge from its lower end to its higher, the way the first face runs along it.</param>
    /// <param name="width">How far each rail is from the edge.</param>
    /// <param name="segments">How many segments the arc is cut into.</param>
    /// <param name="add">Adds a new vertex standing for a vertex of the mesh.</param>
    private int[] Profile(int at, int firstRail, int secondRail, Vector3 first, Vector3 second, Vector3 along, float width, int segments, Func<Vector3, int, int> add)
    {
        var points = new int[segments + 1];
        (points[0], points[segments]) = (firstRail, secondRail);
        if (segments == 1) return points;

        // Where the tangents meet, the width back out from each rail toward the edge, inward of an
        // edge being its face's normal crossed with the way the face runs along it.
        var (start, end) = (_positions[firstRail], _positions[secondRail]);
        var corner = (start - Unit(Vector3.Cross(first, along)) * width + end - Unit(Vector3.Cross(second, -along)) * width) * 0.5f;

        var (u, v) = (start - corner, end - corner);
        var angle = MathF.Acos(Math.Clamp(Vector3.Dot(Unit(u), Unit(v)), -1, 1));
        for (int k = 1; k < segments; k++)
        {
            var s = (float)k / segments;
            Vector3 point;
            if (angle > MathF.PI - 1e-3f || angle < 1e-3f)
                point = Vector3.Lerp(start, end, s);
            else
            {
                // The circle tangent to both legs where the rails are, its middle on the line halving
                // the angle between them, and the point that far round from the first rail.
                var reach = (u.Length() + v.Length()) * 0.5f;
                var middle = corner + Unit(Unit(u) + Unit(v)) * (reach / MathF.Cos(angle / 2));
                var (p, q) = (start - middle, end - middle);
                var turn = MathF.Acos(Math.Clamp(Vector3.Dot(Unit(p), Unit(q)), -1, 1));
                point = turn < 1e-4f ? Vector3.Lerp(start, end, s) : middle + (p * MathF.Sin((1 - s) * turn) + q * MathF.Sin(s * turn)) / MathF.Sin(turn);
            }
            points[k] = add(point, at);
        }
        return points;
    }
}
