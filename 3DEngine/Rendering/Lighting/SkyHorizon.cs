namespace Engine;

/// <summary>
/// The color the sky <see cref="E3D.DrawSkybox()"/> draws below the horizon, and the degrees above
/// it over which the map's image blends into it, a world resource which
/// <see cref="E3D.SetSkyHorizon"/> sets and a clear color takes away.
/// </summary>
/// <remarks>
/// A resource of its own rather than the map's, as <see cref="EnvironmentLight"/> is, so the horizon
/// a program set stays through a change of map. It is the drawn sky's alone, and the map lights the
/// scene from below as before.
/// </remarks>
internal sealed class SkyHorizon
{
    /// <summary>The color below the horizon, sRGB as the program gave it.</summary>
    public Color Color { get; set; }

    /// <summary>The degrees above the horizon over which the image blends into the color, 0 for a sharp edge.</summary>
    public float BlendDegrees { get; set; }
}
