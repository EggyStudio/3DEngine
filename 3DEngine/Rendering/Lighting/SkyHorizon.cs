namespace Engine;

/// <summary>
/// The color the sky <c>E3D.DrawSkybox()</c> draws below the horizon, and the degrees above
/// it over which the map's image blends into it, a world resource which
/// <c>E3D.SetSkyHorizon</c> sets and a clear color takes away.
/// </summary>
/// <remarks>
/// A resource of its own rather than the map's, so the horizon a program set stays through a change
/// of map. It is the drawn sky's alone, and the map's image lights the scene from below whatever
/// the horizon.
/// </remarks>
internal sealed class SkyHorizon
{
    /// <summary>The color below the horizon, sRGB as the program gave it.</summary>
    public Color Color { get; set; }

    /// <summary>The degrees above the horizon over which the image blends into the color, 0 for a sharp edge.</summary>
    public float BlendDegrees { get; set; }
}
