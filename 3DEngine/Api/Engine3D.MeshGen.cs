using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    // -- More mesh generators, in raylib's shapes. Every face winds counterclockwise seen from
    // outside, with a normal and a texture coordinate per vertex.

    /// <summary>Makes a flat regular polygon on the XZ plane, centered on the origin, facing up.</summary>
    public static ModelMesh GenMeshPoly(int sides, float radius)
    {
        sides = Math.Max(3, sides);
        var mesh = new MeshBuilder();
        var center = mesh.Vertex(Vector3.Zero, Vector3.UnitY, new Vector2(0.5f));
        var rim = new uint[sides];
        for (int s = 0; s < sides; s++)
        {
            var (sin, cos) = MathF.SinCos(MathF.Tau * s / sides);
            rim[s] = mesh.Vertex(new Vector3(cos, 0, sin) * radius, Vector3.UnitY, new Vector2(0.5f + cos / 2, 0.5f + sin / 2));
        }
        for (int s = 0; s < sides; s++)
            mesh.Triangle(center, rim[(s + 1) % sides], rim[s]);
        return mesh.Upload();
    }

    /// <summary>Makes a cylinder standing on the XZ plane, from y 0 up to <paramref name="height"/>, closed at both ends.</summary>
    public static ModelMesh GenMeshCylinder(float radius, float height, int slices)
    {
        var mesh = new MeshBuilder();
        mesh.Lathe([(radius, 0, Vector2.UnitX, 1), (radius, height, Vector2.UnitX, 0)], slices);
        mesh.Cap(radius, 0, slices, up: false);
        mesh.Cap(radius, height, slices, up: true);
        return mesh.Upload();
    }

    /// <summary>Makes a cone standing on the XZ plane, its base at y 0 and its tip at <paramref name="height"/>.</summary>
    public static ModelMesh GenMeshCone(float radius, float height, int slices)
    {
        // The side's normal leans from the radius toward the tip by the slope of the side.
        var slope = Vector2.Normalize(new Vector2(height, radius));
        var mesh = new MeshBuilder();
        mesh.Lathe([(radius, 0, slope, 1), (0, height, slope, 0)], slices);
        mesh.Cap(radius, 0, slices, up: false);
        return mesh.Upload();
    }

    /// <summary>Makes the upper half of a sphere, its flat side on the XZ plane and closed.</summary>
    public static ModelMesh GenMeshHemiSphere(float radius, int rings, int slices)
    {
        rings = Math.Max(1, rings);
        var profile = new (float Radius, float Y, Vector2 Normal, float V)[rings + 1];
        for (int r = 0; r <= rings; r++)
        {
            // From the equator up to the pole.
            var (sin, cos) = MathF.SinCos(MathF.PI / 2 * r / rings);
            profile[r] = (radius * cos, radius * sin, new Vector2(cos, sin), 1 - (float)r / rings);
        }
        var mesh = new MeshBuilder();
        mesh.Lathe(profile, slices);
        mesh.Cap(radius, 0, slices, up: false);
        return mesh.Upload();
    }

    /// <summary>
    /// Makes a torus lying on the XZ plane: a tube of radius <paramref name="size"/> around a ring
    /// of radius <paramref name="radius"/>.
    /// </summary>
    public static ModelMesh GenMeshTorus(float radius, float size, int radSeg, int sides)
    {
        var mesh = new MeshBuilder();
        mesh.Tube(t =>
        {
            var (sin, cos) = MathF.SinCos(t);
            var outward = new Vector3(cos, 0, sin);
            return (outward * radius, outward, Vector3.UnitY);
        }, size, radSeg, sides);
        return mesh.Upload();
    }

    /// <summary>
    /// Makes a trefoil knot about <paramref name="radius"/> across from its center, as a tube of
    /// radius <paramref name="size"/>.
    /// </summary>
    public static ModelMesh GenMeshKnot(float radius, float size, int radSeg, int sides)
    {
        // The trefoil reaches three units from its center, so it is scaled to the radius asked for.
        var scale = radius / 3;
        static Vector3 Curve(float t) =>
            new(MathF.Sin(t) + 2 * MathF.Sin(2 * t), MathF.Cos(t) - 2 * MathF.Cos(2 * t), -MathF.Sin(3 * t));

        var mesh = new MeshBuilder();
        mesh.Tube(t =>
        {
            // The Frenet frame, from the curve's first two derivatives. It closes on itself
            // around the knot, so the tube has no twisted seam where it meets its start.
            const float h = 1e-3f;
            var (back, here, ahead) = (Curve(t - h), Curve(t), Curve(t + h));
            var tangent = Vector3.Normalize(ahead - back);
            var bend = ahead - 2 * here + back;
            var normal = Vector3.Normalize(bend - Vector3.Dot(bend, tangent) * tangent);
            return (here * scale, normal, Vector3.Cross(tangent, normal));
        }, size, radSeg, sides);
        return mesh.Upload();
    }

    /// <summary>Collects vertices and triangles for a generated mesh.</summary>
    private sealed class MeshBuilder
    {
        private readonly List<ModelVertex> _vertices = [];
        private readonly List<uint> _indices = [];

        public uint Vertex(Vector3 position, Vector3 normal, Vector2 uv)
        {
            _vertices.Add(new ModelVertex(position, normal, uv));
            return (uint)(_vertices.Count - 1);
        }

        public void Triangle(uint a, uint b, uint c) => _indices.AddRange([a, b, c]);

        /// <summary>Adds a quad as two triangles, turned to face the way its vertices' normals point.</summary>
        public void Quad(uint a, uint b, uint c, uint d)
        {
            var (pa, pb, pc, pd) = (_vertices[(int)a].Position, _vertices[(int)b].Position, _vertices[(int)c].Position, _vertices[(int)d].Position);
            var face = Vector3.Cross(pc - pa, pd - pb);
            var normals = _vertices[(int)a].Normal + _vertices[(int)b].Normal + _vertices[(int)c].Normal + _vertices[(int)d].Normal;
            if (Vector3.Dot(face, normals) < 0) (b, d) = (d, b);
            Triangle(a, b, c);
            Triangle(a, c, d);
        }

        /// <summary>
        /// Turns a profile of rings around the Y axis: each ring's radius and height, the normal in
        /// the plane of radius and height, and its texture row.
        /// </summary>
        public void Lathe(ReadOnlySpan<(float Radius, float Y, Vector2 Normal, float V)> profile, int slices)
        {
            slices = Math.Max(3, slices);
            var first = (uint)_vertices.Count;
            foreach (var (radius, y, normal, v) in profile)
                for (int s = 0; s <= slices; s++)
                {
                    var (sin, cos) = MathF.SinCos(MathF.Tau * s / slices);
                    Vertex(new Vector3(cos * radius, y, sin * radius), new Vector3(cos * normal.X, normal.Y, sin * normal.X), new Vector2((float)s / slices, v));
                }

            var row = (uint)(slices + 1);
            for (int r = 0; r + 1 < profile.Length; r++)
            for (int s = 0; s < slices; s++)
            {
                var a = first + (uint)r * row + (uint)s;
                Quad(a, a + 1, a + row + 1, a + row);
            }
        }

        /// <summary>Closes a ring at height <paramref name="y"/> with a flat disc facing up or down.</summary>
        public void Cap(float radius, float y, int slices, bool up)
        {
            slices = Math.Max(3, slices);
            var normal = up ? Vector3.UnitY : -Vector3.UnitY;
            var center = Vertex(new Vector3(0, y, 0), normal, new Vector2(0.5f));
            var first = (uint)_vertices.Count;
            for (int s = 0; s < slices; s++)
            {
                var (sin, cos) = MathF.SinCos(MathF.Tau * s / slices);
                Vertex(new Vector3(cos * radius, y, sin * radius), normal, new Vector2(0.5f + cos / 2, 0.5f + sin / 2));
            }
            for (int s = 0; s < slices; s++)
            {
                uint here = first + (uint)s, next = first + (uint)((s + 1) % slices);
                if (up) Triangle(center, next, here);
                else Triangle(center, here, next);
            }
        }

        /// <summary>
        /// Sweeps a circle of <paramref name="size"/> along a closed curve, which gives for each
        /// parameter from 0 to 2π a point and two directions across the curve.
        /// </summary>
        public void Tube(Func<float, (Vector3 Point, Vector3 Across, Vector3 Up)> curve, float size, int segments, int sides)
        {
            segments = Math.Max(3, segments);
            sides = Math.Max(3, sides);
            var first = (uint)_vertices.Count;
            for (int i = 0; i <= segments; i++)
            {
                var (point, across, up) = curve(MathF.Tau * (i % segments) / segments);
                for (int j = 0; j <= sides; j++)
                {
                    var (sin, cos) = MathF.SinCos(MathF.Tau * j / sides);
                    var normal = cos * across + sin * up;
                    Vertex(point + normal * size, normal, new Vector2((float)i / segments, (float)j / sides));
                }
            }

            var row = (uint)(sides + 1);
            for (int i = 0; i < segments; i++)
            for (int j = 0; j < sides; j++)
            {
                var a = first + (uint)i * row + (uint)j;
                Quad(a, a + 1, a + row + 1, a + row);
            }
        }

        public ModelMesh Upload() => UploadMesh([.. _vertices], [.. _indices]);
    }
}
