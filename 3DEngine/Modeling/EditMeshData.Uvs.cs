using System.Numerics;

namespace Engine;

// The faces' texture coordinates, projected onto the selected faces along a direction, from a box,
// about a cylinder's axis or round a sphere's middle, moved, scaled and turned, set to a texel
// density and put back to the world-aligned default, each a line of the recipe, so a saved model's
// coordinates are made again with its shape. The same words make the same coordinates in
// BevyCSharp's edit mesh.
internal sealed partial class EditMeshData
{
    /// <summary>
    /// Texture coordinates as an affine function of a point, which a face's own coordinates are
    /// fitted to, so a point a cut or an inset makes on the face takes the coordinates the face had
    /// there.
    /// </summary>
    internal readonly record struct UvMap(Vector3 U, float UOffset, Vector3 V, float VOffset)
    {
        public Vector2 At(Vector3 point) => new(Vector3.Dot(U, point) + UOffset, Vector3.Dot(V, point) + VOffset);
    }

    /// <summary>
    /// A face's coordinates as they were before an operation changed its corners, which the face, or
    /// a piece cut from it, takes again for the corners it keeps, and from its fit for the corners it
    /// gains.
    /// </summary>
    private readonly record struct Carried(int[] Corners, Vector2[]? Uvs, UvMap? Fit)
    {
        public Vector2[]? For(int[] corners, List<Vector3> positions)
        {
            if (Uvs is null) return null;
            var uvs = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                var kept = Array.IndexOf(Corners, corners[i]);
                uvs[i] = kept >= 0 ? Uvs[kept] : Fit?.At(positions[corners[i]]) ?? default;
            }
            return uvs;
        }
    }

    /// <summary>A face's coordinates as they are, to be carried to the corners an operation gives it or a piece of it.</summary>
    private Carried CarryOf(Face face) => new([.. face.Corners], face.Uvs is { } uvs ? [.. uvs] : null, FitOf(face));

    /// <summary>Gives a face new corners, its own coordinates carried to them.</summary>
    private void Recorner(Face face, int[] corners)
    {
        if (face.Uvs is null)
        {
            face.Corners = corners;
            return;
        }
        var carried = CarryOf(face);
        face.Corners = corners;
        face.Uvs = carried.For(corners, _positions);
    }

    /// <summary>A face cut from another, in its slot and smoothing, its own coordinates carried from it.</summary>
    private Face Piece(Carried from, Face face, int[] corners) => new(corners, face.Slot, face.Smooth, from.For(corners, _positions));

    /// <summary>The texture coordinates of a face's corners, in their order, its own or the world-aligned default.</summary>
    /// <remarks>
    /// The default is each corner's place in meters seen along the axis the face's normal is
    /// nearest, which follows the face wherever it moves, so a moved wall shows the grid of the
    /// place it was moved to. A face given its own by a <c>uv</c> operation keeps them on its
    /// corners as they move.
    /// </remarks>
    public Vector2[] UvsOf(int face)
    {
        var known = _faces[face];
        return known.Uvs is { } own ? [.. own] : DefaultUvs(known);
    }

    /// <summary>Whether a face has texture coordinates of its own, given by a <c>uv</c> operation, rather than the world-aligned default.</summary>
    public bool HasOwnUvs(int face) => _faces[face].Uvs is not null;

    /// <summary>Gives the selected faces coordinates by a planar projection along a direction, in meters.</summary>
    /// <param name="facing">The way the projection looks back along, from the faces toward its plane, such as up for a floor.</param>
    /// <remarks>
    /// In meters, as the default is, the picture's first coordinate running to the right as seen
    /// from that side and its second down, the world's up being up in the picture where the
    /// direction is not straight up or down, so a projection along an axis gives a face facing that
    /// axis its default coordinates.
    /// </remarks>
    /// <exception cref="ArgumentException">The direction is zero or not finite.</exception>
    public void UvPlanar(Vector3 facing)
    {
        if (facing == Vector3.Zero || !float.IsFinite(facing.X + facing.Y + facing.Z))
            throw new ArgumentException("A direction to project along is finite and not zero.", nameof(facing));

        var (u, v) = PlanarAxes(Vector3.Normalize(facing));
        foreach (var index in _selectedFaces)
        {
            var face = _faces[index];
            face.Uvs = face.Corners.Select(corner => new Vector2(Vector3.Dot(_positions[corner], u), Vector3.Dot(_positions[corner], v))).ToArray();
        }

        Changed();
        Did($"uv planar {Facing(facing)}");
    }

    /// <summary>Gives the selected faces the world-aligned default as coordinates of their own, which then follow their corners.</summary>
    /// <remarks>
    /// The box projection, each face along the axis its normal is nearest, which is the default,
    /// written onto the faces so a move or a cut after it carries the coordinates with the face.
    /// </remarks>
    public void UvBox()
    {
        foreach (var index in _selectedFaces) _faces[index].Uvs = DefaultUvs(_faces[index]);

        Changed();
        Did("uv box");
    }

    /// <summary>Gives the selected faces coordinates by a cylindrical projection about an axis.</summary>
    /// <param name="axis">The axis, any length but zero.</param>
    /// <param name="center">A point the axis passes through, or null for the middle of the box holding the selection.</param>
    /// <remarks>
    /// The first coordinate is the distance round the axis at the selection's mean distance from
    /// it, and the second the distance down along it, both in meters, so a cylinder's side shows the
    /// grid's meter as its flat faces do. A face across the seam, where the turn comes back to where
    /// it started, is wrapped from its own middle, so it is never stretched round the whole way.
    /// </remarks>
    /// <exception cref="ArgumentException">The axis is zero or not finite.</exception>
    public void UvCylinder(Vector3 axis, Vector3? center)
    {
        if (axis == Vector3.Zero || !float.IsFinite(axis.X + axis.Y + axis.Z))
            throw new ArgumentException("An axis to wrap about is finite and not zero.", nameof(axis));

        var along = Vector3.Normalize(axis);
        var middle = center ?? SelectionMiddle();
        var across = PlanarAxes(along).U;
        var side = Vector3.Cross(along, across);

        (float X, float Y, float V) Place(Vector3 point)
        {
            var offset = point - middle;
            var height = Vector3.Dot(offset, along);
            var radial = offset - along * height;
            return (Vector3.Dot(radial, across), Vector3.Dot(radial, side), -height);
        }

        var radius = MeanRadius(point =>
        {
            var (x, y, _) = Place(point);
            return MathF.Sqrt(x * x + y * y);
        });
        foreach (var index in _selectedFaces) Wrap(_faces[index], Place, radius);

        Changed();
        Did($"uv cylinder {AxisWords(axis)}{Pivot(center)}");
    }

    /// <summary>Gives the selected faces coordinates by a spherical projection about a point.</summary>
    /// <param name="center">The sphere's middle, or null for the middle of the box holding the selection.</param>
    /// <remarks>
    /// The first coordinate is the distance round the world's up axis and the second the distance
    /// down from the equator, along the sphere of the selection's mean distance from its middle, in
    /// meters. A face across the seam is wrapped from its own middle, as <see cref="UvCylinder"/>
    /// wraps one.
    /// </remarks>
    public void UvSphere(Vector3? center)
    {
        var middle = center ?? SelectionMiddle();
        var radius = MeanRadius(point => (point - middle).Length());

        (float X, float Y, float V) Place(Vector3 point)
        {
            var offset = point - middle;
            var level = MathF.Sqrt(offset.X * offset.X + offset.Z * offset.Z);
            return (offset.X, -offset.Z, -MathF.Atan2(offset.Y, level) * radius);
        }

        foreach (var index in _selectedFaces) Wrap(_faces[index], Place, radius);

        Changed();
        Did($"uv sphere{Pivot(center)}");
    }

    /// <summary>Moves the selected faces' coordinates, a whole number leaving a repeating texture where it was.</summary>
    /// <exception cref="ArgumentException">A number is not finite.</exception>
    public void UvMove(float u, float v)
    {
        if (!float.IsFinite(u) || !float.IsFinite(v)) throw new ArgumentException("A move of coordinates is two finite numbers.", nameof(u));

        var by = new Vector2(u, v);
        ChangeUvs(each: false, (uv, _) => uv + by);
        Did($"uv move {F(u)} {F(v)}");
    }

    /// <summary>Scales the selected faces' coordinates about their middle, two showing the picture twice as often, as a whole or each face about its own middle.</summary>
    /// <exception cref="ArgumentException">A factor is zero or not finite.</exception>
    public void UvScale(float u, float v, bool each)
    {
        if (!float.IsFinite(u) || !float.IsFinite(v) || u == 0f || v == 0f)
            throw new ArgumentException("A scale of coordinates is two finite numbers, neither zero.", nameof(u));

        ChangeUvs(each, (uv, middle) => middle + new Vector2((uv.X - middle.X) * u, (uv.Y - middle.Y) * v));
        Did($"uv scale {F(u)}{(u == v ? "" : " " + F(v))}{(each ? " each" : "")}");
    }

    /// <summary>Turns the selected faces' coordinates about their middle, from the first coordinate toward the second, as a whole or each face about its own middle.</summary>
    /// <exception cref="ArgumentException">The angle is not finite.</exception>
    public void UvRotate(float degrees, bool each)
    {
        if (!float.IsFinite(degrees)) throw new ArgumentException("A turn is a finite number of degrees.", nameof(degrees));

        var (sin, cos) = MathF.SinCos(float.DegreesToRadians(degrees));
        ChangeUvs(each, (uv, middle) =>
        {
            var (x, y) = (uv.X - middle.X, uv.Y - middle.Y);
            return middle + new Vector2(x * cos - y * sin, x * sin + y * cos);
        });
        Did($"uv rotate {F(degrees)}{(each ? " each" : "")}");
    }

    /// <summary>Scales the selected faces' coordinates about their middle so a texture of a width shows so many of its pixels a meter.</summary>
    /// <param name="pixelsPerMeter">How many of the texture's pixels a meter of the faces shows.</param>
    /// <param name="texturePixels">How wide the texture is, in pixels.</param>
    /// <remarks>
    /// The density the faces have is measured over the selection as a whole, the square root of
    /// the area their coordinates cover over the area they cover in square meters, and one factor
    /// brings it to the one asked, so faces whose coordinates meet go on meeting. The recipe keeps
    /// the texture's width, so the line makes the same coordinates whether the texture is loaded
    /// or not.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The density is not above zero, or the width is below a pixel.</exception>
    /// <exception cref="InvalidOperationException">The selected faces' coordinates cover no area to measure.</exception>
    public void UvDensity(float pixelsPerMeter, int texturePixels)
    {
        if (!(pixelsPerMeter > 0f) || !float.IsFinite(pixelsPerMeter))
            throw new ArgumentOutOfRangeException(nameof(pixelsPerMeter), pixelsPerMeter, "A density is a finite number of pixels a meter, above zero.");
        ArgumentOutOfRangeException.ThrowIfLessThan(texturePixels, 1);

        if (_selectedFaces.Count > 0)
        {
            double covered = 0, area = 0;
            foreach (var index in _selectedFaces)
            {
                var face = _faces[index];
                covered += Math.Abs(Shoelace(face.Uvs ?? DefaultUvs(face)));
                area += Newell(face.Corners).Length() / 2.0;
            }

            if (covered < 1e-12 || area < 1e-12) throw new InvalidOperationException("The selected faces' coordinates cover no area to measure a density over.");

            var factor = (float)(pixelsPerMeter / texturePixels / Math.Sqrt(covered / area));
            ChangeUvs(each: false, (uv, middle) => middle + (uv - middle) * factor);
        }

        Did($"uv density {F(pixelsPerMeter)} {texturePixels}");
    }

    /// <summary>Puts the selected faces back on the world-aligned default.</summary>
    public void UvReset()
    {
        foreach (var index in _selectedFaces) _faces[index].Uvs = null;

        Changed();
        Did("uv reset");
    }

    /// <summary>
    /// The two directions of a projection along a direction, the picture's right and down, those of
    /// the default for a direction along an axis.
    /// </summary>
    internal static (Vector3 U, Vector3 V) PlanarAxes(Vector3 facing)
    {
        // Straight up or down, the world's up is no help, and the default's axes are taken.
        if (MathF.Abs(facing.Y) > 0.999f) return EditMeshBuild.BoxAxes(facing);

        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, facing));
        return (right, Vector3.Cross(right, facing));
    }

    /// <summary>
    /// A face's coordinates as an affine function of a point in its plane, fitted to its corners by
    /// least squares, which is exact for coordinates a projection along a direction made, or null
    /// where the face has none of its own.
    /// </summary>
    internal UvMap? FitOf(int face) => FitOf(_faces[face]);

    private UvMap? FitOf(Face face)
    {
        if (face.Uvs is not { } uvs) return null;

        var corners = face.Corners;
        var origin = _positions[corners[0]];
        var normal = Unit(Newell(corners));
        var (first, second) = PlanarAxes(normal == Vector3.Zero ? Vector3.UnitY : normal);

        // The corners as places along two directions in the face's plane, from its first corner,
        // and the sums of the normal equations of u = a x + b y + c, and of v alike.
        double xx = 0, xy = 0, yy = 0, sx = 0, sy = 0, xu = 0, yu = 0, su = 0, xv = 0, yv = 0, sv = 0;
        for (int i = 0; i < corners.Length; i++)
        {
            var offset = _positions[corners[i]] - origin;
            double x = Vector3.Dot(offset, first), y = Vector3.Dot(offset, second), u = uvs[i].X, v = uvs[i].Y;
            xx += x * x; xy += x * y; yy += y * y; sx += x; sy += y;
            xu += x * u; yu += y * u; su += u;
            xv += x * v; yv += y * v; sv += v;
        }

        double n = corners.Length;
        var det = xx * (yy * n - sy * sy) - xy * (xy * n - sy * sx) + sx * (xy * sy - yy * sx);
        // A face with no area takes the mean of its coordinates everywhere.
        if (Math.Abs(det) < 1e-12) return new UvMap(Vector3.Zero, (float)(su / n), Vector3.Zero, (float)(sv / n));

        (Vector3 Along, float Offset) Solve(double bx, double by, double b)
        {
            var a = (bx * (yy * n - sy * sy) - xy * (by * n - sy * b) + sx * (by * sy - yy * b)) / det;
            var c = (xx * (by * n - sy * b) - bx * (xy * n - sy * sx) + sx * (xy * b - by * sx)) / det;
            var d = (xx * (yy * b - by * sy) - xy * (xy * b - by * sx) + bx * (xy * sy - yy * sx)) / det;
            var along = first * (float)a + second * (float)c;
            return (along, (float)d - Vector3.Dot(along, origin));
        }

        var (u3, uOffset) = Solve(xu, yu, su);
        var (v3, vOffset) = Solve(xv, yv, sv);
        return new UvMap(u3, uOffset, v3, vOffset);
    }

    private Vector2[] DefaultUvs(Face face)
    {
        var (u, v) = EditMeshBuild.BoxAxes(Newell(face.Corners));
        return face.Corners.Select(corner => new Vector2(Vector3.Dot(_positions[corner], u), Vector3.Dot(_positions[corner], v))).ToArray();
    }

    /// <summary>Changes every selected face's coordinates, the default written onto a face first, about the middle of all of them or of each.</summary>
    private void ChangeUvs(bool each, Func<Vector2, Vector2, Vector2> change)
    {
        var picked = _selectedFaces.Order().ToArray();
        foreach (var index in picked) _faces[index].Uvs ??= DefaultUvs(_faces[index]);

        var whole = UvMiddle(picked);
        foreach (var index in picked)
        {
            var uvs = _faces[index].Uvs!;
            var middle = each ? UvMiddle([index]) : whole;
            for (int i = 0; i < uvs.Length; i++) uvs[i] = change(uvs[i], middle);
        }

        Changed();
    }

    /// <summary>The middle of the box holding some faces' coordinates.</summary>
    private Vector2 UvMiddle(int[] faces)
    {
        if (faces.Length == 0) return default;
        var (low, high) = (new Vector2(float.MaxValue), new Vector2(float.MinValue));
        foreach (var index in faces)
            foreach (var uv in _faces[index].Uvs!)
                (low, high) = (Vector2.Min(low, uv), Vector2.Max(high, uv));
        return (low + high) * 0.5f;
    }

    /// <summary>
    /// Gives a face coordinates round an axis or a point, from each corner's place across it and how
    /// far down, the angle taken within half a turn of the face's middle's.
    /// </summary>
    private void Wrap(Face face, Func<Vector3, (float X, float Y, float V)> place, float radius)
    {
        var mean = Vector3.Zero;
        foreach (var corner in face.Corners) mean += _positions[corner];
        var (mx, my, _) = place(mean / face.Corners.Length);
        var reference = MathF.Atan2(my, mx);

        var uvs = new Vector2[face.Corners.Length];
        for (int i = 0; i < uvs.Length; i++)
        {
            var (x, y, down) = place(_positions[face.Corners[i]]);

            // A corner on the axis, as a cone's tip or a pole is, has no angle, and takes its face's.
            var angle = x * x + y * y < 1e-12f ? reference : MathF.Atan2(y, x);
            if (angle - reference > MathF.PI) angle -= MathF.Tau;
            else if (angle - reference < -MathF.PI) angle += MathF.Tau;

            uvs[i] = new Vector2(angle * radius, down);
        }

        face.Uvs = uvs;
    }

    /// <summary>The mean of a distance over the selected faces' corners, or one where it is none.</summary>
    private float MeanRadius(Func<Vector3, float> distance)
    {
        var corners = _selectedFaces.SelectMany(face => _faces[face].Corners).Distinct().ToArray();
        if (corners.Length == 0) return 1f;

        var mean = corners.Average(corner => distance(_positions[corner]));
        return mean > 1e-6f ? mean : 1f;
    }

    /// <summary>The middle of the box holding the selected faces' corners.</summary>
    private Vector3 SelectionMiddle()
    {
        var bounds = BoundsOf(_selectedFaces.SelectMany(face => _faces[face].Corners).Distinct());
        return (bounds.Min + bounds.Max) * 0.5f;
    }

    /// <summary>Twice the signed area of a loop of coordinates, halved.</summary>
    private static double Shoelace(Vector2[] loop)
    {
        double sum = 0;
        for (int i = 0; i < loop.Length; i++)
        {
            var (a, b) = (loop[i], loop[(i + 1) % loop.Length]);
            sum += (double)a.X * b.Y - (double)b.X * a.Y;
        }
        return sum / 2.0;
    }

    /// <summary>A direction written as the way along an axis, such as <c>+y</c>, where it is one, and as its three numbers where it is not.</summary>
    private static string Facing(Vector3 facing)
    {
        var unit = Vector3.Normalize(facing);
        for (int axis = 0; axis < 3; axis++)
        {
            var along = new Vector3(axis == 0 ? 1 : 0, axis == 1 ? 1 : 0, axis == 2 ? 1 : 0);
            if (unit == along) return "+" + "xyz"[axis];
            if (unit == -along) return "-" + "xyz"[axis];
        }
        return V(facing);
    }
}
