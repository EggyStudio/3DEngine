using System.Buffers.Binary;
using System.IO.Compression;
using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// Bevy's tonemapping tables, which <see cref="Tonemap.AgX"/>, <see cref="Tonemap.TonyMcMapface"/>
/// and <see cref="Tonemap.BlenderFilmic"/> look light up in, read from the KTX2 files staged under
/// <c>source/shaders/tonemapping</c> beside every program.
/// </summary>
/// <remarks>
/// Each file is Bevy's own, its one level's Zstandard supercompression swapped for zlib's by
/// <c>build/bevy-luts.py</c>, so .NET reads it with nothing of its own: a cube of texels, x fastest,
/// then y, then z, in half-float RGBA or in RGB9E5 as Bevy has it.
/// </remarks>
internal static class TonemapTables
{
    /// <summary>A table's width, which its height and depth match, its format and its texels.</summary>
    internal sealed record Table(uint Size, VkFormat Format, byte[] Texels);

    private static ReadOnlySpan<byte> Identifier => [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>The file a curve's table is in, under the staged shaders, or null for a curve worked out without one.</summary>
    public static string? FileOf(Tonemap curve) => curve switch
    {
        Tonemap.AgX => "agx.ktx2",
        Tonemap.TonyMcMapface => "tony_mc_mapface.ktx2",
        Tonemap.BlenderFilmic => "blender_filmic.ktx2",
        _ => null,
    };

    /// <summary>The table of <paramref name="curve"/>, read from beside the program.</summary>
    /// <exception cref="InvalidOperationException">The curve has no table.</exception>
    public static Table Load(Tonemap curve) =>
        Read(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "source", "shaders", "tonemapping",
            FileOf(curve) ?? throw new InvalidOperationException($"{curve} is worked out without a table."))));

    /// <summary>
    /// The one level of a KTX2 file holding a cube of texels, as it is or supercompressed with zlib.
    /// </summary>
    /// <exception cref="InvalidDataException">The file is not such a cube.</exception>
    public static Table Read(ReadOnlySpan<byte> file)
    {
        const uint none = 0, zlib = 3;
        if (file.Length < 104 || !file[..12].SequenceEqual(Identifier))
            throw new InvalidDataException("A tonemapping table is a KTX2 file.");
        var header = file[..104].ToArray();
        uint Word(int at) => BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(at));
        ulong Long(int at) => BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(at));
        var (format, width, height, depth, levels, scheme) = ((VkFormat)Word(12), Word(20), Word(24), Word(28), Word(40), Word(44));
        if (width == 0 || width != height || width != depth || levels != 1 || scheme is not (none or zlib))
            throw new InvalidDataException($"A tonemapping table is one level of a cube, as it is or with zlib, not {width} by {height} by {depth} in {levels} levels with scheme {scheme}.");
        var texelBytes = format switch
        {
            VkFormat.R16G16B16A16Sfloat => 8u,
            VkFormat.E5B9G9R9UfloatPack32 => 4u,
            _ => throw new InvalidDataException($"A tonemapping table is in half-float RGBA or RGB9E5, not {format}."),
        };
        var (offset, length, uncompressed) = (Long(80), Long(88), Long(96));
        if (uncompressed != (ulong)width * height * depth * texelBytes || offset + length > (ulong)file.Length)
            throw new InvalidDataException("A tonemapping table's level does not hold its cube.");
        var level = file.Slice((int)offset, (int)length);
        var texels = new byte[uncompressed];
        if (scheme == none) level.CopyTo(texels);
        else
        {
            using var stream = new ZLibStream(new MemoryStream(level.ToArray()), CompressionMode.Decompress);
            stream.ReadExactly(texels);
        }
        return new Table(width, format, texels);
    }
}
