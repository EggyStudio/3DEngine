using System.Numerics;

namespace Engine;

/// <summary>Builds an edit mesh into the vertices and triangles of one mesh for each material slot.</summary>
/// <remarks>
/// <para>
/// Each face is cut into triangles by ear clipping, so a concave face is cut inside its outline,
/// and keeps corners of its own, so its normal and its texture coordinates are its own where faces
/// meet at an angle. A flat face's corners take its normal, and a smoothed one's take the mean of
/// the normals of the smoothed faces round each corner within its angle, each weighted by its area,
/// so a cylinder's sides are round and its ends flat.
/// </para>
/// <para>
/// A face's texture coordinates are its corners' places in meters, seen along the axis its normal is
/// nearest, so a texture of a meter's grid shows a meter on every face and carries on across faces
/// meeting along a line, and a face moved shows the grid of the place it was moved to. The model
/// pass works a normal map's frame out from the texture coordinates as it draws, so the vertices
/// carry no tangents.
/// </para>
/// </remarks>
internal static class EditMeshBuild
{
    /// <summary>The vertices and triangles of each material slot, in the order of the mesh's slots.</summary>
    /// <remarks>
    /// A slot no face is in is one triangle with no area at a corner of the mesh's bounds, which
    /// draws nothing and keeps the slot's mesh for faces put in it later, and which the collider
    /// leaves out.
    /// </remarks>
    public static (ModelVertex[] Vertices, uint[] Indices)[] Slots(EditMeshData mesh)
    {
        // What is built is the mesh with its modifiers applied, which leave it as it was shaped.
        mesh = mesh.Modified();
        var slots = mesh.Slots.Count;
        var vertices = new List<ModelVertex>[slots];
        var indices = new List<uint>[slots];
        for (int slot = 0; slot < slots; slot++) (vertices[slot], indices[slot]) = ([], []);

        var areas = new Vector3[mesh.FaceCount];
        var around = new List<int>?[mesh.VertexCount];
        for (int face = 0; face < mesh.FaceCount; face++)
        {
            areas[face] = mesh.Area(face);
            foreach (var corner in mesh.Corners(face)) (around[corner] ??= []).Add(face);
        }

        for (int face = 0; face < mesh.FaceCount; face++)
        {
            var corners = mesh.Corners(face);
            var slot = mesh.SlotIndexOf(face);
            var normal = areas[face] == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(areas[face]);
            var smooth = mesh.SmoothOf(face);
            var first = (uint)vertices[slot].Count;
            var (u, v) = BoxAxes(areas[face]);

            var points = new Vector3[corners.Length];
            for (int i = 0; i < corners.Length; i++) points[i] = mesh.Position(corners[i]);
            for (int i = 0; i < corners.Length; i++)
            {
                var cornerNormal = smooth > 0 ? Smoothed(mesh, corners[i], normal, smooth, areas, around) : normal;
                vertices[slot].Add(new ModelVertex(points[i], cornerNormal, new Vector2(Vector3.Dot(points[i], u), Vector3.Dot(points[i], v))));
            }
            foreach (var index in EarClipping.Cut(points, areas[face])) indices[slot].Add(first + (uint)index);
        }

        var empty = new ModelVertex(mesh.Bounds.Min, Vector3.UnitY, Vector2.Zero);
        var built = new (ModelVertex[], uint[])[slots];
        for (int slot = 0; slot < slots; slot++)
            built[slot] = indices[slot].Count > 0 ? ([.. vertices[slot]], [.. indices[slot]]) : ([empty, empty, empty], [0, 1, 2]);
        return built;
    }

    /// <summary>
    /// The two directions a face's texture coordinates are measured along, by the axis its normal is
    /// nearest, chosen so the texture reads the right way round seen from in front of the face, its
    /// top up on a wall.
    /// </summary>
    internal static (Vector3 U, Vector3 V) BoxAxes(Vector3 normal)
    {
        float x = MathF.Abs(normal.X), y = MathF.Abs(normal.Y), z = MathF.Abs(normal.Z);
        // A texture's second coordinate grows downward, so a wall's runs down the world's Y.
        if (y >= x && y >= z) return normal.Y >= 0 ? (Vector3.UnitX, Vector3.UnitZ) : (Vector3.UnitX, -Vector3.UnitZ);
        if (x >= z) return normal.X >= 0 ? (-Vector3.UnitZ, -Vector3.UnitY) : (Vector3.UnitZ, -Vector3.UnitY);
        return normal.Z >= 0 ? (Vector3.UnitX, -Vector3.UnitY) : (-Vector3.UnitX, -Vector3.UnitY);
    }

    /// <summary>
    /// The normal of a smoothed face's corner, the mean of the normals of the smoothed faces round the
    /// corner within the face's angle of it, each by its area.
    /// </summary>
    private static Vector3 Smoothed(EditMeshData mesh, int corner, Vector3 normal, float degrees, Vector3[] areas, List<int>?[] around)
    {
        var least = MathF.Cos(float.DegreesToRadians(degrees)) - 1e-5f;
        var sum = Vector3.Zero;
        foreach (var other in around[corner]!)
        {
            if (mesh.SmoothOf(other) <= 0 || areas[other] == Vector3.Zero) continue;
            if (Vector3.Dot(Vector3.Normalize(areas[other]), normal) >= least) sum += areas[other];
        }
        return sum == Vector3.Zero ? normal : Vector3.Normalize(sum);
    }
}
