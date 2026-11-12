namespace Engine;

/// <summary>
/// The pixel formats a texture may hold, LDR, HDR and block-compressed. The uncompressed ones are
/// produced by <see cref="StbTextureDecoder"/> and BC1, BC5 and BC7 by the KTX2 decoder, and no
/// decoder produces BC3, BC4 or BC6H.
/// </summary>
public enum TextureFormat
{
    /// <summary>Single channel, 8-bit unsigned normalized.</summary>
    R8,
    /// <summary>Two channels, 8-bit unsigned normalized.</summary>
    Rg8,
    /// <summary>Four channels, 8-bit unsigned normalized. Most common LDR format.</summary>
    Rgba8,
    /// <summary>Four channels, 16-bit half-float. Mid-range HDR.</summary>
    Rgba16F,
    /// <summary>Four channels, 32-bit float. High-range HDR (Radiance .hdr decode target).</summary>
    Rgba32F,

    /// <summary>BC1 (DXT1), opaque RGB or 1-bit alpha, 0.5 bytes a pixel.</summary>
    Bc1,
    /// <summary>BC3 (DXT5), RGB with smooth alpha, 1 byte a pixel.</summary>
    Bc3,
    /// <summary>BC4, one channel, 0.5 bytes a pixel.</summary>
    Bc4,
    /// <summary>BC5, two channels, as normal maps use, 1 byte a pixel.</summary>
    Bc5,
    /// <summary>BC6H, HDR RGB, 1 byte a pixel.</summary>
    Bc6H,
    /// <summary>BC7, high-quality LDR RGBA, 1 byte a pixel.</summary>
    Bc7,
}
