using System.Numerics;

namespace Engine;

// The cuts that add vertices along edges and split faces through them, the loop cut and the
// subdivision. A face beside a cut edge that is not itself cut takes the edge's new vertices as
// corners of its own, so the mesh stays closed where it was.
internal sealed partial class EditMeshData
{
    /// <summary>Cuts round the ring of four-cornered faces each selected edge crosses, through each face from edge to opposite edge at a fraction along them.</summary>
    /// <remarks>
    /// <para>
    /// From a selected edge the ring runs through the face beside it to the face's opposite edge and
    /// on through the face beyond, both ways, until it comes round to the edge again or meets a face
    /// that has not four corners or an edge with no face beyond. Each edge the ring crosses takes a
    /// vertex at the fraction along it, measured from the lower-numbered end of the selected edge and
    /// from the matching end of each edge after, and each face of the ring is split in two through
    /// its two new vertices, so a box's side edge cut at a half makes a loop round its middle.
    /// </para>
    /// <para>
    /// The new vertices are selected after, and with them the new loop's edges. Two selected edges
    /// of one ring cut it once.
    /// </para>
    /// </remarks>
    public void LoopCut(float fraction)
    {
        if (!(fraction > 0 && fraction < 1)) throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "A loop cut's fraction is between zero and one.");

        var starts = EdgeUses().Keys.Where(edge => _selectedVertices.Contains(edge.Low) && _selectedVertices.Contains(edge.High)).Order().ToArray();
        var made = new List<int>();
        foreach (var start in starts)
        {
            // An edge an earlier ring cut is two edges after the cut, and is cut no more.
            if (!EdgeUses().ContainsKey(start)) continue;
            made.AddRange(CutRing(start, fraction));
        }

        _selectedFaces.Clear();
        _selectedVertices.Clear();
        _selectedVertices.UnionWith(made);
        Changed();
        Did($"loopcut {F(fraction)}");
    }

    /// <summary>Cuts each selected face into a grid, each of its edges into one more part than the count of cuts, and selects the grid's faces.</summary>
    /// <remarks>
    /// A face of four corners becomes a grid of four-cornered faces, one more along each side than
    /// the count, and a triangle a grid of triangles, and a face of more corners a fan of triangles
    /// from its middle to its corners and the vertices on its edges. A box subdivided once has 26
    /// vertices and 24 faces.
    /// </remarks>
    public void Subdivide(int cuts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(cuts, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(cuts, 64);

        var picked = _selectedFaces.Order().Select(index => _faces[index]).ToHashSet();
        var parts = cuts + 1;
        var points = new Dictionary<(int Low, int High), int[]>();
        foreach (var face in picked)
        {
            var corners = face.Corners;
            for (int k = 0; k < corners.Length; k++)
            {
                var (a, b) = (corners[k], corners[(k + 1) % corners.Length]);
                var key = a < b ? (a, b) : (b, a);
                if (points.ContainsKey(key)) continue;
                points[key] = Enumerable.Range(1, cuts).Select(i => AddVertex(Vector3.Lerp(_positions[key.Item1], _positions[key.Item2], (float)i / parts))).ToArray();
            }
        }

        // The points of an edge in the order they run from one end to the other.
        int[] Along(int from, int to) => from < to ? points[(from, to)] : [.. points[(to, from)].Reverse()];

        var made = new List<Face>();
        var kept = new List<Face>(_faces.Count);
        foreach (var face in _faces)
        {
            if (!picked.Contains(face))
            {
                kept.Add(face);
                continue;
            }
            var c = face.Corners;
            if (c.Length == 4) made.AddRange(GridOf(face, Along, parts));
            else if (c.Length == 3) made.AddRange(TrianglesOf(face, Along, parts));
            else
            {
                var ring = new List<int>();
                for (int k = 0; k < c.Length; k++)
                {
                    ring.Add(c[k]);
                    ring.AddRange(Along(c[k], c[(k + 1) % c.Length]));
                }
                var middle = AddVertex(c.Aggregate(Vector3.Zero, (sum, corner) => sum + _positions[corner]) / c.Length);
                for (int k = 0; k < ring.Count; k++) made.Add(new Face([ring[k], ring[(k + 1) % ring.Count], middle], face.Slot, face.Smooth));
            }
        }

        _faces.Clear();
        _faces.AddRange(kept);
        InsertAlongEdges(points);
        _selectedFaces.Clear();
        _selectedVertices.Clear();
        foreach (var face in made)
        {
            _selectedFaces.Add(_faces.Count);
            _faces.Add(face);
            _selectedVertices.UnionWith(face.Corners);
        }
        Changed();
        Did($"subdivide {cuts}");
    }

    /// <summary>Cuts the selected faces along a plane through a point, each face the plane crosses split in two along it.</summary>
    /// <remarks>
    /// Each edge of a selected face the plane crosses takes a vertex where it does, which a face
    /// beside it takes too, and a face whose outline meets the plane at two corners not side by side
    /// is split between them, so a box cut across its middle with every face selected has a loop
    /// round it. A face the plane meets more often, as a concave one may be, takes the vertices and
    /// stays whole. The vertices along the cut are selected after.
    /// </remarks>
    public void Knife(Vector3 normal, Vector3 point)
    {
        if (normal == Vector3.Zero) throw new ArgumentException("A knife's plane faces a direction that is not zero.", nameof(normal));
        if (!float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
            throw new ArgumentOutOfRangeException(nameof(point), point, "A knife's plane passes through a finite point.");

        // A vertex within a tenth of a millimeter of the plane is on it.
        const float On = 1e-4f;
        var unit = Vector3.Normalize(normal);
        float Side(int vertex) => Vector3.Dot(_positions[vertex] - point, unit);

        var picked = _selectedFaces.Order().Select(index => _faces[index]).ToList();
        var points = new Dictionary<(int Low, int High), int[]>();
        foreach (var face in picked)
        {
            var c = face.Corners;
            for (int k = 0; k < c.Length; k++)
            {
                var (a, b) = (c[k], c[(k + 1) % c.Length]);
                var key = a < b ? (a, b) : (b, a);
                var (low, high) = (Side(key.Item1), Side(key.Item2));
                if (points.ContainsKey(key) || !((low > On && high < -On) || (low < -On && high > On))) continue;
                points[key] = [AddVertex(Vector3.Lerp(_positions[key.Item1], _positions[key.Item2], low / (low - high)))];
            }
        }
        InsertAlongEdges(points);

        var along = new HashSet<int>(points.Values.Select(made => made[0]));
        foreach (var face in picked)
        {
            var c = face.Corners;
            var on = Enumerable.Range(0, c.Length).Where(k => along.Contains(c[k]) || MathF.Abs(Side(c[k])) <= On).ToArray();
            if (on is not [var first, var second] || second - first == 1 || (first == 0 && second == c.Length - 1)) continue;
            face.Corners = c[first..(second + 1)];
            _faces.Add(new Face([.. c[second..], .. c[..(first + 1)]], face.Slot, face.Smooth));
            along.UnionWith([c[first], c[second]]);
        }

        _selectedFaces.Clear();
        _selectedVertices.Clear();
        _selectedVertices.UnionWith(along);
        Changed();
        Did($"knife {AxisWords(normal)}{(point == Vector3.Zero ? "" : " at " + V(point))}");
    }

    // Cuts the ring of faces across one edge, and answers the vertices it made.
    private List<int> CutRing((int Low, int High) start, float fraction)
    {
        // The ring's faces, each with the corner its cut enters at, and each crossed edge with the
        // end its fraction is measured from.
        var ring = new Dictionary<Face, int>();
        var crossed = new Dictionary<(int Low, int High), int> { [start] = start.Low };
        foreach (var first in _faces.Where(face => HasEdge(face, start.Low, start.High)).ToArray())
        {
            var (edge, from, face) = (start, start.Low, first);
            while (face.Corners.Length == 4 && !ring.ContainsKey(face))
            {
                var c = face.Corners;
                var k = Array.FindIndex(c, corner => corner == edge.Low || corner == edge.High);
                if (k == 0 && (c[3] == edge.Low || c[3] == edge.High)) k = 3;
                ring[face] = k;
                // The opposite edge runs the other way round the face, so the corner across from
                // the one the fraction is measured from is the one before it, going round.
                var across = c[k] == from ? c[(k + 3) % 4] : c[(k + 2) % 4];
                var (p, q) = (c[(k + 2) % 4], c[(k + 3) % 4]);
                edge = p < q ? (p, q) : (q, p);
                from = across;
                if (!crossed.TryAdd(edge, from)) break;
                var beyond = _faces.FirstOrDefault(other => other != face && HasEdge(other, p, q));
                if (beyond is null) break;
                face = beyond;
            }
        }

        var points = crossed.ToDictionary(pair => pair.Key, pair =>
        {
            var to = pair.Value == pair.Key.Low ? pair.Key.High : pair.Key.Low;
            return new[] { AddVertex(Vector3.Lerp(_positions[pair.Value], _positions[to], fraction)) };
        });

        var kept = new List<Face>(_faces.Count + ring.Count);
        foreach (var face in _faces)
        {
            if (!ring.TryGetValue(face, out var k))
            {
                kept.Add(face);
                continue;
            }
            var c = face.Corners;
            int Point(int a, int b) => points[a < b ? (a, b) : (b, a)][0];
            var (entering, leaving) = (Point(c[k], c[(k + 1) % 4]), Point(c[(k + 2) % 4], c[(k + 3) % 4]));
            kept.Add(new Face([c[k], entering, leaving, c[(k + 3) % 4]], face.Slot, face.Smooth));
            kept.Add(new Face([entering, c[(k + 1) % 4], c[(k + 2) % 4], leaving], face.Slot, face.Smooth));
        }

        // The faces off the ring whose edges the ring crossed take its vertices, as a face at the
        // end of a ring that stopped does.
        _faces.Clear();
        _faces.AddRange(kept);
        var made = points.Values.Select(point => point[0]).ToList();
        InsertAlongEdges(points, face => !face.Corners.Any(made.Contains));
        return made;
    }

    /// <summary>Puts the points along each edge into every face that runs along it, in the order the face runs, for the faces <paramref name="which"/> takes.</summary>
    private void InsertAlongEdges(Dictionary<(int Low, int High), int[]> points, Func<Face, bool>? which = null)
    {
        foreach (var face in _faces)
        {
            if (which is not null && !which(face)) continue;
            var c = face.Corners;
            var corners = new List<int>(c.Length);
            for (int k = 0; k < c.Length; k++)
            {
                corners.Add(c[k]);
                var (a, b) = (c[k], c[(k + 1) % c.Length]);
                if (a < b && points.TryGetValue((a, b), out var forward)) corners.AddRange(forward);
                else if (a > b && points.TryGetValue((b, a), out var backward)) corners.AddRange(backward.Reverse());
            }
            face.Corners = [.. corners];
        }
    }

    // Whether a face has an edge between two vertices, either way round.
    private static bool HasEdge(Face face, int a, int b)
    {
        var c = face.Corners;
        for (int k = 0; k < c.Length; k++)
        {
            var (from, to) = (c[k], c[(k + 1) % c.Length]);
            if ((from == a && to == b) || (from == b && to == a)) return true;
        }
        return false;
    }

    /// <summary>A face of four corners cut into a grid of four-cornered faces, the given number along each side.</summary>
    private IEnumerable<Face> GridOf(Face face, Func<int, int, int[]> along, int parts)
    {
        var c = face.Corners;
        var (c0, c1, c2, c3) = (_positions[c[0]], _positions[c[1]], _positions[c[2]], _positions[c[3]]);

        // The grid runs from the first corner along the first edge, and from it along the last edge.
        var grid = new int[parts + 1, parts + 1];
        (grid[0, 0], grid[parts, 0], grid[parts, parts], grid[0, parts]) = (c[0], c[1], c[2], c[3]);
        var (bottom, right, top, left) = (along(c[0], c[1]), along(c[1], c[2]), along(c[3], c[2]), along(c[0], c[3]));
        for (int i = 1; i < parts; i++)
        {
            (grid[i, 0], grid[parts, i], grid[i, parts], grid[0, i]) = (bottom[i - 1], right[i - 1], top[i - 1], left[i - 1]);
        }
        for (int i = 1; i < parts; i++)
            for (int j = 1; j < parts; j++)
            {
                var (u, v) = ((float)i / parts, (float)j / parts);
                grid[i, j] = AddVertex((1 - u) * (1 - v) * c0 + u * (1 - v) * c1 + u * v * c2 + (1 - u) * v * c3);
            }

        for (int i = 0; i < parts; i++)
            for (int j = 0; j < parts; j++)
                yield return new Face([grid[i, j], grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]], face.Slot, face.Smooth);
    }

    /// <summary>A triangle cut into a grid of triangles, the given number along each side.</summary>
    private IEnumerable<Face> TrianglesOf(Face face, Func<int, int, int[]> along, int parts)
    {
        var c = face.Corners;
        var (a, b, d) = (_positions[c[0]], _positions[c[1]], _positions[c[2]]);

        // A point is so many parts along the first edge and so many along the last, from the first corner.
        var grid = new int[parts + 1, parts + 1];
        (grid[0, 0], grid[parts, 0], grid[0, parts]) = (c[0], c[1], c[2]);
        var (first, second, last) = (along(c[0], c[1]), along(c[1], c[2]), along(c[0], c[2]));
        for (int k = 1; k < parts; k++) (grid[k, 0], grid[parts - k, k], grid[0, k]) = (first[k - 1], second[k - 1], last[k - 1]);
        for (int i = 1; i < parts; i++)
            for (int j = 1; i + j < parts; j++)
                grid[i, j] = AddVertex(a + (b - a) * ((float)i / parts) + (d - a) * ((float)j / parts));

        for (int i = 0; i < parts; i++)
            for (int j = 0; i + j < parts; j++)
            {
                yield return new Face([grid[i, j], grid[i + 1, j], grid[i, j + 1]], face.Slot, face.Smooth);
                if (i + j < parts - 1) yield return new Face([grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]], face.Slot, face.Smooth);
            }
    }
}
