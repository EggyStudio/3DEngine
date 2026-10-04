using System.Buffers.Binary;
using System.IO.Compression;

namespace Engine;

/// <summary>Writes RGBA pixels as a PNG file, with no dependency beyond the base library.</summary>
/// <remarks>
/// One IDAT chunk, filter type 0 on every row and zlib at the fastest level, which is what a
/// screenshot needs: correct, quick to write, and read by everything.
/// </remarks>
public static class PngWriter
{
    /// <summary>Writes <paramref name="rgba"/> (four bytes per pixel, rows from the top) to <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentException">The pixels do not match the size.</exception>
    public static void Write(string path, ReadOnlySpan<byte> rgba, int width, int height)
    {
        if (width <= 0 || height <= 0 || rgba.Length != width * height * 4)
            throw new ArgumentException($"Expected {width} by {height} pixels of four bytes, and got {rgba.Length} bytes.", nameof(rgba));

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        using var file = File.Create(path);
        Write(file, rgba, width, height);
    }

    /// <summary>Writes <paramref name="rgba"/> (four bytes per pixel, rows from the top) as a PNG to <paramref name="file"/>.</summary>
    /// <exception cref="ArgumentException">The pixels do not match the size.</exception>
    public static void Write(Stream file, ReadOnlySpan<byte> rgba, int width, int height)
    {
        if (width <= 0 || height <= 0 || rgba.Length != width * height * 4)
            throw new ArgumentException($"Expected {width} by {height} pixels of four bytes, and got {rgba.Length} bytes.", nameof(rgba));

        file.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;  // bits per channel
        header[9] = 6;  // RGBA
        Chunk(file, "IHDR"u8, header);

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            var stride = width * 4;
            for (int y = 0; y < height; y++)
            {
                zlib.WriteByte(0);
                zlib.Write(rgba.Slice(y * stride, stride));
            }
        }
        Chunk(file, "IDAT"u8, compressed.ToArray());
        Chunk(file, "IEND"u8, []);
    }

    private static void Chunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        stream.Write(type);
        stream.Write(data);

        Span<byte> sum = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(sum, Crc(Crc(0xFFFFFFFFu, type), data) ^ 0xFFFFFFFFu);
        stream.Write(sum);
    }

    // The CRC-32 PNG specifies (polynomial 0xEDB88320), by table. System.IO.Hashing has one, but
    // as a package of its own, which this does not justify.
    private static readonly uint[] Table = Enumerable.Range(0, 256).Select(n =>
    {
        var c = (uint)n;
        for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        return c;
    }).ToArray();

    private static uint Crc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
