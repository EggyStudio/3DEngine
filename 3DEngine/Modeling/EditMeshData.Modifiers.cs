using System.Numerics;

namespace Engine;

// The modifier stack, operations kept as lines and applied in order to a copy of the mesh when it
// is built, so the mesh they apply to stays as it was shaped and every change to it is built with
// them again. Applying one does it to the mesh itself and drops it from the stack.
internal sealed partial class EditMeshData
{
    /// <summary>A modifier, its words as the recipe's <c>modifier</c> line has them and what it does to a copy of the mesh.</summary>
    private sealed record Modifier(string Line, Action<EditMeshData> Apply);

    private readonly List<Modifier> _modifiers = [];

    // The mesh the stack last made and the version of this one it was made from.
    private (int Version, EditMeshData Mesh)? _modified;

    /// <summary>The modifiers in the order they apply, each as its words after <c>modifier</c> in the recipe.</summary>
    public IReadOnlyList<string> Modifiers => _modifiers.Select(modifier => modifier.Line).ToArray();

    /// <summary>The mesh with every modifier applied in order, this mesh itself where there is none, made again only after a change.</summary>
    public EditMeshData Modified()
    {
        if (_modifiers.Count == 0) return this;
        if (_modified is { } known && known.Version == Version) return known.Mesh;

        var mesh = Copy();
        foreach (var modifier in _modifiers)
        {
            mesh._selectedVertices.UnionWith(Enumerable.Range(0, mesh._positions.Count));
            mesh._selectedFaces.UnionWith(Enumerable.Range(0, mesh._faces.Count));
            modifier.Apply(mesh);
        }
        _modified = (Version, mesh);
        return mesh;
    }

    /// <summary>Adds a modifier that mirrors the mesh across the planes through a pivot square to each axis named, x, y or z, one after another.</summary>
    public void ModifierMirror(string axes, Vector3 pivot)
    {
        ArgumentException.ThrowIfNullOrEmpty(axes);
        if (axes.Any(axis => axis is not ('x' or 'y' or 'z')) || axes.Distinct().Count() != axes.Length)
            throw new ArgumentException("A mirror modifier names its axes as x, y and z, each once, as xz.", nameof(axes));

        var normals = axes.Select(axis => axis switch { 'x' => Vector3.UnitX, 'y' => Vector3.UnitY, _ => Vector3.UnitZ }).ToArray();
        AddModifier($"mirror {axes}{(pivot == Vector3.Zero ? "" : " at " + V(pivot))}", mesh =>
        {
            foreach (var normal in normals)
            {
                mesh.SelectAll();
                mesh.Mirror(normal, pivot);
            }
        });
    }

    /// <summary>Adds a modifier that repeats the mesh a number of times in all, each copy an offset further on.</summary>
    public void ModifierArray(int count, Vector3 offset)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 1024);
        if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y) || !float.IsFinite(offset.Z))
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "An array's offset is finite along each axis.");
        AddModifier($"array {count} {V(offset)}", mesh => mesh.Repeated(Enumerable.Range(1, count - 1).Select(k => offset * k)));
    }

    /// <summary>Adds a modifier that repeats the mesh at each of a curve's positions, moved by the position's offset from the first, which is the mesh itself.</summary>
    public void ModifierArrayCurve(ReadOnlySpan<Vector3> positions)
    {
        if (positions.Length < 2) throw new ArgumentException("An array along a curve takes two positions or more.", nameof(positions));
        foreach (var position in positions)
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
                throw new ArgumentOutOfRangeException(nameof(positions), position, "A curve's positions are finite.");

        var points = positions.ToArray();
        AddModifier($"array curve {string.Join(' ', points.Select(V))}", mesh => mesh.Repeated(points[1..].Select(point => point - points[0])));
    }

    /// <summary>Adds a modifier that bevels every edge two faces run along, or those whose faces meet at more than an angle in degrees.</summary>
    public void ModifierBevel(float width, int segments, float angle)
    {
        if (!(width > 0) || !float.IsFinite(width)) throw new ArgumentOutOfRangeException(nameof(width), width, "A bevel's width is a finite number above zero.");
        ArgumentOutOfRangeException.ThrowIfLessThan(segments, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(segments, 64);
        if (!(angle >= 0 && angle < 180)) throw new ArgumentOutOfRangeException(nameof(angle), angle, "A bevel modifier's angle is from zero to under 180 degrees.");

        AddModifier($"bevel {F(width)} {segments}{(angle == 0 ? "" : " " + F(angle))}", mesh =>
        {
            var least = MathF.Cos(float.DegreesToRadians(angle));
            var normals = new Dictionary<(int, int), Vector3>();
            foreach (var face in mesh._faces)
                for (int k = 0; k < face.Corners.Length; k++)
                    normals[(face.Corners[k], face.Corners[(k + 1) % face.Corners.Length])] = Unit(mesh.Newell(face.Corners));

            // An edge two faces run along, at more than the angle between their normals, or every
            // such edge where the angle is zero, faces in one plane among them.
            var edges = mesh.EdgeUses().Where(edge => edge.Value is { Forward: 1, Backward: 1 }).Select(edge => edge.Key)
                .Where(edge => angle == 0 || Vector3.Dot(normals[(edge.Low, edge.High)], normals[(edge.High, edge.Low)]) < least - 1e-6f).ToHashSet();
            if (edges.Count > 0) mesh.Beveled(edges, width, segments);
            mesh.Compact([]);
        });
    }

    /// <summary>Adds a modifier that subdivides the mesh by Catmull and Clark's rule to a level, each level making every face four-cornered and rounding the shape.</summary>
    public void ModifierSubdivision(int level)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 6);
        AddModifier($"subdivision {level}", mesh =>
        {
            for (int i = 0; i < level; i++) mesh.CatmullClark();
        });
    }

    /// <summary>Adds a modifier that gives the mesh a thickness, a second surface out along its normals, or in where the thickness is below zero, and a rim along its open edges.</summary>
    public void ModifierSolidify(float thickness)
    {
        if (thickness == 0 || !float.IsFinite(thickness)) throw new ArgumentOutOfRangeException(nameof(thickness), thickness, "A solidify's thickness is a finite number other than zero.");
        AddModifier($"solidify {F(thickness)}", mesh => mesh.Solidified(thickness));
    }

    /// <summary>Adds a modifier that cuts every face of more than three corners into triangles.</summary>
    public void ModifierTriangulate() => AddModifier("triangulate", mesh => mesh.Triangulate());

    /// <summary>Moves a modifier from one place in the stack to another, the ones between moving up or down one.</summary>
    public void ModifierMove(int from, int to)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)from, (uint)_modifiers.Count, nameof(from));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)to, (uint)_modifiers.Count, nameof(to));
        var modifier = _modifiers[from];
        _modifiers.RemoveAt(from);
        _modifiers.Insert(to, modifier);
        Changed();
        Did($"modifier move {from} {to}");
    }

    /// <summary>Takes a modifier out of the stack.</summary>
    public void ModifierRemove(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)_modifiers.Count, nameof(index));
        _modifiers.RemoveAt(index);
        Changed();
        Did($"modifier remove {index}");
    }

    /// <summary>Does a modifier to the mesh itself, as though it were first in the stack, and takes it out, the selection left as nothing.</summary>
    public void ModifierApply(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)_modifiers.Count, nameof(index));
        var mesh = Copy();
        mesh._selectedVertices.UnionWith(Enumerable.Range(0, mesh._positions.Count));
        mesh._selectedFaces.UnionWith(Enumerable.Range(0, mesh._faces.Count));
        _modifiers[index].Apply(mesh);

        _positions.Clear();
        _positions.AddRange(mesh._positions);
        _faces.Clear();
        _faces.AddRange(mesh._faces);
        _slots.Clear();
        _slots.AddRange(mesh._slots);
        _looks.Clear();
        _looks.AddRange(mesh._looks);
        _selectedVertices.Clear();
        _selectedFaces.Clear();
        _modifiers.RemoveAt(index);
        Changed();
        Did($"modifier apply {index}");
    }

    private void AddModifier(string line, Action<EditMeshData> apply)
    {
        _modifiers.Add(new Modifier(line, apply));
        Changed();
        Did($"modifier {line}");
    }

    /// <summary>A copy of the mesh's vertices, faces, slots and grid, with nothing selected and no recipe or modifiers of its own.</summary>
    private EditMeshData Copy()
    {
        var copy = new EditMeshData { GridStep = GridStep };
        copy._positions.AddRange(_positions);
        foreach (var face in _faces) copy._faces.Add(new Face([.. face.Corners], face.Slot, face.Smooth, face.Uvs is { } uvs ? [.. uvs] : null));
        copy._slots.Clear();
        copy._slots.AddRange(_slots);
        copy._looks.Clear();
        copy._looks.AddRange(_looks);
        return copy;
    }

    // Adds a copy of every face, moved by each offset given.
    private void Repeated(IEnumerable<Vector3> offsets)
    {
        var (positions, faces) = (_positions.ToArray(), _faces.ToArray());
        foreach (var offset in offsets)
        {
            var first = _positions.Count;
            foreach (var position in positions) _positions.Add(position + offset);
            foreach (var face in faces) AddFace(face.Corners.Select(corner => corner + first).ToArray(), face.Slot, face.Smooth, face.Uvs is { } uvs ? [.. uvs] : null);
        }
        Changed();
    }

    /// <summary>
    /// One level of Catmull and Clark's subdivision. Each face's middle, each edge's middle drawn
    /// toward the middles of the faces beside it, and each vertex drawn toward the faces and edges
    /// round it make a face of four corners for each corner of each face.
    /// </summary>
    /// <remarks>
    /// An edge one face runs along keeps its middle, and a vertex on such edges moves by the rule
    /// for a curve along them, a quarter toward the mean of its two neighbors there, so an open
    /// mesh keeps its outline.
    /// </remarks>
    private void CatmullClark()
    {
        var facePoints = _faces.Select(face => face.Corners.Aggregate(Vector3.Zero, (sum, corner) => sum + _positions[corner]) / face.Corners.Length).ToArray();
        var besideEdge = new Dictionary<(int, int), List<int>>();
        var aroundVertex = new List<int>[_positions.Count];
        for (int f = 0; f < _faces.Count; f++)
        {
            var c = _faces[f].Corners;
            for (int k = 0; k < c.Length; k++)
            {
                var (a, b) = (c[k], c[(k + 1) % c.Length]);
                var key = a < b ? (a, b) : (b, a);
                if (!besideEdge.TryGetValue(key, out var faces)) besideEdge[key] = faces = [];
                faces.Add(f);
                (aroundVertex[a] ??= []).Add(f);
            }
        }

        var edgePoints = new Dictionary<(int, int), Vector3>();
        var edgesOf = new List<(int Other, bool Open)>[_positions.Count];
        foreach (var ((a, b), faces) in besideEdge)
        {
            var middle = (_positions[a] + _positions[b]) / 2;
            edgePoints[(a, b)] = faces.Count == 2 ? (_positions[a] + _positions[b] + facePoints[faces[0]] + facePoints[faces[1]]) / 4 : middle;
            (edgesOf[a] ??= []).Add((b, faces.Count != 2));
            (edgesOf[b] ??= []).Add((a, faces.Count != 2));
        }

        var vertexPoints = new Vector3[_positions.Count];
        for (int v = 0; v < _positions.Count; v++)
        {
            var point = _positions[v];
            if (edgesOf[v] is not { } edges || aroundVertex[v] is not { } faces)
            {
                vertexPoints[v] = point;
                continue;
            }
            var open = edges.Where(edge => edge.Open).ToArray();
            if (open.Length == 2) vertexPoints[v] = point * 0.75f + (_positions[open[0].Other] + _positions[open[1].Other]) * 0.125f;
            else if (open.Length > 0) vertexPoints[v] = point;
            else
            {
                var n = faces.Count;
                var q = faces.Aggregate(Vector3.Zero, (sum, face) => sum + facePoints[face]) / n;
                var r = edges.Aggregate(Vector3.Zero, (sum, edge) => sum + (point + _positions[edge.Other]) / 2) / edges.Count;
                vertexPoints[v] = (q + 2 * r + (n - 3) * point) / n;
            }
        }

        // The new vertices, the old ones first at their new places, then an edge's, then a face's.
        var faces0 = _faces.ToArray();
        var carried = faces0.Select(CarryOf).ToArray();
        for (int v = 0; v < vertexPoints.Length; v++) _positions[v] = vertexPoints[v];
        var edgeVertices = edgePoints.ToDictionary(pair => pair.Key, pair => AddVertex(pair.Value));
        var faceVertices = facePoints.Select(AddVertex).ToArray();
        int EdgeVertex(int a, int b) => edgeVertices[a < b ? (a, b) : (b, a)];

        _faces.Clear();
        for (int f = 0; f < faces0.Length; f++)
        {
            var c = faces0[f].Corners;
            for (int k = 0; k < c.Length; k++)
            {
                var (before, at, after) = (c[(k + c.Length - 1) % c.Length], c[k], c[(k + 1) % c.Length]);
                _faces.Add(Piece(carried[f], faces0[f], [at, EdgeVertex(at, after), faceVertices[f], EdgeVertex(before, at)]));
            }
        }
        _selectedFaces.Clear();
        _selectedVertices.Clear();
        Changed();
    }

    /// <summary>
    /// The mesh given a thickness, a copy of its surface out along its vertices' normals by it, or in
    /// where it is below zero, wound so both face away from the solid between them, and a band of
    /// faces along each open edge joining the two.
    /// </summary>
    private void Solidified(float thickness)
    {
        // A vertex's normal, the faces round it summed by their areas.
        var sums = new Vector3[_positions.Count];
        foreach (var face in _faces)
        {
            var area = Newell(face.Corners);
            foreach (var corner in face.Corners) sums[corner] += area;
        }

        var count = _positions.Count;
        for (int v = 0; v < count; v++) _positions.Add(_positions[v] + Unit(sums[v]) * thickness);

        var open = EdgeUses().Where(edge => edge.Value.Forward + edge.Value.Backward == 1).Select(edge => edge.Key).ToHashSet();
        var faces = _faces.ToArray();
        foreach (var face in faces)
        {
            var copy = face.Corners.Select(corner => corner + count).ToArray();
            // The surface the thickness goes out from faces into the solid unless the copy is the
            // one below it, so one of the two is wound the other way.
            var uvs = face.Uvs is { } own ? (Vector2[])own.Clone() : null;
            if (thickness > 0)
            {
                Array.Reverse(face.Corners);
                if (face.Uvs is not null) Array.Reverse(face.Uvs);
            }
            else
            {
                Array.Reverse(copy);
                if (uvs is not null) Array.Reverse(uvs);
            }
            AddFace(copy, face.Slot, face.Smooth, uvs);

            var c = copy.Select(corner => corner - count).ToArray();
            if (thickness < 0) Array.Reverse(c);
            for (int k = 0; k < c.Length; k++)
            {
                var (a, b) = (c[k], c[(k + 1) % c.Length]);
                if (!open.Contains(a < b ? (a, b) : (b, a))) continue;
                AddFace(thickness > 0 ? [a, b, b + count, a + count] : [b, a, a + count, b + count], face.Slot, face.Smooth);
            }
        }
        Changed();
    }
}
