namespace Engine;

internal sealed partial class BloomRenderer
{
    /// <summary>
    /// Spreads the HDR target's light down the levels and back up into the first one, each level
    /// added onto the one above, or with a scatter laid over it by that share, over as many levels
    /// as halve the window to four pixels along its longer side, so its haze can cross the window.
    /// </summary>
    /// <remarks>
    /// A scattered bloom keeps the light past its threshold, easing in over half the threshold so
    /// what blooms has no edge. The threshold is measured on the light after the exposure, and so
    /// after the exposure that follows the scene too where that is on. The chain reads the light
    /// before the exposure, so the threshold is divided by the exposure set and the key here, and
    /// multiplied on the GPU by the scene's adapted luminance, which <see cref="Adapt"/> wrote
    /// earlier in the frame.
    /// </remarks>
    public void DrawChain(RenderContext renderContext, BloomSettings bloom, FrameEffects? effects)
    {
        if (_sized is not { Linear: not null, First: { } first } sized || _down is null || _up is null || _scatterUp is null) return;
        var levels = sized.Levels;
        var scattered = bloom.Scatter > 0;
        var count = scattered ? levels.Length : sized.EngineLevels;
        var (threshold, knee, adapted, white) = (bloom.Threshold, 0.1f, 0f, 0f);
        if (scattered)
        {
            effects ??= new FrameEffects();
            var follows = effects.AutoExposure && _adaptedFrame == _frame;
            // White after the exposure as the light before it, which the GPU multiplies by the
            // scene's adapted luminance where the exposure follows it, the threshold measured on it
            // and the light held under 256 of it (bloom.slang).
            white = 1 / Math.Max(effects.Exposure * (follows ? effects.AutoExposureKey : 1), 1e-6f);
            threshold *= white;
            (knee, adapted) = (0.5f, follows ? 1 : 0);
        }
        for (int i = 0; i < count; i++)
        {
            var source = i == 0 ? sized.Scene.Extent : levels[i - 1].Extent;
            Pass(renderContext, levels[i], LoadOp.Clear, _down, i == 0 ? first[_adaptedLast] : sized.Down[i]!, new Push
            {
                TexelX = 1f / source.Width, TexelY = 1f / source.Height, Threshold = threshold, Mode = i == 0 ? 0 : 1,
                Knee = knee, Adapted = adapted, White = white,
            });
        }
        for (int i = count - 1; i > 0; i--)
        {
            var source = levels[i].Extent;
            Pass(renderContext, levels[i - 1], LoadOp.Load, scattered ? _scatterUp : _up, sized.Up[i], new Push
            {
                TexelX = 1f / source.Width, TexelY = 1f / source.Height, Mode = 2, Scatter = scattered ? bloom.Scatter : 0,
            });
        }
    }

    // A set of the bloom chain's, reading a level, or the decoded scene for the first, and an
    // adapted texel.
    private IDescriptorSet BloomSet(GraphicsDevice device, IImageView view, RenderTarget adapted)
    {
        var set = device.CreateDescriptorSet(_oneTexture!);
        device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(view, _sampler!, 0));
        device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(adapted.ColorView, _sampler!, 1));
        return set;
    }
}
