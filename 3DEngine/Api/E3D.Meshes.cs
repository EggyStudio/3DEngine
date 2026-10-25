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
    /// colors are kept for the light field's tint, the glow lights and the global illumination
    /// reference, and the GPU is given none, so a shader that reads <c>COLOR0</c> or
    /// <c>TEXCOORD1</c> on such a mesh reads white and zero. Its positions are written in place by
    /// <see cref="UpdateMeshBuffer{T}"/> at 0, it takes no skin, and the pass that scatters light
    /// under a surface leaves it out.
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
    /// bytes in, by raylib's index for it, 0 its positions, 1 its texture coordinates and 2 its
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

    /// <summary>
    /// Makes a mesh of the vertices compute shaders write into storage buffers, drawn as many as a
    /// shader wrote into the first four bytes of <paramref name="count"/>, at most
    /// <paramref name="capacity"/>, with no copy through the CPU and no wait.
    /// </summary>
    /// <param name="positions">
    /// Each vertex's position in the mesh's own space, three floats a vertex, which a shader writes
    /// as a <c>RWStructuredBuffer&lt;float&gt;</c> three to a vertex, every three vertices a triangle.
    /// </param>
    /// <param name="stream">
    /// The vertices' stream of the program's own, one <paramref name="format"/> a vertex, which a
    /// vertex stage of the program's own reads as <c>CUSTOM0</c>, or a default buffer for none.
    /// </param>
    /// <param name="format">What each vertex of <paramref name="stream"/> holds.</param>
    /// <param name="count">The buffer whose first four bytes, a <c>uint</c>, a shader writes the vertex count into.</param>
    /// <param name="capacity">The most vertices drawn, which a larger count is held to, as many as <paramref name="positions"/> holds at most.</param>
    /// <returns>The mesh, or an invalid one where a buffer is not loaded or is too small for the capacity, with the reason in the log.</returns>
    /// <remarks>
    /// <para>
    /// A dispatch writes the buffers and a draw reads them in the same frame, the count turned into
    /// an indirect draw's arguments by the GPU before the frame draws, so a mesher in compute puts a
    /// mesh on the screen with nothing read back. The mesh is drawn by <see cref="DrawMesh"/>, its
    /// shadow cast and its depth drawn as any mesh's, and lit by the model pass's own shader by a zero
    /// normal, as a mesh uploaded from its positions alone is, so a shader of the program's own
    /// unpacks its normal from the stream.
    /// </para>
    /// <para>
    /// It is culled by the box <see cref="SetMeshBoundingBox"/> gives it, and drawn wherever it is
    /// until one is given. Its vertices are on the GPU alone, so picking, bodies and the functions that
    /// read a mesh's vertices pass it over. <see cref="UnloadMesh"/> frees the mesh and not the
    /// buffers, and a buffer unloaded first unloads the meshes over it.
    /// </para>
    /// </remarks>
    public static ModelMesh LoadMeshFromShaderBuffers(ShaderBuffer positions, ShaderBuffer stream, MeshStreamFormat format, ShaderBuffer count, int capacity) =>
        LoadMeshFromShaderBuffers(positions, stream, format, default, count, capacity, 0, 0);

    /// <summary>
    /// Makes a mesh of the vertices compute shaders write into storage buffers, as the overload
    /// without colors does, with each vertex's color from <paramref name="colors"/> where given, and
    /// lying in a range of the buffers, from vertex <paramref name="first"/> as many vertices as a
    /// shader wrote into the uint at <paramref name="countAt"/> of <paramref name="count"/>, at most
    /// <paramref name="capacity"/>.
    /// </summary>
    /// <param name="positions">Each vertex's position, three floats a vertex, as the overload without colors takes them.</param>
    /// <param name="stream">The vertices' stream of the program's own, or a default buffer for none.</param>
    /// <param name="format">What each vertex of <paramref name="stream"/> holds.</param>
    /// <param name="colors">
    /// Each vertex's color, four bytes a vertex, red, green and blue in sRGB and alpha, as a shader
    /// writes a <c>uint</c> of them red lowest, which the model pass multiplies the material by and
    /// the scene's field takes the surface's color from, or a default buffer for none.
    /// </param>
    /// <param name="count">The buffer a shader writes the vertex count into, a <c>uint</c> a mesh.</param>
    /// <param name="capacity">The most vertices drawn, a whole number of triangles.</param>
    /// <param name="first">The mesh's first vertex in <paramref name="positions"/>, its stream and its colors.</param>
    /// <param name="countAt">Which <c>uint</c> of <paramref name="count"/> holds the mesh's vertex count.</param>
    /// <returns>The mesh, or an invalid one where a buffer is not loaded or is too small for the range, with the reason in the log.</returns>
    /// <remarks>
    /// <para>
    /// A program meshing many sections in compute keeps them in a few large buffers, each section a
    /// range of a capacity it may fill and a count of its own, so no section waits for the CPU to
    /// count its faces, and drawing and the scene's field read each range alone.
    /// </para>
    /// <para>
    /// A mesh whose stream is a program's own, as a voxel game's two uints a vertex its terrain
    /// shader unpacks, gives the field no color, every surface of it the material's, unless its
    /// colors are given, as a mesh uploaded from its positions with its colors gives them.
    /// </para>
    /// </remarks>
    public static ModelMesh LoadMeshFromShaderBuffers(ShaderBuffer positions, ShaderBuffer stream, MeshStreamFormat format, ShaderBuffer colors,
        ShaderBuffer count, int capacity, int first, int countAt)
    {
        if (!TryRes<ShaderBufferStore>(out var buffers) || buffers.Get(positions.Id) is not { } held || buffers.Get(count.Id) is not { } counter)
        {
            ApiLogger.Warn("LoadMeshFromShaderBuffers: the positions or the count buffer is not loaded, so no mesh is made.");
            return default;
        }
        if (first < 0 || countAt < 0 || (ulong)(countAt + 1) * 4 > counter.Description.Size)
        {
            ApiLogger.Warn($"LoadMeshFromShaderBuffers: a first vertex of {first} or a count at {countAt} lies outside its buffer, the count's {counter.Description.Size} bytes, so no mesh is made.");
            return default;
        }
        var reach = (ulong)first + (ulong)capacity;
        if (capacity <= 0 || capacity % 3 != 0 || reach * 12 > held.Description.Size)
        {
            ApiLogger.Warn($"LoadMeshFromShaderBuffers: a capacity of {capacity} from vertex {first} is not a whole number of triangles the positions' {held.Description.Size} bytes hold, twelve a vertex, so no mesh is made.");
            return default;
        }
        (IBuffer, MeshStreamFormat)? own = null;
        if (stream.IsValid)
        {
            if (buffers.Get(stream.Id) is not { } streamed || reach * (ulong)MeshStream.SizeOf(format) > streamed.Description.Size)
            {
                ApiLogger.Warn($"LoadMeshFromShaderBuffers: the stream holds fewer than {reach} vertices of {format}, so no mesh is made.");
                return default;
            }
            own = (streamed, format);
        }
        IBuffer? tints = null;
        if (colors.IsValid)
        {
            if (buffers.Get(colors.Id) is not { } colored || reach * 4 > colored.Description.Size)
            {
                ApiLogger.Warn($"LoadMeshFromShaderBuffers: the colors hold fewer than {reach} vertices of four bytes, so no mesh is made.");
                return default;
            }
            tints = colored;
        }
        var id = Meshes.AddCounted(held, own, counter, (uint)capacity, tints, (uint)first, (uint)countAt);
        return new ModelMesh(id, capacity, capacity / 3, default);
    }

    /// <summary>
    /// Sets the box in its own space a mesh over shader buffers is culled by, which
    /// <see cref="GetMeshBoundingBox"/> then gives, since the CPU knows nothing of its vertices.
    /// </summary>
    /// <remarks>
    /// Such a mesh is drawn wherever it is until it is given one. A mesh of vertices on the CPU takes
    /// its box from them, and is left as it is with a warning.
    /// </remarks>
    public static void SetMeshBoundingBox(ModelMesh mesh, BoundingBox box)
    {
        if (!mesh.IsValid) return;
        if (!Meshes.SetCountedBox(mesh.Id, box))
            ApiLogger.Warn("SetMeshBoundingBox: the mesh's box comes from its vertices, a box being set for a mesh over shader buffers alone, so it is left as it is.");
    }

    /// <summary>Frees a mesh. Drawing it afterward draws nothing.</summary>
    public static void UnloadMesh(ModelMesh mesh)
    {
        if (mesh.IsValid) Meshes.Remove(mesh.Id);
    }
}
