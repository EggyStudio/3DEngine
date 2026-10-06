namespace Engine;

/// <summary>
/// How pixels are laid out in memory, raylib's <c>PixelFormat</c> under its values, which
/// <see cref="Engine3D.LoadImageRaw"/> reads and <see cref="Engine3D.ImageFormat"/> brings an image to.
/// </summary>
/// <remarks>
/// An image here always holds four bytes a pixel, red, green, blue and alpha. A format is read into
/// them, and <see cref="Engine3D.ImageFormat"/> keeps of each pixel what the format keeps. The
/// compressed formats are named for raylib's values and read by neither.
/// </remarks>
public enum PixelFormat
{
    /// <summary>A byte of gray a pixel.</summary>
    UncompressedGrayscale = 1,
    /// <summary>A byte of gray and a byte of alpha a pixel.</summary>
    UncompressedGrayAlpha,
    /// <summary>Sixteen bits a pixel, five of red, six of green and five of blue.</summary>
    UncompressedR5G6B5,
    /// <summary>A byte each of red, green and blue.</summary>
    UncompressedR8G8B8,
    /// <summary>Sixteen bits a pixel, five each of red, green and blue and one of alpha.</summary>
    UncompressedR5G5B5A1,
    /// <summary>Sixteen bits a pixel, four each of red, green, blue and alpha.</summary>
    UncompressedR4G4B4A4,
    /// <summary>A byte each of red, green, blue and alpha, as an image here holds them.</summary>
    UncompressedR8G8B8A8,
    /// <summary>A float a pixel, read as red.</summary>
    UncompressedR32,
    /// <summary>A float each of red, green and blue.</summary>
    UncompressedR32G32B32,
    /// <summary>A float each of red, green, blue and alpha.</summary>
    UncompressedR32G32B32A32,
    /// <summary>A half float a pixel, read as red.</summary>
    UncompressedR16,
    /// <summary>A half float each of red, green and blue.</summary>
    UncompressedR16G16B16,
    /// <summary>A half float each of red, green, blue and alpha.</summary>
    UncompressedR16G16B16A16,
    /// <summary>DXT1 blocks without alpha, which is not read.</summary>
    CompressedDxt1Rgb,
    /// <summary>DXT1 blocks with a bit of alpha, which is not read.</summary>
    CompressedDxt1Rgba,
    /// <summary>DXT3 blocks, which are not read.</summary>
    CompressedDxt3Rgba,
    /// <summary>DXT5 blocks, which are not read.</summary>
    CompressedDxt5Rgba,
    /// <summary>ETC1 blocks, which are not read.</summary>
    CompressedEtc1Rgb,
    /// <summary>ETC2 blocks without alpha, which are not read.</summary>
    CompressedEtc2Rgb,
    /// <summary>ETC2 blocks with EAC alpha, which are not read.</summary>
    CompressedEtc2EacRgba,
    /// <summary>PVRTC blocks without alpha, which are not read.</summary>
    CompressedPvrtRgb,
    /// <summary>PVRTC blocks with alpha, which are not read.</summary>
    CompressedPvrtRgba,
    /// <summary>ASTC blocks of 4 by 4, which are not read.</summary>
    CompressedAstc4x4Rgba,
    /// <summary>ASTC blocks of 8 by 8, which are not read.</summary>
    CompressedAstc8x8Rgba,
}
