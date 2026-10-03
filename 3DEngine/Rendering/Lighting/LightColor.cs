using System.Numerics;

namespace Engine;

/// <summary>
/// Helpers for translating <see cref="Light"/> color / intensity / temperature parameters
/// into a single linear-RGB radiant emission triple consumed by the renderer. Centralised
/// so the offline (extract) and online (debug HUD / editor preview) paths agree.
/// </summary>
public static class LightColor
{
    /// <summary>
    /// Computes the effective premultiplied emission for a light:
    /// <c>color * intensity * 2^exposure</c>, optionally tinted by the blackbody color of
    /// <see cref="Light.ColorTemperature"/> when <see cref="Light.EnableColorTemperature"/>
    /// is <c>true</c>. Matches the convention documented on
    /// <see cref="SceneLightPayload"/>.
    /// </summary>
    public static Vector3 ComputeEmittedColor(in Light light)
    {
        var emission = light.Color * light.Intensity * MathF.Pow(2f, light.Exposure);

        if (light.EnableColorTemperature == true && light.ColorTemperature is { } kelvin && kelvin > 0f)
            emission *= KelvinToRgb(kelvin);

        return emission;
    }

    /// <summary>
    /// Approximate Planckian-locus blackbody color in linear sRGB, matching the algorithm
    /// used by Pixar's <c>UsdLuxBlackbodyTemperatureAsRgb</c> closely enough for visual
    /// previews. Valid for ~1000K..40000K; out-of-range inputs are clamped before fitting.
    /// Curves are Tanner Helland's polynomial fit, then converted from sRGB^2.2 to linear.
    /// </summary>
    /// <param name="kelvin">Temperature in Kelvin.</param>
    /// <returns>Linear-RGB tint in [0,1].</returns>
    public static Vector3 KelvinToRgb(float kelvin)
    {
        // Tanner Helland's piecewise polynomial fit. Output is gamma-encoded sRGB; we
        // de-gamma at the end so callers can multiply linear color values directly.
        var t = Math.Clamp(kelvin, 1000f, 40000f) / 100f;

        float r, g, b;

        // Red
        if (t <= 66f)
            r = 255f;
        else
            r = 329.698727446f * MathF.Pow(t - 60f, -0.1332047592f);

        // Green
        if (t <= 66f)
            g = 99.4708025861f * MathF.Log(t) - 161.1195681661f;
        else
            g = 288.1221695283f * MathF.Pow(t - 60f, -0.0755148492f);

        // Blue
        if (t >= 66f)
            b = 255f;
        else if (t <= 19f)
            b = 0f;
        else
            b = 138.5177312231f * MathF.Log(t - 10f) - 305.0447927307f;

        var srgb = new Vector3(
            Math.Clamp(r, 0f, 255f) / 255f,
            Math.Clamp(g, 0f, 255f) / 255f,
            Math.Clamp(b, 0f, 255f) / 255f);

        // sRGB encoded -> linear (gamma 2.2 approximation; close enough for tinting).
        return new Vector3(
            MathF.Pow(srgb.X, 2.2f),
            MathF.Pow(srgb.Y, 2.2f),
            MathF.Pow(srgb.Z, 2.2f));
    }
}