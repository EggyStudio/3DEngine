namespace Engine;

/// <summary>
/// Render graph node that draws every render target used this frame, before the window's pass, so
/// the window can sample what was drawn into them.
/// </summary>
/// <remarks>
/// A target is drawn only in a frame that sends something to it. It is cleared first, to the color
/// <c>ClearBackground</c> set inside its <c>BeginTextureMode</c> or to transparent black, then its
/// models and its immediate shapes are drawn, in that order, as the window's are. A target that
/// draws models through a camera of its own has the shadow map drawn for that camera before it
/// (<see cref="TargetShadows"/>), and the window's is drawn after every target.
/// </remarks>
public sealed class TargetsNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        var drawList = renderWorld.TryGet<DrawList>();
        var textures = renderWorld.TryGet<GpuTextures>();
        if (drawList is null || textures is null || drawList.TargetClears.Count == 0) return;

        var immediate = renderWorld.TryGet<ImmediateRenderer>();
        var models = renderWorld.TryGet<ModelRenderer>();

        foreach (var (id, clear) in drawList.TargetClears)
        {
            if (textures.TargetFor(id) is not { } target) continue;
            if (models is not null && renderWorld.TryGet<TargetShadows>() is { } shadows
                && shadows.ByTarget.TryGetValue(id, out var own) && own.Shadow is { } shadow)
                models.DrawShadow(renderContext, renderWorld, shadow, id);

            var c = clear.ToVector4();
            var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
                target.RenderPass, target.Framebuffer, target.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(c.X, c.Y, c.Z, c.W)));
            pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
            pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);

            models?.Draw(pass, target.RenderPass, renderContext, renderWorld, id);
            immediate?.Draw(pass, target.RenderPass, renderContext, renderWorld, id);

            pass.EndRenderPass();
        }
    }
}
