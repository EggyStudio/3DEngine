namespace Engine;

/// <summary>
/// Holds the active swapchain render pass begun by <see cref="MainPassNode"/>.
/// Overlay nodes (webview, imgui) retrieve this from <see cref="RenderWorld"/>
/// to draw into the same render pass, eliminating per-overlay render pass begin/end overhead.
/// The pass is ended by <see cref="Renderer"/> after all graph nodes have executed.
/// </summary>
public sealed class ActiveSwapchainPass : IDisposable
{
    /// <summary>The open tracked render pass for the current frame.</summary>
    public TrackedRenderPass Pass { get; }

    /// <summary>The swapchain extent for viewport/scissor setup.</summary>
    public Extent2D Extent { get; }

    /// <summary>Creates a new active pass wrapper.</summary>
    public ActiveSwapchainPass(TrackedRenderPass pass, Extent2D extent)
    {
        Pass = pass;
        Extent = extent;
    }

    /// <summary>Ends the render pass.</summary>
    public void Dispose() => Pass.Dispose();
}
