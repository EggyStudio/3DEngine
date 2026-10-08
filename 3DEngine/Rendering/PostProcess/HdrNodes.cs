namespace Engine;

/// <summary>
/// Render graph node that draws the window's scene into the HDR target every frame it shows one,
/// and decodes it where a pass reads its light, before <see cref="BloomNode"/> spreads it and
/// <see cref="MainPassNode"/> composites it into the window.
/// </summary>
/// <remarks>
/// The scene is the window's models and its draw list up to the last batch drawn with depth, the
/// 3D shapes inside <c>BeginMode3D</c>. What is drawn after that, a game's interface, is left to
/// <see cref="ImmediateNode"/>, which draws it into the window over the composite, so it is never
/// bloomed or tonemapped and keeps its exact colors.
/// </remarks>
internal sealed class HdrSceneNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        renderWorld.Remove<BloomFrame>();
        var bloom = renderWorld.TryGet<BloomRenderer>();
        if (bloom is null) return;
        if (!BloomRenderer.ShowsScene(renderWorld) || renderWorld.TryGet<SwapchainTarget>() is not { } swapchain)
        {
            bloom.Release();
            return;
        }

        var split = Split(renderWorld.TryGet<DrawList>());
        if (!bloom.DrawScene(renderContext, renderWorld, swapchain.Extent, split)) return;
        renderWorld.Set(new BloomFrame(split));
        // The window's particles laid over by alpha sorted from its camera again where a render
        // texture's sort came after the window's, ahead of the pass they are drawn in.
        renderWorld.TryGet<ParticleRenderer>()?.SortFor(renderContext, renderWorld, 0);
        // The scene's light, kept for the reflections of the frame after where light bounces.
        if (BloomRenderer.ReadsLight(renderWorld) && bloom.Decode(renderContext, renderWorld) is { } light)
            renderWorld.TryGet<GlobalIlluminationRenderer>()?.KeepFrame(renderContext, renderWorld, light, swapchain.Extent);
    }

    // The batch after the window's last one drawn with depth, or the first when it has none.
    private static int Split(DrawList? drawList)
    {
        if (drawList is null) return 0;
        var batches = drawList.Batches;
        for (int i = batches.Count - 1; i >= 0; i--)
            if (batches[i].Target == 0 && batches[i].DepthTest) return i + 1;
        return 0;
    }
}

/// <summary>Render graph node that spreads the HDR target's brightest light down and up the bloom chain, and runs the effects that read the scene's light, when <see cref="HdrSceneNode"/> drew it this frame.</summary>
internal sealed class BloomNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<BloomFrame>() is null || renderWorld.TryGet<BloomRenderer>() is not { } renderer) return;
        var bloom = renderWorld.TryGet<BloomSettings>();
        if (bloom is { On: true }) renderer.DrawChain(renderContext, bloom.Threshold);
        var effects = renderWorld.TryGet<FrameEffects>();
        if (effects is { AutoExposure: true }) renderer.Adapt(renderContext, effects);
        // The depth of field and motion blur, run every frame the scene is drawn, so motion blur
        // knows the camera of the frame before once it is turned on.
        renderer.Lens(renderContext, renderWorld, effects ?? new FrameEffects(), renderWorld.TryGet<WindowView>());
        // With FXAA the composite is drawn ahead, into the 8-bit frame FXAA reads in the window's pass.
        if (effects is { Fxaa: true }) renderer.CompositeForFxaa(renderContext, bloom, effects);
    }
}
