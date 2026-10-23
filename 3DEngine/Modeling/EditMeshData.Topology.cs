using System.Numerics;

namespace Engine;

// The operations that change which vertices and faces there are, rather than where they are.
internal sealed partial class EditMeshData
{
    // How many recipes a join is reading inside one another, so a model that joins itself is
    // refused rather than read until the stack runs out.
    [ThreadStatic]
    private static int _joinDepth;

    /// <summary>Pushes the selected faces out along their normals, the sides between them and what they left filled.</summary>
    /// <remarks>
    /// <para>
    /// The faces selected move together as one region, so two faces side by side stay joined and a
    /// side is filled along the region's edge alone. A corner where faces of the region meet at an
    /// angle moves so each of them goes the whole distance along its own normal, so the top and a
    /// side of a box pushed out by a meter stand a meter out each, rather than less along a corner's
    /// mean.
    /// </para>
    /// <para>
    /// The region's faces are selected after, moved, so another extrude pushes them further. A face
    /// whose edge was open, as a plane's are, takes a side there too, so a plane pushed up is a box
    /// open underneath.
    /// </para>
    /// </remarks>
    public void Extrude(float distance)
    {
        if (!float.IsFinite(distance)) throw new ArgumentOutOfRangeException(nameof(distance), distance, "An extrude's distance is a finite number.");

        ExtrudeRegion(normals => Offset(normals, distance));
        Did($"extrude {F(distance)}");
    }

    /// <summary>Pushes the selected faces out by one vector, every corner of the region moved by it, as a wall is pushed up from a floor's edge.</summary>
    public void Extrude(Vector3 by)
    {
        if (!float.IsFinite(by.X) || !float.IsFinite(by.Y) || !float.IsFinite(by.Z))
            throw new ArgumentOutOfRangeException(nameof(by), by, "An extrude is a finite distance along each axis.");

        ExtrudeRegion(_ => by);
        Did($"extrude {V(by)}");
    }

    /// <summary>Makes each selected face a smaller face inside it, with a band of faces round it.</summary>
    /// <remarks>
    /// Each face on its own, its corners moved in along the line halfway between their two edges so
    /// each edge moves in by the distance, the smaller faces selected after, so an extrude after an
    /// inset pulls out a panel or pushes in a window's recess. A distance as wide as the face turns
    /// the smaller face inside out, which the operation leaves as asked.
    /// </remarks>
    public void Inset(float distance)
    {
        if (!(distance > 0) || !float.IsFinite(distance)) throw new ArgumentOutOfRangeException(nameof(distance), distance, "An inset's distance is a finite number above zero.");

        var picked = _selectedFaces.Order().ToArray();
        _selectedVertices.Clear();
        foreach (var index in picked)
        {
            var face = _faces[index];
            var corners = face.Corners;
            var normal = Unit(Newell(corners));
            var inner = new int[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                var before = _positions[corners[(i + corners.Length - 1) % corners.Length]];
                var at = _positions[corners[i]];
                var after = _positions[corners[(i + 1) % corners.Length]];

                // Inward of an edge running counterclockwise about the normal is the normal crossed
                // with it, and the corner moves so it is the distance in from both its edges.
                var inFromBefore = Unit(Vector3.Cross(normal, at - before));
                var inFromAfter = Unit(Vector3.Cross(normal, after - at));
                var meet = 1 + Vector3.Dot(inFromBefore, inFromAfter);
                var offset = meet > 1e-4f ? (inFromBefore + inFromAfter) * (distance / meet) : inFromBefore * distance;
                inner[i] = AddVertex(at + offset);
            }
            for (int i = 0; i < corners.Length; i++)
            {
                var next = (i + 1) % corners.Length;
                AddFace([corners[i], corners[next], inner[next], inner[i]], face.Slot, face.Smooth);
            }
            face.Corners = inner;
            _selectedVertices.UnionWith(inner);
        }

        Changed();
        Did($"inset {F(distance)}");
    }

    /// <summary>Takes away the selected faces, or where none is selected the selected vertices with every face using them.</summary>
    /// <remarks>
    /// A vertex no face uses after is taken away too. A hole filled takes a face running round it the
    /// way the faces taken away did, so it faces the way they faced, and the faces filling are
    /// selected after, where nothing is otherwise.
    /// </remarks>
    public void Delete(bool fill)
    {
        var going = _selectedFaces.Count > 0
            ? _selectedFaces.ToHashSet()
            : Enumerable.Range(0, _faces.Count).Where(face => _faces[face].Corners.Any(_selectedVertices.Contains)).ToHashSet();
        var vertices = _selectedFaces.Count > 0 ? [] : _selectedVertices.ToHashSet();

        // The edges of the faces taken away that a face staying runs along the other way, which are
        // the rims of the holes, followed round into loops.
        var caps = new List<(int[] Corners, int Slot)>();
        if (fill)
        {
            var kept = new HashSet<(int, int)>();
            for (int face = 0; face < _faces.Count; face++)
            {
                if (going.Contains(face)) continue;
                var corners = _faces[face].Corners;
                for (int i = 0; i < corners.Length; i++) kept.Add((corners[i], corners[(i + 1) % corners.Length]));
            }

            var rim = new Dictionary<int, (int To, int Slot)>();
            foreach (var face in going.Order())
            {
                var corners = _faces[face].Corners;
                for (int i = 0; i < corners.Length; i++)
                {
                    var (from, to) = (corners[i], corners[(i + 1) % corners.Length]);
                    if (kept.Contains((to, from))) rim.TryAdd(from, (to, _faces[face].Slot));
                }
            }

            while (rim.Count > 0)
            {
                var start = rim.Keys.Min();
                var loop = new List<int> { start };
                var slot = rim[start].Slot;
                var at = start;
                while (rim.Remove(at, out var step) && step.To != start)
                {
                    loop.Add(step.To);
                    at = step.To;
                }
                if (loop.Count >= 3) caps.Add((loop.ToArray(), slot));
            }
        }

        RemoveFaces(going);
        _selectedVertices.Clear();
        _selectedFaces.Clear();
        foreach (var (corners, slot) in caps) _selectedFaces.Add(AddFace(corners, slot));
        foreach (var face in _selectedFaces) _selectedVertices.UnionWith(_faces[face].Corners);

        Compact(vertices);
        Did(fill ? "delete fill" : "delete");
    }

    /// <summary>Joins the selected vertices nearer one another than a distance into one, where the first of them is.</summary>
    /// <remarks>
    /// A face left with a corner twice in a row loses the second, and one left with fewer than three
    /// corners is taken away, as a side between two faces moved onto each other is. What is joined is
    /// the vertex of the lowest index among those near it, so a recipe joins the same way each time
    /// it is run.
    /// </remarks>
    public void Merge(float distance)
    {
        if (!(distance >= 0) || !float.IsFinite(distance)) throw new ArgumentOutOfRangeException(nameof(distance), distance, "A merge's distance is a finite number, zero or more.");

        // Each vertex goes to the first kept one within the distance, found among those in its own
        // cell of a grid as wide as the distance and the cells beside it.
        var cell = Math.Max(distance, 1e-6f);
        var cells = new Dictionary<(long, long, long), List<int>>();
        var into = new Dictionary<int, int>();
        foreach (var vertex in SelectedVertices())
        {
            var point = _positions[vertex];
            var (cx, cy, cz) = ((long)MathF.Floor(point.X / cell), (long)MathF.Floor(point.Y / cell), (long)MathF.Floor(point.Z / cell));
            var found = -1;
            for (int dx = -1; dx <= 1 && found < 0; dx++)
                for (int dy = -1; dy <= 1 && found < 0; dy++)
                    for (int dz = -1; dz <= 1 && found < 0; dz++)
                    {
                        if (!cells.TryGetValue((cx + dx, cy + dy, cz + dz), out var near)) continue;
                        foreach (var kept in near)
                        {
                            if ((_positions[kept] - point).Length() > distance) continue;
                            found = kept;
                            break;
                        }
                    }

            if (found >= 0)
            {
                into[vertex] = found;
                continue;
            }
            if (!cells.TryGetValue((cx, cy, cz), out var here)) cells[(cx, cy, cz)] = here = [];
            here.Add(vertex);
        }

        if (into.Count > 0)
        {
            var gone = new HashSet<int>();
            for (int index = 0; index < _faces.Count; index++)
            {
                var face = _faces[index];
                var corners = face.Corners.Select(corner => into.GetValueOrDefault(corner, corner)).ToList();
                // A corner the same as the one after it, round to the first, said once.
                for (int i = corners.Count - 1; i >= 0 && corners.Count > 1; i--)
                    if (corners[i] == corners[(i + 1) % corners.Count]) corners.RemoveAt(i);
                if (corners.Count < 3) gone.Add(index);
                else face.Corners = [.. corners];
            }
            _selectedVertices.ExceptWith(into.Keys);
            RemoveFaces(gone);
        }

        Compact([]);
        Did($"merge {F(distance)}");
    }

    /// <summary>Turns the selected faces to face the other way, their corners run backwards.</summary>
    public void Flip()
    {
        foreach (var face in _selectedFaces) Array.Reverse(_faces[face].Corners);
        Changed();
        Did("flip");
    }

    /// <summary>Cuts each selected face of more than three corners into triangles, as the build cuts it.</summary>
    /// <remarks>
    /// By ear clipping, so a concave face is cut inside its outline, the first triangle kept at the
    /// face's index and the rest after the last face, each in the face's slot. The triangles are
    /// what is selected after.
    /// </remarks>
    public void Triangulate()
    {
        foreach (var index in _selectedFaces.Order().ToArray())
        {
            var face = _faces[index];
            if (face.Corners.Length == 3) continue;

            var corners = face.Corners;
            var triangles = EarClipping.Cut(corners.Select(corner => _positions[corner]).ToArray(), Newell(corners));
            face.Corners = [corners[triangles[0]], corners[triangles[1]], corners[triangles[2]]];
            for (int t = 3; t < triangles.Length; t += 3)
                _selectedFaces.Add(AddFace([corners[triangles[t]], corners[triangles[t + 1]], corners[triangles[t + 2]]], face.Slot, face.Smooth));
        }
        Changed();
        Did("triangulate");
    }

    /// <summary>Copies the selected faces where they are, with vertices of their own, and selects the copy, which moves without the first.</summary>
    public void Duplicate()
    {
        var copies = new Dictionary<int, int>();
        var made = new List<int>();
        foreach (var index in _selectedFaces.Order().ToArray())
        {
            var face = _faces[index];
            var corners = face.Corners.Select(corner =>
            {
                if (!copies.TryGetValue(corner, out var copy)) copies[corner] = copy = AddVertex(_positions[corner]);
                return copy;
            }).ToArray();
            made.Add(AddFace(corners, face.Slot, face.Smooth));
        }

        _selectedFaces.Clear();
        _selectedFaces.UnionWith(made);
        _selectedVertices.Clear();
        _selectedVertices.UnionWith(copies.Values);
        Changed();
        Did("duplicate");
    }

    /// <summary>Adds another model's mesh, made from its recipe's file, to this one, and selects it.</summary>
    /// <remarks>
    /// The other model's faces keep their slots by name, a slot this mesh has none of being made. The
    /// recipe keeps the file's path rather than what it held, so a change to the joined model reaches
    /// every model that joins it the next time each is made.
    /// </remarks>
    /// <exception cref="FileNotFoundException">There is no such model.</exception>
    /// <exception cref="FormatException">A line of the model's recipe is not an operation, or the model joins itself.</exception>
    public void Join(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (path.Any(char.IsWhiteSpace)) throw new ArgumentException("A joined model's path is one word, with no space in it.", nameof(path));

        var other = Nested(path);
        var offset = _positions.Count;
        _positions.AddRange(other._positions);
        var made = new List<int>();
        foreach (var face in other._faces)
            made.Add(AddFace(face.Corners.Select(corner => corner + offset).ToArray(), SlotIndex(other._slots[face.Slot]), face.Smooth));

        _selectedFaces.Clear();
        _selectedFaces.UnionWith(made);
        _selectedVertices.Clear();
        _selectedVertices.UnionWith(Enumerable.Range(offset, other._positions.Count));
        Changed();
        Did($"join {path}");
    }

    /// <summary>A model made from its file inside a join, refused where models are read inside one another sixteen deep.</summary>
    private static EditMeshData Nested(string path)
    {
        if (_joinDepth >= 16) throw new InvalidOperationException($"{path} reads models inside one another sixteen deep, which is a model reading itself.");
        _joinDepth++;
        try
        {
            return Load(path);
        }
        finally
        {
            _joinDepth--;
        }
    }

    /// <summary>
    /// Pushes out the selected faces as a region, each new corner moved by what <paramref name="offset"/>
    /// answers for the normals of the region's faces round it.
    /// </summary>
    private void ExtrudeRegion(Func<List<Vector3>, Vector3> offset)
    {
        var region = _selectedFaces.Order().ToArray();
        if (region.Length == 0) return;

        var inside = new HashSet<(int, int)>();
        foreach (var face in region)
        {
            var corners = _faces[face].Corners;
            for (int i = 0; i < corners.Length; i++) inside.Add((corners[i], corners[(i + 1) % corners.Length]));
        }

        // The region's rim, its edges no other face of it runs along the other way, in the order the
        // faces have them, and the normals of the region's faces round each of its corners.
        var rim = new List<(int From, int To, int Slot, float Smooth)>();
        var normals = new Dictionary<int, List<Vector3>>();
        foreach (var face in region)
        {
            var corners = _faces[face].Corners;
            var normal = Unit(Newell(corners));
            for (int i = 0; i < corners.Length; i++)
            {
                var (from, to) = (corners[i], corners[(i + 1) % corners.Length]);
                if (!inside.Contains((to, from))) rim.Add((from, to, _faces[face].Slot, _faces[face].Smooth));
                if (!normals.TryGetValue(from, out var around)) normals[from] = around = [];
                if (!around.Any(known => (known - normal).LengthSquared() < 1e-8f)) around.Add(normal);
            }
        }

        // A corner on the rim is left where it is for the faces outside the region, and the region
        // takes a copy of it, moved, while a corner inside the region moves itself.
        var copies = new Dictionary<int, int>();
        foreach (var (from, to, _, _) in rim)
        {
            if (!copies.ContainsKey(from)) copies[from] = AddVertex(_positions[from]);
            if (!copies.ContainsKey(to)) copies[to] = AddVertex(_positions[to]);
        }
        foreach (var (corner, around) in normals)
        {
            var moved = copies.GetValueOrDefault(corner, corner);
            _positions[moved] += offset(around);
        }
        foreach (var face in region)
        {
            var corners = _faces[face].Corners;
            for (int i = 0; i < corners.Length; i++) corners[i] = copies.GetValueOrDefault(corners[i], corners[i]);
        }
        foreach (var (from, to, slot, smooth) in rim) AddFace([from, to, copies[to], copies[from]], slot, smooth);

        _selectedVertices.Clear();
        foreach (var face in region) _selectedVertices.UnionWith(_faces[face].Corners);
        Changed();
    }

    /// <summary>
    /// The move that puts a corner a distance out along each of the normals of the faces round it,
    /// the nearest to it in least squares where they cannot all be met, as at the tip of a pyramid.
    /// </summary>
    private static Vector3 Offset(List<Vector3> normals, float distance)
    {
        if (normals.Count == 1) return normals[0] * distance;
        if (normals.Count == 2)
        {
            var meet = 1 + Vector3.Dot(normals[0], normals[1]);
            return meet > 1e-4f ? (normals[0] + normals[1]) * (distance / meet) : normals[0] * distance;
        }

        // The sum of each normal's outer product with itself, and of the normals, the normal
        // equations of v · n = d for every n, solved by Cramer's rule.
        double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0, bx = 0, by = 0, bz = 0;
        foreach (var n in normals)
        {
            xx += n.X * n.X; xy += n.X * n.Y; xz += n.X * n.Z;
            yy += n.Y * n.Y; yz += n.Y * n.Z; zz += n.Z * n.Z;
            bx += n.X * distance; by += n.Y * distance; bz += n.Z * distance;
        }

        var det = xx * (yy * zz - yz * yz) - xy * (xy * zz - yz * xz) + xz * (xy * yz - yy * xz);
        if (Math.Abs(det) < 1e-9)
        {
            var mean = Vector3.Zero;
            foreach (var n in normals) mean += n;
            return Unit(mean) * distance;
        }
        var x = (bx * (yy * zz - yz * yz) - xy * (by * zz - yz * bz) + xz * (by * yz - yy * bz)) / det;
        var y = (xx * (by * zz - yz * bz) - bx * (xy * zz - yz * xz) + xz * (xy * bz - by * xz)) / det;
        var z = (xx * (yy * bz - by * yz) - xy * (xy * bz - by * xz) + bx * (xy * yz - yy * xz)) / det;
        return new Vector3((float)x, (float)y, (float)z);
    }

    /// <summary>Takes faces away, the selection's indices moved to where the faces after them went.</summary>
    private void RemoveFaces(HashSet<int> going)
    {
        if (going.Count == 0) return;

        var moved = new Dictionary<int, int>();
        var kept = new List<Face>(_faces.Count - going.Count);
        for (int face = 0; face < _faces.Count; face++)
        {
            if (going.Contains(face)) continue;
            moved[face] = kept.Count;
            kept.Add(_faces[face]);
        }
        _faces.Clear();
        _faces.AddRange(kept);

        var selected = _selectedFaces.Where(moved.ContainsKey).Select(face => moved[face]).ToArray();
        _selectedFaces.Clear();
        _selectedFaces.UnionWith(selected);
        Changed();
    }

    /// <summary>Takes away the vertices given and every vertex no face uses, and numbers the rest again in order.</summary>
    private void Compact(HashSet<int> going)
    {
        var used = new HashSet<int>();
        foreach (var face in _faces) used.UnionWith(face.Corners);

        var moved = new int[_positions.Count];
        var kept = new List<Vector3>(used.Count);
        for (int vertex = 0; vertex < _positions.Count; vertex++)
        {
            if (!used.Contains(vertex) || going.Contains(vertex))
            {
                moved[vertex] = -1;
                continue;
            }
            moved[vertex] = kept.Count;
            kept.Add(_positions[vertex]);
        }
        _positions.Clear();
        _positions.AddRange(kept);
        foreach (var face in _faces)
            for (int i = 0; i < face.Corners.Length; i++) face.Corners[i] = moved[face.Corners[i]];

        var selected = _selectedVertices.Where(vertex => moved[vertex] >= 0).Select(vertex => moved[vertex]).ToArray();
        _selectedVertices.Clear();
        _selectedVertices.UnionWith(selected);
        Changed();
    }
}
