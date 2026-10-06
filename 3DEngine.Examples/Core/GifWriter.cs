namespace Engine.Examples;

/// <summary>
/// An animated GIF written a frame at a time, which <c>core_screen_recording</c> records the
/// window into, in the place of the msf_gif.h raylib's example includes, with its calls.
/// </summary>
/// <remarks>
/// Each frame has a color table of its own. Its colors are cut to <c>maxBitDepth</c> bits a pixel,
/// split between red, green and blue with the odd one to green, and to a bit fewer at a time until
/// they fit the 256 a table holds, as msf_gif cuts them.
/// </remarks>
internal sealed class GifWriter
{
    private readonly List<byte> _data = [];
    private int _width, _height;

    /// <summary>Starts a GIF of <paramref name="width"/> by <paramref name="height"/> pixels that loops for ever.</summary>
    public void Begin(int width, int height)
    {
        (_width, _height) = (width, height);
        _data.Clear();
        _data.AddRange("GIF89a"u8);
        Short(width);
        Short(height);
        _data.AddRange([0, 0, 0]);
        // The NETSCAPE2.0 block, which has a viewer loop the frames
        _data.AddRange([0x21, 0xFF, 11]);
        _data.AddRange("NETSCAPE2.0"u8);
        _data.AddRange([3, 1, 0, 0, 0]);
    }

    /// <summary>Adds a frame of RGBA pixels, rows from the top <paramref name="pitch"/> bytes apart, shown for <paramref name="centiseconds"/>.</summary>
    public void Frame(byte[] pixels, int centiseconds, int maxBitDepth, int pitch)
    {
        var count = _width * _height;
        var indices = new byte[count];
        var palette = new List<int>();
        for (int depth = Math.Clamp(maxBitDepth, 3, 24); depth >= 3; depth--)
        {
            int blue = depth / 3, red = (depth - blue) / 2, green = depth - blue - red;
            var colors = new Dictionary<int, int>();
            palette.Clear();
            var fits = true;
            for (int i = 0; i < count && fits; i++)
            {
                var at = i / _width * pitch + i % _width * 4;
                var key = pixels[at] >> (8 - red) << (green + blue) | pixels[at + 1] >> (8 - green) << blue | pixels[at + 2] >> (8 - blue);
                if (!colors.TryGetValue(key, out var index))
                {
                    if (colors.Count == 256)
                    {
                        fits = false;
                        break;
                    }
                    colors[key] = index = colors.Count;
                    palette.Add(Expand(key >> (green + blue), red) << 16 | Expand(key >> blue & ((1 << green) - 1), green) << 8 | Expand(key & ((1 << blue) - 1), blue));
                }
                indices[i] = (byte)index;
            }
            if (fits) break;
        }

        var tableBits = 1;
        while (1 << tableBits < palette.Count) tableBits++;

        // How long the frame is shown, before the next is drawn over it
        _data.AddRange([0x21, 0xF9, 4, 0]);
        Short(centiseconds);
        _data.AddRange([0, 0]);

        _data.Add(0x2C);
        Short(0);
        Short(0);
        Short(_width);
        Short(_height);
        _data.Add((byte)(0x80 | (tableBits - 1)));
        for (int i = 0; i < 1 << tableBits; i++)
        {
            var color = i < palette.Count ? palette[i] : 0;
            _data.AddRange([(byte)(color >> 16), (byte)(color >> 8), (byte)color]);
        }
        Compress(indices, Math.Max(2, tableBits));
    }

    /// <summary>Ends the GIF and gives its bytes.</summary>
    public byte[] End()
    {
        _data.Add(0x3B);
        return [.. _data];
    }

    // A color cut to so many bits, spread back over a byte.
    private static int Expand(int value, int bits) => value * 255 / ((1 << bits) - 1);

    private void Short(int value) => _data.AddRange([(byte)value, (byte)(value >> 8)]);

    // The indices compressed by LZW as GIF has it, the codes growing a bit wider as the table
    // grows and starting again at 4096, written in pieces of at most 255 bytes.
    private void Compress(byte[] indices, int minCodeSize)
    {
        _data.Add((byte)minCodeSize);
        int clear = 1 << minCodeSize, end = clear + 1;
        int next = end + 1, codeSize = minCodeSize + 1;
        var table = new Dictionary<int, int>();
        var block = new List<byte>(255);
        int bits = 0, held = 0;

        void Emit(int code)
        {
            held |= code << bits;
            bits += codeSize;
            while (bits >= 8)
            {
                block.Add((byte)held);
                held >>= 8;
                bits -= 8;
                if (block.Count == 255) Flush();
            }
        }
        void Flush()
        {
            _data.Add((byte)block.Count);
            _data.AddRange(block);
            block.Clear();
        }

        Emit(clear);
        int prefix = indices[0];
        for (int i = 1; i < indices.Length; i++)
        {
            int symbol = indices[i], key = prefix << 8 | symbol;
            if (table.TryGetValue(key, out var code))
            {
                prefix = code;
                continue;
            }
            Emit(prefix);
            if (next < 4096)
            {
                table[key] = next++;
                if (next > 1 << codeSize && codeSize < 12) codeSize++;
            }
            else
            {
                Emit(clear);
                table.Clear();
                (next, codeSize) = (end + 1, minCodeSize + 1);
            }
            prefix = symbol;
        }
        Emit(prefix);
        Emit(end);
        if (bits > 0) block.Add((byte)held);
        if (block.Count > 0) Flush();
        _data.Add(0);
    }
}
