using System.Numerics;

namespace Engine;

// The operations that join faces or close holes, the bridge, the fill and the dissolves. Each works
// from the loops round a set of faces, the edges a face of the set runs along that no other face of
// it runs along the other way, followed round from end to end.
internal sealed partial class EditMeshData
{
    /// <summary>Joins the loops round two regions of selected faces, taking the faces away and filling between the loops with a band of four-cornered faces.</summary>
    /// <remarks>
    /// The selected faces are two regions, each with one loop round it of the same count of
    /// vertices, as two faces facing each other across a gap are. Each vertex of the first loop is
    /// joined to the vertex of the second that makes the band's edges shortest in sum, so the band
    /// runs straight across rather than twisted. The band is selected after.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The selected faces are not two loops of the same count.</exception>
    public void Bridge()
    {
        var region = _selectedFaces.Order().ToArray();
        var loops = Loops(region);
        if (loops is not [var first, var second] || first.Count != second.Count)
            throw new InvalidOperationException("A bridge joins two regions of selected faces, each with one loop round it of the same count of vertices.");

        var (slot, smooth) = (_faces[region[0]].Slot, _faces[region[0]].Smooth);
        var count = first.Count;

        // The second loop runs the other way along the band from the first, so its vertex across
        // from the first loop's vertex i is (offset - i), and the offset is the one nearest in sum.
        var offset = Enumerable.Range(0, count).MinBy(o =>
            Enumerable.Range(0, count).Sum(i => (_positions[first[i]] - _positions[second[((o - i) % count + count) % count]]).Length()));
        int Across(int i) => second[((offset - i) % count + count) % count];

        RemoveFaces([.. region]);
        _selectedFaces.Clear();
        _selectedVertices.Clear();
        for (int i = 0; i < count; i++)
        {
            var next = (i + 1) % count;
            _selectedFaces.Add(AddFace([first[i], first[next], Across(next), Across(i)], slot, smooth));
        }
        foreach (var face in _selectedFaces) _selectedVertices.UnionWith(_faces[face].Corners);
        Compact([]);
        Did("bridge");
    }

    /// <summary>Closes each hole whose edges all have both ends selected with one face, which is selected after.</summary>
    /// <remarks>
    /// A hole's edge is one that one face runs along and none runs back, and the face filling runs
    /// along it the other way, so it faces the way the faces round it face.
    /// </remarks>
    public void Fill()
    {
        var runs = new Dictionary<(int, int), Face>();
        foreach (var face in _faces)
            for (int k = 0; k < face.Corners.Length; k++) runs.TryAdd((face.Corners[k], face.Corners[(k + 1) % face.Corners.Length]), face);

        var next = new Dictionary<int, int>();
        var beside = new Dictionary<int, Face>();
        foreach (var ((from, to), face) in runs)
        {
            if (runs.ContainsKey((to, from)) || !_selectedVertices.Contains(from) || !_selectedVertices.Contains(to)) continue;
            next.TryAdd(to, from);
            beside.TryAdd(to, face);
        }

        var made = new List<int>();
        foreach (var loop in Chains(next))
            made.Add(AddFace([.. loop], beside[loop[0]].Slot, beside[loop[0]].Smooth));

        _selectedFaces.Clear();
        _selectedFaces.UnionWith(made);
        _selectedVertices.Clear();
        foreach (var face in made) _selectedVertices.UnionWith(_faces[face].Corners);
        Changed();
        Did("fill");
    }

    /// <summary>Joins each region of selected faces into one face round it, taking away the edges and the vertices inside it.</summary>
    /// <remarks>
    /// A region is the selected faces joined by the edges they share. One with a hole in it, which
    /// has more than one loop round it, is left as it was. The vertices on the loop stay, so the
    /// shape is kept, and the joined faces are selected after.
    /// </remarks>
    public void Dissolve()
    {
        var picked = _selectedFaces.ToHashSet();
        var groups = Groups(picked, (a, b) => picked.Contains(a) && picked.Contains(b));
        DissolveGroups(groups);
        Did("dissolve");
    }

    /// <summary>Takes away each selected edge that two faces run along, joining the faces on either side into one.</summary>
    /// <remarks>
    /// The faces joined across the selected edges are one face round them, a group with a hole in
    /// it left as it was, as <see cref="Dissolve"/> leaves a region. A vertex left between two
    /// edges in line stays, so the shape is kept, and the joined faces are selected after.
    /// </remarks>
    public void DissolveEdges()
    {
        var all = Enumerable.Range(0, _faces.Count).ToHashSet();
        var groups = Groups(all, (_, _) => true, edge => _selectedVertices.Contains(edge.Low) && _selectedVertices.Contains(edge.High));
        DissolveGroups(groups);
        Did("dissolve edges");
    }

    // Each group of two or more faces made one face round it, where one loop goes round it.
    private void DissolveGroups(List<int[]> groups)
    {
        var going = new HashSet<int>();
        var made = new List<Face>();
        foreach (var group in groups)
        {
            if (group.Length < 2 || Loops(group) is not [var loop]) continue;
            going.UnionWith(group);
            made.Add(new Face([.. loop], _faces[group[0]].Slot, _faces[group[0]].Smooth));
        }

        RemoveFaces(going);
        _selectedFaces.Clear();
        _selectedVertices.Clear();
        foreach (var face in made)
        {
            _selectedFaces.Add(_faces.Count);
            _faces.Add(face);
            _selectedVertices.UnionWith(face.Corners);
        }
        Compact([]);
    }

    /// <summary>
    /// The faces given in groups joined by the edges two of them run along in opposite directions,
    /// where <paramref name="joins"/> takes the two faces and <paramref name="across"/> the edge.
    /// </summary>
    private List<int[]> Groups(HashSet<int> faces, Func<int, int, bool> joins, Func<(int Low, int High), bool>? across = null)
    {
        var parent = faces.ToDictionary(face => face, face => face);
        int Root(int face)
        {
            while (parent[face] != face) face = parent[face] = parent[parent[face]];
            return face;
        }

        var runs = new Dictionary<(int, int), int>();
        foreach (var face in faces)
        {
            var c = _faces[face].Corners;
            for (int k = 0; k < c.Length; k++) runs.TryAdd((c[k], c[(k + 1) % c.Length]), face);
        }
        foreach (var ((from, to), face) in runs)
        {
            if (from > to || !runs.TryGetValue((to, from), out var other) || other == face || !joins(face, other)) continue;
            if (across is not null && !across((from, to))) continue;
            parent[Root(face)] = Root(other);
        }

        return faces.GroupBy(Root).Select(group => group.Order().ToArray()).OrderBy(group => group[0]).ToList();
    }

    /// <summary>
    /// The loops round some faces, each the edges a face of them runs along that no other face of
    /// them runs back along, in the order the faces run them, or null where a vertex is on such an
    /// edge twice in one direction, as where two regions touch at a corner.
    /// </summary>
    private List<List<int>>? Loops(IReadOnlyCollection<int> faces)
    {
        var runs = new HashSet<(int, int)>();
        foreach (var face in faces)
        {
            var c = _faces[face].Corners;
            for (int k = 0; k < c.Length; k++) runs.Add((c[k], c[(k + 1) % c.Length]));
        }

        var next = new Dictionary<int, int>();
        foreach (var (from, to) in runs)
            if (!runs.Contains((to, from)) && !next.TryAdd(from, to)) return null;
        // The edges round a set of faces close up, so a chain left open has a vertex met twice.
        var loops = Chains(next);
        return loops.Sum(loop => loop.Count) == next.Count ? loops : null;
    }

    /// <summary>The closed chains of a map from each vertex to the next, each of three or more, the lowest vertex of each first.</summary>
    private static List<List<int>> Chains(Dictionary<int, int> next)
    {
        var left = new Dictionary<int, int>(next);
        var loops = new List<List<int>>();
        while (left.Count > 0)
        {
            var start = left.Keys.Min();
            var loop = new List<int> { start };
            var at = start;
            var closed = false;
            while (left.Remove(at, out var step))
            {
                if (step == start)
                {
                    closed = true;
                    break;
                }
                loop.Add(step);
                at = step;
            }
            if (closed && loop.Count >= 3) loops.Add(loop);
        }
        return loops;
    }
}
