using System.Numerics;

namespace Engine;

// The booleans, a solid added, taken away or kept where two overlap, and the openings and rooms made
// with them, each the mesh made again from what the partition (Csg) leaves, joined back into faces
// (FaceMerge).
internal sealed partial class EditMeshData
{
    /// <summary>How near a point is to a plane to lie in it where the mesh says no other, a hundredth of a millimeter.</summary>
    public const float DefaultEpsilon = 1e-5f;

    /// <summary>How far beyond a wall's faces a window or a door is cut, a centimeter, so its sides lie in no plane of the wall's.</summary>
    public const float OpeningMargin = 0.01f;

    private static readonly HashSet<string> Solids = new(StringComparer.Ordinal) { "box", "cylinder", "cone", "wedge", "stairs", "arch", "room", "ramp" };

    /// <summary>How near a point is to a plane to lie in it, for the booleans, and ten times it how near two points are to be one.</summary>
    public float BooleanEpsilon { get; private set; } = DefaultEpsilon;

    /// <summary>Sets how near a point is to a plane to lie in it, for the booleans, above zero and up to a centimeter.</summary>
    public void Epsilon(float distance)
    {
        if (!(distance > 0 && distance <= 0.01f)) throw new ArgumentOutOfRangeException(nameof(distance), distance, "A boolean's epsilon is above zero and up to a centimeter.");
        BooleanEpsilon = distance;
        Did($"epsilon {F(distance)}");
    }

    /// <summary>Joins another solid into this one where they overlap, a model's recipe file moved by an offset or a solid primitive's line.</summary>
    /// <remarks>
    /// The faces where the two overlap are taken away and the rest joined, faces in one plane and one
    /// slot joined into one, so two boxes side by side make one box. Both are closed solids, as a
    /// boolean needs, and what is made is too. A primitive joined rather than added, as a flight of
    /// stairs standing on a floor and against a wall, is one solid with them, where a primitive
    /// added touching them is a second solid whose edges a later boolean welds to theirs, four faces
    /// then running along each and every one of them counted open.
    /// </remarks>
    public void Union(string pathOrSolid, Vector3 at) => Combine(pathOrSolid, at, Csg.Union, "union");

    /// <summary>Takes another solid away from this one, a model's recipe file moved by an offset or a solid primitive's line.</summary>
    public void Subtract(string pathOrSolid, Vector3 at) => Combine(pathOrSolid, at, Csg.Subtract, "subtract");

    /// <summary>Keeps what this solid and another both hold, a model's recipe file moved by an offset or a solid primitive's line.</summary>
    public void Intersect(string pathOrSolid, Vector3 at) => Combine(pathOrSolid, at, Csg.Intersect, "intersect");

    /// <summary>Takes a solid primitive, made where its line places it, away from this mesh, the faces it left selected.</summary>
    /// <exception cref="ArgumentException">The line is not a solid primitive's.</exception>
    public void Cut(string primitive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primitive);
        var first = primitive.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
        if (!Solids.Contains(first))
            throw new ArgumentException($"A cut is made with a solid primitive, a box, cylinder, cone, wedge, stairs, arch, room or ramp, and {first} is none.", nameof(primitive));

        var cutter = Solid(primitive);
        Apply(cutter, Csg.Subtract);
        Did($"cut {cutter._recipe[0]}");
    }

    /// <summary>Cuts a window through the wall whose face was selected first, measured on every selected face in its plane, as deep as the wall.</summary>
    /// <remarks>
    /// The opening is a box cut through the wall a centimeter past each of its faces, as deep as a
    /// ray from the face finds the wall's other side, so a room's wall is cut through and the wall
    /// across the room is not, and no side of the opening lies in a plane of the wall's, which the
    /// booleans would cut as their planes say. Its middle is <paramref name="along"/> from the wall's
    /// left edge seen from in front, and its bottom <paramref name="sill"/> above the wall's bottom.
    /// </remarks>
    /// <exception cref="InvalidOperationException">No face is selected, or the face is a floor or a ceiling rather than a wall.</exception>
    public void Window(float width, float height, float along, float sill)
    {
        Opening(width, height, along, sill, door: false);
        Did($"cut window {F(width)} {F(height)} {F(along)} {F(sill)}");
    }

    /// <summary>Cuts a door through the wall whose face was selected first, from its bottom, as deep as the wall.</summary>
    /// <remarks>As <see cref="Window"/>, the box going a centimeter below the face's bottom, so the door stands on the floor.</remarks>
    /// <exception cref="InvalidOperationException">No face is selected, or the face is a floor or a ceiling rather than a wall.</exception>
    public void Door(float width, float height, float along)
    {
        Opening(width, height, along, 0, door: true);
        Did($"cut door {F(width)} {F(height)} {F(along)}");
    }

    /// <summary>Adds a room, a box with its inside hollowed out by a thickness of wall, floor and ceiling, and selects it.</summary>
    /// <remarks>
    /// Two boxes, the outside facing out and the inside facing in, a closed solid of sixteen vertices
    /// and twelve faces holding the outside's volume less the inside's, which a window and a door are
    /// cut through.
    /// </remarks>
    public void Room(Vector3 size, float thickness, Vector3 at, bool centered)
    {
        Positive(size.X, nameof(size));
        Positive(size.Y, nameof(size));
        Positive(size.Z, nameof(size));
        Positive(thickness, nameof(thickness));
        if (2 * thickness >= MathF.Min(size.X, MathF.Min(size.Y, size.Z)))
            throw new ArgumentOutOfRangeException(nameof(thickness), thickness, "A room's walls are less than half as thick as its narrowest side.");

        var (first, firstFace) = AddPrism([new(0, 0), new(size.Z, 0), new(size.Z, size.X), new(0, size.X)], Axis.Y, 0, size.Y);
        var (inner, t) = (_faces.Count, thickness);
        AddPrism([new(t, t), new(size.Z - t, t), new(size.Z - t, size.X - t), new(t, size.X - t)], Axis.Y, t, size.Y - t);
        for (int face = inner; face < _faces.Count; face++) Array.Reverse(_faces[face].Corners);

        Placed(first, firstFace, at, centered);
        Did($"room {V(size)} {F(thickness)}{Placement(at, centered)}");
    }

    /// <summary>Adds a ramp rising along Z, a wedge given in the words of a flight of stairs, and selects it.</summary>
    public void Ramp(float rise, float run, float width, Vector3 at, bool centered)
    {
        Positive(rise, nameof(rise));
        Positive(run, nameof(run));
        Positive(width, nameof(width));

        Prism([new(0, 0), new(0, run), new(rise, run)], Axis.X, width, at, centered);
        Did($"ramp {F(rise)} {F(run)} {F(width)}{Placement(at, centered)}");
    }

    /// <summary>The nearest face a ray meets, and how far along the ray, among the faces a test allows, or nothing where it meets none.</summary>
    internal (int Face, float Distance)? Cast(Vector3 origin, Vector3 direction, Func<int, bool>? allowed = null)
    {
        (int Face, float Distance)? nearest = null;
        for (int face = 0; face < _faces.Count; face++)
        {
            if (allowed is not null && !allowed(face)) continue;
            var corners = _faces[face].Corners;
            var points = corners.Select(corner => _positions[corner]).ToArray();
            var triangles = EarClipping.Cut(points, Newell(corners));
            for (int t = 0; t < triangles.Length; t += 3)
                if (Meets(origin, direction, points[triangles[t]], points[triangles[t + 1]], points[triangles[t + 2]]) is { } distance
                    && (nearest is null || distance < nearest.Value.Distance))
                    nearest = (face, distance);
        }
        return nearest;
    }

    // Runs a boolean on this mesh and another, a model's file moved or a primitive's line, and records it.
    private void Combine(string path, Vector3 at, Func<IEnumerable<Csg.Polygon>, IEnumerable<Csg.Polygon>, double, List<Csg.Polygon>> operation, string word)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        // A solid primitive's line in the path's place, which a path of one word cannot be taken for.
        var words = path.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1 && Solids.Contains(words[0]))
        {
            if (at != Vector3.Zero) throw new ArgumentException("A primitive's line places it with its own at, and a boolean moves only a model read from a file.", nameof(at));
            var solid = Solid(path);
            Apply(solid, operation);
            Did($"{word} {solid._recipe[0]}");
            return;
        }

        if (path.Any(char.IsWhiteSpace)) throw new ArgumentException("A model's path in a boolean is one word, with no space in it, or a solid primitive's line.", nameof(path));
        var other = Nested(path);
        for (int vertex = 0; vertex < other._positions.Count; vertex++) other._positions[vertex] += at;
        Apply(other, operation);
        Did($"{word} {path}{(at == Vector3.Zero ? "" : " at " + V(at))}");
    }

    // Makes this mesh again from what a boolean of it and another leaves, the other's faces selected,
    // its slots added by name.
    private void Apply(EditMeshData other, Func<IEnumerable<Csg.Polygon>, IEnumerable<Csg.Polygon>, double, List<Csg.Polygon>> operation)
    {
        var epsilon = (double)BooleanEpsilon;
        var pieces = operation(Polygons(this, second: false), Polygons(other, second: true), epsilon);
        var (points, faces) = FaceMerge.Faces(pieces, epsilon * 10);

        _positions.Clear();
        _positions.AddRange(points.Select(point => new Vector3((float)point.X, (float)point.Y, (float)point.Z)));
        _faces.Clear();
        _selectedFaces.Clear();
        _selectedVertices.Clear();
        foreach (var (corners, source) in faces)
        {
            var face = AddFace(corners, SlotIndex(source.Slot), source.Smooth);
            if (!source.Second) continue;
            _selectedFaces.Add(face);
            _selectedVertices.UnionWith(corners);
        }
        Compact([]);
    }

    // A mesh's faces as convex polygons for the partition, each face cut into triangles, with what it came from.
    private static IEnumerable<Csg.Polygon> Polygons(EditMeshData mesh, bool second)
    {
        foreach (var face in mesh._faces)
        {
            var points = face.Corners.Select(corner => mesh._positions[corner]).ToArray();
            var triangles = EarClipping.Cut(points, mesh.Newell(face.Corners));
            var source = new Csg.Source(mesh._slots[face.Slot], face.Smooth, second);
            for (int t = 0; t < triangles.Length; t += 3)
            {
                var (a, b, c) = (Point(points[triangles[t]]), Point(points[triangles[t + 1]]), Point(points[triangles[t + 2]]));
                if (Csg.Point.Cross(b - a, c - a).Length < 1e-12) continue;
                yield return new Csg.Polygon([a, b, c], source);
            }
        }

        static Csg.Point Point(Vector3 at) => new(at.X, at.Y, at.Z);
    }

    // Cuts a box through the wall whose face was selected first, from a height above its bottom.
    private void Opening(float width, float height, float along, float bottom, bool door)
    {
        Positive(width, nameof(width));
        Positive(height, nameof(height));
        if (!float.IsFinite(along)) throw new ArgumentOutOfRangeException(nameof(along), along, "An opening's place along the wall is a finite number.");
        if (!(bottom >= 0) || !float.IsFinite(bottom)) throw new ArgumentOutOfRangeException(nameof(bottom), bottom, "A window's sill is zero or more.");
        if (_selectedFaces.Count == 0) throw new InvalidOperationException("A window or a door is cut through the wall whose face is selected first, and no face is selected.");

        var face = _selectedFaces.Min();
        var normal = Normal(face);
        if (MathF.Abs(normal.Y) > 0.7f) throw new InvalidOperationException("A window or a door is cut through a wall, a face facing sideways, and the face selected first is a floor or a ceiling.");

        // The wall's own frame, right along it seen from in front, up the world's up made square to
        // it, and out along its normal, which turns a box's corners into the wall's as they are.
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, normal));
        var up = Vector3.Cross(normal, right);
        var plane = Vector3.Dot(normal, _positions[_faces[face].Corners[0]]);

        // The wall is every selected face in the first one's plane, so a wall an earlier opening cut
        // into pieces is measured whole, from its left edge and its bottom.
        var corners = _selectedFaces
            .Where(other => Vector3.Dot(Normal(other), normal) > 0.9999f && MathF.Abs(Vector3.Dot(normal, _positions[_faces[other].Corners[0]]) - plane) < 1e-3f)
            .SelectMany(other => _faces[other].Corners.Select(corner => _positions[corner]))
            .ToArray();
        var left = corners.Min(point => Vector3.Dot(point, right));
        var low = corners.Min(point => Vector3.Dot(point, up));

        var foot = door ? low - OpeningMargin : low + bottom;
        var tall = door ? height + OpeningMargin : height;
        var middle = right * (left + along) + up * (foot + tall / 2) + normal * plane;

        // As deep as the wall, the distance from the face to the nearest face behind it the ray leaves
        // the wall through, or the whole mesh's depth where it leaves through none.
        var start = middle + normal * OpeningMargin;
        var bounds = Bounds;
        var depth = Cast(start, -normal, other => other != face && Vector3.Dot(Normal(other), -normal) > 0) is { } far
            ? far.Distance - OpeningMargin
            : (bounds.Max - bounds.Min).Length();

        var cutter = new EditMeshData();
        cutter.Box(new Vector3(width, tall, depth + 2 * OpeningMargin), Vector3.Zero, false);
        cutter._slots[0] = _slots[0];
        var origin = right * (left + along - width / 2) + up * foot + normal * (plane - depth - OpeningMargin);
        for (int vertex = 0; vertex < cutter._positions.Count; vertex++)
        {
            var local = cutter._positions[vertex];
            cutter._positions[vertex] = origin + right * local.X + up * local.Y + normal * local.Z;
        }
        Apply(cutter, Csg.Subtract);
    }

    // Where a ray meets a triangle from either side, by Möller and Trumbore's test, or nothing.
    private static float? Meets(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c)
    {
        var (edge, other) = (b - a, c - a);
        var p = Vector3.Cross(direction, other);
        var det = Vector3.Dot(edge, p);
        if (MathF.Abs(det) < 1e-9f) return null;

        var inverse = 1 / det;
        var s = origin - a;
        var u = Vector3.Dot(s, p) * inverse;
        if (u < 0 || u > 1) return null;
        var q = Vector3.Cross(s, edge);
        var v = Vector3.Dot(direction, q) * inverse;
        if (v < 0 || u + v > 1) return null;
        var t = Vector3.Dot(other, q) * inverse;
        return t > 0 ? t : null;
    }

    // A solid primitive made from its line for a boolean, its faces in this mesh's first slot, so a
    // mesh whose first slot was renamed takes no slot called default from it.
    private EditMeshData Solid(string line)
    {
        var solid = new EditMeshData();
        solid.Run(line);
        solid._slots[0] = _slots[0];
        return solid;
    }
}
