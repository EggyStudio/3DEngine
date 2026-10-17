using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// A vertex stream of the program's own, a value each vertex of a mesh carries beside its position,
/// normal and texture coordinates, which a vertex stage of the program's own reads as
/// <c>CUSTOM0</c>.
/// </summary>
/// <remarks>
/// <para>
/// It is for what a vertex holds that the mesh's own streams do not say, such as a voxel's face and
/// texture packed into four bytes and unpacked in the shader, a blend weight or a wind phase. The stage declares the
/// input by its semantic, as <c>uint packed : CUSTOM0</c>, and the model pass places it where Slang
/// put it. A mesh carries one stream, given to <see cref="E3D.UploadMesh(ModelVertex[], uint[], Color[], System.Numerics.Vector2[], MeshStream)"/>
/// and written in place by <see cref="E3D.UpdateMeshBuffer{T}"/> at
/// <see cref="E3D.MeshStreamBuffer"/>, which never uploads the mesh again.
/// </para>
/// <para>
/// Bytes packed for the shader are a <see cref="MeshStreamFormat.UInt"/> and not a float, because
/// the bits of a float are read as a float, so a value whose bits 23 to 30 are all set is not a
/// number and one whose are all clear is a denormal, which a GPU may flush to zero. A shader that
/// reads the stream drawn with a mesh without one reads zero.
/// </para>
/// </remarks>
/// <param name="Format">What each vertex holds.</param>
/// <param name="Data">The format's bytes for each vertex in turn, as <see cref="From{T}"/> lays them out.</param>
public sealed record MeshStream(MeshStreamFormat Format, byte[] Data)
{
    /// <summary>How many vertices the stream holds.</summary>
    public int Count => Data.Length / SizeOf(Format);

    /// <summary>
    /// A stream from values laid out as the format's bytes, such as a <c>uint</c> a vertex for
    /// <see cref="MeshStreamFormat.UInt"/> or a <see cref="System.Numerics.Vector4"/> a vertex for
    /// <see cref="MeshStreamFormat.Float4"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The values are not a whole number of the format's vertices.</exception>
    public static MeshStream From<T>(MeshStreamFormat format, ReadOnlySpan<T> values) where T : unmanaged
    {
        var bytes = MemoryMarshal.AsBytes(values).ToArray();
        if (bytes.Length % SizeOf(format) != 0)
            throw new ArgumentException($"{bytes.Length} bytes are not a whole number of {format} vertices, which are {SizeOf(format)} bytes each.", nameof(values));
        return new MeshStream(format, bytes);
    }

    /// <summary>Bytes a vertex of <paramref name="format"/>.</summary>
    public static int SizeOf(MeshStreamFormat format) => format switch
    {
        MeshStreamFormat.UInt2 => 8,
        MeshStreamFormat.Float4 => 16,
        _ => 4,
    };
}
