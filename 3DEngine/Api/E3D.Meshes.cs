using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

public static partial class E3D
{
    // -- Meshes of the program's own

    /// <summary>Uploads vertices and triangle indices as a mesh.</summary>
    /// <exception cref="ArgumentException">The indices are not whole triangles, or one is out of range.</exception>
    public static ModelMesh UploadMesh(ModelVertex[] vertices, uint[] indices) => UploadMesh(vertices, indices, null, null);

    /// <summary>
    /// Uploads vertices and triangle indices as a mesh, with a color at each vertex and a second
    /// texture coordinate at each, either of which may be null, as raylib's mesh has its
    /// <c>colors</c> and <c>texcoords2</c>.
    /// </summary>
    /// <remarks>
    /// The colors are sRGB, as every color is, and multiply the material's color and texture. The
    /// second texture coordinates are read by a shader of the program's own, as a lightmap's are.
    /// Each is a buffer of its own beside the vertices, a mesh given one carries the other at its
    /// default, white or zero, and a mesh given neither is drawn from its vertices alone.
    /// </remarks>
    /// <exception cref="ArgumentException">The indices are not whole triangles, one is out of range, or the colors or coordinates are not one a vertex.</exception>
    public static ModelMesh UploadMesh(ModelVertex[] vertices, uint[] indices, Color[]? colors, Vector2[]? texcoords2) =>
        UploadMesh(vertices, indices, colors, texcoords2, null);

    /// <summary>
    /// Uploads vertices and triangle indices as a mesh, with its colors and second texture
    /// coordinates where given, and a stream of the program's own, a value each vertex carries that
    /// a vertex stage of the program's own reads as <c>CUSTOM0</c>.
    /// </summary>
    /// <remarks>
    /// The stream is a buffer of its own beside the vertices, which <see cref="UpdateMeshBuffer{T}"/>
    /// writes in place at <see cref="MeshStreamBuffer"/>. The model pass's own shader does not read
    /// it, and a shader that reads it drawn with a mesh without one reads zero.
    /// </remarks>
    /// <exception cref="ArgumentException">The indices are not whole triangles, one is out of range, or the colors, coordinates or stream are not one a vertex.</exception>
    public static ModelMesh UploadMesh(ModelVertex[] vertices, uint[] indices, Color[]? colors, Vector2[]? texcoords2, MeshStream? stream)
    {
        var positions = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) positions[i] = vertices[i].Position;
        var id = Meshes.Add(vertices, indices, colors is null && texcoords2 is null ? null : new MeshStore.Streams(colors, texcoords2), stream);
        return new ModelMesh(id, vertices.Length, indices.Length / 3, BoundingBox.Around(positions));
    }

    /// <summary>
    /// Uploads a mesh of positions alone, twelve bytes a vertex, with a stream of the program's own
    /// and a color at each vertex where given, for a mesh whose normal and texture coordinates its
    /// stream packs, as a voxel world's chunks are.
    /// </summary>
    /// <remarks>
    /// The GPU is given the positions alone, and a vertex stage reads the normal and texture
    /// coordinate as zero, so such a mesh is drawn with a shader of the program's own that unpacks
    /// them from the stream, the model pass's own lighting it by a zero normal, which is warned of
    /// once where it draws one. Its shadow is cast by its positions as any mesh's is, and the
    /// light field, the glow lights, picking and bodies read its positions with zero normals. Its
    /// positions are written in place by <see cref="UpdateMeshBuffer{T}"/> at 0, it takes no skin,
    /// and the pass that scatters light under a surface leaves it out.
    /// </remarks>
    /// <exception cref="ArgumentException">The indices are not whole triangles, one is out of range, or the stream or colors are not one a vertex.</exception>
    public static ModelMesh UploadMesh(Vector3[] positions, uint[] indices, MeshStream? stream, Color[]? colors)
    {
        var id = Meshes.AddPositions(positions, indices, stream, colors);
        return new ModelMesh(id, positions.Length, indices.Length / 3, BoundingBox.Around(positions));
    }

    /// <summary>
    /// The index <see cref="UpdateMeshBuffer{T}"/> writes a mesh's stream of the program's own at
    /// (<see cref="MeshStream"/>), past raylib's 0 to 8, which end at a skin's weights.
    /// </summary>
    public const int MeshStreamBuffer = 9;

    /// <summary>
    /// Replaces a mesh's vertices with as many new ones, keeping its triangles, as raylib's
    /// <c>UpdateMeshBuffer</c> does for the vertex arrays.
    /// </summary>
    /// <remarks>
    /// The mesh's bounds are those it was made with. A mesh uploaded from its positions alone takes
    /// their positions, its normals and texture coordinates read as zero, which is warned of once a
    /// mesh.
    /// </remarks>
    /// <exception cref="ArgumentException">The count differs from the mesh's.</exception>
    public static void UpdateMeshVertices(ModelMesh mesh, ModelVertex[] vertices)
    {
        if (!mesh.IsValid) return;
        if (Meshes.IsPositionsOnly(mesh.Id, firstAsked: true))
            ApiLogger.Warn("UpdateMeshVertices: a mesh uploaded from its positions alone takes the vertices' positions, its normals and texture coordinates being read as zero.");
        Meshes.UpdateVertices(mesh.Id, vertices);
    }

    /// <summary>
    /// Writes <paramref name="data"/> into one of a mesh's arrays, starting <paramref name="offset"/>
    /// bytes in, by raylib's index for it: 0 its positions, 1 its texture coordinates and 2 its
    /// normals, as floats, 3 its colors, four bytes each, and 5 its second texture coordinates, or
    /// at <see cref="MeshStreamBuffer"/> its stream of the program's own, in the stream's format.
    /// </summary>
    /// <remarks>
    /// A mesh keeps its position, normal and texture coordinate together in one vertex, which the
    /// write changes in place, keeping the bounds it was made with, as <see cref="UpdateMeshVertices"/>
    /// does. Writing colors or second texture coordinates to a mesh without them gives it both, the
    /// rest of each white or zero, where raylib's mesh has no buffer to write into until rlgl loads
    /// one, as its lightmap example does. Tangents (4) and the indices (6) are not kept, so a write
    /// to either is left out with a warning, and bytes past the array's end are left out. The
    /// stream of the program's own is written into the next of a ring of buffers and nothing else
    /// of the mesh is uploaded again; a mesh made without one is left as it is with a warning,
    /// since the write does not say the stream's format.
    /// </remarks>
    public static void UpdateMeshBuffer<T>(ModelMesh mesh, int index, ReadOnlySpan<T> data, int offset) where T : unmanaged
    {
        if (!mesh.IsValid || !Meshes.TryGetData(mesh.Id, out var vertices, out _)) return;
        var bytes = MemoryMarshal.AsBytes(data);
        switch (index)
        {
            case 1 or 2 when Meshes.IsPositionsOnly(mesh.Id):
                ApiLogger.Warn($"UpdateMeshBuffer: a mesh uploaded from its positions alone keeps no {(index == 1 ? "texture coordinates" : "normals")} ({index}), which its stream packs, so it is left as it is.");
                break;
            case 0 or 1 or 2:
            {
                // Where the array sits in ModelVertex, and how many bytes it has a vertex.
                var (at, size) = index switch { 0 => (0, 12), 1 => (24, 8), _ => (12, 12) };
                var moved = (ModelVertex[])vertices.Clone();
                var array = new byte[moved.Length * size];
                var packed = MemoryMarshal.AsBytes(moved.AsSpan());
                var stride = Marshal.SizeOf<ModelVertex>();
                for (int i = 0; i < moved.Length; i++) packed.Slice(i * stride + at, size).CopyTo(array.AsSpan(i * size));
                if (!Write(array, bytes, offset)) return;
                for (int i = 0; i < moved.Length; i++) array.AsSpan(i * size, size).CopyTo(packed[(i * stride + at)..]);
                Meshes.UpdateVertices(mesh.Id, moved);
                break;
            }
            case MeshStreamBuffer:
            {
                if (Meshes.CustomOf(mesh.Id) is not { } custom)
                {
                    ApiLogger.Warn($"UpdateMeshBuffer: the mesh has no stream of the program's own ({MeshStreamBuffer}), which UploadMesh gives it with its format, so it is left as it is.");
                    return;
                }
                var written = (byte[])custom.Data.Clone();
                if (Write(written, bytes, offset)) Meshes.UpdateCustom(mesh.Id, custom with { Data = written });
                break;
            }
            case 3 or 5:
            {
                var streams = Meshes.StreamsOf(mesh.Id);
                var colors = streams?.Colors is { } c ? (Color[])c.Clone() : Enumerable.Repeat(Color.White, vertices.Length).ToArray();
                var texcoords2 = streams?.Texcoords2 is { } t ? (Vector2[])t.Clone() : new Vector2[vertices.Length];
                var written = index == 3 ? Write(MemoryMarshal.AsBytes(colors.AsSpan()), bytes, offset) : Write(MemoryMarshal.AsBytes(texcoords2.AsSpan()), bytes, offset);
                if (written) Meshes.SetStreams(mesh.Id, new MeshStore.Streams(colors, texcoords2));
                break;
            }
            default:
                ApiLogger.Warn($"UpdateMeshBuffer: a mesh keeps no array {index}, its positions (0), texture coordinates (1), normals (2), colors (3), second texture coordinates (5) and stream of the program's own ({MeshStreamBuffer}) being those it has, so it is left as it is.");
                break;
        }

        static bool Write(Span<byte> array, ReadOnlySpan<byte> bytes, int offset)
        {
            if (offset < 0 || offset >= array.Length) return false;
            bytes[..Math.Min(bytes.Length, array.Length - offset)].CopyTo(array[offset..]);
            return true;
        }
    }

    /// <summary>Frees a mesh. Drawing it afterward draws nothing.</summary>
    public static void UnloadMesh(ModelMesh mesh)
    {
        if (mesh.IsValid) Meshes.Remove(mesh.Id);
    }
}
