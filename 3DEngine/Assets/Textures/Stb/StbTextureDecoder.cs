using StbImageSharp;

namespace Engine;

/// <summary>
/// <see cref="ITextureDecoder"/> backed by <c>StbImageSharp</c> - a pure-managed port of
/// the venerable <c>stb_image.h</c>. Covers ~95% of textures referenced by typical model
/// files: PNG, JPEG, BMP, TGA, PSD, GIF, HDR (Radiance), PIC, and PNM/PPM/PGM.
/// </summary>
/// <remarks>
/// <para>
/// <b>Format selection:</b> dispatches on file extension to either the LDR path
/// (<see cref="ImageResult.FromStream(Stream, ColorComponents)"/>) - producing
/// <see cref="TextureFormat.Rgba8"/> - or the HDR path
/// (<see cref="ImageResultFloat.FromStream(Stream, ColorComponents)"/>) for
/// <c>.hdr</c> / <c>.pic</c> files - producing <see cref="TextureFormat.Rgba32F"/>.
/// </para>
/// <para>
/// <b>Channel expansion:</b> always decodes to RGBA so the renderer can use a single
/// upload path. Single-channel and RGB sources are expanded to 4 channels by the
/// underlying stb decoder. Future revisions can downcast to <see cref="TextureFormat.R8"/>
/// / <see cref="TextureFormat.Rg8"/> based on <see cref="ImageResult.SourceComp"/> if a
/// caller hint asks for it.
/// </para>
/// <para>
/// <b>Color space:</b> stb itself doesn't expose the PNG sRGB chunk, so unless
/// <see cref="TextureLoadSettings.ColorSpace"/> is set the decoder leaves the texture as
/// <see cref="TextureColorSpace.Linear"/>. <c>SceneSpawner</c> tags texture loads from
/// material payloads with the right space (BaseColor / Emissive → sRGB; everything else
/// → Linear).
/// </para>
/// </remarks>
public sealed class StbTextureDecoder : ITextureDecoder
{
    private static readonly ILogger Logger = Log.Category("Engine.Textures.Stb");

    private static readonly string[] LdrExtensions =
    [
        ".png", ".jpg", ".jpeg", ".bmp", ".tga", ".psd", ".gif",
        ".pnm", ".ppm", ".pgm",
    ];

    private static readonly string[] HdrExtensions =
    [
        ".hdr", ".pic",
    ];

    private static readonly string[] AllExtensions = [.. LdrExtensions, .. HdrExtensions];

    /// <inheritdoc />
    public string[] Extensions => AllExtensions;

    /// <inheritdoc />
    public string FormatId => "stb";

    /// <inheritdoc />
    public Task<Texture> DecodeAsync(AssetLoadContext context, TextureLoadSettings settings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(settings);

        var ext = context.Path.Extension;
        return Task.FromResult(IsHdrExtension(ext)
            ? DecodeHdr(context, settings)
            : DecodeLdr(context, settings));
    }

    private static bool IsHdrExtension(string ext)
    {
        foreach (var h in HdrExtensions)
            if (string.Equals(h, ext, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static Texture DecodeLdr(AssetLoadContext context, TextureLoadSettings settings)
    {
        var stream = context.GetStream();
        if (stream.CanSeek) stream.Position = 0;

        var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha)
                    ?? throw new InvalidOperationException(
                        $"StbTextureDecoder: ImageResult.FromStream returned null for '{context.Path}'.");

        Logger.Debug(
            $"StbTextureDecoder(LDR): '{context.Path}' decoded - {image.Width}x{image.Height}, " +
            $"src comps={image.SourceComp}, out=Rgba8 ({image.Data.Length} bytes).");

        return new Texture
        {
            Pixels = image.Data,
            Width = image.Width,
            Height = image.Height,
            MipCount = 1,
            Format = TextureFormat.Rgba8,
            ColorSpace = settings.ColorSpace ?? TextureColorSpace.Linear,
            SourcePath = context.Path.ToString(),
            SourceFormat = "stb",
        };
    }

    private static Texture DecodeHdr(AssetLoadContext context, TextureLoadSettings settings)
    {
        var stream = context.GetStream();
        if (stream.CanSeek) stream.Position = 0;

        var image = ImageResultFloat.FromStream(stream, ColorComponents.RedGreenBlueAlpha)
                    ?? throw new InvalidOperationException(
                        $"StbTextureDecoder: ImageResultFloat.FromStream returned null for '{context.Path}'.");

        // Pack float[] into byte[] without copying semantics: BlockCopy preserves the
        // little-endian IEEE-754 layout the GPU expects for Rgba32F.
        var bytes = new byte[image.Data.Length * sizeof(float)];
        Buffer.BlockCopy(image.Data, 0, bytes, 0, bytes.Length);

        Logger.Debug(
            $"StbTextureDecoder(HDR): '{context.Path}' decoded - {image.Width}x{image.Height}, " +
            $"src comps={image.SourceComp}, out=Rgba32F ({bytes.Length} bytes).");

        return new Texture
        {
            Pixels = bytes,
            Width = image.Width,
            Height = image.Height,
            MipCount = 1,
            Format = TextureFormat.Rgba32F,
            // HDR data is by definition linear; ignore caller hint for safety.
            ColorSpace = TextureColorSpace.Linear,
            SourcePath = context.Path.ToString(),
            SourceFormat = "stb",
        };
    }
}