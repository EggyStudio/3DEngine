using System.Numerics;

namespace Engine;

/// <summary>An image carried inside a model file.</summary>
/// <remarks>
/// Either <see cref="Encoded"/> holds a whole image file (PNG or JPEG, as glTF stores them) with
/// <see cref="FormatHint"/> naming its kind, or <see cref="Rgba"/> holds decoded pixels,
/// <see cref="Width"/> by <see cref="Height"/>, which a few formats store raw.
/// </remarks>
/// <param name="FileName">The name the image had before it was embedded, when the file kept it.</param>
/// <param name="Encoded">The image file's bytes, or null for raw pixels.</param>
/// <param name="FormatHint">The encoded image's kind, such as <c>png</c> or <c>jpg</c>.</param>
/// <param name="Width">The width of raw pixels, or zero for an encoded image.</param>
/// <param name="Height">The height of raw pixels, or zero for an encoded image.</param>
/// <param name="Rgba">Raw pixels, four bytes each, or null for an encoded image.</param>
public sealed record SceneEmbeddedTexture(string? FileName, byte[]? Encoded, string FormatHint, int Width, int Height, byte[]? Rgba);
