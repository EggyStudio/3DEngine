using System.Buffers.Binary;
using System.IO.Compression;

namespace Engine;

/// <summary>Writes RGBA pixels as a PNG file, a still picture or an animated one, with no dependency beyond the base library.</summary>
/// <remarks>
/// <para>
/// A still picture is one IDAT chunk, filter type 0 on every row and zlib at the fastest level,
/// which a screenshot needs, since it is correct, quick to write and read by everything.
/// </para>
/// <para>
/// An animated PNG is a PNG whose first frame is its picture, so a reader that knows nothing of
/// animation shows the first frame, and whose other frames follow in chunks of their own, each with
/// its delay, as browsers and GitHub's pages play them. A frame after the first holds the rectangle
/// that changed since the frame before, laid over it, so a still part of a recording costs nothing
/// after its first frame, and each row is filtered the way that leaves it smallest, as a PNG
/// writer's adaptive filter does, since a recording of many frames is the file a page carries.
/// </para>
/// </remarks>
internal static class PngWriter
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

    /// <summary>Writes an animated PNG of frames of one size, each shown for its delay.</summary>
    /// <param name="output">Where the file goes.</param>
    /// <param name="frames">The frames, all the size of the first.</param>
    /// <param name="delays">How long each is shown, in milliseconds, up to a minute.</param>
    /// <param name="loops">How many times it plays, zero for forever.</param>
    /// <exception cref="ArgumentException">There are no frames.</exception>
    public static void Animated(Stream output, IReadOnlyList<RgbaFrame> frames, IReadOnlyList<int> delays, int loops = 0)
    {
        if (frames.Count == 0) throw new ArgumentException("An animation has a frame at least.", nameof(frames));

        var width = frames[0].Width;
        var height = frames[0].Height;
        output.Write([0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]);
        Span<byte> header = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header[4..], height);
        header[8] = 8;  // bits per channel
        header[9] = 6;  // RGBA
        Chunk(output, "IHDR"u8, header);

        Span<byte> control = stackalloc byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(control, (uint)frames.Count);
        BinaryPrimitives.WriteUInt32BigEndian(control[4..], (uint)loops);
        Chunk(output, "acTL"u8, control);

        // Frame controls and frame data are numbered together, from zero, so a reader finds a chunk
        // missing or out of order.
        var sequence = 0u;
        Span<byte> frameControl = stackalloc byte[26];
        for (var i = 0; i < frames.Count; i++)
        {
            var area = i == 0 ? (0, 0, width, height) : frames[i].ChangedFrom(frames[i - 1]);
            // A frame like the one before still takes its time, as one pixel laid over itself.
            if (area.Item3 == 0) area = (0, 0, 1, 1);

            BinaryPrimitives.WriteUInt32BigEndian(frameControl, sequence++);
            BinaryPrimitives.WriteUInt32BigEndian(frameControl[4..], (uint)area.Item3);
            BinaryPrimitives.WriteUInt32BigEndian(frameControl[8..], (uint)area.Item4);
            BinaryPrimitives.WriteUInt32BigEndian(frameControl[12..], (uint)area.Item1);
            BinaryPrimitives.WriteUInt32BigEndian(frameControl[16..], (uint)area.Item2);
            BinaryPrimitives.WriteUInt16BigEndian(frameControl[20..], (ushort)Math.Clamp(delays[i], 0, 60_000));
            BinaryPrimitives.WriteUInt16BigEndian(frameControl[22..], 1000);
            frameControl[24] = 0;  // left as it is for the next frame
            frameControl[25] = 0;  // written over what is there, not blended
            Chunk(output, "fcTL"u8, frameControl);

            var rows = Filtered(frames[i], area);
            if (i == 0)
            {
                Chunk(output, "IDAT"u8, rows);
                continue;
            }
            var data = new byte[rows.Length + 4];
            BinaryPrimitives.WriteUInt32BigEndian(data, sequence++);
            rows.CopyTo(data, 4);
            Chunk(output, "fdAT"u8, data);
        }
        Chunk(output, "IEND"u8, []);
    }

    // A rectangle of a frame's rows, each led by the filter that leaves it smallest, compressed.
    private static byte[] Filtered(RgbaFrame frame, (int X, int Y, int Width, int Height) area)
    {
        var stride = area.Width * 4;
        var rows = new MemoryStream();
        using (var packed = new ZLibStream(rows, CompressionLevel.Optimal, leaveOpen: true))
        {
            var above = new byte[stride];
            var row = new byte[stride];
            var best = new byte[stride];
            var trial = new byte[stride];
            for (var y = 0; y < area.Height; y++)
            {
                Array.Copy(frame.Pixels, ((area.Y + y) * frame.Width + area.X) * 4, row, 0, stride);
                // Each of the five filters tried, and the one whose bytes, read as signed, sum
                // smallest kept, which is the measure the format's own writers use.
                var chosen = 0;
                var smallest = long.MaxValue;
                for (var filter = 0; filter < 5; filter++)
                {
                    long sum = 0;
                    for (var i = 0; i < stride; i++)
                    {
                        var left = i >= 4 ? row[i - 4] : 0;
                        var up = above[i];
                        var corner = i >= 4 ? above[i - 4] : 0;
                        var value = (byte)(row[i] - filter switch
                        {
                            1 => left,
                            2 => up,
                            3 => (left + up) / 2,
                            4 => Paeth(left, up, corner),
                            _ => 0,
                        });
                        trial[i] = value;
                        sum += Math.Abs((int)(sbyte)value);
                    }
                    if (sum >= smallest) continue;
                    smallest = sum;
                    chosen = filter;
                    (best, trial) = (trial, best);
                }
                packed.WriteByte((byte)chosen);
                packed.Write(best);
                (above, row) = (row, above);
            }
        }
        return rows.ToArray();
    }

    // The neighbor nearest a guess made from three, the left, the one above and the one above left.
    internal static int Paeth(int left, int up, int corner)
    {
        var guess = left + up - corner;
        var toLeft = Math.Abs(guess - left);
        var toUp = Math.Abs(guess - up);
        var toCorner = Math.Abs(guess - corner);
        if (toLeft <= toUp && toLeft <= toCorner) return left;
        return toUp <= toCorner ? up : corner;
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

    internal static uint Crc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes) crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
