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
        var aspectRatio = (float)w / h;
        return Generate(width, height, (x, y) =>
        {
            float nx = (x + offsetX) * (scale / w), ny = (y + offsetY) * (scale / h);
            // The wider side spans more of the noise, so its features stay round.
            if (w > h) nx *= aspectRatio;
            else ny /= aspectRatio;
            var value = PerlinFbm(nx, ny, 1f, octaves: 6);
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
            // A point in a neighboring square can be nearer than the one in this one.
            for (int j = Math.Max(0, tileY - 1); j <= Math.Min(down - 1, tileY + 1); j++)
            for (int i = Math.Max(0, tileX - 1); i <= Math.Min(across - 1, tileX + 1); i++)
                nearest = MathF.Min(nearest, Vector2.Distance(new Vector2(x, y), seeds[j * across + i]));
            var gray = (byte)Math.Min(255, nearest * 256 / tileSize);
            return new Color(gray, gray, gray);
        });
    }

    // stb_perlin's noise (Sean Barrett, public domain), which raylib's GenImagePerlinNoise calls,
    // summed over octaves each twice the frequency and half the weight of the one before, each
    // octave with a seed of its own, from roughly -1 to 1.
    private static float PerlinFbm(float x, float y, float z, int octaves)
    {
        float sum = 0, frequency = 1, amplitude = 1;
        for (int i = 0; i < octaves; i++)
        {
            sum += Perlin(x * frequency, y * frequency, z * frequency, (byte)i) * amplitude;
            frequency *= 2;
            amplitude *= 0.5f;
        }
        return sum;
    }

    // Ken Perlin's improved noise of 2002, with stb_perlin's table and its twelve gradients.
    private static float Perlin(float x, float y, float z, byte seed)
    {
        int px = (int)MathF.Floor(x), py = (int)MathF.Floor(y), pz = (int)MathF.Floor(z);
        int x0 = px & 255, x1 = (px + 1) & 255;
        int y0 = py & 255, y1 = (py + 1) & 255;
        int z0 = pz & 255, z1 = (pz + 1) & 255;

        x -= px;
        y -= py;
        z -= pz;
        float u = Ease(x), v = Ease(y), w = Ease(z);

        int r0 = PerlinTable[x0 + seed], r1 = PerlinTable[x1 + seed];
        int r00 = PerlinTable[r0 + y0], r01 = PerlinTable[r0 + y1];
        int r10 = PerlinTable[r1 + y0], r11 = PerlinTable[r1 + y1];

        float n000 = Grad(PerlinGradients[r00 + z0], x, y, z);
        float n001 = Grad(PerlinGradients[r00 + z1], x, y, z - 1);
        float n010 = Grad(PerlinGradients[r01 + z0], x, y - 1, z);
        float n011 = Grad(PerlinGradients[r01 + z1], x, y - 1, z - 1);
        float n100 = Grad(PerlinGradients[r10 + z0], x - 1, y, z);
        float n101 = Grad(PerlinGradients[r10 + z1], x - 1, y, z - 1);
        float n110 = Grad(PerlinGradients[r11 + z0], x - 1, y - 1, z);
        float n111 = Grad(PerlinGradients[r11 + z1], x - 1, y - 1, z - 1);

        float n0 = Lerp(Lerp(n000, n001, w), Lerp(n010, n011, w), v);
        float n1 = Lerp(Lerp(n100, n101, w), Lerp(n110, n111, w), v);
        return Lerp(n0, n1, u);

        static float Ease(float t) => ((t * 6 - 15) * t + 10) * t * t * t;
        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Grad(int index, float x, float y, float z) => index switch
        {
            0 => x + y, 1 => -x + y, 2 => x - y, 3 => -x - y,
            4 => x + z, 5 => -x + z, 6 => x - z, 7 => -x - z,
            8 => y + z, 9 => -y + z, 10 => y - z, _ => -y - z,
        };
    }

    // stb_perlin's permutation of 0 to 255, and the gradient each entry picks, twice over, so an
    // index past 255 needs no wrap.
    private static readonly int[] PerlinTable = [.. PerlinPermutation, .. PerlinPermutation];
    private static readonly int[] PerlinGradients = [.. PerlinGradientIndices, .. PerlinGradientIndices];

    private static ReadOnlySpan<int> PerlinPermutation =>
    [
        23, 125, 161, 52, 103, 117, 70, 37, 247, 101, 203, 169, 124, 126, 44, 123,
        152, 238, 145, 45, 171, 114, 253, 10, 192, 136, 4, 157, 249, 30, 35, 72,
        175, 63, 77, 90, 181, 16, 96, 111, 133, 104, 75, 162, 93, 56, 66, 240,
        8, 50, 84, 229, 49, 210, 173, 239, 141, 1, 87, 18, 2, 198, 143, 57,
        225, 160, 58, 217, 168, 206, 245, 204, 199, 6, 73, 60, 20, 230, 211, 233,
        94, 200, 88, 9, 74, 155, 33, 15, 219, 130, 226, 202, 83, 236, 42, 172,
        165, 218, 55, 222, 46, 107, 98, 154, 109, 67, 196, 178, 127, 158, 13, 243,
        65, 79, 166, 248, 25, 224, 115, 80, 68, 51, 184, 128, 232, 208, 151, 122,
        26, 212, 105, 43, 179, 213, 235, 148, 146, 89, 14, 195, 28, 78, 112, 76,
        250, 47, 24, 251, 140, 108, 186, 190, 228, 170, 183, 139, 39, 188, 244, 246,
        132, 48, 119, 144, 180, 138, 134, 193, 82, 182, 120, 121, 86, 220, 209, 3,
        91, 241, 149, 85, 205, 150, 113, 216, 31, 100, 41, 164, 177, 214, 153, 231,
        38, 71, 185, 174, 97, 201, 29, 95, 7, 92, 54, 254, 191, 118, 34, 221,
        131, 11, 163, 99, 234, 81, 227, 147, 156, 176, 17, 142, 69, 12, 110, 62,
        27, 255, 0, 194, 59, 116, 242, 252, 19, 21, 187, 53, 207, 129, 64, 135,
        61, 40, 167, 237, 102, 223, 106, 159, 197, 189, 215, 137, 36, 32, 22, 5,
    ];

    private static ReadOnlySpan<int> PerlinGradientIndices =>
    [
        7, 9, 5, 0, 11, 1, 6, 9, 3, 9, 11, 1, 8, 10, 4, 7,
        8, 6, 1, 5, 3, 10, 9, 10, 0, 8, 4, 1, 5, 2, 7, 8,
        7, 11, 9, 10, 1, 0, 4, 7, 5, 0, 11, 6, 1, 4, 2, 8,
        8, 10, 4, 9, 9, 2, 5, 7, 9, 1, 7, 2, 2, 6, 11, 5,
        5, 4, 6, 9, 0, 1, 1, 0, 7, 6, 9, 8, 4, 10, 3, 1,
        2, 8, 8, 9, 10, 11, 5, 11, 11, 2, 6, 10, 3, 4, 2, 4,
        9, 10, 3, 2, 6, 3, 6, 10, 5, 3, 4, 10, 11, 2, 9, 11,
        1, 11, 10, 4, 9, 4, 11, 0, 4, 11, 4, 0, 0, 0, 7, 6,
        10, 4, 1, 3, 11, 5, 3, 4, 2, 9, 1, 3, 0, 1, 8, 0,
        6, 7, 8, 7, 0, 4, 6, 10, 8, 2, 3, 11, 11, 8, 0, 2,
        4, 8, 3, 0, 0, 10, 6, 1, 2, 2, 4, 5, 6, 0, 1, 3,
        11, 9, 5, 5, 9, 6, 9, 8, 3, 8, 1, 8, 9, 6, 9, 11,
        10, 7, 5, 6, 5, 9, 1, 3, 7, 0, 2, 10, 11, 2, 6, 1,
        3, 11, 7, 7, 2, 1, 7, 3, 0, 8, 1, 1, 5, 0, 6, 10,
        11, 11, 0, 2, 7, 0, 10, 8, 3, 5, 7, 1, 11, 1, 0, 7,
        9, 0, 11, 5, 10, 3, 2, 3, 5, 9, 7, 9, 8, 4, 6, 5,
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
    /// as tall as the GIF, and says how many there are.
    /// </summary>
    /// <remarks>
    /// raylib's image is one frame tall with the rest after it in memory, where this one is as
    /// tall as all of them, since an image's pixels here are always its size. The frames lie end
    /// to end in both, so a frame's offset in the pixels is the same.
    /// </remarks>
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
            // An image that is not a GIF is one frame, as raylib loads it.
            using var stream = File.OpenRead(path);
            Span<byte> signature = stackalloc byte[4];
            if (stream.ReadAtLeast(signature, 4, throwOnEndOfStream: false) < 4 || !signature.SequenceEqual("GIF8"u8))
            {
                var still = LoadImage(fileName);
                frames = IsImageValid(still) ? 1 : 0;
                return still;
            }
            stream.Position = 0;
            return GifFrames(stream, out frames);
        }
        // StbImageSharp throws a plain Exception for bytes that are not a GIF, so any is caught.
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            frames = 0;
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
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
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            ApiLogger.Warn($"LoadImageFromMemory: the {fileType} data could not be decoded: {ex.Message}");
            return default;
        }
    }

    /// <summary>
    /// A texture's pixels read back from the GPU, a render texture's color among them, as the
    /// frames drawn so far left it, or an invalid image when it is not on the GPU yet.
    /// </summary>
    /// <remarks>
    /// The call waits for the frames in flight to finish, so it is for saving a picture of a
    /// render texture, as <c>ExportImage(LoadImageFromTexture(target.Texture), "shot.png")</c>
    /// does, rather than for every frame. A texture loaded or given new pixels since the last frame
    /// is read from those pixels, which have not reached the GPU yet, so a texture reads back at
    /// once as raylib's does. A render target reads once a frame has drawn it.
    /// </remarks>
    public static Image LoadImageFromTexture(Texture2D texture)
    {
        if (texture.IsValid && Textures.PendingPixels(texture.Id) is { } pending)
            return new Image(pending, texture.Width, texture.Height);
        if (!texture.IsValid || ComputeDevice is not { } device
            || Res<Renderer>().RenderWorld.TryGet<GpuTextures>()?.ImageFor(texture.Id) is not { } image)
        {
            ApiLogger.Warn("LoadImageFromTexture: the texture is not on the GPU, so nothing is read.");
            return default;
        }
        var extent = image.Description.Extent;
        return new Image(device.ReadPixels(image), (int)extent.Width, (int)extent.Height);
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
    public static void ImageDraw(ref Image dst, Image src, Rectangle srcRec, Rectangle dstRec, Color tint)
    {
        if (!src.IsValid || srcRec.Width <= 0 || srcRec.Height <= 0) return;
        var (x, y, w, h) = Clip(dst, dstRec);
        for (int row = y; row < y + h; row++)
        for (int column = x; column < x + w; column++)
        {
            var u = (int)(srcRec.X + (column - dstRec.X + 0.5f) * srcRec.Width / dstRec.Width);
            var v = (int)(srcRec.Y + (row - dstRec.Y + 0.5f) * srcRec.Height / dstRec.Height);
            if (u < 0 || v < 0 || u >= src.Width || v >= src.Height) continue;
            var over = Multiply(GetImageColor(src, u, v), tint);
            SetPixel(dst, column, row, Blend(GetImageColor(dst, column, row), over));
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
