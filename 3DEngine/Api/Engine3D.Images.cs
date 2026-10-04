using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    // -- Generating images

    /// <summary>
    /// Makes an image that blends from <paramref name="start"/> to <paramref name="end"/> along a
    /// direction in degrees, where 0 runs top to bottom and 90 left to right, as raylib's does.
    /// </summary>
    public static Image GenImageGradientLinear(int width, int height, int direction, Color start, Color end)
    {
        var (sin, cos) = MathF.SinCos(float.DegreesToRadians(90 - direction));
        // Each pixel's position along the direction, scaled so the image's extreme corners reach 0 and 1.
        var reach = MathF.Abs(cos) * (width - 1) + MathF.Abs(sin) * (height - 1);
        var offset = MathF.Min(0, cos * (width - 1)) + MathF.Min(0, sin * (height - 1));
        return Generate(width, height, (x, y) =>
            Lerp(start, end, reach > 0 ? (x * cos + y * sin - offset) / reach : 0));
    }

    /// <summary>
    /// Makes an image that blends from <paramref name="inner"/> at its center to
    /// <paramref name="outer"/> at its edge, holding the inner color for the first
    /// <paramref name="density"/> part of the radius.
    /// </summary>
    public static Image GenImageGradientRadial(int width, int height, float density, Color inner, Color outer)
    {
        var radius = MathF.Min(width, height) / 2f;
        var center = new Vector2(width, height) / 2f;
        return Generate(width, height, (x, y) =>
        {
            var t = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center) / radius;
            return Lerp(inner, outer, Math.Clamp((t - density) / Math.Max(1e-6f, 1 - density), 0, 1));
        });
    }

    /// <summary>Makes an image of white and black pixels, white with the chance <paramref name="factor"/>.</summary>
    public static Image GenImageWhiteNoise(int width, int height, float factor)
    {
        return Generate(width, height, (_, _) => RandomSingle() < factor ? Color.White : Color.Black);
    }

    /// <summary>
    /// Makes an image of Perlin noise in grays, six octaves of it, at <paramref name="scale"/> over
    /// the image's width and height and moved by the offsets, as raylib's is.
    /// </summary>
    public static Image GenImagePerlinNoise(int width, int height, int offsetX, int offsetY, float scale)
    {
        var w = Math.Max(1, width);
        var h = Math.Max(1, height);
        return Generate(width, height, (x, y) =>
        {
            var value = PerlinFbm((x + offsetX) * (scale / w), (y + offsetY) * (scale / h), 1f, octaves: 6);
            var gray = (byte)(Math.Clamp((value + 1) / 2, 0, 1) * 255);
            return new Color(gray, gray, gray);
        });
    }

    /// <summary>
    /// Makes an image of cells in grays, one point in each square of <paramref name="tileSize"/>
    /// pixels, each pixel darker the nearer it is to a point, as raylib's is.
    /// </summary>
    public static Image GenImageCellular(int width, int height, int tileSize)
    {
        tileSize = Math.Max(1, tileSize);
        int across = Math.Max(1, width / tileSize), down = Math.Max(1, height / tileSize);
        var seeds = new Vector2[across * down];
        for (int i = 0; i < seeds.Length; i++)
            seeds[i] = new Vector2(i % across * tileSize + RandomBelow(tileSize), i / across * tileSize + RandomBelow(tileSize));

        return Generate(width, height, (x, y) =>
        {
            int tileX = Math.Min(x / tileSize, across - 1), tileY = Math.Min(y / tileSize, down - 1);
            var nearest = float.MaxValue;
            // A point in a neighbouring square can be nearer than the one in this one.
            for (int j = Math.Max(0, tileY - 1); j <= Math.Min(down - 1, tileY + 1); j++)
            for (int i = Math.Max(0, tileX - 1); i <= Math.Min(across - 1, tileX + 1); i++)
                nearest = MathF.Min(nearest, Vector2.Distance(new Vector2(x, y), seeds[j * across + i]));
            var gray = (byte)Math.Min(255, nearest * 256 / tileSize);
            return new Color(gray, gray, gray);
        });
    }

    // Ken Perlin's improved noise, summed over octaves each twice the frequency and half the
    // weight of the one before, as stb_perlin's fbm is, from roughly -1 to 1.
    private static float PerlinFbm(float x, float y, float z, int octaves)
    {
        float sum = 0, frequency = 1, amplitude = 1;
        for (int i = 0; i < octaves; i++)
        {
            sum += Perlin(x * frequency, y * frequency, z * frequency) * amplitude;
            frequency *= 2;
            amplitude *= 0.5f;
        }
        return sum;
    }

    private static float Perlin(float x, float y, float z)
    {
        int xi = (int)MathF.Floor(x) & 255, yi = (int)MathF.Floor(y) & 255, zi = (int)MathF.Floor(z) & 255;
        x -= MathF.Floor(x);
        y -= MathF.Floor(y);
        z -= MathF.Floor(z);
        float u = Fade(x), v = Fade(y), w = Fade(z);
        int a = PerlinTable[xi] + yi, aa = PerlinTable[a] + zi, ab = PerlinTable[a + 1] + zi;
        int b = PerlinTable[xi + 1] + yi, ba = PerlinTable[b] + zi, bb = PerlinTable[b + 1] + zi;
        return Lerp(w,
            Lerp(v, Lerp(u, Grad(PerlinTable[aa], x, y, z), Grad(PerlinTable[ba], x - 1, y, z)),
                    Lerp(u, Grad(PerlinTable[ab], x, y - 1, z), Grad(PerlinTable[bb], x - 1, y - 1, z))),
            Lerp(v, Lerp(u, Grad(PerlinTable[aa + 1], x, y, z - 1), Grad(PerlinTable[ba + 1], x - 1, y, z - 1)),
                    Lerp(u, Grad(PerlinTable[ab + 1], x, y - 1, z - 1), Grad(PerlinTable[bb + 1], x - 1, y - 1, z - 1))));

        static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        static float Lerp(float t, float a, float b) => a + t * (b - a);
        static float Grad(int hash, float x, float y, float z)
        {
            var h = hash & 15;
            float u = h < 8 ? x : y, v = h < 4 ? y : h is 12 or 14 ? x : z;
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        }
    }

    // Perlin's permutation of 0 to 255, twice over, so an index past 255 needs no wrap.
    private static readonly int[] PerlinTable = [.. PerlinPermutation, .. PerlinPermutation];

    private static ReadOnlySpan<int> PerlinPermutation =>
    [
        151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225, 140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23,
        190, 6, 148, 247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32, 57, 177, 33, 88, 237, 149, 56, 87, 174,
        20, 125, 136, 171, 168, 68, 175, 74, 165, 71, 134, 139, 48, 27, 166, 77, 146, 158, 231, 83, 111, 229, 122, 60, 211, 133,
        230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54, 65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89,
        18, 169, 200, 196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64, 52, 217, 226, 250, 124, 123, 5,
        202, 38, 147, 118, 126, 255, 82, 85, 212, 207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42, 223, 183, 170, 213, 119,
        248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9, 129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224,
        232, 178, 185, 112, 104, 218, 246, 97, 228, 251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241, 81, 51, 145, 235,
        249, 14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157, 184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138,
        236, 205, 93, 222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180,
    ];

    /// <summary>A copy of an image, with pixels of its own.</summary>
    public static Image ImageCopy(Image image) => image with { Data = (byte[])image.Data.Clone() };

    /// <summary>A new image of the part of <paramref name="image"/> that <paramref name="rec"/> covers, clipped to the image.</summary>
    public static Image ImageFromImage(Image image, Rectangle rec)
    {
        var (x, y, w, h) = Clip(image, rec);
        var data = new byte[w * h * 4];
        for (int row = 0; row < h; row++)
            Array.Copy(image.Data, ((y + row) * image.Width + x) * 4, data, row * w * 4, w * 4);
        return new Image(data, w, h);
    }

    /// <summary>An image encoded as a file's bytes, of <paramref name="fileType"/>, which is <c>".png"</c>.</summary>
    /// <returns>The bytes, or none for an empty image or another type, with the reason in the log.</returns>
    public static byte[] ExportImageToMemory(Image image, string fileType)
    {
        if (!image.IsValid) return [];
        if (!fileType.TrimStart('.').Equals("png", StringComparison.OrdinalIgnoreCase))
        {
            ApiLogger.Warn($"ExportImageToMemory: '{fileType}' is not a type the engine writes, which is PNG.");
            return [];
        }
        using var memory = new MemoryStream();
        PngWriter.Write(memory, image.Data, image.Width, image.Height);
        return memory.ToArray();
    }

    /// <summary>
    /// Reads every frame of an animated GIF into one image, the frames stacked from the top, each
    /// as tall as the GIF, and says how many there are, as raylib's does.
    /// </summary>
    /// <returns>The frames, or an empty image when the file cannot be read, with the reason in the log.</returns>
    public static Image LoadImageAnim(string fileName, out int frames)
    {
        frames = 0;
        var path = ResolveFile(fileName);
        if (path is null)
        {
            ApiLogger.Warn($"LoadImageAnim: '{fileName}' was not found beside the program or in the working directory.");
            return default;
        }
        try
        {
            using var stream = File.OpenRead(path);
            return GifFrames(stream, out frames);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
        {
            ApiLogger.Warn($"LoadImageAnim: '{fileName}' could not be decoded: {ex.Message}");
            return default;
        }
    }

    /// <summary>Reads every frame of an animated GIF held in memory, as <see cref="LoadImageAnim"/> reads a file.</summary>
    public static Image LoadImageAnimFromMemory(string fileType, byte[] fileData, out int frames)
    {
        frames = 0;
        try
        {
            return GifFrames(new MemoryStream(fileData), out frames);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
        {
            ApiLogger.Warn($"LoadImageAnimFromMemory: the {fileType} data could not be decoded: {ex.Message}");
            return default;
        }
    }

    /// <summary>Decodes an image held in memory, as a file's bytes, the type named by its extension as ".png".</summary>
    /// <returns>The image, or an invalid one when the bytes cannot be decoded, with the reason in the log.</returns>
    public static Image LoadImageFromMemory(string fileType, byte[] fileData)
    {
        try
        {
            var result = StbImageSharp.ImageResult.FromMemory(fileData, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
            return new Image(result.Data, result.Width, result.Height);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ArgumentException)
        {
            ApiLogger.Warn($"LoadImageFromMemory: the {fileType} data could not be decoded: {ex.Message}");
            return default;
        }
    }

    /// <summary>Whether an image holds pixels, as one loaded or made does.</summary>
    public static bool IsImageValid(Image image) => image.IsValid;

    /// <summary>Crops an image to the smallest rectangle holding every pixel with alpha above <paramref name="threshold"/>, from 0 to 1.</summary>
    public static void ImageAlphaCrop(ref Image image, float threshold)
    {
        var border = GetImageAlphaBorder(image, threshold);
        if (border.Width > 0 && border.Height > 0) ImageCrop(ref image, border);
    }

    /// <summary>Grows an image's canvas to the next power of two each way, the new pixels <paramref name="fill"/>, as an old GPU's textures had to be.</summary>
    public static void ImageToPOT(ref Image image, Color fill)
    {
        if (!image.IsValid) return;
        var width = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)image.Width);
        var height = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)image.Height);
        if (width != image.Width || height != image.Height) ImageResizeCanvas(ref image, width, height, 0, 0, fill);
    }

    /// <summary>The different colors of an image, in the order they are first met row by row, up to <paramref name="maxPaletteSize"/> of them.</summary>
    public static Color[] LoadImagePalette(Image image, int maxPaletteSize)
    {
        var palette = new List<Color>();
        var seen = new HashSet<Color>();
        for (int y = 0; y < image.Height && palette.Count < maxPaletteSize; y++)
            for (int x = 0; x < image.Width && palette.Count < maxPaletteSize; x++)
            {
                var color = GetImageColor(image, x, y);
                if (seen.Add(color)) palette.Add(color);
            }
        return [.. palette];
    }

    // Every frame of an animated GIF stacked from the top into one image, each as tall as the GIF.
    private static Image GifFrames(Stream stream, out int frames)
    {
        frames = 0;
        var all = new List<byte[]>();
        int width = 0, height = 0;
        foreach (var frame in StbImageSharp.ImageResult.AnimatedGifFramesFromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha))
        {
            (width, height) = (frame.Width, frame.Height);
            all.Add([.. frame.Data]);
        }
        if (all.Count == 0) return default;
        frames = all.Count;
        var data = new byte[width * height * 4 * all.Count];
        for (int f = 0; f < all.Count; f++) all[f].AsSpan(0, width * height * 4).CopyTo(data.AsSpan(f * width * height * 4));
        return new Image(data, width, height * all.Count);
    }

    /// <summary>Writes an image to a PNG file.</summary>
    /// <returns>Whether it was written. The reason it was not is in the log.</returns>
    public static bool ExportImage(Image image, string fileName)
    {
        if (!image.IsValid) return false;
        try
        {
            PngWriter.Write(fileName, image.Data, image.Width, image.Height);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ApiLogger.Warn($"ExportImage: '{fileName}' could not be written: {ex.Message}");
            return false;
        }
    }

    // -- Changing an image's size and orientation

    /// <summary>Cuts an image down to the part <paramref name="rec"/> covers.</summary>
    public static void ImageCrop(ref Image image, Rectangle rec) => image = ImageFromImage(image, rec);

    /// <summary>Scales an image to a new size, blending neighboring pixels.</summary>
    public static void ImageResize(ref Image image, int newWidth, int newHeight)
    {
        var source = image;
        var (sx, sy) = ((float)source.Width / newWidth, (float)source.Height / newHeight);
        image = Generate(newWidth, newHeight, (x, y) =>
        {
            // The source position under this pixel's center, between four source pixels.
            var fx = Math.Clamp((x + 0.5f) * sx - 0.5f, 0, source.Width - 1);
            var fy = Math.Clamp((y + 0.5f) * sy - 0.5f, 0, source.Height - 1);
            int x0 = (int)fx, y0 = (int)fy;
            int x1 = Math.Min(x0 + 1, source.Width - 1), y1 = Math.Min(y0 + 1, source.Height - 1);
            var top = Lerp(GetImageColor(source, x0, y0), GetImageColor(source, x1, y0), fx - x0);
            var bottom = Lerp(GetImageColor(source, x0, y1), GetImageColor(source, x1, y1), fx - x0);
            return Lerp(top, bottom, fy - y0);
        });
    }

    /// <summary>Scales an image to a new size, taking the nearest pixel, which keeps pixel art sharp.</summary>
    public static void ImageResizeNN(ref Image image, int newWidth, int newHeight)
    {
        var source = image;
        image = Generate(newWidth, newHeight, (x, y) =>
            GetImageColor(source, x * source.Width / newWidth, y * source.Height / newHeight));
    }

    /// <summary>
    /// Changes an image's size without scaling it, placing it at an offset in the new size and
    /// filling the rest with <paramref name="fill"/>.
    /// </summary>
    public static void ImageResizeCanvas(ref Image image, int newWidth, int newHeight, int offsetX, int offsetY, Color fill)
    {
        var canvas = GenImageColor(newWidth, newHeight, fill);
        CopyPixels(image, new Rectangle(0, 0, image.Width, image.Height), ref canvas, offsetX, offsetY);
        image = canvas;
    }

    /// <summary>Turns an image upside down.</summary>
    public static void ImageFlipVertical(ref Image image)
    {
        var source = image;
        image = Generate(source.Width, source.Height, (x, y) => GetImageColor(source, x, source.Height - 1 - y));
    }

    /// <summary>Mirrors an image left to right.</summary>
    public static void ImageFlipHorizontal(ref Image image)
    {
        var source = image;
        image = Generate(source.Width, source.Height, (x, y) => GetImageColor(source, source.Width - 1 - x, y));
    }

    /// <summary>Turns an image a quarter turn clockwise.</summary>
    public static void ImageRotateCW(ref Image image)
    {
        var source = image;
        image = Generate(source.Height, source.Width, (x, y) => GetImageColor(source, y, source.Height - 1 - x));
    }

    /// <summary>Turns an image a quarter turn counterclockwise.</summary>
    public static void ImageRotateCCW(ref Image image)
    {
        var source = image;
        image = Generate(source.Height, source.Width, (x, y) => GetImageColor(source, source.Width - 1 - y, x));
    }

    // -- Changing an image's colors, in place

    /// <summary>Multiplies every pixel by a color.</summary>
    public static void ImageColorTint(ref Image image, Color color) => EachPixel(image, c => Multiply(c, color));

    /// <summary>Inverts every pixel's red, green and blue.</summary>
    public static void ImageColorInvert(ref Image image) =>
        EachPixel(image, c => new Color((byte)(255 - c.R), (byte)(255 - c.G), (byte)(255 - c.B), c.A));

    /// <summary>Turns every pixel gray, by its perceived brightness.</summary>
    public static void ImageColorGrayscale(ref Image image) => EachPixel(image, c =>
    {
        var gray = (byte)Math.Round(0.299f * c.R + 0.587f * c.G + 0.114f * c.B);
        return new Color(gray, gray, gray, c.A);
    });

    /// <summary>Changes every pixel's contrast, from -100 (flat gray) to 100.</summary>
    public static void ImageColorContrast(ref Image image, float contrast)
    {
        var scale = MathF.Pow((100 + Math.Clamp(contrast, -100, 100)) / 100, 2);
        byte Adjust(byte v) => (byte)Math.Clamp(((v / 255f - 0.5f) * scale + 0.5f) * 255, 0, 255);
        EachPixel(image, c => new Color(Adjust(c.R), Adjust(c.G), Adjust(c.B), c.A));
    }

    /// <summary>Adds to every pixel's red, green and blue, from -255 to 255.</summary>
    public static void ImageColorBrightness(ref Image image, int brightness)
    {
        brightness = Math.Clamp(brightness, -255, 255);
        byte Adjust(byte v) => (byte)Math.Clamp(v + brightness, 0, 255);
        EachPixel(image, c => new Color(Adjust(c.R), Adjust(c.G), Adjust(c.B), c.A));
    }

    /// <summary>Replaces every pixel of exactly one color with another.</summary>
    public static void ImageColorReplace(ref Image image, Color color, Color replace) =>
        EachPixel(image, c => c == color ? replace : c);

    // -- Drawing into an image, in place. Shapes replace the pixels they cover, and ImageDraw blends.

    /// <summary>Fills a whole image with one color.</summary>
    public static void ImageClearBackground(ref Image image, Color color) => EachPixel(image, _ => color);

    /// <summary>Sets one pixel, when it is inside the image.</summary>
    public static void ImageDrawPixel(ref Image image, int x, int y, Color color) => SetPixel(image, x, y, color);

    /// <summary>Sets one pixel, when it is inside the image.</summary>
    public static void ImageDrawPixelV(ref Image image, Vector2 position, Color color) =>
        SetPixel(image, (int)position.X, (int)position.Y, color);

    /// <summary>Draws a line one pixel wide between two points.</summary>
    public static void ImageDrawLine(ref Image image, int startX, int startY, int endX, int endY, Color color)
    {
        // Bresenham's line, which steps one pixel at a time along the longer axis.
        int dx = Math.Abs(endX - startX), dy = -Math.Abs(endY - startY);
        int sx = startX < endX ? 1 : -1, sy = startY < endY ? 1 : -1;
        int error = dx + dy, x = startX, y = startY;
        while (true)
        {
            SetPixel(image, x, y, color);
            if (x == endX && y == endY) break;
            var doubled = 2 * error;
            if (doubled >= dy) { error += dy; x += sx; }
            if (doubled <= dx) { error += dx; y += sy; }
        }
    }

    /// <summary>Draws a line one pixel wide between two points.</summary>
    public static void ImageDrawLineV(ref Image image, Vector2 start, Vector2 end, Color color) =>
        ImageDrawLine(ref image, (int)start.X, (int)start.Y, (int)end.X, (int)end.Y, color);

    /// <summary>Fills a circle.</summary>
    public static void ImageDrawCircle(ref Image image, int centerX, int centerY, int radius, Color color)
    {
        for (int y = -radius; y <= radius; y++)
        {
            var half = (int)MathF.Sqrt(radius * radius - y * y);
            for (int x = -half; x <= half; x++)
                SetPixel(image, centerX + x, centerY + y, color);
        }
    }

    /// <summary>Draws a circle's outline one pixel wide.</summary>
    public static void ImageDrawCircleLines(ref Image image, int centerX, int centerY, int radius, Color color)
    {
        // The midpoint circle, one octant computed and mirrored into the other seven.
        int x = radius, y = 0, error = 1 - radius;
        while (x >= y)
        {
            foreach (var (px, py) in new[] { (x, y), (y, x), (-y, x), (-x, y), (-x, -y), (-y, -x), (y, -x), (x, -y) })
                SetPixel(image, centerX + px, centerY + py, color);
            y++;
            if (error < 0) error += 2 * y + 1;
            else
            {
                x--;
                error += 2 * (y - x) + 1;
            }
        }
    }

    /// <summary>Fills a rectangle.</summary>
    public static void ImageDrawRectangle(ref Image image, int x, int y, int width, int height, Color color) =>
        ImageDrawRectangleRec(ref image, new Rectangle(x, y, width, height), color);

    /// <summary>Fills a rectangle.</summary>
    public static void ImageDrawRectangleRec(ref Image image, Rectangle rec, Color color)
    {
        var (x, y, w, h) = Clip(image, rec);
        for (int row = y; row < y + h; row++)
        for (int column = x; column < x + w; column++)
            SetPixel(image, column, row, color);
    }

    /// <summary>Draws a rectangle's outline, <paramref name="thick"/> pixels wide, inside its edge.</summary>
    public static void ImageDrawRectangleLines(ref Image image, Rectangle rec, int thick, Color color)
    {
        thick = Math.Max(1, thick);
        ImageDrawRectangleRec(ref image, rec with { Height = thick }, color);
        ImageDrawRectangleRec(ref image, rec with { Y = rec.Y + rec.Height - thick, Height = thick }, color);
        ImageDrawRectangleRec(ref image, rec with { Width = thick }, color);
        ImageDrawRectangleRec(ref image, rec with { X = rec.X + rec.Width - thick, Width = thick }, color);
    }

    /// <summary>
    /// Draws part of one image into a rectangle of another, scaled to fit by the nearest pixel,
    /// multiplied by <paramref name="tint"/> and blended over what is there by its alpha.
    /// </summary>
    public static void ImageDraw(ref Image destination, Image source, Rectangle sourceRec, Rectangle destinationRec, Color tint)
    {
        if (!source.IsValid || sourceRec.Width <= 0 || sourceRec.Height <= 0) return;
        var (x, y, w, h) = Clip(destination, destinationRec);
        for (int row = y; row < y + h; row++)
        for (int column = x; column < x + w; column++)
        {
            var u = (int)(sourceRec.X + (column - destinationRec.X + 0.5f) * sourceRec.Width / destinationRec.Width);
            var v = (int)(sourceRec.Y + (row - destinationRec.Y + 0.5f) * sourceRec.Height / destinationRec.Height);
            if (u < 0 || v < 0 || u >= source.Width || v >= source.Height) continue;
            var over = Multiply(GetImageColor(source, u, v), tint);
            SetPixel(destination, column, row, Blend(GetImageColor(destination, column, row), over));
        }
    }

    // -- Helpers

    private static Image Generate(int width, int height, Func<int, int, Color> pixel)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var image = new Image(new byte[width * height * 4], width, height);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            SetPixel(image, x, y, pixel(x, y));
        return image;
    }

    private static void EachPixel(Image image, Func<Color, Color> change)
    {
        for (int y = 0; y < image.Height; y++)
        for (int x = 0; x < image.Width; x++)
            SetPixel(image, x, y, change(GetImageColor(image, x, y)));
    }

    private static void SetPixel(Image image, int x, int y, Color color)
    {
        if ((uint)x >= (uint)image.Width || (uint)y >= (uint)image.Height) return;
        var i = (y * image.Width + x) * 4;
        (image.Data[i], image.Data[i + 1], image.Data[i + 2], image.Data[i + 3]) = (color.R, color.G, color.B, color.A);
    }

    // The whole pixels of a rectangle that lie inside an image.
    private static (int X, int Y, int Width, int Height) Clip(Image image, Rectangle rec)
    {
        var x0 = Math.Clamp((int)MathF.Floor(rec.X), 0, image.Width);
        var y0 = Math.Clamp((int)MathF.Floor(rec.Y), 0, image.Height);
        var x1 = Math.Clamp((int)MathF.Floor(rec.X + rec.Width), 0, image.Width);
        var y1 = Math.Clamp((int)MathF.Floor(rec.Y + rec.Height), 0, image.Height);
        return (x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
    }

    private static void CopyPixels(Image source, Rectangle sourceRec, ref Image destination, int x, int y)
    {
        var (sx, sy, w, h) = Clip(source, sourceRec);
        for (int row = 0; row < h; row++)
        for (int column = 0; column < w; column++)
            SetPixel(destination, x + column, y + row, GetImageColor(source, sx + column, sy + row));
    }

    private static Color Lerp(Color a, Color b, float t)
    {
        byte Mix(byte from, byte to) => (byte)Math.Round(from + (to - from) * Math.Clamp(t, 0, 1));
        return new Color(Mix(a.R, b.R), Mix(a.G, b.G), Mix(a.B, b.B), Mix(a.A, b.A));
    }

    // Porter and Duff's "over": the source's color weighted by its alpha, over what shows through.
    private static Color Blend(Color under, Color over)
    {
        if (over.A == 255) return over;
        if (over.A == 0) return under;
        float a = over.A / 255f, b = under.A / 255f * (1 - a), alpha = a + b;
        byte Mix(byte top, byte bottom) => (byte)Math.Round((top * a + bottom * b) / alpha);
        return new Color(Mix(over.R, under.R), Mix(over.G, under.G), Mix(over.B, under.B), (byte)Math.Round(alpha * 255));
    }
}
