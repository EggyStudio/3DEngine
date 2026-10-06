using System.Numerics;

namespace Engine;

/// <summary>
/// The par_shapes library's meshes as far as raylib makes its spheres, hemispheres, cylinders,
/// cones, tori and knots from them: a surface over a grid, a texture coordinate of the grid at each
/// point, normals averaged across the seams, and par_shapes' scale, turn, move and merge, so a
/// texture lies on them as it does on raylib's.
/// </summary>
internal sealed class ParShape
{
    public readonly List<Vector3> Points = [];
    public readonly List<Vector3> Normals = [];
    public readonly List<Vector2> Uvs = [];
    public readonly List<int> Triangles = [];

    // par_shapes' PAR_PI, a double, which its parameters are scaled by before they are made floats.
    public const double Pi = 3.14159265359;

    /// <summary>
    /// par_shapes_create_parametric: <paramref name="surface"/> over a grid of
    /// <paramref name="slices"/> by <paramref name="stacks"/>, given the stack's share and the
    /// slice's, each from 0 to 1, which are the texture coordinate too.
    /// </summary>
    public static ParShape Parametric(Func<float, float, Vector3> surface, int slices, int stacks)
    {
        var shape = new ParShape();
        for (int stack = 0; stack < stacks + 1; stack++)
        {
            var u = (float)stack / stacks;
            for (int slice = 0; slice < slices + 1; slice++)
            {
                var v = (float)slice / slices;
                shape.Points.Add(surface(u, v));
                shape.Uvs.Add(new Vector2(u, v));
            }
        }

        var row = 0;
        for (int stack = 0; stack < stacks; stack++, row += slices + 1)
            for (int slice = 0; slice < slices; slice++)
            {
                var next = slice + 1;
                shape.Triangles.AddRange([row + slice + slices + 1, row + next, row + slice,
                    row + slice + slices + 1, row + next + slices + 1, row + next]);
            }

        shape.WeldedNormals();
        return shape;
    }

    /// <summary>par_shapes_create_disk: a fan of <paramref name="slices"/> about the origin, turned to face <paramref name="normal"/>.</summary>
    /// <remarks>
    /// par_shapes turns the disk about the axis across its own normal and the normal asked for,
    /// which for a normal straight along z is of no length, so it is left as it is facing +z and
    /// turned half a turn about nothing, every point and normal negated, facing -z. raylib's caps
    /// are made so, and their turns after put them where they belong.
    /// </remarks>
    public static ParShape Disk(float radius, int slices, Vector3 normal)
    {
        var shape = new ParShape();
        shape.Points.Add(Vector3.Zero);
        for (int i = 0; i < slices; i++)
        {
            var theta = (float)(i * Pi * 2 / slices);
            shape.Points.Add(new Vector3(radius * MathF.Cos(theta), radius * MathF.Sin(theta), 0));
        }
        var n = Normalize(normal);
        for (int i = 0; i < shape.Points.Count; i++) shape.Normals.Add(n);
        for (int i = 0; i < slices; i++) shape.Triangles.AddRange([0, 1 + i, 1 + (i + 1) % slices]);
        shape.Rotate(MathF.Acos(n.Z), Normalize(Vector3.Cross(n, -Vector3.UnitZ)));
        return shape;
    }

    /// <summary>Gives every point the same texture coordinate, as raylib does a cap's.</summary>
    public void FillUvs(float value)
    {
        Uvs.Clear();
        for (int i = 0; i < Points.Count; i++) Uvs.Add(new Vector2(value));
    }

    /// <summary>par_shapes_scale: points scaled, and normals by the inverse where the scale differs along the axes.</summary>
    public void Scale(float x, float y, float z)
    {
        for (int i = 0; i < Points.Count; i++) Points[i] *= new Vector3(x, y, z);
        if (Normals.Count == 0 || (x == y && y == z)) return;
        Vector3 inverse;
        if (x != 0 && y != 0 && z != 0) inverse = new Vector3(1 / x, 1 / y, 1 / z);
        else inverse = new Vector3(x == 0 && y != 0 && z != 0 ? 1 : 0, y == 0 && x != 0 && z != 0 ? 1 : 0, z == 0 && x != 0 && y != 0 ? 1 : 0);
        for (int i = 0; i < Normals.Count; i++) Normals[i] = Normalize(Normals[i] * inverse);
    }

    /// <summary>par_shapes_rotate: points and normals turned <paramref name="radians"/> about <paramref name="axis"/>.</summary>
    public void Rotate(float radians, Vector3 axis)
    {
        float s = MathF.Sin(radians), c = MathF.Cos(radians);
        float x = axis.X, y = axis.Y, z = axis.Z, k = 1 - c;
        var col0 = new Vector3(x * x * k + c, x * y * k + z * s, z * x * k - y * s);
        var col1 = new Vector3(x * y * k - z * s, y * y * k + c, y * z * k + x * s);
        var col2 = new Vector3(z * x * k + y * s, y * z * k - x * s, z * z * k + c);
        Vector3 Turn(Vector3 p) => col0 * p.X + col1 * p.Y + col2 * p.Z;
        for (int i = 0; i < Points.Count; i++) Points[i] = Turn(Points[i]);
        for (int i = 0; i < Normals.Count; i++) Normals[i] = Turn(Normals[i]);
    }

    /// <summary>par_shapes_translate.</summary>
    public void Translate(float x, float y, float z)
    {
        for (int i = 0; i < Points.Count; i++) Points[i] += new Vector3(x, y, z);
    }

    /// <summary>par_shapes_merge: another shape's points and triangles added after this one's.</summary>
    public void Merge(ParShape other)
    {
        var offset = Points.Count;
        Points.AddRange(other.Points);
        Normals.AddRange(other.Normals);
        Uvs.AddRange(other.Uvs);
        foreach (var index in other.Triangles) Triangles.Add(offset + index);
    }

    /// <summary>The shape as a mesh of the engine's, its points shared by their triangles.</summary>
    public ModelMesh Upload()
    {
        var vertices = new ModelVertex[Points.Count];
        for (int i = 0; i < vertices.Length; i++) vertices[i] = new ModelVertex(Points[i], Normals[i], Uvs[i]);
        return Engine3D.UploadMesh(vertices, [.. Triangles.Select(t => (uint)t)]);
    }

    // par_shapes__compute_welded_normals. Points closer than a thousandth of a cell of the grid
    // par_shapes sorts them on, 19 cells across the shape's box each way, count as one, so the two
    // copies of a point along a seam, and a pole's ring of them, take the normal of every face
    // around them, each face weighted by its area.
    private void WeldedNormals()
    {
        Vector3 min = Points[0], max = Points[0];
        foreach (var p in Points)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        static float Cells(float low, float high) => high == low ? 1 : 19 / (high - low);
        var scale = new Vector3(Cells(min.X, max.X), Cells(min.Y, max.Y), Cells(min.Z, max.Z));
        const float Epsilon = 0.001f;

        var weld = new int[Points.Count];
        var buckets = new Dictionary<(int, int, int), List<int>>();
        for (int i = 0; i < Points.Count; i++)
        {
            var p = (Points[i] - min) * scale;
            var key = ((int)MathF.Floor(p.X / Epsilon), (int)MathF.Floor(p.Y / Epsilon), (int)MathF.Floor(p.Z / Epsilon));
            weld[i] = i;
            for (int dx = -1; dx <= 1 && weld[i] == i; dx++)
            for (int dy = -1; dy <= 1 && weld[i] == i; dy++)
            for (int dz = -1; dz <= 1 && weld[i] == i; dz++)
                if (buckets.TryGetValue((key.Item1 + dx, key.Item2 + dy, key.Item3 + dz), out var near))
                    foreach (var j in near)
                        if (Vector3.Distance((Points[j] - min) * scale, p) < Epsilon)
                        {
                            weld[i] = weld[j];
                            break;
                        }
            if (!buckets.TryGetValue(key, out var bucket)) buckets[key] = bucket = [];
            bucket.Add(i);
        }

        var sums = new Vector3[Points.Count];
        for (int t = 0; t < Triangles.Count; t += 3)
        {
            int a = weld[Triangles[t]], b = weld[Triangles[t + 1]], c = weld[Triangles[t + 2]];
            var area = Vector3.Cross(Points[b] - Points[a], Points[c] - Points[a]);
            sums[a] += area;
            sums[b] += area;
            sums[c] += area;
        }
        Normals.Clear();
        for (int i = 0; i < Points.Count; i++) Normals.Add(Normalize(sums[weld[i]]));
    }

    // par_shapes__normalize3, which leaves a vector of no length as it is.
    private static Vector3 Normalize(Vector3 v)
    {
        var length = v.Length();
        return length > 0 ? v / length : v;
    }
}
