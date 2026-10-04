namespace Engine;

/// <summary>
/// The first node drawing to the window, which begins the swapchain render pass, clearing it to the
/// <see cref="ClearColor"/>, and publishes it as <see cref="ActiveSwapchainPass"/>.
/// </summary>
/// <remarks>
/// The pass is left open so the nodes after it (models, immediate shapes, ImGui) draw into it
/// without a begin and end of their own, and <see cref="Renderer"/> ends it once every node has
/// run. Mesh entities are drawn by the models node, through <see cref="MeshEntityDraws"/>.
/// </remarks>
/// <seealso cref="ActiveSwapchainPass"/>
public sealed class MainPassNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<SwapchainTarget>() is not { } swapchainTarget) return;

        var clearColor = renderWorld.TryGet<ClearColor>() is { } cc ? cc : ClearColor.Black;
        var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            swapchainTarget.RenderPass,
            swapchainTarget.Framebuffer,
            swapchainTarget.Extent,
            LoadOp.Clear,
            StoreOp.Store,
            clearColor));

        var extent = swapchainTarget.Extent;
        pass.SetViewport(0, 0, extent.Width, extent.Height, 0, 1);
        pass.SetScissor(0, 0, extent.Width, extent.Height);

        renderWorld.Set(new ActiveSwapchainPass(pass, extent));
    }
}
