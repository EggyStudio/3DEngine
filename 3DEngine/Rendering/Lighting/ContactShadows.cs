namespace Engine;

/// <summary>Where the sun's contact shadows through the scene's distance field darken its light, which <c>SetContactShadows</c> chooses.</summary>
public enum ContactShadows
{
    /// <summary>Everywhere the field reaches, beside the shadow map's own, the default.</summary>
    On,

    /// <summary>
    /// Past the shadow map's reach alone, so a shadow the map holds keeps its hard edge and the
    /// ground beyond the map is still shadowed.
    /// </summary>
    PastShadowMap,

    /// <summary>Nowhere, the shadow map's shadows alone.</summary>
    Off,
}
