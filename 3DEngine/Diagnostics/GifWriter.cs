namespace Engine;

/// <summary>Writes an animated GIF of frames, in managed code with nothing taken for it.</summary>
/// <remarks>
/// <para>
/// A GIF holds 256 colors, so one palette is chosen for every frame, by median cut over all of
/// their pixels: the colors are counted in 32 steps a channel, and the box of colors holding the
/// most pixels across the widest range is cut at its median along that range until there are 255,
/// the last kept clear. Each color is the mean of the pixels its box holds rather than the box's
/// middle, so a flat color the picture holds comes back as it was.
/// </para>
/// <para>
/// A pixel between two colors of the palette is dithered by a pattern rather than by spreading
/// its error, between the color nearest it and the one on the other side, the second chosen where
/// the pixel lies further toward it than the pattern's threshold at that place. A pattern is the
/// same in every frame, so a part of a picture that does not move gives the same indices in each,
/// where spread error would shimmer, and a pixel that is a color of the palette is never dithered.
/// </para>
/// <para>
/// A frame after the first holds the rectangle that changed since the frame before, its pixels
/// that did not change left clear, and is laid over what was shown, so a still part costs nothing
/// after its first frame. Delays are kept in hundredths of a second, the format's unit, each
/// rounded so the sum of those written stays as near as it can to the sum of those given.
/// </para>
/// </remarks>
internal static class GifWriter
{
    /// <summary>The index left clear, which a frame after the first gives a pixel that did not change.</summary>
    private const int Clear = 255;

    /// <summary>The threshold each pixel of an eight by eight tile is held to, Bayer's, a sixty-fourth apart.</summary>
    private static readonly byte[] Pattern =
    [
        0, 32, 8, 40, 2, 34, 10, 42,
        48, 16, 56, 24, 50, 18, 58, 26,
        12, 44, 4, 36, 14, 46, 6, 38,
        60, 28, 52, 20, 62, 30, 54, 22,
        3, 35, 11, 43, 1, 33, 9, 41,
        51, 19, 59, 27, 49, 17, 57, 25,
        15, 47, 7, 39, 13, 45, 5, 37,
        63, 31, 55, 23, 61, 29, 53, 21,
    ];

    /// <summary>Writes an animated GIF of frames of one size, each shown for its delay.</summary>
    /// <param name="output">Where the file goes.</param>
    /// <param name="frames">The frames, all the size of the first, which the format holds up to 65535 a side.</param>
    /// <param name="delays">How long each is shown, in milliseconds.</param>
    /// <param name="loops">How many times it plays after the first, zero for forever.</param>
    public static void Write(Stream output, IReadOnlyList<RgbaFrame> frames, IReadOnlyList<int> delays, int loops = 0)
    {
        if (frames.Count == 0) throw new ArgumentException("An animation has a frame at least.", nameof(frames));

        var width = frames[0].Width;
        var height = frames[0].Height;
        var palette = MedianCut(frames, Clear);
        var nearest = Nearest(palette.Colors, palette.Means);

        var writer = new BinaryWriter(output);
        writer.Write("GIF89a"u8);
        writer.Write((ushort)width);
        writer.Write((ushort)height);
        writer.Write((byte)0xF7); // a global palette of 256 colors, eight bits a channel
        writer.Write((byte)0);    // the background, which nothing shows
        writer.Write((byte)0);    // square pixels

        var table = new byte[256 * 3];
        palette.Colors.AsSpan().CopyTo(table);
        writer.Write(table);

        // Netscape's block, which every reader takes for how many times to play.
        writer.Write([0x21, 0xFF, 0x0B]);
        writer.Write("NETSCAPE2.0"u8);
        writer.Write([0x03, 0x01]);
        writer.Write((ushort)loops);
        writer.Write((byte)0);

        byte[]? shown = null;
        long given = 0, written = 0;

        for (var i = 0; i < frames.Count; i++)
        {
            var indices = Map(frames[i], palette.Colors, nearest);
            var (left, top, across, down) = shown is null ? (0, 0, width, height) : Changed(indices, shown, width, height);

            given += delays[i];
            var hundredths = (int)Math.Clamp((given + 5) / 10 - written, 0, ushort.MaxValue);
            written += hundredths;

            writer.Write([0x21, 0xF9, 0x04]);
            writer.Write((byte)((1 << 2) | (shown is null ? 0 : 1))); // left in place, and clear where it did not change
            writer.Write((ushort)hundredths);
            writer.Write((byte)Clear);
            writer.Write((byte)0);

            writer.Write((byte)0x2C);
            writer.Write((ushort)left);
            writer.Write((ushort)top);
            writer.Write((ushort)across);
            writer.Write((ushort)down);
            writer.Write((byte)0); // no palette of its own, not interlaced

            var part = new byte[across * down];
            for (var y = 0; y < down; y++)
            {
                for (var x = 0; x < across; x++)
                {
                    var at = (top + y) * width + left + x;
                    part[y * across + x] = shown is not null && shown[at] == indices[at] ? (byte)Clear : indices[at];
                }
            }

            writer.Flush();
            Lzw(output, part);
            shown = indices;
        }

        output.WriteByte(0x3B);
        output.Flush();
    }

    /// <summary>The palette's colors, three bytes each, and the bins' mean colors it was cut from.</summary>
    internal readonly record struct Cut(byte[] Colors, int Count, int[] Means);

    /// <summary>
    /// Cuts the colors of every frame into as many as <paramref name="most"/>, by median cut over
    /// their counts in 32 steps a channel.
    /// </summary>
    internal static Cut MedianCut(IReadOnlyList<RgbaFrame> frames, int most)
    {
        const int Bins = 1 << 15;
        var counts = new long[Bins];
        var sums = new long[Bins * 3];

        foreach (var frame in frames)
        {
            var pixels = frame.Pixels;
            for (var at = 0; at < pixels.Length; at += 4)
            {
                var bin = Bin(pixels[at], pixels[at + 1], pixels[at + 2]);
                counts[bin]++;
                sums[bin * 3] += pixels[at];
                sums[bin * 3 + 1] += pixels[at + 1];
                sums[bin * 3 + 2] += pixels[at + 2];
            }
        }

        // The mean color of each bin, or its middle where nothing fell in it, against which the
        // palette is matched for every color a frame holds.
        var means = new int[Bins];
        for (var bin = 0; bin < Bins; bin++)
        {
            means[bin] = counts[bin] == 0
                ? Pack(((bin >> 10) << 3) + 4, (((bin >> 5) & 31) << 3) + 4, ((bin & 31) << 3) + 4)
                : Pack((int)(sums[bin * 3] / counts[bin]), (int)(sums[bin * 3 + 1] / counts[bin]), (int)(sums[bin * 3 + 2] / counts[bin]));
        }

        var used = Enumerable.Range(0, Bins).Where(bin => counts[bin] > 0).ToArray();
        var boxes = new List<(int Start, int End)> { (0, used.Length) };

        while (boxes.Count < most)
        {
            // The box holding the most pixels across the widest range, by their product, so a
            // wide range few pixels fall in waits behind a narrower one most of the picture is.
            var chosen = -1;
            var axis = 0;
            double score = 0;
            for (var b = 0; b < boxes.Count; b++)
            {
                var (start, end) = boxes[b];
                if (end - start < 2) continue;

                var (side, along) = Widest(used, start, end);
                long pixels = 0;
                for (var i = start; i < end; i++) pixels += counts[used[i]];

                if (side == 0 || (double)pixels * side <= score) continue;
                score = (double)pixels * side;
                chosen = b;
                axis = along;
            }

            if (chosen < 0) break;

            var (from, to) = boxes[chosen];
            var shift = axis switch { 0 => 10, 1 => 5, _ => 0 };
            Array.Sort(used, from, to - from, Comparer<int>.Create((a, b) => ((a >> shift) & 31) - ((b >> shift) & 31)));

            long total = 0, half = 0;
            for (var i = from; i < to; i++) total += counts[used[i]];

            var split = from + 1;
            for (var i = from; i < to - 1; i++)
            {
                half += counts[used[i]];
                split = i + 1;
                if (half * 2 >= total) break;
            }

            boxes[chosen] = (from, split);
            boxes.Add((split, to));
        }

        var colors = new byte[boxes.Count * 3];
        for (var b = 0; b < boxes.Count; b++)
        {
            long count = 0, r = 0, g = 0, bl = 0;
            for (var i = boxes[b].Start; i < boxes[b].End; i++)
            {
                var bin = used[i];
                count += counts[bin];
                r += sums[bin * 3];
                g += sums[bin * 3 + 1];
                bl += sums[bin * 3 + 2];
            }

            colors[b * 3] = (byte)((r + count / 2) / count);
            colors[b * 3 + 1] = (byte)((g + count / 2) / count);
            colors[b * 3 + 2] = (byte)((bl + count / 2) / count);
        }

        return new Cut(colors, boxes.Count, means);
    }

    /// <summary>For each bin, the color of the palette nearest the bin's mean.</summary>
    private static byte[] Nearest(byte[] colors, int[] means)
    {
        var count = colors.Length / 3;
        var nearest = new byte[means.Length];

        for (var bin = 0; bin < means.Length; bin++)
        {
            var (r, g, b) = Unpack(means[bin]);
            var best = 0;
            var smallest = int.MaxValue;
            for (var i = 0; i < count; i++)
            {
                var dr = r - colors[i * 3];
                var dg = g - colors[i * 3 + 1];
                var db = b - colors[i * 3 + 2];
                var distance = dr * dr + dg * dg + db * db;
                if (distance >= smallest) continue;
                smallest = distance;
                best = i;
            }

            nearest[bin] = (byte)best;
        }

        return nearest;
    }

    /// <summary>A frame's pixels as indices of the palette, dithered by the pattern.</summary>
    private static byte[] Map(RgbaFrame frame, byte[] colors, byte[] nearest)
    {
        var indices = new byte[frame.Width * frame.Height];
        var pixels = frame.Pixels;

        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var at = (y * frame.Width + x) * 4;
                int r = pixels[at], g = pixels[at + 1], b = pixels[at + 2];

                var first = nearest[Bin(r, g, b)];
                int er = r - colors[first * 3], eg = g - colors[first * 3 + 1], eb = b - colors[first * 3 + 2];
                var chosen = first;

                // The color on the other side of the pixel from the nearest, and how far toward it
                // the pixel lies, which the pattern's threshold at this place decides between.
                if (er * er + eg * eg + eb * eb > 3)
                {
                    var second = nearest[Bin(Math.Clamp(r + er, 0, 255), Math.Clamp(g + eg, 0, 255), Math.Clamp(b + eb, 0, 255))];
                    if (second != first)
                    {
                        int dr = colors[second * 3] - colors[first * 3], dg = colors[second * 3 + 1] - colors[first * 3 + 1], db = colors[second * 3 + 2] - colors[first * 3 + 2];
                        var toward = (float)(er * dr + eg * dg + eb * db) / (dr * dr + dg * dg + db * db);
                        if (toward > (Pattern[(y & 7) * 8 + (x & 7)] + 0.5f) / 64f) chosen = second;
                    }
                }

                indices[y * frame.Width + x] = chosen;
            }
        }

        return indices;
    }

    /// <summary>The rectangle holding every index that differs from those shown, or one pixel where none does.</summary>
    private static (int Left, int Top, int Across, int Down) Changed(byte[] now, byte[] shown, int width, int height)
    {
        int left = width, top = height, right = -1, bottom = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (now[y * width + x] == shown[y * width + x]) continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        // A frame like the one before still takes its time, as one clear pixel.
        return bottom < 0 ? (0, 0, 1, 1) : (left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>
    /// Writes indices compressed by the format's LZW, from codes of nine bits growing to twelve,
    /// the table started again once it holds 4096, in blocks of up to 255 bytes.
    /// </summary>
    private static void Lzw(Stream output, ReadOnlySpan<byte> indices)
    {
        const int Least = 8;
        const int Start = 1 << Least;
        const int End = Start + 1;

        output.WriteByte(Least);
        var blocks = new Blocks(output);
        var size = Least + 1;
        var next = End + 1;
        var table = new Dictionary<int, int>(4096);

        blocks.Code(Start, size);
        int prefix = indices[0];

        for (var i = 1; i < indices.Length; i++)
        {
            int index = indices[i];
            var key = (prefix << 8) | index;
            if (table.TryGetValue(key, out var known))
            {
                prefix = known;
                continue;
            }

            blocks.Code(prefix, size);
            if (next == 4096)
            {
                blocks.Code(Start, size);
                table.Clear();
                next = End + 1;
                size = Least + 1;
            }
            else
            {
                // A code is read as wide as the next one the table will hold needs, so the width
                // grows as the code about to be added stops fitting.
                if (next >= 1 << size) size++;
                table[key] = next++;
            }

            prefix = index;
        }

        blocks.Code(prefix, size);
        blocks.Code(End, size);
        blocks.Finish();
    }

    /// <summary>Packs codes a bit at a time from the lowest, in blocks led by their length.</summary>
    private sealed class Blocks(Stream output)
    {
        private readonly byte[] _block = new byte[255];
        private int _filled;
        private int _bits;
        private int _pending;

        public void Code(int code, int size)
        {
            _bits |= code << _pending;
            _pending += size;

            while (_pending >= 8)
            {
                Byte((byte)_bits);
                _bits >>= 8;
                _pending -= 8;
            }
        }

        public void Finish()
        {
            if (_pending > 0) Byte((byte)_bits);
            if (_filled > 0) Flush();
            output.WriteByte(0);
        }

        private void Byte(byte value)
        {
            _block[_filled++] = value;
            if (_filled == _block.Length) Flush();
        }

        private void Flush()
        {
            output.WriteByte((byte)_filled);
            output.Write(_block, 0, _filled);
            _filled = 0;
        }
    }

    private static (int Side, int Axis) Widest(int[] used, int start, int end)
    {
        int rLow = 31, rHigh = 0, gLow = 31, gHigh = 0, bLow = 31, bHigh = 0;
        for (var i = start; i < end; i++)
        {
            var bin = used[i];
            int r = bin >> 10, g = (bin >> 5) & 31, b = bin & 31;
            rLow = Math.Min(rLow, r); rHigh = Math.Max(rHigh, r);
            gLow = Math.Min(gLow, g); gHigh = Math.Max(gHigh, g);
            bLow = Math.Min(bLow, b); bHigh = Math.Max(bHigh, b);
        }

        int red = rHigh - rLow, green = gHigh - gLow, blue = bHigh - bLow;
        if (green >= red && green >= blue) return (green, 1);
        return red >= blue ? (red, 0) : (blue, 2);
    }

    private static int Bin(int r, int g, int b) => ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);

    private static int Pack(int r, int g, int b) => (r << 16) | (g << 8) | b;

    private static (int R, int G, int B) Unpack(int color) => (color >> 16, (color >> 8) & 255, color & 255);
}
