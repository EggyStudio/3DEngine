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
    /// sunlit surfaces it did not reach, its ray passing near the field's surfaces, which stand up to
    /// a cell outside the meshes. Sponza's gallery at noon read its sunlit parapet and column base at
    /// 0.38 to 0.43 of a path-traced reference's light, and at 0.83 to 0.93 with the map alone.
    /// </remarks>
    PastShadowMap,

    /// <summary>Nowhere, the shadow map's shadows alone.</summary>
    Off,
}
