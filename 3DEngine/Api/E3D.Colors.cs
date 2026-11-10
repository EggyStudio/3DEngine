using System.Numerics;

namespace Engine;

public static partial class E3D
{
    // -- Colors, worked out as raylib's rtextures works them out, so a ported program's colors
    // come out the same.

    /// <summary>The color with its alpha set to <paramref name="alpha"/>, from 0 to 1.</summary>
    public static Color Fade(Color color, float alpha) => ColorAlpha(color, alpha);

    /// <summary>The color with its alpha set to <paramref name="alpha"/>, from 0 to 1.</summary>
    public static Color ColorAlpha(Color color, float alpha) =>
        color with { A = (byte)(255f * Math.Clamp(alpha, 0f, 1f)) };

    /// <summary>The color as a number, red in the high byte and alpha in the low, as <c>0xRRGGBBAA</c>.</summary>
    public static int ColorToInt(Color color) =>
        unchecked((int)((uint)color.R << 24 | (uint)color.G << 16 | (uint)color.B << 8 | color.A));

    /// <summary>The color of a number written as <c>0xRRGGBBAA</c>, as <see cref="ColorToInt"/> gives.</summary>
    public static Color GetColor(uint hexValue) =>
        new((byte)(hexValue >> 24), (byte)(hexValue >> 16), (byte)(hexValue >> 8), (byte)hexValue);

    /// <summary>The color as four floats from 0 to 1.</summary>
    public static Vector4 ColorNormalize(Color color) => color.ToVector4();

    /// <summary>The color of four floats from 0 to 1.</summary>
    public static Color ColorFromNormalized(Vector4 normalized) =>
        new((byte)(normalized.X * 255f), (byte)(normalized.Y * 255f), (byte)(normalized.Z * 255f), (byte)(normalized.W * 255f));

    /// <summary>
    /// The light of a channel stored as sRGB, as a color's channel over 255 or a picture's is, from
    /// 0 to 1, by the sRGB curve.
    /// </summary>
    /// <remarks>
    /// Light adds and scales as numbers do, where sRGB, which spends more of its levels on the
    /// dark, does not, so a color is decoded before it is mixed, lit or compared by brightness and
    /// encoded again to be shown (<see cref="LinearToSrgb"/>). A gamma of 2.2 lies near the curve
    /// but not on it, darker in the shadows.
    /// </remarks>
    public static float SrgbToLinear(float value) => Srgb.Decode(value);

    /// <summary>A channel's light as sRGB stores it, from 0 to 1 for light from 0 to 1, by the sRGB curve.</summary>
    public static float LinearToSrgb(float value) => Srgb.Encode(value);

    /// <summary>
    /// The color's light, its red, green and blue decoded from sRGB (<see cref="SrgbToLinear"/>),
    /// with its alpha from 0 to 1 as it is, since alpha is no light.
    /// </summary>
    public static Vector4 ColorToLinear(Color color) =>
        new(Srgb.Decode(color.R), Srgb.Decode(color.G), Srgb.Decode(color.B), color.A / 255f);

    /// <summary>
    /// The color of a light, its red, green and blue encoded to sRGB (<see cref="LinearToSrgb"/>)
    /// and rounded, with its alpha from 0 to 1 as it is, each held to 0 and 255.
    /// </summary>
    public static Color ColorFromLinear(Vector4 linear) => new(Srgb.EncodeByte(linear.X), Srgb.EncodeByte(linear.Y), Srgb.EncodeByte(linear.Z),
        (byte)Math.Clamp(MathF.Round(linear.W * 255), 0, 255));

    /// <summary>The color's hue in degrees from 0 to 360, and its saturation and value from 0 to 1.</summary>
    /// <remarks>A gray, black included, has no hue, and is given 0 where raylib gives black NaN.</remarks>
    public static Vector3 ColorToHSV(Color color)
    {
        var (r, g, b) = (color.R / 255f, color.G / 255f, color.B / 255f);
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var delta = max - min;
        if (delta < 0.00001f || max <= 0) return new Vector3(0, 0, max);

        var hue = r >= max ? (g - b) / delta
            : g >= max ? 2 + (b - r) / delta
            : 4 + (r - g) / delta;
        hue *= 60;
        if (hue < 0) hue += 360;
        return new Vector3(hue, delta / max, max);
    }

    /// <summary>The opaque color of a hue in degrees, and a saturation and value from 0 to 1.</summary>
    public static Color ColorFromHSV(float hue, float saturation, float value)
    {
        // Each channel is the value less a share of it that rises and falls with the hue, the
        // channel's phase around the circle set by the constant.
        byte Channel(float phase)
        {
            var k = (phase + hue / 60f) % 6;
            if (k < 0) k += 6;
            k = Math.Clamp(MathF.Min(k, 4 - k), 0, 1);
            return (byte)((value - value * saturation * k) * 255f);
        }
        return new Color(Channel(5), Channel(3), Channel(1));
    }

    /// <summary>
    /// The opaque color of the light a black body gives at <paramref name="kelvin"/>, from 1,000 to
    /// 15,000, the brightest of its channels at 255, so 2,700 is a lamp's warm white, 5,600 the
    /// sun's at midday and 9,000 a blue sky's.
    /// </summary>
    /// <remarks>
    /// The color is read off the Planckian locus by Krystek's fit in CIE 1960 coordinates, taken to
    /// sRGB's primaries in linear light and encoded, so a light given it and its strength in lux
    /// (<see cref="SetLightIlluminance"/>) takes the hue alone.
    /// </remarks>
    public static Color ColorFromTemperature(float kelvin)
    {
        var t = Math.Clamp(float.IsFinite(kelvin) ? kelvin : 6500, 1000, 15000);
        var u = (0.860117757f + 1.54118254e-4f * t + 1.28641212e-7f * t * t) / (1 + 8.42420235e-4f * t + 7.08145163e-7f * t * t);
        var v = (0.317398726f + 4.22806245e-5f * t + 4.20481691e-8f * t * t) / (1 - 2.89741816e-5f * t + 1.61456053e-7f * t * t);
        var (x, y) = (3 * u / (2 * u - 8 * v + 4), 2 * v / (2 * u - 8 * v + 4));
        var (cx, cz) = (x / y, (1 - x - y) / y);
        var linear = Vector3.Max(Vector3.Zero, new Vector3(
            3.2406f * cx - 1.5372f - 0.4986f * cz,
            -0.9689f * cx + 1.8758f + 0.0415f * cz,
            0.0557f * cx - 0.2040f + 1.0570f * cz));
        linear /= MathF.Max(linear.X, MathF.Max(linear.Y, linear.Z));
        return new Color(Srgb.EncodeByte(linear.X), Srgb.EncodeByte(linear.Y), Srgb.EncodeByte(linear.Z));
    }

    /// <summary>The color multiplied by <paramref name="tint"/>, channel by channel.</summary>
    public static Color ColorTint(Color color, Color tint) => new(
        (byte)(color.R * tint.R / 255), (byte)(color.G * tint.G / 255),
        (byte)(color.B * tint.B / 255), (byte)(color.A * tint.A / 255));

    /// <summary>
    /// The color darkened toward black for a <paramref name="factor"/> below 0 or lightened toward
    /// white above it, from -1 to 1, its alpha kept.
    /// </summary>
    public static Color ColorBrightness(Color color, float factor)
    {
        factor = Math.Clamp(factor, -1f, 1f);
        float Channel(byte c) => factor < 0 ? c * (1 + factor) : (255 - c) * factor + c;
        return new Color((byte)Channel(color.R), (byte)Channel(color.G), (byte)Channel(color.B), color.A);
    }

    /// <summary>
    /// The color with its contrast lowered toward gray for a <paramref name="contrast"/> below 0 or
    /// raised above it, from -1 to 1, its alpha kept.
    /// </summary>
    public static Color ColorContrast(Color color, float contrast)
    {
        contrast = Math.Clamp(contrast, -1f, 1f);
        var scale = (1 + contrast) * (1 + contrast);
        byte Channel(byte c) => (byte)Math.Clamp(((c / 255f - 0.5f) * scale + 0.5f) * 255, 0, 255);
        return new Color(Channel(color.R), Channel(color.G), Channel(color.B), color.A);
    }

    /// <summary>
    /// <paramref name="src"/> tinted by <paramref name="tint"/> and laid over <paramref name="dst"/>
    /// by its alpha, the color a pixel takes when one is drawn over the other.
    /// </summary>
    public static Color ColorAlphaBlend(Color dst, Color src, Color tint)
    {
        // raylib's integer arithmetic, which divides by 256 and so adds one to each factor first.
        src = new Color(
            (byte)(src.R * (tint.R + 1) >> 8), (byte)(src.G * (tint.G + 1) >> 8),
            (byte)(src.B * (tint.B + 1) >> 8), (byte)(src.A * (tint.A + 1) >> 8));
        if (src.A == 0) return dst;
        if (src.A == 255) return src;

        var alpha = (uint)src.A + 1;
        var outA = (alpha * 256 + dst.A * (256 - alpha)) >> 8;
        if (outA == 0) return new Color(255, 255, 255, 0);
        byte Channel(byte s, byte d) => (byte)(((s * alpha * 256 + d * (uint)dst.A * (256 - alpha)) / outA) >> 8);
        return new Color(Channel(src.R, dst.R), Channel(src.G, dst.G), Channel(src.B, dst.B), (byte)outA);
    }

    /// <summary>The color <paramref name="factor"/> of the way from <paramref name="color1"/> to <paramref name="color2"/>, from 0 to 1.</summary>
    public static Color ColorLerp(Color color1, Color color2, float factor)
    {
        factor = Math.Clamp(factor, 0f, 1f);
        byte Channel(byte a, byte b) => (byte)((1 - factor) * a + factor * b);
        return new Color(Channel(color1.R, color2.R), Channel(color1.G, color2.G), Channel(color1.B, color2.B), Channel(color1.A, color2.A));
    }

    /// <summary>Whether two colors are the same in all four channels.</summary>
    public static bool ColorIsEqual(Color col1, Color col2) => col1 == col2;
}
