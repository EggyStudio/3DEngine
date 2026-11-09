namespace Engine;

/// <summary>A picture of a recording, four bytes a pixel, red, green, blue then alpha, rows top to bottom.</summary>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Pixels">Its pixels, four bytes each.</param>
internal readonly record struct RgbaFrame(int Width, int Height, byte[] Pixels)
{
    /// <summary>The frame at another size, each pixel the mean of those it covers, or the nearest where it grows.</summary>
    /// <remarks>
    /// A mean over the source's pixels each target pixel covers, so a capture made smaller keeps
    /// thin lines as a fainter line rather than dropping them between the pixels kept, as taking
    /// every other pixel would.
    /// </remarks>
    public RgbaFrame Resized(int width, int height)
    {
        if (width == Width && height == Height) return this;

        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var top = y * Height / height;
            var bottom = Math.Max(top + 1, (y + 1) * Height / height);

            for (var x = 0; x < width; x++)
            {
                var left = x * Width / width;
                var right = Math.Max(left + 1, (x + 1) * Width / width);
                int r = 0, g = 0, b = 0, a = 0, count = 0;

                for (var sy = top; sy < bottom; sy++)
                {
                    for (var sx = left; sx < right; sx++)
                    {
                        var at = (sy * Width + sx) * 4;
                        r += Pixels[at];
                        g += Pixels[at + 1];
                        b += Pixels[at + 2];
                        a += Pixels[at + 3];
                        count++;
                    }
                }

                var to = (y * width + x) * 4;
                pixels[to] = (byte)((r + count / 2) / count);
                pixels[to + 1] = (byte)((g + count / 2) / count);
                pixels[to + 2] = (byte)((b + count / 2) / count);
                pixels[to + 3] = (byte)((a + count / 2) / count);
            }
        }

        return new RgbaFrame(width, height, pixels);
    }

    /// <summary>
    /// The smallest rectangle holding every pixel that differs from another frame of the same size,
    /// or an empty one where none does.
    /// </summary>
    public (int X, int Y, int Width, int Height) ChangedFrom(RgbaFrame before)
    {
        int left = Width, top = Height, right = -1, bottom = -1;
        var now = Pixels.AsSpan();
        var then = before.Pixels.AsSpan();

        for (var y = 0; y < Height; y++)
        {
            var row = y * Width * 4;
            if (now.Slice(row, Width * 4).SequenceEqual(then.Slice(row, Width * 4))) continue;

            top = Math.Min(top, y);
            bottom = y;
            for (var x = 0; x < Width; x++)
            {
                if (now.Slice(row + x * 4, 4).SequenceEqual(then.Slice(row + x * 4, 4))) continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
            }
        }

        return bottom < 0 ? (0, 0, 0, 0) : (left, top, right - left + 1, bottom - top + 1);
    }
}
