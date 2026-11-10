namespace Engine;

/// <summary>
/// A part of the renderer a plugin adds before the renderer starts (<see cref="Renderer.AddPart"/>),
/// which loads its shaders and sets its renderers in the render world as the renderer starts, adds
/// its nodes to the render graph after the renderer's own, ordered among them by edges, and is let
/// go with the renderer, so a program built without its plugin loads and draws none of it.
/// </summary>
internal interface IRendererPart : IDisposable
{
    /// <summary>Loads the part's shaders on <paramref name="device"/>, where the renderer draws through Vulkan, and sets its renderers in the render world.</summary>
    void Initialize(Renderer renderer, GraphicsDevice? device, AssetServer server);

    /// <summary>Adds the part's nodes to <paramref name="graph"/>, which holds every node of the renderer's own.</summary>
    void AddNodes(RenderGraph graph);
}
