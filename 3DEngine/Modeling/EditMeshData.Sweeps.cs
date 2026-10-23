using System.Numerics;

namespace Engine;

// The operations that copy, sweep or relax the selection, the mirror, the spin and the smooth.
internal sealed partial class EditMeshData
{
    // A vertex within a tenth of a millimeter of a mirror's plane or a spin's axis is on it.
    private const float OnPlane = 1e-4f;

    /// <summary>Copies the selected faces mirrored across the plane through a point square to an axis, the vertices on the plane shared by both, and selects the copy.</summary>
    /// <remarks>
    /// A copy is wound the other way from its face, so it faces out as the face does. A vertex on the
    /// plane is put exactly on it and kept for the copy, so a half whose open side lies on the plane
    /// mirrored makes a whole with no vertex twice. A face lying in the plane is not copied, since
    /// the copy would lie on it facing the other way, and a half to be mirrored is left open there.
    /// </remarks>
    public void Mirror(Vector3 axis, Vector3 pivot)
    {
        if (axis == Vector3.Zero) throw new ArgumentException("A mirror's plane is square to an axis that is not zero.", nameof(axis));
        var normal = Vector3.Normalize(axis);

        var copies = new Dictionary<int, int>();
        int Copy(int vertex)
        {
            if (copies.TryGetValue(vertex, out var copy)) return copy;
            var distance = Vector3.Dot(_positions[vertex] - pivot, normal);
            if (MathF.Abs(distance) <= OnPlane)
            {
                _positions[vertex] -= normal * distance;
                return copies[vertex] = vertex;
            }
            return copies[vertex] = AddVertex(_positions[vertex] - normal * (2 * distance));
        }

        var made = new List<int>();
        foreach (var index in _selectedFaces.Order().ToArray())
        {
            var face = _faces[index];
            if (face.Corners.All(corner => MathF.Abs(Vector3.Dot(_positions[corner] - pivot, normal)) <= OnPlane)) continue;
            var corners = face.Corners.Select(Copy).ToArray();
            Array.Reverse(corners);
            made.Add(AddFace(corners, face.Slot, face.Smooth, face.Uvs?.Reverse().ToArray()));
        }

        _selectedFaces.Clear();
        _selectedFaces.UnionWith(made);
        _selectedVertices.Clear();
        foreach (var face in made) _selectedVertices.UnionWith(_faces[face].Corners);
        Changed();
        Did($"mirror {AxisWords(axis)}{(pivot == Vector3.Zero ? "" : " at " + V(pivot))}");
    }

    /// <summary>Sweeps the selected faces, or where none is selected the selected edges, round an axis through a point by an angle in steps, a band of faces each step.</summary>
    /// <remarks>
    /// <para>
    /// The selected faces turn as one region, as an extrude moves them, the edges round the region
    /// sweeping a band each step and the faces ending turned by the whole angle, so a face spun a
    /// quarter turn is a bent solid open where it started. Spun a whole turn the sweep meets itself,
    /// its last step joined to where it started and the faces taken away, so a face beside the
    /// axis spun round is a ring with no opening.
    /// </para>
    /// <para>
    /// With no face selected, each selected edge sweeps a band of its own, as a lathe turns a
    /// profile, and the last step's vertices are selected after. A vertex on the axis stays on it,
    /// so a face of the band there is a triangle.
    /// </para>
    /// </remarks>
    public void Spin(Vector3 axis, float degrees, int steps, Vector3 center)
    {
        if (axis == Vector3.Zero) throw new ArgumentException("A spin turns about an axis that is not zero.", nameof(axis));
        if (!(MathF.Abs(degrees) > 0 && MathF.Abs(degrees) <= 360)) throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "A spin turns by an angle above zero and up to a whole turn either way.");
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(steps, 256);

        var unit = Vector3.Normalize(axis);
        var whole = MathF.Abs(MathF.Abs(degrees) - 360) < 1e-3f;
        var region = _selectedFaces.Order().ToArray();

        // The edges that sweep, each in the way a face runs along it, with that face's slot.
        var sweeping = new List<(int From, int To, int Slot, float Smooth)>();
        if (region.Length > 0)
        {
            var inside = new HashSet<(int, int)>();
            foreach (var face in region)
                for (int k = 0; k < _faces[face].Corners.Length; k++) inside.Add((_faces[face].Corners[k], _faces[face].Corners[(k + 1) % _faces[face].Corners.Length]));
            foreach (var face in region)
            {
                var c = _faces[face].Corners;
                for (int k = 0; k < c.Length; k++)
                    if (!inside.Contains((c[(k + 1) % c.Length], c[k]))) sweeping.Add((c[k], c[(k + 1) % c.Length], _faces[face].Slot, _faces[face].Smooth));
            }
        }
        else
        {
            var taken = new HashSet<(int, int)>();
            foreach (var face in _faces)
            {
                var c = face.Corners;
                for (int k = 0; k < c.Length; k++)
                {
                    var (a, b) = (c[k], c[(k + 1) % c.Length]);
                    if (_selectedVertices.Contains(a) && _selectedVertices.Contains(b) && taken.Add(a < b ? (a, b) : (b, a)))
                        sweeping.Add((a, b, face.Slot, face.Smooth));
                }
            }
        }

        Vector3 Turned(Vector3 point, float by) => center + Vector3.Transform(point - center, Quaternion.CreateFromAxisAngle(unit, float.DegreesToRadians(by)));
        bool OnAxis(int vertex)
        {
            var offset = _positions[vertex] - center;
            return (offset - unit * Vector3.Dot(offset, unit)).Length() <= OnPlane;
        }

        // Each sweeping vertex at each step, the first step its own and the last the first again
        // where the turn is whole.
        var rings = new Dictionary<int, int[]>();
        foreach (var (from, to, _, _) in sweeping)
            foreach (var vertex in new[] { from, to })
            {
                if (rings.ContainsKey(vertex)) continue;
                var ring = new int[steps + 1];
                ring[0] = vertex;
                for (int k = 1; k <= steps; k++)
                    ring[k] = OnAxis(vertex) || (whole && k == steps) ? vertex : AddVertex(Turned(_positions[vertex], degrees * k / steps));
                rings[vertex] = ring;
            }

        var made = new List<int>();
        for (int k = 0; k < steps; k++)
            foreach (var (from, to, slot, smooth) in sweeping)
            {
                int[] corners = [rings[from][k], rings[to][k], rings[to][k + 1], rings[from][k + 1]];
                var distinct = corners.Where((corner, i) => corner != corners[(i + 1) % 4]).ToArray();
                if (distinct.Length >= 3) made.Add(AddFace(distinct, slot, smooth));
            }

        _selectedFaces.Clear();
        _selectedVertices.Clear();
        if (region.Length > 0 && whole)
        {
            // The faces turned a whole turn would lie where the sweep started, inside the ring.
            RemoveFaces([.. region]);
            made = made.Select(face => face - region.Length).ToList();
            _selectedFaces.UnionWith(made);
            foreach (var face in made) _selectedVertices.UnionWith(_faces[face].Corners);
        }
        else if (region.Length > 0)
        {
            // The region ends turned by the whole angle, its edges on the last step and its other
            // vertices turned where they stand.
            var turned = new HashSet<int>();
            foreach (var face in region)
            {
                var c = _faces[face].Corners;
                for (int i = 0; i < c.Length; i++)
                {
                    if (rings.TryGetValue(c[i], out var ring)) c[i] = ring[steps];
                    else if (turned.Add(c[i])) _positions[c[i]] = Turned(_positions[c[i]], degrees);
                }
                _selectedFaces.Add(face);
                _selectedVertices.UnionWith(c);
            }
        }
        else
            foreach (var ring in rings.Values) _selectedVertices.Add(ring[steps]);

        Changed();
        Compact([]);
        Did($"spin {AxisWords(axis)} {F(degrees)} {steps}{(center == Vector3.Zero ? "" : " at " + V(center))}");
    }

    /// <summary>Moves each selected vertex toward the mean of the vertices it shares an edge with by a factor, all from where they stood.</summary>
    /// <remarks>
    /// A factor of one puts each vertex on its neighbors' mean, and smaller ones part of the way, so
    /// smoothing a box's corners all at once by a half draws it in to a smaller box.
    /// </remarks>
    public void Smooth(float factor)
    {
        if (!(factor >= 0 && factor <= 1)) throw new ArgumentOutOfRangeException(nameof(factor), factor, "A smooth's factor is from zero to one.");

        var neighbors = new Dictionary<int, List<int>>();
        foreach (var (low, high) in EdgeUses().Keys)
        {
            if (!neighbors.TryGetValue(low, out var lows)) neighbors[low] = lows = [];
            if (!neighbors.TryGetValue(high, out var highs)) neighbors[high] = highs = [];
            lows.Add(high);
            highs.Add(low);
        }

        var moved = SelectedVertices().Where(neighbors.ContainsKey).ToDictionary(vertex => vertex, vertex =>
        {
            var mean = neighbors[vertex].Aggregate(Vector3.Zero, (sum, other) => sum + _positions[other]) / neighbors[vertex].Count;
            return _positions[vertex] + (mean - _positions[vertex]) * factor;
        });
        foreach (var (vertex, position) in moved) _positions[vertex] = position;

        Changed();
        Did($"smooth {F(factor)}");
    }
}
