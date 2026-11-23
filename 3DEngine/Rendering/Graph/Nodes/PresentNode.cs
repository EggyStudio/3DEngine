namespace Engine;

/// <summary>
/// The node between the picture and the interface. Where the frames are drawn at the picture's size
/// of its own, it ends the pass <see cref="MainPassNode"/> opened on the frame image, begins the
/// window's pass on the window's own image at its size, cleared black, draws the picture onto it
/// where the window places it, and publishes that pass as <see cref="ActiveSwapchainPass"/>, so the
/// interface's nodes after it (the interface's batches, ImGui) draw at the window's size. Where the
/// frames are drawn at the window's size it does nothing, and the interface draws on in the one
/// pass.
/// </summary>
internal sealed class PresentNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<SwapchainTarget>() is not { Shown: { } shown } target
            || renderWorld.TryGet<ActiveSwapchainPass>() is not { } picture) return;
        picture.Dispose();
        var extent = target.ShownExtent;
        var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, shown, extent, LoadOp.Clear, StoreOp.Store, ClearColor.Black));
        renderContext.Device.DrawPicture(renderContext.CommandBuffer, target.ImageIndex);
        pass.SetViewport(0, 0, extent.Width, extent.Height, 0, 1);
        pass.SetScissor(0, 0, extent.Width, extent.Height);
        renderWorld.Set(new ActiveSwapchainPass(pass, extent));
    }
}
