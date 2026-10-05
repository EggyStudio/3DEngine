using System.Numerics;

namespace Engine;

/// <summary>Colors for lights.</summary>
internal static class LightColor
{
    /// <summary>
    /// The color of a black body at a temperature in Kelvin, as linear RGB from 0 to 1: about 1900 K
    /// for a candle, 2700 K for a household bulb, 6500 K for daylight.
    /// </summary>
    /// <remarks>Tanner Helland's fit, valid from about 1000 K to 40000 K, with the input clamped to that.</remarks>
    public static Vector3 FromKelvin(float kelvin)
    {
        var t = Math.Clamp(kelvin, 1000f, 40000f) / 100f;
        float r, g, b;
        if (t <= 66f)
        {
            r = 255f;
            g = 99.4708025861f * MathF.Log(t) - 161.1195681661f;
            b = t <= 19f ? 0f : 138.5177312231f * MathF.Log(t - 10f) - 305.0447927307f;
        }
        else
        {
            r = 329.698727446f * MathF.Pow(t - 60f, -0.1332047592f);
            g = 288.1221695283f * MathF.Pow(t - 60f, -0.0755148492f);
            b = 255f;
        }
        // The fit gives gamma-encoded sRGB, and lights are linear.
        static float Linear(float c) => MathF.Pow(Math.Clamp(c, 0f, 255f) / 255f, 2.2f);
        return new Vector3(Linear(r), Linear(g), Linear(b));
    }
}
