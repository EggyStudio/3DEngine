using System.Numerics;

namespace Engine;

public static partial class Engine3D
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
