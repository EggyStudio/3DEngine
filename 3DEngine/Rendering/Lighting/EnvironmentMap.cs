using System.Numerics;

namespace Engine;

/// <summary>
/// The light from all around a scene, an equirectangular image of linear light the renderer filters
/// into a cube prefiltered by roughness, which the model pass reflects off every surface, and into
/// its irradiance, which the pass scatters off the diffuse share. A world resource, set by
/// <see cref="Engine3D.SetEnvironmentMap(string, float)"/> or inserted directly.
/// </summary>
/// <remarks>
/// <para>
/// The image has longitude across and latitude down, its top row straight up, decoded from sRGB or
/// read as linear light from a Radiance file. The frame that first sees a map uploads the image
/// with its mips and filters it on the GPU (<see cref="GraphicsDevice.RecordEnvironmentFilter"/>):
/// resampled into a cube, mip 0 for a mirror and each mip after it the light as a surface of
/// roughness <c>mip / (mips - 1)</c> reflects it, by GGX importance sampling around the direction
/// it is looked up by, as Brian Karis's split sum takes it.
/// </para>
/// <para>
/// The diffuse share is lit by the image's irradiance, every direction's light weighted by its
/// cosine to the normal, held as nine spherical harmonic coefficients, as Ramamoorthi and Hanrahan
/// give it, so a diffuse surface is lit by the whole sky rather than the part near its normal.
/// Making a map on the CPU is decoding its image, and the filter is a frame's GPU work.
/// </para>
/// </remarks>
internal sealed class EnvironmentMap
{
    // The sky cube's largest face, past which a face costs more memory than a backdrop shows.
    private const int MaxSkySize = 512;

    // The widest image uploaded, the width every Vulkan device makes an image of. A wider one is
    // halved on the CPU until it fits, which leaves a sky cube of 512 its detail.
    private const int MaxWidth = 4096;

    private EnvironmentMap(Half[] pixels, int width, int height, float intensity, int size)
    {
        Pixels = pixels;
        Width = width;
        Height = height;
        Intensity = intensity;
        Size = size;
        MipLevels = (int)Math.Log2(size) + 1;
        SkySize = (int)Math.Clamp(System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(1, width / 4)), (uint)size, (uint)Math.Max(size, MaxSkySize));
    }

    /// <summary>The image's RGBA half floats, linear, row by row from the top.</summary>
    public Half[] Pixels { get; }

    /// <summary>The image's width in pixels, longitude across.</summary>
    public int Width { get; }

    /// <summary>The image's height in pixels, latitude down.</summary>
    public int Height { get; }

    /// <summary>The width of a face of the filtered cube at its first mip.</summary>
    public int Size { get; }

    /// <summary>How many mips the filtered cube has, from a mirror at the first to fully rough at the last.</summary>
    public int MipLevels { get; }

    /// <summary>The width of a face of the sky cube, which keeps the image's own detail for drawing it as a backdrop, a quarter of its width up to 512.</summary>
    public int SkySize { get; }

    /// <summary>What the map's light is multiplied by.</summary>
    public float Intensity { get; set; }

    /// <summary>Makes a map from an equirectangular image of sRGB color, filtered into faces <paramref name="faceSize"/> texels wide.</summary>
    /// <remarks>Eight bits a channel cap the light at white, so a sun is no brighter than a cloud. <see cref="FromHdrFile"/> keeps it.</remarks>
    /// <exception cref="ArgumentException">The image is empty.</exception>
    public static EnvironmentMap FromEquirectangular(Image image, float intensity = 1, int faceSize = 64)
    {
        if (image.Width <= 0 || image.Height <= 0 || image.Data.Length < image.Width * image.Height * 4)
            throw new ArgumentException("An environment needs an image with pixels.", nameof(image));

        if (image.Width > MaxWidth)
        {
            var linear = new Vector3[image.Width * image.Height];
            for (int i = 0; i < linear.Length; i++)
                linear[i] = new Vector3(Decode(image.Data[i * 4]), Decode(image.Data[i * 4 + 1]), Decode(image.Data[i * 4 + 2]));
            return FromLinear(linear, image.Width, image.Height, intensity, faceSize);
        }

        // Each byte's light as a half float, looked up, since a 4096 by 2048 image decoded a channel
        // at a time through a power took most of half a second.
        var decoded = new Half[256];
        for (int i = 0; i < 256; i++) decoded[i] = (Half)Decode((byte)i);
        var halves = new Half[image.Width * image.Height * 4];
        var data = image.Data;
        Parallel.For(0, image.Height, y =>
        {
            for (int i = y * image.Width; i < (y + 1) * image.Width; i++)
            {
                halves[i * 4] = decoded[data[i * 4]];
                halves[i * 4 + 1] = decoded[data[i * 4 + 1]];
                halves[i * 4 + 2] = decoded[data[i * 4 + 2]];
                halves[i * 4 + 3] = Half.One;
            }
        });
        return new EnvironmentMap(halves, image.Width, image.Height, intensity, FaceSize(faceSize));
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

        while (width > MaxWidth)
            (pixels, width, height) = Halved(pixels, width, height);

        // Half floats stop at 65504, and a negative or broken pixel would spread through the filter.
        var halves = new Half[width * height * 4];
        var source = pixels;
        Parallel.For(0, height, y =>
        {
            for (int i = y * width; i < (y + 1) * width; i++)
            {
                var p = source[i];
                var clean = float.IsFinite(p.X + p.Y + p.Z) ? Vector3.Clamp(p, Vector3.Zero, new Vector3(65000)) : Vector3.Zero;
                halves[i * 4] = (Half)clean.X;
                halves[i * 4 + 1] = (Half)clean.Y;
                halves[i * 4 + 2] = (Half)clean.Z;
                halves[i * 4 + 3] = Half.One;
            }
        });
        return new EnvironmentMap(halves, width, height, intensity, FaceSize(faceSize));
    }

    // A face width a power of two, so each mip halves it evenly.
    private static int FaceSize(int faceSize) => Math.Max(1, (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)faceSize));

    // The image at half its width and height, each pixel the average of four.
    private static (Vector3[] Pixels, int Width, int Height) Halved(Vector3[] pixels, int width, int height)
    {
        int w = Math.Max(1, width / 2), h = Math.Max(1, height / 2);
        var half = new Vector3[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            int x0 = Math.Min(width - 1, x * 2), x1 = Math.Min(width - 1, x * 2 + 1);
            int y0 = Math.Min(height - 1, y * 2), y1 = Math.Min(height - 1, y * 2 + 1);
            half[y * w + x] = (pixels[y0 * width + x0] + pixels[y0 * width + x1] + pixels[y1 * width + x0] + pixels[y1 * width + x1]) / 4;
        }
        return (half, w, h);
    }

    private static float Decode(byte value)
    {
        var c = value / 255f;
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }
}

/// <summary>Render graph node that filters a new environment map on the GPU before any pass samples it.</summary>
internal sealed class EnvironmentNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) =>
        renderWorld.TryGet<ModelRenderer>()?.FilterEnvironment(renderContext, renderWorld);
}
