namespace Engine;

/// <summary>Where the sun's contact shadows through the scene's distance field darken its light, which <c>SetContactShadows</c> chooses.</summary>
public enum ContactShadows
{
    /// <summary>Everywhere the field reaches, beside the shadow map's own.</summary>
    On,

    /// <summary>
    /// Past the shadow map's reach alone, so a shadow the map holds keeps its hard edge and the
    /// ground beyond the map is still shadowed, the default.
    /// </summary>
    /// <remarks>
    /// Within the map's reach the map holds the sun's light, and a contact shadow there darkened
    /// sunlit surfaces it did not reach, its ray passing near the field's surfaces, which stand up
    /// to a cell outside the meshes, as Sponza's gallery at noon read its sunlit parapet and column
    /// base far darker than a path-traced reference.
    /// </remarks>
    PastShadowMap,

    /// <summary>Nowhere, the shadow map's shadows alone.</summary>
    Off,
}
