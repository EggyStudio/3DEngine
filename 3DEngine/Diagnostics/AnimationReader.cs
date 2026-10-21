using System.Buffers.Binary;
using System.IO.Compression;

namespace Engine;

/// <summary>
/// Reads an animated GIF or PNG back into its frames as they are shown, each laid over the ones
/// before as the file says, with its delay.
/// </summary>
/// <remarks>
/// What a recording is checked against, so the frames a file holds are read by the same rules a
/// browser shows them by, rather than by counting chunks. It reads what the engine's writers
/// write and what the formats hold besides, a GIF's own palettes, interlaced rows apart, and a
/// PNG of eight bits a channel with or without alpha, its frames blended or laid over and put back
/// after as each says.
/// </remarks>
internal static class AnimationReader
{
    /// <summary>The frames of an animation as they are shown, and how long each is.</summary>
    /// <param name="Frames">The whole picture as each frame leaves it.</param>
    /// <param name="Delays">How long each is shown, in milliseconds.</param>
    /// <param name="Loops">How many times it plays, zero for forever, or -1 where the file does not say.</param>
    internal sealed record Animation(IReadOnlyList<RgbaFrame> Frames, IReadOnlyList<int> Delays, int Loops);

    /// <summary>Reads a GIF.</summary>
    /// <exception cref="InvalidDataException">The bytes are not a GIF this reads.</exception>
    public static Animation Gif(ReadOnlySpan<byte> file)
    {
        if (file.Length < 13 || !(file[..6].SequenceEqual("GIF89a"u8) || file[..6].SequenceEqual("GIF87a"u8)))
            throw new InvalidDataException("The file does not start as a GIF does.");

        int width = BinaryPrimitives.ReadUInt16LittleEndian(file[6..]), height = BinaryPrimitives.ReadUInt16LittleEndian(file[8..]);
        var at = 13;
        byte[]? global = null;
        if ((file[10] & 0x80) != 0)
        {
            var size = 3 << ((file[10] & 7) + 1);
            global = file.Slice(at, size).ToArray();
            at += size;
        }

        var canvas = new byte[width * height * 4];
        var frames = new List<RgbaFrame>();
        var delays = new List<int>();
        var loops = -1;
        int delay = 0, clear = -1, disposal = 0;

        while (at < file.Length)
        {
            switch (file[at++])
            {
                case 0x21 when file[at] == 0xF9:
                    disposal = (file[at + 2] >> 2) & 7;
                    delay = BinaryPrimitives.ReadUInt16LittleEndian(file[(at + 3)..]) * 10;
                    clear = (file[at + 2] & 1) != 0 ? file[at + 5] : -1;
                    at = Skip(file, at + 1);
                    break;

                case 0x21 when file[at] == 0xFF:
                    var named = file.Slice(at + 2, file[at + 1]);
                    var data = at + 2 + file[at + 1];
                    if (named.SequenceEqual("NETSCAPE2.0"u8) && file[data] >= 3 && file[data + 1] == 1)
                        loops = BinaryPrimitives.ReadUInt16LittleEndian(file[(data + 2)..]);
                    at = Skip(file, data);
                    break;

                case 0x21:
                    at = Skip(file, at + 1);
                    break;

                case 0x2C:
                {
                    int left = BinaryPrimitives.ReadUInt16LittleEndian(file[at..]), top = BinaryPrimitives.ReadUInt16LittleEndian(file[(at + 2)..]);
                    int across = BinaryPrimitives.ReadUInt16LittleEndian(file[(at + 4)..]), down = BinaryPrimitives.ReadUInt16LittleEndian(file[(at + 6)..]);
                    var packed = file[at + 8];
                    at += 9;

                    var palette = global;
                    if ((packed & 0x80) != 0)
                    {
                        var size = 3 << ((packed & 7) + 1);
                        palette = file.Slice(at, size).ToArray();
                        at += size;
                    }

                    if ((packed & 0x40) != 0) throw new InvalidDataException("An interlaced GIF frame is not read here.");
                    if (palette is null) throw new InvalidDataException("A GIF frame has no palette to read its colors from.");

                    var least = file[at++];
                    var compressed = new MemoryStream();
                    while (file[at] != 0)
                    {
                        compressed.Write(file.Slice(at + 1, file[at]));
                        at += file[at] + 1;
                    }

                    at++;
                    var indices = Lzw(compressed.ToArray(), least, across * down);
                    var before = disposal == 3 ? (byte[])canvas.Clone() : null;

                    for (var y = 0; y < down; y++)
                    {
                        for (var x = 0; x < across; x++)
                        {
                            int index = indices[y * across + x];
                            if (index == clear || left + x >= width || top + y >= height) continue;

                            var to = ((top + y) * width + left + x) * 4;
                            canvas[to] = palette[index * 3];
                            canvas[to + 1] = palette[index * 3 + 1];
                            canvas[to + 2] = palette[index * 3 + 2];
                            canvas[to + 3] = 255;
                        }
                    }

                    frames.Add(new RgbaFrame(width, height, (byte[])canvas.Clone()));
                    delays.Add(delay);

                    if (disposal == 2) Fill(canvas, width, (left, top, across, down), [0, 0, 0, 0]);
                    if (before is not null) canvas = before;
                    delay = 0;
                    clear = -1;
                    disposal = 0;
                    break;
                }

                case 0x3B:
                    return new Animation(frames, delays, loops);

                default:
                    throw new InvalidDataException($"A GIF block starts with {file[at - 1]:X2} at byte {at - 1}.");
            }
        }

        throw new InvalidDataException("The GIF ends before its trailer.");
    }

    /// <summary>Reads a PNG, animated or still.</summary>
    /// <exception cref="InvalidDataException">The bytes are not a PNG this reads.</exception>
    public static Animation Png(ReadOnlySpan<byte> file)
    {
        ReadOnlySpan<byte> signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
        if (file.Length < 8 || !file[..8].SequenceEqual(signature)) throw new InvalidDataException("The file does not start as a PNG does.");

        int width = 0, height = 0, channels = 4, loops = -1;
        var animated = false;
        var parts = new List<(int X, int Y, int Width, int Height, int Delay, int Dispose, int Blend, MemoryStream Data)>();
        var still = new MemoryStream();
        var at = 8;

        while (at + 12 <= file.Length)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(file[at..]);
            var name = file.Slice(at + 4, 4);
            var data = file.Slice(at + 8, length);
            var check = BinaryPrimitives.ReadUInt32BigEndian(file[(at + 8 + length)..]);
            if ((PngWriter.Crc(0xFFFFFFFFu, file.Slice(at + 4, 4 + length)) ^ 0xFFFFFFFFu) != check)
                throw new InvalidDataException($"The PNG's {System.Text.Encoding.ASCII.GetString(name)} chunk fails its check.");

            at += 12 + length;

            if (name.SequenceEqual("IHDR"u8))
            {
                width = (int)BinaryPrimitives.ReadUInt32BigEndian(data);
                height = (int)BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
                if (data[8] != 8 || data[9] is not (2 or 6) || data[12] != 0)
                    throw new InvalidDataException("Only a PNG of eight bits a channel, with or without alpha and not interlaced, is read here.");
                channels = data[9] == 6 ? 4 : 3;
            }
            else if (name.SequenceEqual("acTL"u8))
            {
                animated = true;
                loops = (int)BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
            }
            else if (name.SequenceEqual("fcTL"u8))
            {
                var numerator = BinaryPrimitives.ReadUInt16BigEndian(data[20..]);
                var denominator = BinaryPrimitives.ReadUInt16BigEndian(data[22..]);
                parts.Add(((int)BinaryPrimitives.ReadUInt32BigEndian(data[12..]), (int)BinaryPrimitives.ReadUInt32BigEndian(data[16..]),
                    (int)BinaryPrimitives.ReadUInt32BigEndian(data[4..]), (int)BinaryPrimitives.ReadUInt32BigEndian(data[8..]),
                    numerator * 1000 / (denominator == 0 ? 100 : denominator), data[24], data[25], new MemoryStream()));
            }
            else if (name.SequenceEqual("IDAT"u8))
            {
                // The picture, which is the first frame where a frame's control came before it, and
                // a picture shown by readers that know no animation where none did.
                (parts.Count > 0 ? parts[^1].Data : still).Write(data);
            }
            else if (name.SequenceEqual("fdAT"u8))
            {
                parts[^1].Data.Write(data[4..]);
            }
            else if (name.SequenceEqual("IEND"u8))
            {
                break;
            }
        }

        if (!animated || parts.Count == 0)
            return new Animation([new RgbaFrame(width, height, Rows(still.ToArray(), width, height, channels))], [0], loops);

        var canvas = new byte[width * height * 4];
        var frames = new List<RgbaFrame>();
        var delays = new List<int>();

        foreach (var part in parts)
        {
            var pixels = Rows(part.Data.ToArray(), part.Width, part.Height, channels);
            var before = part.Dispose == 2 ? (byte[])canvas.Clone() : null;

            for (var y = 0; y < part.Height; y++)
            {
                for (var x = 0; x < part.Width; x++)
                {
                    var from = (y * part.Width + x) * 4;
                    var to = ((part.Y + y) * width + part.X + x) * 4;
                    if (part.Blend == 0 || pixels[from + 3] == 255)
                    {
                        Array.Copy(pixels, from, canvas, to, 4);
                        continue;
                    }

                    // Laid over what is there by its alpha, as the format's blend says.
                    var alpha = pixels[from + 3] / 255f;
                    for (var c = 0; c < 3; c++) canvas[to + c] = (byte)MathF.Round(pixels[from + c] * alpha + canvas[to + c] * (1f - alpha));
                    canvas[to + 3] = (byte)Math.Max(canvas[to + 3], pixels[from + 3]);
                }
            }

            frames.Add(new RgbaFrame(width, height, (byte[])canvas.Clone()));
            delays.Add(part.Delay);

            if (part.Dispose == 1) Fill(canvas, width, (part.X, part.Y, part.Width, part.Height), [0, 0, 0, 0]);
            if (before is not null) canvas = before;
        }

        return new Animation(frames, delays, loops);
    }

    /// <summary>A PNG's rows inflated and their filters undone, as four bytes a pixel.</summary>
    private static byte[] Rows(byte[] compressed, int width, int height, int channels)
    {
        using var inflated = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
        var stride = width * channels;
        var raw = new byte[(stride + 1) * height];
        inflated.ReadExactly(raw);

        var pixels = new byte[width * height * 4];
        var above = new byte[stride];
        var row = new byte[stride];

        for (var y = 0; y < height; y++)
        {
            var filter = raw[y * (stride + 1)];
            Array.Copy(raw, y * (stride + 1) + 1, row, 0, stride);

            for (var i = 0; i < stride; i++)
            {
                var left = i >= channels ? row[i - channels] : 0;
                var up = above[i];
                var corner = i >= channels ? above[i - channels] : 0;
                row[i] = (byte)(row[i] + filter switch
                {
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => PngWriter.Paeth(left, up, corner),
                    _ => 0,
                });
            }

            for (var x = 0; x < width; x++)
            {
                var to = (y * width + x) * 4;
                pixels[to] = row[x * channels];
                pixels[to + 1] = row[x * channels + 1];
                pixels[to + 2] = row[x * channels + 2];
                pixels[to + 3] = channels == 4 ? row[x * channels + 3] : (byte)255;
            }

            (above, row) = (row, above);
        }

        return pixels;
    }

    /// <summary>A GIF frame's codes read back into its indices, the table grown and started again as the writer did.</summary>
    private static byte[] Lzw(byte[] data, int least, int count)
    {
        var start = 1 << least;
        var end = start + 1;
        var size = least + 1;
        var next = end + 1;
        var prefixes = new int[4096];
        var suffixes = new byte[4096];
        var lengths = new int[4096];
        for (var code = 0; code < start; code++)
        {
            suffixes[code] = (byte)code;
            lengths[code] = 1;
        }

        var indices = new byte[count];
        var written = 0;
        var previous = -1;
        int bits = 0, pending = 0, at = 0;

        while (written < count)
        {
            while (pending < size)
            {
                if (at >= data.Length) return indices;
                bits |= data[at++] << pending;
                pending += 8;
            }

            var code = bits & ((1 << size) - 1);
            bits >>= size;
            pending -= size;

            if (code == start)
            {
                size = least + 1;
                next = end + 1;
                previous = -1;
                continue;
            }

            if (code == end) break;

            // A code not in the table yet is the one about to be added, the previous run and its
            // own first index.
            int first;
            if (previous < 0)
            {
                first = code;
                written = Emit(code, indices, written, prefixes, suffixes, lengths);
            }
            else if (code < next)
            {
                written = Emit(code, indices, written, prefixes, suffixes, lengths);
                first = FirstOf(code, prefixes, lengths);
            }
            else
            {
                first = FirstOf(previous, prefixes, lengths);
                written = Emit(previous, indices, written, prefixes, suffixes, lengths);
                if (written < count) indices[written++] = (byte)first;
            }

            if (previous >= 0 && next < 4096)
            {
                prefixes[next] = previous;
                suffixes[next] = (byte)first;
                lengths[next] = lengths[previous] + 1;
                next++;
                if (next == 1 << size && size < 12) size++;
            }

            previous = code;
        }

        return indices;
    }

    private static int Emit(int code, byte[] indices, int written, int[] prefixes, byte[] suffixes, int[] lengths)
    {
        var length = lengths[code];
        var last = Math.Min(written + length, indices.Length) - 1;
        for (var i = written + length - 1; i >= written; i--)
        {
            if (i <= last) indices[i] = suffixes[code];
            code = prefixes[code];
        }

        return last + 1;
    }

    private static int FirstOf(int code, int[] prefixes, int[] lengths)
    {
        for (var i = lengths[code]; i > 1; i--) code = prefixes[code];
        return code;
    }

    private static int Skip(ReadOnlySpan<byte> file, int at)
    {
        while (file[at] != 0) at += file[at] + 1;
        return at + 1;
    }

    private static void Fill(byte[] canvas, int width, (int X, int Y, int Width, int Height) area, ReadOnlySpan<byte> color)
    {
        for (var y = area.Y; y < area.Y + area.Height; y++)
        {
            for (var x = area.X; x < area.X + area.Width; x++) color.CopyTo(canvas.AsSpan((y * width + x) * 4));
        }
    }
}
