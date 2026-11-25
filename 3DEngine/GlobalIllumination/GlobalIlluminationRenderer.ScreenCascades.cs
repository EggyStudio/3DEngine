namespace Engine;

internal sealed partial class GlobalIlluminationRenderer
{
    // The levels of the screen's cascades past the first where gi.screen_cascades asks, or none.
    private GpuScreenCascades? _cascades;

    // What the screen's cascades are traced with this frame, where gi.screen_cascades asks, the
    // screen's probes trace their own rays and the device's rays have the window's meshes, the
    // levels made again where the window or the tile changes, or null, the levels let go.
    private ScreenCascadeTrace? ScreenCascades(RenderWorld renderWorld, GraphicsDevice device, bool probesTrace, uint width, uint height, int tile,
        IImageView environment, ISampler environmentSampler)
    {
        if (probesTrace && _rays is not null && renderWorld.TryGet<GlobalIlluminationSettings>() is { ScreenCascades: true }
            && GlobalIlluminationStages.Of(device).TracesScreenCascades)
        {
            if (_cascades is null || _cascades.Width != width || _cascades.Height != height || _cascades.Tile != tile)
            {
                if (_cascades is not null) _retired.Add((_frame, _cascades));
                _cascades = GpuScreenCascades.Create(device, width, height, tile);
            }
            return new ScreenCascadeTrace(_cascades, _rays, environment, environmentSampler);
        }
        RetireScreenCascades();
        return null;
    }

    // Lets the screen's cascades' levels go, once no frame in flight reads them.
    private void RetireScreenCascades()
    {
        if (_cascades is not null) _retired.Add((_frame, _cascades));
        _cascades = null;
    }
}
