using System.Numerics;

namespace Engine;

// What a mesh entity's arrays are uploaded as, and the sphere its group is culled by.
internal sealed partial class MeshEntityDraws
{
    // The sphere around a mesh's positions, at the middle of their box.
    private static (Vector3 Center, float Radius) Sphere(Vector3[] positions)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var p in positions)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        var center = (min + max) / 2;
        var radius = 0f;
        foreach (var p in positions) radius = MathF.Max(radius, Vector3.DistanceSquared(p, center));
        return (center, MathF.Sqrt(radius));
    }

    private static ModelVertex[] Vertices(in Mesh mesh)
    {
        if (mesh.Indices is { } indices) return SharedVertices(mesh, indices);
        var count = mesh.Positions.Length / 3 * 3;
        var vertices = new ModelVertex[count];
        for (int i = 0; i < count; i += 3)
        {
            var (a, b, c) = (mesh.Positions[i], mesh.Positions[i + 1], mesh.Positions[i + 2]);
            // A triangle with no normals of its own is lit flat, by the normal of its face.
            var face = Vector3.Cross(b - a, c - a);
            face = face.LengthSquared() > 0 ? Vector3.Normalize(face) : Vector3.UnitY;
            for (int k = 0; k < 3; k++)
            {
                var n = mesh.Normals is { } normals && i + k < normals.Length ? normals[i + k] : face;
                var uv = mesh.Uvs is { } uvs && i + k < uvs.Length ? uvs[i + k] : Vector2.Zero;
                vertices[i + k] = new ModelVertex(mesh.Positions[i + k], n, uv);
            }
        }
        return vertices;
    }

    // The vertices of a mesh whose triangles name them, each position once, a vertex with no normal
    // of its own lit by the mean of the faces it is a corner of, weighed by their areas.
    private static ModelVertex[] SharedVertices(in Mesh mesh, uint[] indices)
    {
        var positions = mesh.Positions;
        var normals = mesh.Normals is { } given && given.Length >= positions.Length ? null : new Vector3[positions.Length];
        if (normals is not null)
            for (int i = 0; i + 2 < indices.Length; i += 3)
            {
                var (a, b, c) = (indices[i], indices[i + 1], indices[i + 2]);
                if (a >= positions.Length || b >= positions.Length || c >= positions.Length) continue;
                var face = Vector3.Cross(positions[b] - positions[a], positions[c] - positions[a]);
                normals[a] += face;
                normals[b] += face;
                normals[c] += face;
            }
        var vertices = new ModelVertex[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var n = normals is null ? mesh.Normals![i] : normals[i].LengthSquared() > 0 ? Vector3.Normalize(normals[i]) : Vector3.UnitY;
            var uv = mesh.Uvs is { } uvs && i < uvs.Length ? uvs[i] : Vector2.Zero;
            vertices[i] = new ModelVertex(positions[i], n, uv);
        }
        return vertices;
    }

    // The corners of a mesh's triangles, those its indices name where every corner is one of its
    // positions, or its positions in order, three a triangle.
    private static uint[] Indices(in Mesh mesh)
    {
        if (mesh.Indices is { } indices)
        {
            var kept = new List<uint>(indices.Length / 3 * 3);
            for (int i = 0; i + 2 < indices.Length; i += 3)
                if (indices[i] < mesh.Positions.Length && indices[i + 1] < mesh.Positions.Length && indices[i + 2] < mesh.Positions.Length)
                    kept.AddRange([indices[i], indices[i + 1], indices[i + 2]]);
            return [.. kept];
        }
        var count = mesh.Positions.Length / 3 * 3;
        var order = new uint[count];
        for (uint i = 0; i < count; i++) order[i] = i;
        return order;
    }
}
