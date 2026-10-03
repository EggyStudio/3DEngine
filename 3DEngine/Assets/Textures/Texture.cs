namespace Engine;

/// <summary>
/// CPU-side texture asset. Holds decoded pixel bytes plus the metadata needed to upload
/// them to a GPU texture later. Backend-agnostic: produced by any
/// <see cref="ITextureDecoder"/> (StbImageSharp today; KTX2 / DDS / EXR tomorrow) and
/// consumed by the renderer when material payloads come up for binding.
/// </summary>
/// <remarks>
/// <para>
/// <b>Memory layout:</b> <see cref="Pixels"/> is a tightly packed, row-major
/// top-left-origin pixel buffer. Each pixel is <see cref="TextureFormatInfo.BytesPerPixel"/>
/// bytes wide; the row stride is <c>Width * BytesPerPixel</c> (no padding). Mip chains
/// follow the level 0 data sequentially, each at half the previous extents (rounded down,
/// floor 1) - importers that don't generate mips set <see cref="MipCount"/> to 1 and the
/// renderer can build them on upload.
/// </para>
/// <para>
/// <b>Color space:</b> <see cref="ColorSpace"/> is informational only - decoders never
/// transform pixel values. Samplers / shaders are expected to honour it (sRGB textures
/// use sRGB-aware sampling on the GPU). The convention matches glTF: BaseColor / Emissive
/// are sRGB; Normal / MetallicRoughness / Occlusion / AmbientOcclusion are Linear.
/// </para>
/// </remarks>
/// <seealso cref="ITextureDecoder"/>
/// <seealso cref="TextureAssetLoader"/>
/// <seealso cref="TexturesPlugin"/>
public sealed class Texture
{
    /// <summary>Tightly packed pixel bytes (top-left origin, row-major, no padding).</summary>
    public required byte[] Pixels { get; init; }

    /// <summary>Width of mip level 0, in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Height of mip level 0, in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>
    /// Number of mip levels packed sequentially in <see cref="Pixels"/>. <c>1</c> means
    /// only the base level is present; the renderer is free to generate the chain on
    /// upload.
    /// </summary>
    public int MipCount { get; init; } = 1;

    /// <summary>Pixel format of <see cref="Pixels"/>.</summary>
    public required TextureFormat Format { get; init; }

    /// <summary>How sampled values should be interpreted by the GPU.</summary>
    public TextureColorSpace ColorSpace { get; init; } = TextureColorSpace.Linear;

    /// <summary>
    /// Source asset path (the <see cref="AssetPath"/> the loader resolved this from),
    /// for diagnostics and hot-reload identity. May be empty for procedurally generated
    /// textures.
    /// </summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>
    /// Identifier of the decoder backend that produced this asset (e.g. <c>"stb"</c>,
    /// <c>"ktx2"</c>). Mirrors <see cref="SceneAsset.SourceFormat"/>.
    /// </summary>
    public string SourceFormat { get; init; } = string.Empty;
}

/// <summary>
/// Pixel format enumeration covering the LDR / HDR / block-compressed formats the engine
/// will eventually upload. Only the uncompressed entries are produced by
/// <see cref="StbTextureDecoder"/> today; the BC* / KTX2 entries are placeholders for
/// future <c>Engine.Textures.Ktx2</c> / <c>Engine.Textures.Dds</c> backends.
/// </summary>
public enum TextureFormat
{
    /// <summary>Single channel, 8-bit unsigned normalised.</summary>
    R8,
    /// <summary>Two channels, 8-bit unsigned normalised.</summary>
    Rg8,
    /// <summary>Four channels, 8-bit unsigned normalised. Most common LDR format.</summary>
    Rgba8,
    /// <summary>Four channels, 16-bit half-float. Mid-range HDR.</summary>
    Rgba16F,
    /// <summary>Four channels, 32-bit float. High-range HDR (Radiance .hdr decode target).</summary>
    Rgba32F,

    /// <summary>BC1 (DXT1) - opaque RGB or 1-bit alpha; 0.5 bpp.</summary>
    Bc1,
    /// <summary>BC3 (DXT5) - RGB + smooth alpha; 1 bpp.</summary>
    Bc3,
    /// <summary>BC4 - single channel; 0.5 bpp.</summary>
    Bc4,
    /// <summary>BC5 - two channel (typical for normal maps); 1 bpp.</summary>
    Bc5,
    /// <summary>BC6H - HDR RGB; 1 bpp.</summary>
    Bc6H,
    /// <summary>BC7 - high-quality LDR RGBA; 1 bpp.</summary>
    Bc7,
}

/// <summary>How the GPU should interpret the texture's stored values when sampling.</summary>
public enum TextureColorSpace
{
    /// <summary>Values are already linear; no conversion on sample.</summary>
    Linear,

    /// <summary>
    /// Values are sRGB-encoded; the GPU should linearise on sample. Convention for
    /// BaseColor and Emissive textures.
    /// </summary>
    Srgb,
}

/// <summary>Static helpers for <see cref="TextureFormat"/> metadata.</summary>
public static class TextureFormatInfo
{
    /// <summary>
    /// Bytes per pixel for the uncompressed formats. Throws for block-compressed formats,
    /// which are sized per-block (use <see cref="BytesPerBlock"/> for those).
    /// </summary>
    public static int BytesPerPixel(TextureFormat format) => format switch
    {
        TextureFormat.R8      => 1,
        TextureFormat.Rg8     => 2,
        TextureFormat.Rgba8   => 4,
        TextureFormat.Rgba16F => 8,
        TextureFormat.Rgba32F => 16,
        _ => throw new ArgumentException(
            $"TextureFormat.{format} is block-compressed; use BytesPerBlock instead.", nameof(format)),
    };

    /// <summary>
    /// Bytes per 4x4 block for the BC* formats. Throws for uncompressed formats
    /// (use <see cref="BytesPerPixel"/>).
    /// </summary>
    public static int BytesPerBlock(TextureFormat format) => format switch
    {
        TextureFormat.Bc1  => 8,
        TextureFormat.Bc4  => 8,
        TextureFormat.Bc3  => 16,
        TextureFormat.Bc5  => 16,
        TextureFormat.Bc6H => 16,
        TextureFormat.Bc7  => 16,
        _ => throw new ArgumentException(
            $"TextureFormat.{format} is not block-compressed; use BytesPerPixel instead.", nameof(format)),
    };

    /// <summary>True when <paramref name="format"/> is one of the BC* block-compressed formats.</summary>
    public static bool IsBlockCompressed(TextureFormat format) => format
        is TextureFormat.Bc1 or TextureFormat.Bc3 or TextureFormat.Bc4
        or TextureFormat.Bc5 or TextureFormat.Bc6H or TextureFormat.Bc7;
}