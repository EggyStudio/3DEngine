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
    /// Makes a torus standing on the XY plane, as raylib's does: a ring of radius
    /// <paramref name="size"/> / 2 and a tube <paramref name="radius"/> of that thick, so 0.25
    /// makes a tube a quarter of the ring's radius.
    /// </summary>
    /// <remarks>
    /// The radius is held from 0.1 to 1, and fewer than three segments or sides make no mesh, as
    /// raylib's. Its surface is par_shapes' torus, which raylib draws, a ring of 1 scaled by half
    /// the size.
    /// </remarks>
    public static ModelMesh GenMeshTorus(float radius, float size, int radSeg, int sides)
    {
        if (sides < 3 || radSeg < 3) return default;
        radius = Math.Clamp(radius, 0.1f, 1.0f);

        var scale = size / 2;
        var mesh = new MeshBuilder();
        mesh.Tube(t =>
        {
            var (sin, cos) = MathF.SinCos(t);
            var outward = new Vector3(cos, sin, 0);
            return (outward * scale, outward, Vector3.UnitZ);
        }, radius * scale, radSeg, sides);
        return mesh.Upload();
    }

    /// <summary>
    /// Makes a trefoil knot as raylib's does: par_shapes' knot, about 0.8 across from its center
    /// and a tube of <paramref name="radius"/> / 10, scaled by <paramref name="size"/>.
    /// </summary>
    /// <remarks>The radius is held from 0.5 to 3, and fewer than three segments or sides make no mesh, as raylib's.</remarks>
    public static ModelMesh GenMeshKnot(float radius, float size, int radSeg, int sides)
    {
        if (sides < 3 || radSeg < 3) return default;
        radius = Math.Clamp(radius, 0.5f, 3.0f);

        // par_shapes' trefoil, its curve and the frame its tube is swept in, over two turns
        const float a = 0.5f, b = 0.3f, c = 0.5f;
        var mesh = new MeshBuilder();
        mesh.Tube(t =>
        {
            var u = 2 * t;
            var (sinU, cosU) = MathF.SinCos(u);
            var (sin15, cos15) = MathF.SinCos(1.5f * u);
            var r = a + b * cos15;
            var point = new Vector3(r * cosU, r * sinU, c * sin15);

            var tangent = Vector3.Normalize(new Vector3(
                -1.5f * b * sin15 * cosU - r * sinU,
                -1.5f * b * sin15 * sinU + r * cosU,
                1.5f * c * cos15));
            var across = Vector3.Normalize(new Vector3(tangent.Y, -tangent.X, 0));
            var up = Vector3.Cross(tangent, across);
            return (point * size, across, up);
        }, radius * 0.1f * size, radSeg, sides);
        return mesh.Upload();
    }

    /// <summary>
    /// Makes terrain from an image: a grid with a vertex per pixel, raised by the pixel's
    /// brightness, from the origin to <paramref name="size"/>, where black is at y 0 and white at
    /// <c>size.Y</c>.
    /// </summary>
    /// <remarks>As raylib's, the terrain lies on +X and +Z from the origin rather than centered.</remarks>
    public static ModelMesh GenMeshHeightmap(Image heightmap, Vector3 size)
    {
        if (!heightmap.IsValid || heightmap.Width < 2 || heightmap.Height < 2) return default;
        int w = heightmap.Width, h = heightmap.Height;
        var step = new Vector3(size.X / (w - 1), size.Y / 255f, size.Z / (h - 1));

        float Height(int x, int z)
        {
            var c = GetImageColor(heightmap, Math.Clamp(x, 0, w - 1), Math.Clamp(z, 0, h - 1));
            return (c.R + c.G + c.B) / 3f * step.Y;
        }

        var mesh = new MeshBuilder();
        for (int z = 0; z < h; z++)
        for (int x = 0; x < w; x++)
        {
            // The normal from the slopes to the neighbors on each side.
            var normal = Vector3.Normalize(new Vector3(
                (Height(x - 1, z) - Height(x + 1, z)) / (2 * step.X), 1,
                (Height(x, z - 1) - Height(x, z + 1)) / (2 * step.Z)));
            mesh.Vertex(new Vector3(x * step.X, Height(x, z), z * step.Z), normal, new Vector2((float)x / (w - 1), (float)z / (h - 1)));
        }

        for (int z = 0; z + 1 < h; z++)
        for (int x = 0; x + 1 < w; x++)
        {
            var a = (uint)(z * w + x);
            mesh.Quad(a, a + 1, a + 1 + (uint)w, a + (uint)w);
        }
        return mesh.Upload();
    }

    /// <summary>
    /// Makes a maze from an image as raylib's does: a block of <paramref name="cubeSize"/> for every
    /// white pixel, centered on the pixel's place, so pixel (x, y) stands at x by y times the size,
    /// and a floor and a roof over every black pixel.
    /// </summary>
    /// <remarks>
    /// A block has its top and bottom and the sides that face a black pixel or the map's edge, and
    /// a pixel of any other color makes nothing. Each face takes a quarter of a texture laid out as
    /// raylib's atlas is: the right and front sides the top left, the left and back sides the top
    /// right, a block's top and an open cell's roof the bottom left, and a block's bottom and the
    /// floor the bottom right. The roofs face down, so from above they are culled and from inside
    /// the maze they are its ceiling, as a material is single-sided unless set.
    /// </remarks>
    public static ModelMesh GenMeshCubicmap(Image cubicmap, Vector3 cubeSize)
    {
        if (!cubicmap.IsValid) return default;
        int width = cubicmap.Width, height = cubicmap.Height;
        Color[] pixels = LoadImageColors(cubicmap);
        bool Is(int x, int z, Color color) => pixels[z * width + x] == color;

        var mesh = new MeshBuilder();
        float w = cubeSize.X, h = cubeSize.Z, h2 = cubeSize.Y;

        // Each face's quarter of the atlas, as x, y, width and height in texture coordinates
        var right = new Vector4(0.0f, 0.0f, 0.5f, 0.5f);
        var left = new Vector4(0.5f, 0.0f, 0.5f, 0.5f);
        var front = new Vector4(0.0f, 0.0f, 0.5f, 0.5f);
        var back = new Vector4(0.5f, 0.0f, 0.5f, 0.5f);
        var top = new Vector4(0.0f, 0.5f, 0.5f, 0.5f);
        var bottom = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);

        // A corner of a quarter: 0 its left or top, 1 its right or bottom.
        static Vector2 At(Vector4 rect, int u, int v) => new(rect.X + u * rect.Z, rect.Y + v * rect.W);

        // Two triangles of raylib's, their corners and texture coordinates in its order
        void Triangles(Vector3 normal, ReadOnlySpan<Vector3> corners, ReadOnlySpan<Vector2> uvs)
        {
            for (int i = 0; i < 6; i += 3)
                mesh.Triangle(mesh.Vertex(corners[i], normal, uvs[i]), mesh.Vertex(corners[i + 1], normal, uvs[i + 1]), mesh.Vertex(corners[i + 2], normal, uvs[i + 2]));
        }

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                // The cell's eight corners, the first four at the top
                var v1 = new Vector3(w * (x - 0.5f), h2, h * (z - 0.5f));
                var v2 = new Vector3(w * (x - 0.5f), h2, h * (z + 0.5f));
                var v3 = new Vector3(w * (x + 0.5f), h2, h * (z + 0.5f));
                var v4 = new Vector3(w * (x + 0.5f), h2, h * (z - 0.5f));
                var v5 = new Vector3(w * (x + 0.5f), 0, h * (z - 0.5f));
                var v6 = new Vector3(w * (x - 0.5f), 0, h * (z - 0.5f));
                var v7 = new Vector3(w * (x - 0.5f), 0, h * (z + 0.5f));
                var v8 = new Vector3(w * (x + 0.5f), 0, h * (z + 0.5f));

                if (Is(x, z, Color.White))
                {
                    // The top, which is seen only from outside the map, and the bottom
                    Triangles(Vector3.UnitY, [v1, v2, v3, v1, v3, v4], [At(top, 0, 0), At(top, 0, 1), At(top, 1, 1), At(top, 0, 0), At(top, 1, 1), At(top, 1, 0)]);
                    Triangles(-Vector3.UnitY, [v6, v8, v7, v6, v5, v8], [At(bottom, 1, 0), At(bottom, 0, 1), At(bottom, 1, 1), At(bottom, 1, 0), At(bottom, 0, 0), At(bottom, 0, 1)]);

                    // The sides that face an open cell or the edge
                    if ((z < height - 1 && Is(x, z + 1, Color.Black)) || z == height - 1)
                        Triangles(Vector3.UnitZ, [v2, v7, v3, v3, v7, v8], [At(front, 0, 0), At(front, 0, 1), At(front, 1, 0), At(front, 1, 0), At(front, 0, 1), At(front, 1, 1)]);
                    if ((z > 0 && Is(x, z - 1, Color.Black)) || z == 0)
                        Triangles(-Vector3.UnitZ, [v1, v5, v6, v1, v4, v5], [At(back, 1, 0), At(back, 0, 1), At(back, 1, 1), At(back, 1, 0), At(back, 0, 0), At(back, 0, 1)]);
                    if ((x < width - 1 && Is(x + 1, z, Color.Black)) || x == width - 1)
                        Triangles(Vector3.UnitX, [v3, v8, v4, v4, v8, v5], [At(right, 0, 0), At(right, 0, 1), At(right, 1, 0), At(right, 1, 0), At(right, 0, 1), At(right, 1, 1)]);
                    if ((x > 0 && Is(x - 1, z, Color.Black)) || x == 0)
                        Triangles(-Vector3.UnitX, [v1, v7, v2, v1, v6, v7], [At(left, 0, 0), At(left, 1, 1), At(left, 1, 0), At(left, 0, 0), At(left, 0, 1), At(left, 1, 1)]);
                }
                else if (Is(x, z, Color.Black))
                {
                    // The roof, facing down, and the floor, facing up
                    Triangles(-Vector3.UnitY, [v1, v3, v2, v1, v4, v3], [At(top, 0, 0), At(top, 1, 1), At(top, 0, 1), At(top, 0, 0), At(top, 1, 0), At(top, 1, 1)]);
                    Triangles(Vector3.UnitY, [v6, v7, v8, v6, v8, v5], [At(bottom, 1, 0), At(bottom, 1, 1), At(bottom, 0, 1), At(bottom, 1, 0), At(bottom, 0, 1), At(bottom, 0, 0)]);
                }
            }
        }
        return mesh.Upload();
    }

    /// <summary>
    /// The triangles of a mesh as a <see cref="Mesh"/> component, three positions to a triangle with
    /// their normals and texture coordinates, so an entity draws a generated or loaded shape.
    /// </summary>
    /// <remarks>
    /// The arrays are made each call, and one component may be given to many entities, which share
    /// its upload as they share the arrays. A mesh with no data, as one unloaded, gives an empty one.
    /// </remarks>
    public static Mesh GetMeshComponent(ModelMesh mesh)
    {
        if (!Meshes.TryGetData(mesh.Id, out var vertices, out var indices))
        {
            ApiLogger.Warn("GetMeshComponent: the mesh has no data.");
            return new Mesh([]);
        }
        var count = indices.Length / 3 * 3;
        var positions = new Vector3[count];
        var normals = new Vector3[count];
        var uvs = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            var v = vertices[indices[i]];
            (positions[i], normals[i], uvs[i]) = (v.Position, v.Normal, v.Uv);
        }
        return new Mesh(positions, normals, uvs);
    }

    /// <summary>
    /// Writes a mesh as a Wavefront OBJ file, its positions, texture coordinates, normals and
    /// triangles, as raylib's <c>ExportMesh</c> does, answering whether it was written.
    /// </summary>
    /// <remarks>
    /// Texture coordinates are written with V from the bottom, as OBJ counts it, so the file loads
    /// back the way it was drawn. Only the mesh's shape is written, and no material.
    /// </remarks>
    public static bool ExportMesh(ModelMesh mesh, string fileName)
    {
        if (!Meshes.TryGetData(mesh.Id, out var vertices, out var indices))
        {
            ApiLogger.Warn($"ExportMesh: the mesh has no data to write.");
            return false;
        }
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        var text = new System.Text.StringBuilder();
        text.AppendLine("# Exported by 3DEngine");
        foreach (var v in vertices) text.Append(invariant, $"v {v.Position.X} {v.Position.Y} {v.Position.Z}\n");
        foreach (var v in vertices) text.Append(invariant, $"vt {v.Uv.X} {1 - v.Uv.Y}\n");
        foreach (var v in vertices) text.Append(invariant, $"vn {v.Normal.X} {v.Normal.Y} {v.Normal.Z}\n");
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            uint a = indices[i] + 1, b = indices[i + 1] + 1, c = indices[i + 2] + 1;
            text.Append(invariant, $"f {a}/{a}/{a} {b}/{b}/{b} {c}/{c}/{c}\n");
        }
        try
        {
            File.WriteAllText(fileName, text.ToString());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ApiLogger.Warn($"ExportMesh: '{fileName}' could not be written: {ex.Message}");
            return false;
        }
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
