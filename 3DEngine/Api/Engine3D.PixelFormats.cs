namespace Engine;

public static partial class Engine3D
{
    // -- Pixel formats. An image holds four bytes a pixel, and a format is read into them, as the
    // GPU reads raylib's textures of the format: a channel of fewer bits spread across 0 to 255, a
    // format of one channel read as red, gray as all three, and one without alpha opaque.

    /// <summary>
    /// Reads an image of <paramref name="width"/> by <paramref name="height"/> pixels from a file of
    /// pixels alone, laid out as <paramref name="format"/> says after <paramref name="headerSize"/>
    /// bytes, as raylib's <c>LoadImageRaw</c> does.
    /// </summary>
    /// <returns>The image, or an invalid one when the file cannot be read, is too short or holds a compressed format, with the reason in the log.</returns>
    public static Image LoadImageRaw(string fileName, int width, int height, PixelFormat format, int headerSize)
    {
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadImageRaw: '{fileName}' was not found beside the program or in the working directory.");
            return default;
        }
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ApiLogger.Warn($"LoadImageRaw: '{fileName}' could not be read, {ex.Message}.");
            return default;
        }
        if (PixelSize(format) is not { } size)
        {
            ApiLogger.Warn($"LoadImageRaw: {format} is compressed, which is not read.");
            return default;
        }
        if (width <= 0 || height <= 0 || headerSize < 0 || bytes.Length < headerSize + (long)width * height * size)
        {
            ApiLogger.Warn($"LoadImageRaw: '{fileName}' holds {bytes.Length} bytes, short of a header of {headerSize} and {width} by {height} pixels of {format}.");
            return default;
        }
        var data = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++)
            WritePixel(data, i, ReadPixel(bytes.AsSpan(headerSize + i * size, size), format));
        return new Image(data, width, height);
    }

    /// <summary>
    /// Keeps of each pixel what <paramref name="newFormat"/> keeps, as raylib's <c>ImageFormat</c>
    /// converts an image to it: gray for the gray formats, fewer levels of each channel for the
    /// formats of fewer bits, no alpha for those without it, and red alone for those of one channel.
    /// </summary>
    /// <remarks>
    /// The image still holds four bytes a pixel, so <see cref="PixelFormat.UncompressedR8G8B8A8"/>
    /// leaves it as it is. A compressed format leaves it as it is too, with a warning, as raylib's does.
    /// </remarks>
    public static void ImageFormat(ref Image image, PixelFormat newFormat)
    {
        if (!image.IsValid) return;
        if (PixelSize(newFormat) is not { } size)
        {
            ApiLogger.Warn($"ImageFormat: {newFormat} is compressed, which an image here is not converted to.");
            return;
        }
        Span<byte> pixel = stackalloc byte[size];
        for (int i = 0; i < image.Width * image.Height; i++)
        {
            var c = ReadPixel(image.Data.AsSpan(i * 4, 4), PixelFormat.UncompressedR8G8B8A8);
            WriteFormatted(pixel, c, newFormat);
            WritePixel(image.Data, i, ReadPixel(pixel, newFormat));
        }
    }

    // The bytes a pixel of an uncompressed format takes, or null for a compressed one.
    private static int? PixelSize(PixelFormat format) => format switch
    {
        PixelFormat.UncompressedGrayscale => 1,
        PixelFormat.UncompressedGrayAlpha or PixelFormat.UncompressedR5G6B5 or PixelFormat.UncompressedR5G5B5A1
            or PixelFormat.UncompressedR4G4B4A4 or PixelFormat.UncompressedR16 => 2,
        PixelFormat.UncompressedR8G8B8 => 3,
        PixelFormat.UncompressedR8G8B8A8 or PixelFormat.UncompressedR32 => 4,
        PixelFormat.UncompressedR16G16B16 => 6,
        PixelFormat.UncompressedR16G16B16A16 => 8,
        PixelFormat.UncompressedR32G32B32 => 12,
        PixelFormat.UncompressedR32G32B32A32 => 16,
        _ => null,
    };

    private static void WritePixel(byte[] data, int index, Color c) =>
        (data[index * 4], data[index * 4 + 1], data[index * 4 + 2], data[index * 4 + 3]) = (c.R, c.G, c.B, c.A);

    // A pixel of the format as the GPU reads it, each channel from 0 to 1 as a byte.
    private static Color ReadPixel(ReadOnlySpan<byte> p, PixelFormat format)
    {
        static byte Level(int value, int max) => (byte)((value * 255 + max / 2) / max);
        static byte Unit(float value) => (byte)Math.Clamp((int)(value * 255.0f), 0, 255);
        static float Float(ReadOnlySpan<byte> p, int at) => BitConverter.ToSingle(p.Slice(at * 4, 4));
        static float Half(ReadOnlySpan<byte> p, int at) => (float)BitConverter.ToHalf(p.Slice(at * 2, 2));
        int u16 = p.Length >= 2 ? p[0] | (p[1] << 8) : 0;
        return format switch
        {
            PixelFormat.UncompressedGrayscale => new Color(p[0], p[0], p[0], 255),
            PixelFormat.UncompressedGrayAlpha => new Color(p[0], p[0], p[0], p[1]),
            PixelFormat.UncompressedR5G6B5 => new Color(Level(u16 >> 11, 31), Level((u16 >> 5) & 63, 63), Level(u16 & 31, 31), 255),
            PixelFormat.UncompressedR5G5B5A1 => new Color(Level(u16 >> 11, 31), Level((u16 >> 6) & 31, 31), Level((u16 >> 1) & 31, 31), (byte)((u16 & 1) * 255)),
            PixelFormat.UncompressedR4G4B4A4 => new Color(Level(u16 >> 12, 15), Level((u16 >> 8) & 15, 15), Level((u16 >> 4) & 15, 15), Level(u16 & 15, 15)),
            PixelFormat.UncompressedR8G8B8 => new Color(p[0], p[1], p[2], 255),
            PixelFormat.UncompressedR32 => new Color(Unit(Float(p, 0)), 0, 0, 255),
            PixelFormat.UncompressedR32G32B32 => new Color(Unit(Float(p, 0)), Unit(Float(p, 1)), Unit(Float(p, 2)), 255),
            PixelFormat.UncompressedR32G32B32A32 => new Color(Unit(Float(p, 0)), Unit(Float(p, 1)), Unit(Float(p, 2)), Unit(Float(p, 3))),
            PixelFormat.UncompressedR16 => new Color(Unit(Half(p, 0)), 0, 0, 255),
            PixelFormat.UncompressedR16G16B16 => new Color(Unit(Half(p, 0)), Unit(Half(p, 1)), Unit(Half(p, 2)), 255),
            PixelFormat.UncompressedR16G16B16A16 => new Color(Unit(Half(p, 0)), Unit(Half(p, 1)), Unit(Half(p, 2)), Unit(Half(p, 3))),
            _ => new Color(p[0], p[1], p[2], p[3]),
        };
    }

    // A pixel written as the format holds it, by raylib's ImageFormat's arithmetic.
    private static void WriteFormatted(Span<byte> p, Color c, PixelFormat format)
    {
        var (r, g, b, a) = (c.R / 255.0f, c.G / 255.0f, c.B / 255.0f, c.A / 255.0f);
        var gray = r * 0.299f + g * 0.587f + b * 0.114f;
        static int Round(float value, int max) => (int)MathF.Round(value * max);
        static void U16(Span<byte> p, int value) => (p[0], p[1]) = ((byte)value, (byte)(value >> 8));
        static void Floats(Span<byte> p, params ReadOnlySpan<float> values)
        {
            for (int i = 0; i < values.Length; i++) BitConverter.TryWriteBytes(p.Slice(i * 4, 4), values[i]);
        }
        static void Halves(Span<byte> p, params ReadOnlySpan<float> values)
        {
            for (int i = 0; i < values.Length; i++) BitConverter.TryWriteBytes(p.Slice(i * 2, 2), (Half)values[i]);
        }
        switch (format)
        {
            case PixelFormat.UncompressedGrayscale: p[0] = (byte)(gray * 255.0f); break;
            case PixelFormat.UncompressedGrayAlpha: (p[0], p[1]) = ((byte)(gray * 255.0f), (byte)(a * 255.0f)); break;
            case PixelFormat.UncompressedR5G6B5: U16(p, Round(r, 31) << 11 | Round(g, 63) << 5 | Round(b, 31)); break;
            // raylib keeps the alpha where it is past 50 of 255
            case PixelFormat.UncompressedR5G5B5A1: U16(p, Round(r, 31) << 11 | Round(g, 31) << 6 | Round(b, 31) << 1 | (a > 50 / 255.0f ? 1 : 0)); break;
            case PixelFormat.UncompressedR4G4B4A4: U16(p, Round(r, 15) << 12 | Round(g, 15) << 8 | Round(b, 15) << 4 | Round(a, 15)); break;
            case PixelFormat.UncompressedR8G8B8: (p[0], p[1], p[2]) = (c.R, c.G, c.B); break;
            case PixelFormat.UncompressedR32: Floats(p, gray); break;
            case PixelFormat.UncompressedR32G32B32: Floats(p, r, g, b); break;
            case PixelFormat.UncompressedR32G32B32A32: Floats(p, r, g, b, a); break;
            case PixelFormat.UncompressedR16: Halves(p, gray); break;
            case PixelFormat.UncompressedR16G16B16: Halves(p, r, g, b); break;
            case PixelFormat.UncompressedR16G16B16A16: Halves(p, r, g, b, a); break;
            default: (p[0], p[1], p[2], p[3]) = (c.R, c.G, c.B, c.A); break;
        }
    }
}
