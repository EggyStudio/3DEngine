namespace Engine;

/// <summary>
/// The camera the lights and the sky given in physical units are seen through, and those lights,
/// each with the quantity it was given in (<c>E3D.SetCameraExposure</c>,
/// <c>E3D.SetLightIlluminance</c>).
/// </summary>
/// <remarks>
/// A light's intensity is the light a white diffuse surface facing it returns, with no pi, so a sun
/// of E lux, which a white diffuse surface returns as E / pi candelas a square meter, takes E / pi,
/// and a lamp of I candelas I / pi at a unit's distance. The camera's exposure multiplies that
/// before the light reaches the frame, as a sensor that saturates at 1.2 times 2 to the EV100
/// candelas a square meter sees it, so a white surface in a noon sun reads about 1 rather than tens
/// of thousands, which a frame of half floats holds no more than 65,504 of.
/// </remarks>
internal sealed class PhysicalUnits
{
    /// <summary>
    /// The camera's exposure value at ISO 100, a sunny day's 15 unless a program set its camera,
    /// ISO 100 at 1/125 s and f/16 being 14.97.
    /// </summary>
    public float Ev100 { get; set; } = 15;

    /// <summary>What a luminance in candelas a square meter is multiplied by to give the frame's light.</summary>
    public float PreExposure => 1 / (1.2f * MathF.Pow(2, Ev100));

    /// <summary>The lights given in physical units, with the quantity each was given in.</summary>
    public Dictionary<LightHandle, (float Value, PhysicalQuantity Quantity)> Lights { get; } = [];

    /// <summary>The exposure value at ISO 100 of a camera at <paramref name="iso"/>, a shutter of <paramref name="seconds"/> and an f-number of <paramref name="aperture"/>.</summary>
    public static float Ev100Of(float iso, float seconds, float aperture) => MathF.Log2(aperture * aperture / seconds * 100 / iso);

    /// <summary>
    /// The intensity the engine's lights take for a light of <paramref name="kind"/> given
    /// <paramref name="value"/> in <paramref name="quantity"/>, its color of luminance
    /// <paramref name="luminance"/> carrying the rest, seen through this camera.
    /// </summary>
    public float IntensityOf(LightKind kind, float value, PhysicalQuantity quantity, float outerAngle, float luminance)
    {
        // Candelas, or lux for a light from all around or from a direction, which a white diffuse
        // surface returns over pi either way.
        var candela = quantity switch
        {
            PhysicalQuantity.LuminousFlux when kind == LightKind.Point => value / (4 * MathF.PI),
            PhysicalQuantity.LuminousFlux when kind == LightKind.Spot =>
                value / (2 * MathF.PI * MathF.Max(1e-4f, 1 - MathF.Cos(float.DegreesToRadians(Math.Clamp(outerAngle, 0, 180))))),
            _ => value,
        };
        return candela / MathF.PI * PreExposure / MathF.Max(luminance, 1e-4f);
    }
}

/// <summary>What a light's physical value measures.</summary>
internal enum PhysicalQuantity
{
    /// <summary>Lux on a surface facing it, for the sun or the light from all around.</summary>
    Illuminance,

    /// <summary>Candelas along its axis, for a point or spot light.</summary>
    LuminousIntensity,

    /// <summary>Lumens it sends out in all, over a sphere for a point light and its cone for a spot.</summary>
    LuminousFlux,
}
