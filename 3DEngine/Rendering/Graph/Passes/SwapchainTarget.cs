namespace Engine;

/// <summary>
/// Per-frame resource holding the swapchain render pass, framebuffer, and extent.
/// Populated by <see cref="RendererContext"/> and stored in <see cref="RenderWorld"/> each frame.
/// Nodes use this to begin their own render passes targeting the swapchain.
/// </summary>
/// <seealso cref="RendererContext"/>
/// <seealso cref="TrackedRenderPass"/>
public sealed class SwapchainTarget
{
    /// <summary>The swapchain-compatible render pass (loadOp=Clear). Used by the first node to clear the framebuffer.</summary>
    public IRenderPass RenderPass { get; }

    /// <summary>The swapchain-compatible render pass (loadOp=Load). Used by subsequent nodes to preserve existing content.</summary>
    public IRenderPass LoadRenderPass { get; }

    /// <summary>The framebuffer for the current swapchain image.</summary>
    public IFramebuffer Framebuffer { get; }

    /// <summary>The swapchain extent in pixels.</summary>
    public Extent2D Extent { get; }

    /// <summary>Creates a new swapchain target for the current frame.</summary>
    /// <param name="renderPass">The swapchain render pass (loadOp=Clear).</param>
    /// <param name="loadRenderPass">The swapchain render pass (loadOp=Load).</param>
    /// <param name="framebuffer">The framebuffer for the acquired swapchain image.</param>
    /// <param name="extent">The swapchain extent.</param>
    public SwapchainTarget(IRenderPass renderPass, IRenderPass loadRenderPass, IFramebuffer framebuffer, Extent2D extent)
    {
        RenderPass = renderPass;
        LoadRenderPass = loadRenderPass;
        Framebuffer = framebuffer;
        Extent = extent;
    }
}
