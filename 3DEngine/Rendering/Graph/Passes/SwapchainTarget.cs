namespace Engine;

/// <summary>
/// Per-frame resource holding the swapchain render pass, framebuffer, and extent.
/// Populated by <see cref="RendererContext"/> and stored in <see cref="RenderWorld"/> each frame.
/// Nodes use this to begin their own render passes targeting the swapchain.
/// </summary>
/// <seealso cref="RendererContext"/>
/// <seealso cref="TrackedRenderPass"/>
internal sealed class SwapchainTarget
{
    /// <summary>The window's pass, which the pipelines drawing into the swapchain are made for.</summary>
    public IRenderPass RenderPass { get; }

    /// <summary>The framebuffer for the current swapchain image.</summary>
    public IFramebuffer Framebuffer { get; }

    /// <summary>The swapchain extent in pixels.</summary>
    public Extent2D Extent { get; }

    /// <summary>Creates a new swapchain target for the current frame.</summary>
    /// <param name="renderPass">The window's pass.</param>
    /// <param name="framebuffer">The framebuffer for the acquired swapchain image.</param>
    /// <param name="extent">The swapchain extent.</param>
    public SwapchainTarget(IRenderPass renderPass, IFramebuffer framebuffer, Extent2D extent)
    {
        RenderPass = renderPass;
        Framebuffer = framebuffer;
        Extent = extent;
    }
}
