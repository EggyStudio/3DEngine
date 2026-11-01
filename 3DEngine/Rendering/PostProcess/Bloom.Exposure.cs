using System.Runtime.InteropServices;

namespace Engine;

internal sealed partial class BloomRenderer
{
    /// <summary>
    /// Measures the HDR frame and moves the exposure that follows it toward what it measured, at
    /// the pace and within the bounds <paramref name="effects"/> sets.
    /// </summary>
    public void Adapt(RenderContext renderContext, FrameEffects effects)
    {
        if (_sized is not { Measure: { } measured } sized || !_shownLinear || _measure is null || _measured is null || _adapted is null
            || _adaptSets is null) return;
        // All the way on the first frame and after frames without it, so a scene does not fade in
        // from the last one seen, and otherwise by the share the time since the last frame gives.
        var seconds = (float)_sinceAdapted.Elapsed.TotalSeconds;
        _sinceAdapted.Restart();
        var following = _adaptedFrame == _frame - 1;
        var share = following ? 1 - MathF.Exp(-Math.Min(seconds, 0.25f) * effects.AutoExposureSpeed) : 1;
        var darkening = following ? 1 - MathF.Exp(-Math.Min(seconds, 0.25f) * effects.AutoExposureDarkeningSpeed) : 1;
        // The luminance each bound of the exposure brings to the key, as log2.
        var low = MathF.Log2(effects.AutoExposureKey / Math.Max(effects.AutoExposureMax, 1e-3f));
        var high = MathF.Log2(effects.AutoExposureKey / Math.Max(effects.AutoExposureMin, 1e-3f));
        var push = new ExposurePush
        {
            Mode = 0, Share = share, Low = Math.Min(low, high), High = high,
            FromShare = effects.AutoExposureLow, ToShare = effects.AutoExposureHigh, DarkeningShare = darkening,
        };
        // The adapting pass weighs the picture as the metering says, 1 the whole of it and 2 its middle.
        var adapting = effects.AutoExposureMetering == ExposureMetering.CenterWeighted ? 2 : 1;
        Pass(renderContext, _measured, LoadOp.Clear, _measure, measured, push);
        var next = 1 - _adaptedLast;
        Pass(renderContext, _adapted[next], LoadOp.Clear, _measure, _adaptSets[next], push with { Mode = adapting });
        _adaptedLast = next;
        _adaptedFrame = _frame;
    }

    // The measured frame and the two adapted texels, made once whatever the window's size, and
    // cleared, so the composite can bind them in the layout a texture is sampled in before the
    // exposure first follows the scene.
    private RenderTarget[] Adapted(GraphicsDevice device, RenderContext renderContext)
    {
        if (_adapted is not null) return _adapted;
        _exposureVertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _exposureVertexSpv));
        _exposureFragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _exposureFragmentSpv));
        _measured = device.CreateRenderTarget(MeasuredSize, MeasuredSize, ImageFormat.R16G16B16A16_Float, depth: false, multisampled: false);
        _adapted = [.. Enumerable.Range(0, 2).Select(_ => device.CreateRenderTarget(1, 1, ImageFormat.R16G16B16A16_Float, depth: false, multisampled: false))];
        foreach (var target in (RenderTarget[])[_measured, .. _adapted])
        {
            using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
                target.RenderPass, target.Framebuffer, target.Extent, LoadOp.Clear, StoreOp.Store, ClearColor.Black));
        }
        _adaptSets = [.. Enumerable.Range(0, 2).Select(i =>
        {
            var set = device.CreateDescriptorSet(_exposureLayout!);
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(_measured.ColorView, _sampler!, 0));
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(_adapted[1 - i].ColorView, _sampler!, 1));
            return set;
        })];
        _measure = Pipeline(device, _measured.RenderPass, _exposureVertex, _exposureFragment, _exposureLayout!, blend: null, Marshal.SizeOf<ExposurePush>());
        return _adapted;
    }
}
