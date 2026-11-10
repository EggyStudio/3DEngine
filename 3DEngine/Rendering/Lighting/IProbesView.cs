namespace Engine;

/// <summary>
/// The probes of global illumination drawn as cubes into the window's scene, where a command asks,
/// set in the render world by the illumination's part, so the HDR frame draws them without naming
/// the illumination.
/// </summary>
internal interface IProbesView
{
    /// <summary>Draws the probes shown into <paramref name="pass"/>, the window's scene, hidden by its meshes.</summary>
    void DrawProbes(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, RenderWorld renderWorld);
}
