using System.Numerics;
using RadianceCascades;
using static Engine.Illumination;

namespace Engine;

internal sealed partial class GlobalIlluminationRenderer
{
    // The window's surfaces lit by the module's gather where gi.surface asks, or by its split
    // cascades where gi.layout split asks, or none.
    private GpuSurfaceGather? _surface;

    // The split cascades' store where gi.layout split asks, kept from frame to frame, or none.
    private GpuSplitStore? _split;

    /// <summary>The split cascades' store's shape, the module's defaults.</summary>
    internal static readonly SplitStoreShape SplitShape = new();

    /// <summary>The window's surfaces' gathered light, or none where no gather runs.</summary>
    internal GpuSurfaceGather? Surface => _surface;

    /// <summary>The split cascades' store, or none where the probes are not split.</summary>
    internal GpuSplitStore? Split => _split;

    // Lights the window's surfaces by the module's gather after the world's probes are traced and
    // merged, or runs its split cascades over the window where the layout is split, the grid's
    // probes lighting what their rays meet, each pixel's point read from the window's depth
    // through its camera without temporal antialiasing's move, as the screen's probes read it,
    // where the window has a depth, and lets the gather go otherwise.
    private void GatherSurfaces(RenderContext renderContext, RenderWorld renderWorld, GraphicsDevice device, GpuSceneField field, IImageView environment,
        ISampler environmentSampler)
    {
        var stages = GlobalIlluminationStages.Of(device);
        var settings = renderWorld.TryGet<GlobalIlluminationSettings>();
        var split = settings is { Layout: ProbeLayout.Split } && stages.SplitsCascades;
        if (!split) RetireSplit();
        // A way of the split's keeps the mean of its rays, and a ray marched through a field that
        // does not yet hold the scene meets none of it, so the store is made once the field is
        // whole, the grid's probes lighting the window until then.
        var waits = split && _split is null && renderWorld.TryGet<SceneFieldRenderer>()?.Plan is not { Whole: true };
        if (_gi is not null && SurfaceGatherOn(renderWorld) && (split || stages.GathersSurfaces) && !waits && renderWorld.TryGet<WindowDepth>() is { } depth
            && renderWorld.TryGet<WindowView>() is { } window && renderWorld.TryGet<SwapchainTarget>() is { } swapchain
            && Matrix4x4.Invert(window.Unjittered, out var inverse))
        {
            var (width, height) = (swapchain.Extent.Width, swapchain.Extent.Height);
            var causes = split && settings!.SplitCauseRead;
            if (_surface is null || _surface.Width != width || _surface.Height != height || (_surface.Causes is not null) != causes)
            {
                if (_surface is not null) _retired.Add((_frame, _surface));
                _surface = GpuSurfaceGather.Create(device, width, height, causes);
            }
            var seen = window.Turned is { } turned ? new ViewEye(window.Eye, turned * window.Back) : (ViewEye?)null;
            var origin = renderWorld.TryGet<RenderOrigin>();
            var bytes = ViewBytes(window.Unjittered, inverse, window.Eye, seen, null, depth.Extent, swapchain.Extent, Vector2.Zero, null, origin, settings);
            if (split)
            {
                _split ??= GpuSplitStore.Create(device, SplitShape);
                _retired.Add((_frame, stages.RecordSplitGather(renderContext.CommandBuffer, _gi, _surface, _split, field, depth.View, depth.Sampler, depth.Normals, bytes,
                    environment, environmentSampler, _rays, GpuSplitStore.OriginAgainst(SplitShape, origin),
                    GpuSplitStore.ChangesOf(SplitChanges(renderWorld, origin), GpuSplitStore.OriginAgainst(SplitShape, origin)), settings!.SplitAgree)));
            }
            else
                _retired.Add((_frame, stages.RecordSurfaceGather(renderContext.CommandBuffer, _gi, _surface, field, depth.View, depth.Sampler, bytes)));
        }
        else
        {
            if (_surface is not null) _retired.Add((_frame, _surface));
            _surface = null;
            RetireSplit();
        }
    }

    // Lets the split cascades' store go once no frame in flight reads it.
    private void RetireSplit()
    {
        if (_split is not null) _retired.Add((_frame, _split));
        _split = null;
    }
}
