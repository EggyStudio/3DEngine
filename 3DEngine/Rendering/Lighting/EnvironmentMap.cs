using System.Numerics;

namespace Engine;

/// <summary>
/// The light from all around a scene, as a cube map prefiltered by roughness, which the model pass
/// reflects off every surface and scatters off its diffuse share. A world resource, set by
/// <see cref="Engine3D.SetEnvironmentMap(string, float)"/> or inserted directly.
/// </summary>
/// <remarks>
/// <para>
/// Made on the CPU from an equirectangular image (longitude across, latitude down, the top row
/// straight up), decoded from sRGB. Mip 0 is the image itself, for a mirror, and each mip after it
/// is the image as a surface of roughness <c>mip / (mips - 1)</c> reflects it, by GGX importance
/// sampling around the direction it is looked up by, as Brian Karis's split sum takes it. Each
/// sample reads a level of the image blurred to the solid angle it stands for, so 64 samples a
/// texel come out smooth.
/// </para>
/// <para>
/// The light the diffuse share scatters is the image's irradiance, every direction's light
/// weighted by its cosine to the normal, held as nine spherical harmonic coefficients
/// (<see cref="Irradiance"/>), as Ramamoorthi and Hanrahan give it. A diffuse surface is lit by
/// the whole sky that way, where a blurred lookup lights it by the part near its normal. Making a
/// map takes a few hundred milliseconds for a 64 texel face, so it is meant for a level's start
/// rather than every frame.
/// </para>
/// </remarks>
public sealed class EnvironmentMap
{
    private const int Samples = 64;

    // The sky cube's largest face, past which a face costs more memory than a backdrop shows.
    private const int MaxSkySize = 512;

    private EnvironmentMap(int size, int mipLevels, Half[] texels, float intensity, int skySize, Half[] skyTexels, Vector3[] irradiance)
    {
        Irradiance = irradiance;
        SkySize = skySize;
        SkyTexels = skyTexels;
        Size = size;
        MipLevels = mipLevels;
        Texels = texels;
        Intensity = intensity;
    }

    /// <summary>The width of a face at the first mip.</summary>
    public int Size { get; }

    /// <summary>How many mips, from a mirror at the first to fully rough at the last.</summary>
    public int MipLevels { get; }

    /// <summary>RGBA half floats, linear, mip by mip and face by face in Vulkan's order (+X, -X, +Y, -Y, +Z, -Z).</summary>
    public Half[] Texels { get; }

    /// <summary>The width of a face of the sky cube, which keeps the image's own detail for drawing it as a backdrop.</summary>
    public int SkySize { get; }

    /// <summary>
    /// The sky cube's RGBA half floats, linear, face by face in Vulkan's order, one mip, resampled
    /// from the image with no prefiltering, at about the image's own resolution.
    /// </summary>
    public Half[] SkyTexels { get; }

    /// <summary>
    /// The image's irradiance divided by pi, the light a white diffuse surface returns, as nine
    /// spherical harmonic coefficients of bands 0 to 2 in the order the model pass evaluates them
    /// (Y00, Y1-1, Y10, Y11, Y2-2, Y2-1, Y20, Y21, Y22), each already multiplied by its band's
    /// share of a cosine lobe.
    /// </summary>
    public Vector3[] Irradiance { get; }

    /// <summary>What the map's light is multiplied by.</summary>
    public float Intensity { get; set; }

    /// <summary>Makes a map from an equirectangular image of sRGB color, with faces <paramref name="faceSize"/> texels wide.</summary>
    /// <remarks>Eight bits a channel cap the light at white, so a sun is no brighter than a cloud. <see cref="FromHdrFile"/> keeps it.</remarks>
    /// <exception cref="ArgumentException">The image is empty.</exception>
    public static EnvironmentMap FromEquirectangular(Image image, float intensity = 1, int faceSize = 64)
    {
        if (image.Width <= 0 || image.Height <= 0 || image.Data.Length < image.Width * image.Height * 4)
            throw new ArgumentException("An environment needs an image with pixels.", nameof(image));

        var linear = new Vector3[image.Width * image.Height];
        for (int i = 0; i < linear.Length; i++)
            linear[i] = new Vector3(Decode(image.Data[i * 4]), Decode(image.Data[i * 4 + 1]), Decode(image.Data[i * 4 + 2]));
        return FromLinear(linear, image.Width, image.Height, intensity, faceSize);
    }

    /// <summary>
    /// Makes a map from a Radiance <c>.hdr</c> file's equirectangular image, whose pixels are linear
    /// light past 1, so a sun outshines the sky around it in what a metal reflects.
    /// </summary>
    /// <exception cref="ArgumentException">The bytes are not a Radiance image.</exception>
    public static EnvironmentMap FromHdrFile(byte[] file, float intensity = 1, int faceSize = 64)
    {
        StbImageSharp.ImageResultFloat image;
        try
        {
            image = StbImageSharp.ImageResultFloat.FromMemory(file, StbImageSharp.ColorComponents.RedGreenBlue);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            throw new ArgumentException($"Not a Radiance image: {ex.Message}", nameof(file), ex);
        }

        var linear = new Vector3[image.Width * image.Height];
        for (int i = 0; i < linear.Length; i++)
            linear[i] = new Vector3(image.Data[i * 3], image.Data[i * 3 + 1], image.Data[i * 3 + 2]);
        return FromLinear(linear, image.Width, image.Height, intensity, faceSize);
    }

    /// <summary>Makes a map from an equirectangular image of linear light, row by row from the top, which may pass 1.</summary>
    /// <exception cref="ArgumentException">The pixels are fewer than the size says.</exception>
    public static EnvironmentMap FromLinear(Vector3[] pixels, int width, int height, float intensity = 1, int faceSize = 64)
    {
        if (width <= 0 || height <= 0 || pixels.Length < width * height)
            throw new ArgumentException("An environment needs an image with pixels.", nameof(pixels));

        faceSize = Math.Max(1, (int)BitOperations.RoundUpToPowerOf2((uint)faceSize));
        int mips = BitOperations.Log2((uint)faceSize) + 1;
        var source = Pyramid.From(pixels, width, height);

        // Each mip's GGX samples around +Z, with the level of the image each reads.
        var lobes = new (Vector3 Direction, float Level)[mips][];
        for (int m = 1; m < mips; m++) lobes[m] = Lobe((float)m / (mips - 1), source.TexelSolidAngle);

        var offsets = new int[mips + 1];
        for (int m = 0; m < mips; m++)
        {
            int s = Math.Max(1, faceSize >> m);
            offsets[m + 1] = offsets[m] + s * s * 6 * 4;
        }
        var texels = new Half[offsets[mips]];

        for (int m = 0; m < mips; m++)
        {
            int s = Math.Max(1, faceSize >> m);
            int mip = m;
            // A texel of this mip covers about this many of the image's, which its level blurs over.
            float ownLevel = MathF.Max(0, 0.5f * MathF.Log2(4 * MathF.PI / (6f * s * s) / source.TexelSolidAngle));
            Parallel.For(0, 6 * s, row =>
            {
                int face = row / s, y = row % s;
                for (int x = 0; x < s; x++)
                {
                    var n = Direction(face, (x + 0.5f) / s * 2 - 1, (y + 0.5f) / s * 2 - 1);
                    var color = mip == 0 ? source.Sample(n, ownLevel) : Filtered(source, n, lobes[mip]);
                    int at = offsets[mip] + ((face * s + y) * s + x) * 4;
                    texels[at] = (Half)color.X;
                    texels[at + 1] = (Half)color.Y;
                    texels[at + 2] = (Half)color.Z;
                    texels[at + 3] = (Half)1f;
                }
            });
        }

        // A face spans a quarter of the image's width, so that many texels keep its detail.
        var skySize = (int)Math.Clamp(BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, width / 4)), (uint)faceSize, (uint)Math.Max(faceSize, MaxSkySize));
        return new EnvironmentMap(faceSize, mips, texels, intensity, skySize, Resampled(source, skySize), source.Irradiance());
    }

    /// <summary>
    /// Makes a map from six square frames drawn from <paramref name="eye"/>, each four bytes a pixel
    /// of display-encoded color, rows from the top, through the view-projection beside it, as a
    /// <see cref="ReflectionProbe"/> captures its room.
    /// </summary>
    /// <remarks>
    /// Each direction is read from the frame whose view looks most nearly along it, through that
    /// view's own projection, so the faces may be drawn with any orientation. The color is decoded
    /// from sRGB, the model pass's tonemap undone and the <paramref name="exposure"/> the faces were
    /// drawn at divided out, which gives the light back below the knee over the exposure.
    /// </remarks>
    internal static EnvironmentMap FromCapture(byte[][] faces, int size, Matrix4x4[] viewProjections, Vector3 eye, int faceSize = 32, float exposure = 1)
    {
        int width = 4 * size, height = 2 * size;
        var forwards = new Vector3[6];
        for (int f = 0; f < 6; f++)
        {
            // The way a view looks, the point in the middle of its far plane less its eye.
            Matrix4x4.Invert(viewProjections[f], out var inverse);
            var far = Vector4.Transform(new Vector4(0, 0, 1, 1), inverse);
            forwards[f] = Vector3.Normalize(new Vector3(far.X, far.Y, far.Z) / far.W - eye);
        }

        var pixels = new Vector3[width * height];
        Parallel.For(0, height, y =>
        {
            var theta = (y + 0.5f) / height * MathF.PI;
            for (int x = 0; x < width; x++)
            {
                var phi = ((x + 0.5f) / width - 0.5f) * 2 * MathF.PI;
                var d = new Vector3(MathF.Sin(theta) * MathF.Sin(phi), MathF.Cos(theta), -MathF.Sin(theta) * MathF.Cos(phi));
                int face = 0;
                for (int f = 1; f < 6; f++)
                    if (Vector3.Dot(forwards[f], d) > Vector3.Dot(forwards[face], d)) face = f;
                var clip = Vector4.Transform(new Vector4(eye + d, 1), viewProjections[face]);
                int px = Math.Clamp((int)((clip.X / clip.W + 1) / 2 * size), 0, size - 1);
                int py = Math.Clamp((int)((clip.Y / clip.W + 1) / 2 * size), 0, size - 1);
                var at = (py * size + px) * 4;
                pixels[y * width + x] = Untonemapped(new Vector3(
                    SrgbToLinear(faces[face][at]), SrgbToLinear(faces[face][at + 1]), SrgbToLinear(faces[face][at + 2]))) / exposure;
            }
        });
        return FromLinear(pixels, width, height, 1, faceSize);
    }

    private static float SrgbToLinear(byte value)
    {
        var c = value / 255f;
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }

    // The model pass's tonemap turned back, the identity below its knee of 0.9 and the exponential
    // shoulder above it undone, up to the brightest a byte can say.
    private static Vector3 Untonemapped(Vector3 color)
    {
        const float knee = 0.9f;
        var peak = MathF.Max(color.X, MathF.Max(color.Y, color.Z));
        if (peak <= knee) return color;
        var shoulder = MathF.Min((peak - knee) / (1 - knee), 0.999f);
        var original = knee - (1 - knee) * MathF.Log(1 - shoulder);
        return color * (original / peak);
    }

    /// <summary>The nine real spherical harmonics of bands 0 to 2 in a direction, in <see cref="Irradiance"/>'s order.</summary>
    internal static void Harmonics(Vector3 d, Span<float> y)
    {
        y[0] = 0.282095f;
        y[1] = 0.488603f * d.Y;
        y[2] = 0.488603f * d.Z;
        y[3] = 0.488603f * d.X;
        y[4] = 1.092548f * d.X * d.Y;
        y[5] = 1.092548f * d.Y * d.Z;
        y[6] = 0.315392f * (3 * d.Z * d.Z - 1);
        y[7] = 1.092548f * d.X * d.Z;
        y[8] = 0.546274f * (d.X * d.X - d.Y * d.Y);
    }

    /// <summary>The light <see cref="Irradiance"/> gives a diffuse surface facing <paramref name="normal"/>, before intensity.</summary>
    public Vector3 IrradianceAt(Vector3 normal)
    {
        Span<float> y = stackalloc float[9];
        Harmonics(Vector3.Normalize(normal), y);
        var sum = Vector3.Zero;
        for (int i = 0; i < 9; i++) sum += Irradiance[i] * y[i];
        return Vector3.Max(sum, Vector3.Zero);
    }

    // The image resampled into a cube of faces size texels wide, each texel reading the image
    // blurred to its solid angle.
    private static Half[] Resampled(Pyramid source, int size)
    {
        var texels = new Half[size * size * 6 * 4];
        float level = MathF.Max(0, 0.5f * MathF.Log2(4 * MathF.PI / (6f * size * size) / source.TexelSolidAngle));
        Parallel.For(0, 6 * size, row =>
        {
            int face = row / size, y = row % size;
            for (int x = 0; x < size; x++)
            {
                var color = source.Sample(Direction(face, (x + 0.5f) / size * 2 - 1, (y + 0.5f) / size * 2 - 1), level);
                int at = ((face * size + y) * size + x) * 4;
                texels[at] = (Half)color.X;
                texels[at + 1] = (Half)color.Y;
                texels[at + 2] = (Half)color.Z;
                texels[at + 3] = (Half)1f;
            }
        });
        return texels;
    }

    /// <summary>The direction a cube texel looks along, for face coordinates from -1 to 1, as Vulkan's cube lookup has it.</summary>
    internal static Vector3 Direction(int face, float u, float v) => Vector3.Normalize(face switch
    {
        0 => new Vector3(1, -v, -u),
        1 => new Vector3(-1, -v, u),
        2 => new Vector3(u, 1, v),
        3 => new Vector3(u, -1, -v),
        4 => new Vector3(u, -v, 1),
        _ => new Vector3(-u, -v, -1),
    });

    // The image as a surface around n reflects it, every sample of the lobe turned from +Z to n
    // and weighted by its cosine.
    private static Vector3 Filtered(Pyramid source, Vector3 n, (Vector3 Direction, float Level)[] lobe)
    {
        var up = MathF.Abs(n.Z) < 0.999f ? Vector3.UnitZ : Vector3.UnitX;
        var tangent = Vector3.Normalize(Vector3.Cross(up, n));
        var bitangent = Vector3.Cross(n, tangent);

        var sum = Vector3.Zero;
        float weight = 0;
        foreach (var (d, level) in lobe)
        {
            var l = tangent * d.X + bitangent * d.Y + n * d.Z;
            sum += source.Sample(l, level) * d.Z;
            weight += d.Z;
        }
        return weight > 0 ? sum / weight : source.Sample(n, 0);
    }

    // Light directions around +Z (the normal and the eye both) for a roughness, by GGX importance
    // sampling on a Hammersley set, with the image's level each sample's solid angle blurs over.
    private static (Vector3, float)[] Lobe(float roughness, float texelSolidAngle)
    {
        float a = MathF.Max(roughness * roughness, 1e-3f);
        float a2 = a * a;
        var lobe = new List<(Vector3, float)>(Samples);
        for (uint i = 0; i < Samples; i++)
        {
            float x = (float)i / Samples;
            float y = RadicalInverse(i);
            float phi = 2 * MathF.PI * x;
            float cosTheta = MathF.Sqrt((1 - y) / (1 + (a2 - 1) * y));
            float sinTheta = MathF.Sqrt(1 - cosTheta * cosTheta);
            var h = new Vector3(sinTheta * MathF.Cos(phi), sinTheta * MathF.Sin(phi), cosTheta);
            var l = 2 * h.Z * h - Vector3.UnitZ;
            if (l.Z <= 0) continue;

            // With the eye along the normal the sample's pdf is D / 4.
            float d = a2 / (MathF.PI * MathF.Pow(h.Z * h.Z * (a2 - 1) + 1, 2));
            float solidAngle = 1 / (Samples * d / 4 + 1e-4f);
            lobe.Add((l, MathF.Max(0, 0.5f * MathF.Log2(solidAngle / texelSolidAngle) + 1)));
        }
        return [.. lobe];
    }

    // Van der Corput's sequence, the bits of i mirrored about the binary point.
    private static float RadicalInverse(uint i)
    {
        i = (i << 16) | (i >> 16);
        i = ((i & 0x55555555u) << 1) | ((i & 0xAAAAAAAAu) >> 1);
        i = ((i & 0x33333333u) << 2) | ((i & 0xCCCCCCCCu) >> 2);
        i = ((i & 0x0F0F0F0Fu) << 4) | ((i & 0xF0F0F0F0u) >> 4);
        i = ((i & 0x00FF00FFu) << 8) | ((i & 0xFF00FF00u) >> 8);
        return i / 4294967296f;
    }

    // The equirectangular image in linear color, halved level by level, sampled by direction.
    private sealed class Pyramid
    {
        private readonly List<(Vector3[] Pixels, int Width, int Height)> _levels = [];

        public float TexelSolidAngle { get; private init; }

        public static Pyramid From(Vector3[] linear, int width, int height)
        {
            // Half floats stop at 65504, and a negative or broken pixel would spread through the blur.
            var clean = new Vector3[width * height];
            for (int i = 0; i < clean.Length; i++)
            {
                var p = linear[i];
                clean[i] = float.IsFinite(p.X + p.Y + p.Z) ? Vector3.Clamp(p, Vector3.Zero, new Vector3(65000)) : Vector3.Zero;
            }

            var pyramid = new Pyramid { TexelSolidAngle = 4 * MathF.PI / (width * height) };
            pyramid._levels.Add((clean, width, height));
            while (pyramid._levels[^1] is { Width: > 1 } or { Height: > 1 })
            {
                var (pixels, w, h) = pyramid._levels[^1];
                int w2 = Math.Max(1, w / 2), h2 = Math.Max(1, h / 2);
                var half = new Vector3[w2 * h2];
                for (int y = 0; y < h2; y++)
                for (int x = 0; x < w2; x++)
                {
                    int x0 = Math.Min(w - 1, x * 2), x1 = Math.Min(w - 1, x * 2 + 1);
                    int y0 = Math.Min(h - 1, y * 2), y1 = Math.Min(h - 1, y * 2 + 1);
                    half[y * w2 + x] = (pixels[y0 * w + x0] + pixels[y0 * w + x1] + pixels[y1 * w + x0] + pixels[y1 * w + x1]) / 4;
                }
                pyramid._levels.Add((half, w2, h2));
            }
            return pyramid;
        }

        // The image projected onto the harmonics, each pixel weighted by the solid angle it
        // covers, then each band scaled by its share of a cosine lobe over pi (1, 2/3 and 1/4),
        // so the sum in a direction is the light a white diffuse surface facing it returns. A
        // level of about 128 pixels across is fine enough, since the bands hold no finer detail.
        public Vector3[] Irradiance()
        {
            var (pixels, w, h) = _levels.FirstOrDefault(l => l.Width <= 128, _levels[^1]);
            var coefficients = new Vector3[9];
            Span<float> y = stackalloc float[9];
            float pixelAngle = 2 * MathF.PI / w * (MathF.PI / h);
            for (int row = 0; row < h; row++)
            {
                float theta = (row + 0.5f) / h * MathF.PI;
                float sinTheta = MathF.Sin(theta), cosTheta = MathF.Cos(theta);
                for (int x = 0; x < w; x++)
                {
                    // The inverse of Sample's mapping, u across from the back and v down from the top.
                    float phi = ((x + 0.5f) / w - 0.5f) * 2 * MathF.PI;
                    var d = new Vector3(sinTheta * MathF.Sin(phi), cosTheta, -sinTheta * MathF.Cos(phi));
                    Harmonics(d, y);
                    var light = pixels[row * w + x] * (pixelAngle * sinTheta);
                    for (int i = 0; i < 9; i++) coefficients[i] += light * y[i];
                }
            }
            for (int i = 0; i < 9; i++) coefficients[i] *= i == 0 ? 1f : i < 4 ? 2f / 3 : 0.25f;
            return coefficients;
        }

        public Vector3 Sample(Vector3 direction, float level)
        {
            level = Math.Clamp(level, 0, _levels.Count - 1);
            int below = (int)level;
            int above = Math.Min(below + 1, _levels.Count - 1);
            var u = 0.5f + MathF.Atan2(direction.X, -direction.Z) / (2 * MathF.PI);
            var v = MathF.Acos(Math.Clamp(direction.Y, -1, 1)) / MathF.PI;
            return Vector3.Lerp(Bilinear(_levels[below], u, v), Bilinear(_levels[above], u, v), level - below);
        }

        private static Vector3 Bilinear((Vector3[] Pixels, int Width, int Height) level, float u, float v)
        {
            var (pixels, w, h) = level;
            float x = u * w - 0.5f, y = Math.Clamp(v * h - 0.5f, 0, h - 1);
            int x0 = (int)MathF.Floor(x), y0 = (int)MathF.Floor(y);
            float fx = x - x0, fy = y - y0;
            int Wrap(int i) => ((i % w) + w) % w;
            int y1 = Math.Min(y0 + 1, h - 1);
            var top = Vector3.Lerp(pixels[y0 * w + Wrap(x0)], pixels[y0 * w + Wrap(x0 + 1)], fx);
            var bottom = Vector3.Lerp(pixels[y1 * w + Wrap(x0)], pixels[y1 * w + Wrap(x0 + 1)], fx);
            return Vector3.Lerp(top, bottom, fy);
        }

    }

    private static float Decode(byte value)
    {
        var c = value / 255f;
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }
}
