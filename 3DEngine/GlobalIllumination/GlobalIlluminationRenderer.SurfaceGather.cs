using System.Numerics;
using static Engine.Illumination;

namespace Engine;

internal sealed partial class GlobalIlluminationRenderer
{
    // The window's surfaces lit by the module's gather where gi.surface asks, or none.
    private GpuSurfaceGather? _surface;

    // Lights the window's surfaces by the module's gather after the world's probes are traced and
    // merged, each pixel's point read from the window's depth through its camera without temporal
    // antialiasing's move, as the screen's probes read it, where gi.surface asks and the window has
    // a depth, and lets the gather go otherwise.
    private void GatherSurfaces(RenderContext renderContext, RenderWorld renderWorld, GraphicsDevice device, GpuSceneField field)
    {
        var stages = GlobalIlluminationStages.Of(device);
        if (_gi is not null && SurfaceGatherOn(renderWorld) && stages.GathersSurfaces && renderWorld.TryGet<WindowDepth>() is { } depth
            && renderWorld.TryGet<WindowView>() is { } window && renderWorld.TryGet<SwapchainTarget>() is { } swapchain
            && Matrix4x4.Invert(window.Unjittered, out var inverse))
        {
            var (width, height) = (swapchain.Extent.Width, swapchain.Extent.Height);
            if (_surface is null || _surface.Width != width || _surface.Height != height)
            {
                if (_surface is not null) _retired.Add((_frame, _surface));
                _surface = GpuSurfaceGather.Create(device, width, height);
            }
            var seen = window.Turned is { } turned ? new ViewEye(window.Eye, turned * window.Back) : (ViewEye?)null;
            var bytes = ViewBytes(window.Unjittered, inverse, window.Eye, seen, null, depth.Extent, swapchain.Extent, Vector2.Zero, null,
                renderWorld.TryGet<RenderOrigin>(), renderWorld.TryGet<GlobalIlluminationSettings>());
            _retired.Add((_frame, stages.RecordSurfaceGather(renderContext.CommandBuffer, _gi, _surface, field, depth.View, depth.Sampler, bytes)));
        }
        else if (_surface is not null)
        {
            _retired.Add((_frame, _surface));
            _surface = null;
        }
    }
}
