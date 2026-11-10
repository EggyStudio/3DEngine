namespace Engine;

/// <summary>
/// The sRGB curve between a channel as a picture stores it, from 0 to 1, and its light, which the
/// engine's colors, pictures and passes go through each way (<c>E3D.SrgbToLinear</c>,
/// <c>E3D.LinearToSrgb</c>), and which <c>color.slang</c> works out the same on the GPU.
/// </summary>
internal static class Srgb
{
    /// <summary>The light of a channel stored as sRGB, from 0 to 1.</summary>
    public static float Decode(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    /// <summary>The light of a channel stored as a byte of sRGB.</summary>
    public static float Decode(byte value) => Decode(value / 255f);

    /// <summary>A channel's light as sRGB stores it, from 0 to 1 for light from 0 to 1.</summary>
    public static float Encode(float c) => c <= 0.0031308f ? 12.92f * c : 1.055f * MathF.Pow(c, 1 / 2.4f) - 0.055f;

    /// <summary>A channel's light as a byte of sRGB, held to 0 and 255.</summary>
    public static byte EncodeByte(float c) => (byte)Math.Clamp(MathF.Round(Encode(c) * 255), 0, 255);
}
