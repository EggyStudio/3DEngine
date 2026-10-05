using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>How much light past a threshold spreads into the pixels around it, a world resource the renderer reads each frame.</summary>
/// <seealso cref="Engine3D.SetBloom"/>
public sealed class BloomSettings
{
    /// <summary>How much of the spread light is added to the frame, 0 for none, which draws the frame with no HDR target at all.</summary>
    public float Intensity { get; set; }

    /// <summary>The brightness in linear light past which a pixel blooms, 1 unless set, so only what is brighter than white does.</summary>
    public float Threshold { get; set; } = 1;

    /// <summary>Whether the frame is drawn through the HDR target this frame.</summary>
    public bool On => Intensity > 0;
}

/// <summary>
/// The frame being drawn through the HDR target, which <see cref="HdrSceneNode"/> sets for the
/// nodes after it and removes when bloom is off.
/// </summary>
/// <param name="Split">
/// The index in the <see cref="DrawList"/> of the first batch drawn into the window after the
/// composite rather than into the HDR target, the one after the window's last batch with depth.
/// </param>
public sealed record BloomFrame(int Split);

/// <summary>
/// The HDR frame and its bloom: a half-float target the scene is drawn into, a chain of half-float
/// levels down to a thirty-second of the window that spread its brightest light, and the pass that
/// adds them and tonemaps the result into the window.
/// </summary>
/// <remarks>
/// <para>
/// The target and the levels are made at the window's size the first frame bloom is on and again
/// when the window's size changes, and those they replace are destroyed
/// <see cref="GpuTextures.RetireFrames"/> frames later, once no frame in flight reads them. They
/// are let go the same way the first frame bloom is off.
/// </para>
/// <para>
/// The first level keeps the light past <see cref="BloomSettings.Threshold"/>, and each level is
/// added back onto the one above it on the way up, so the first ends up holding the light spread
/// over every size, the levels' sum. The composite divides it by their number, so an intensity of 1
/// adds as much light as was past the threshold, spread.
/// </para>
/// </remarks>
public sealed class BloomRenderer : IDisposable
{
    private const int MaxLevels = 5;

    [StructLayout(LayoutKind.Sequential)]
    private struct Push
    {
        public float TexelX, TexelY, Threshold, Mode;
    }

    // As dof.slang and motion_blur.slang read them.
    [StructLayout(LayoutKind.Sequential)]
    private struct DofPush
    {
        public Matrix4x4 InverseViewProjection;
        public Vector4 EyeAndFocus, Lens;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlurPush
    {
        public Matrix4x4 Reprojection;
        public Vector4 Amount;
    }

    // As exposure.slang reads it.
    [StructLayout(LayoutKind.Sequential)]
    private struct ExposurePush
    {
        public float Mode, Share, Low, High;
    }

    // The width and height of the target the frame's luminance is measured into, as exposure.slang has it.
    private const int MeasuredSize = 64;

    // As composite.slang reads it.
    [StructLayout(LayoutKind.Sequential)]
    private struct CompositePush
    {
        public Vector4 Bloom, Grade, Tint, Exposure;
    }

    // The luminance a scene's mean is brought to by the exposure that follows it, about what the
    // engine's lit scenes show at an exposure of 1.
    internal const float AutoExposureKey = 0.18f;

    private readonly ReadOnlyMemory<byte> _bloomVertexSpv, _bloomFragmentSpv, _compositeVertexSpv, _compositeFragmentSpv,
        _fxaaVertexSpv, _fxaaFragmentSpv, _exposureVertexSpv, _exposureFragmentSpv;
    private IShader? _bloomVertex, _bloomFragment, _compositeVertex, _compositeFragment, _fxaaVertex, _fxaaFragment,
        _exposureVertex, _exposureFragment;
    // The sets each pass reads its images through, as its shader declares them.
    private readonly DescriptorSetLayoutBinding[] _bloomBindings, _compositeBindings, _fxaaBindings, _exposureBindings;
    private IDescriptorSetLayout? _oneTexture, _twoTextures, _fxaaLayout, _exposureLayout;
    private ISampler? _sampler, _depthSampler;
    private IPipeline? _down, _up, _measure, _dof, _blur;
    // The depth of field's and the motion blur's passes, which read the scene and its depth.
    private readonly ReadOnlyMemory<byte> _dofVertexSpv, _dofFragmentSpv, _blurVertexSpv, _blurFragmentSpv;
    private readonly DescriptorSetLayoutBinding[] _lensBindings;
    private IShader? _dofVertex, _dofFragment, _blurVertex, _blurFragment;
    private IDescriptorSetLayout? _lensLayout;
    // The image the composite reads this frame, the scene or the last lens pass's, and the camera
    // of the frame before with the frame it was seen in, which motion blur measures movement from.
    private IImageView? _shown;
    private Matrix4x4? _lastViewProjection;
    private long _lastViewFrame = -2;
    // The exposure that follows the scene. The measured log luminance of the frame, the two texels
    // the adapted value goes back and forth between, one written each frame from the other, and
    // which was written last.
    private RenderTarget? _measured;
    private RenderTarget[]? _adapted;
    private int _adaptedLast;
    private long _adaptedFrame = -2;
    private readonly System.Diagnostics.Stopwatch _sinceAdapted = new();
    // The set each adapted texel is written through, reading the measured frame and the other texel.
    private IDescriptorSet[]? _adaptSets;
    // The composite's and FXAA's pipelines by the pass they draw in, the window's or the 8-bit
    // target the composite draws into ahead of FXAA.
    private readonly Dictionary<IRenderPass, IPipeline> _composites = [], _fxaas = [];
    private Sized? _sized;
    private readonly List<(long Frame, IDisposable Disposable)> _retired = [];
    private long _frame;

    // What is made for one size of the window: the HDR target, the levels, and the sets each pass
    // reads its source through.
    private sealed class Sized(Extent2D extent, RenderTarget scene, RenderTarget[] levels, IDescriptorSet[] down, IDescriptorSet[] up,
        IDescriptorSet[] composite, IDescriptorSet measure) : IDisposable
    {
        // The composite's sets by the image they read, the scene or a lens pass's, the two lens
        // passes' targets once one is on, and their sets by the image they read with the depth.
        public Dictionary<IImageView, IDescriptorSet[]> CompositeFrom { get; } = new() { [scene.ColorView] = composite };
        public RenderTarget[]? Lens { get; set; }
        public Dictionary<(IImageView Source, bool Exact), IDescriptorSet> LensFrom { get; } = [];

        // The 8-bit frame the composite draws into for FXAA to read, made the first frame FXAA is on.
        public RenderTarget? Shown { get; set; }
        public IDescriptorSet? ShownSet { get; set; }

        public Extent2D Extent { get; } = extent;
        public RenderTarget Scene { get; } = scene;
        public RenderTarget[] Levels { get; } = levels;
        public IDescriptorSet[] Down { get; } = down;
        public IDescriptorSet[] Up { get; } = up;
        // The set the frame is measured through.
        public IDescriptorSet Measure { get; } = measure;

        public void Dispose()
        {
            foreach (var set in Down) set.Dispose();
            foreach (var set in Up) set.Dispose();
            foreach (var sets in CompositeFrom.Values)
                foreach (var set in sets) set.Dispose();
            foreach (var set in LensFrom.Values) set.Dispose();
            if (Lens is not null)
                foreach (var target in Lens) target.Dispose();
            Measure.Dispose();
            ShownSet?.Dispose();
            Shown?.Dispose();
            foreach (var level in Levels) level.Dispose();
            Scene.Dispose();
        }
    }

    /// <summary>
    /// Creates the renderer from <c>bloom.slang</c>, <c>composite.slang</c>, <c>fxaa.slang</c>,
    /// <c>exposure.slang</c>, <c>dof.slang</c> and <c>motion_blur.slang</c>, compiled.
    /// </summary>
    public BloomRenderer(ShaderProgram bloom, ShaderProgram composite, ShaderProgram fxaa, ShaderProgram exposure,
        ShaderProgram dof, ShaderProgram motionBlur)
    {
        (_dofVertexSpv, _dofFragmentSpv, _lensBindings) = (dof.Vertex, dof.Fragment, dof.LayoutOf(0));
        (_blurVertexSpv, _blurFragmentSpv) = (motionBlur.Vertex, motionBlur.Fragment);
        (_exposureVertexSpv, _exposureFragmentSpv, _exposureBindings) = (exposure.Vertex, exposure.Fragment, exposure.LayoutOf(0));
        (_bloomVertexSpv, _bloomFragmentSpv, _bloomBindings) = (bloom.Vertex, bloom.Fragment, bloom.LayoutOf(0));
        (_compositeVertexSpv, _compositeFragmentSpv, _compositeBindings) = (composite.Vertex, composite.Fragment, composite.LayoutOf(0));
        (_fxaaVertexSpv, _fxaaFragmentSpv, _fxaaBindings) = (fxaa.Vertex, fxaa.Fragment, fxaa.LayoutOf(0));
    }

    /// <summary>Whether the frame is drawn through the HDR target, with bloom or any effect over it on.</summary>
    public static bool IsOn(RenderWorld renderWorld) =>
        renderWorld.TryGet<BloomSettings>() is { On: true } || renderWorld.TryGet<FrameEffects>() is { Active: true };

    /// <summary>
    /// Draws the window's models, and its draw list's batches before <paramref name="split"/>, into
    /// the HDR target, cleared to the clear color in linear light, which is made or remade first at
    /// <paramref name="extent"/>.
    /// </summary>
    /// <returns>False on a device that cannot make the target, which leaves the frame to be drawn without it.</returns>
    public bool DrawScene(RenderContext renderContext, RenderWorld renderWorld, Extent2D extent, int split)
    {
        if (renderContext.Device is not GraphicsDevice device) return false;
        Retire();
        _shown = null;
        var sized = Ensure(device, renderContext, extent);
        var clear = renderWorld.TryGet<ClearColor>() is { } set ? set : ClearColor.Black;
        var linear = new ClearColor(SrgbToLinear(clear.R), SrgbToLinear(clear.G), SrgbToLinear(clear.B), clear.A);

        var target = sized.Scene;
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, LoadOp.Clear, StoreOp.Store, linear));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        renderWorld.TryGet<ModelRenderer>()?.Draw(pass, target.RenderPass, renderContext, renderWorld, target: 0);
        renderWorld.TryGet<ParticleRenderer>()?.Draw(pass, target.RenderPass, renderContext, renderWorld);
        renderWorld.TryGet<ImmediateRenderer>()?.Draw(pass, target.RenderPass, renderContext, renderWorld, target: 0, end: split, linear: true);
        return true;
    }

    /// <summary>Spreads the HDR target's light down the levels and back up into the first one.</summary>
    public void DrawChain(RenderContext renderContext, float threshold)
    {
        if (_sized is not { } sized || _down is null || _up is null) return;
        var levels = sized.Levels;
        for (int i = 0; i < levels.Length; i++)
        {
            var source = i == 0 ? sized.Scene.Extent : levels[i - 1].Extent;
            Pass(renderContext, levels[i], LoadOp.Clear, _down, sized.Down[i],
                new Push { TexelX = 1f / source.Width, TexelY = 1f / source.Height, Threshold = threshold, Mode = i == 0 ? 0 : 1 });
        }
        for (int i = levels.Length - 1; i > 0; i--)
        {
            var source = levels[i].Extent;
            Pass(renderContext, levels[i - 1], LoadOp.Load, _up, sized.Up[i],
                new Push { TexelX = 1f / source.Width, TexelY = 1f / source.Height, Mode = 2 });
        }
    }

    /// <summary>
    /// Draws the HDR target into the whole of <paramref name="pass"/>, with the bloom added, scaled
    /// by the exposure, brought under 1 by the curve, graded, encoded and vignetted as
    /// <paramref name="bloom"/> and <paramref name="effects"/> say.
    /// </summary>
    public void Composite(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, BloomSettings? bloom, FrameEffects? effects)
    {
        if (_sized is not { } sized) return;
        if (!_composites.TryGetValue(renderPass, out var pipeline))
            _composites[renderPass] = pipeline = Pipeline(renderContext.Device, renderPass, _compositeVertex!, _compositeFragment!, _twoTextures!,
                additive: false, Marshal.SizeOf<CompositePush>());
        var first = sized.Levels[0].Extent;
        effects ??= new FrameEffects();
        var tint = Linear(effects.Tint);
        var push = new CompositePush
        {
            Bloom = new Vector4(1f / first.Width, 1f / first.Height, bloom is { On: true } ? bloom.Intensity / sized.Levels.Length : 0, effects.Exposure),
            Grade = new Vector4(effects.Contrast, effects.Saturation, (float)effects.Tonemap, effects.Vignette),
            Tint = new Vector4(tint, Math.Min(effects.VignetteRadius, 0.99f)),
            Exposure = new Vector4(effects.AutoExposure && _adaptedFrame == _frame ? 1 : 0, AutoExposureKey, 0, 0),
        };
        pass.SetPipeline(pipeline);
        pass.SetBindGroup(pipeline, CompositeSets(renderContext.Device, sized, _shown ?? sized.Scene.ColorView)[_adaptedLast]);
        pass.PushConstants(pipeline, ShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<CompositePush>(in push)));
        pass.Draw(3);
    }

    /// <summary>Composites into the 8-bit frame FXAA reads, made the first time it is asked for.</summary>
    public void CompositeForFxaa(RenderContext renderContext, BloomSettings? bloom, FrameEffects? effects)
    {
        if (_sized is not { } sized || renderContext.Device is not GraphicsDevice device) return;
        if (sized.Shown is null)
        {
            sized.Shown = device.CreateRenderTarget(sized.Extent.Width, sized.Extent.Height, ImageFormat.Undefined, depth: false, multisampled: false);
            sized.ShownSet = device.CreateDescriptorSet(_fxaaLayout!);
            device.UpdateDescriptorSet(sized.ShownSet, null, new CombinedImageSamplerBinding(sized.Shown.ColorView, _sampler!, 0));
        }
        var target = sized.Shown;
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, LoadOp.Clear, StoreOp.Store, ClearColor.Black));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        Composite(pass, target.RenderPass, renderContext, bloom, effects);
    }

    /// <summary>Draws the composited frame through FXAA over the whole of <paramref name="pass"/>, the window's.</summary>
    public void Fxaa(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext)
    {
        if (_sized is not { Shown: { } shown, ShownSet: { } set }) return;
        _fxaaVertex ??= renderContext.Device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _fxaaVertexSpv));
        _fxaaFragment ??= renderContext.Device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fxaaFragmentSpv));
        if (!_fxaas.TryGetValue(renderPass, out var pipeline))
            _fxaas[renderPass] = pipeline = Pipeline(renderContext.Device, renderPass, _fxaaVertex, _fxaaFragment, _fxaaLayout!, additive: false);
        var push = new Push { TexelX = 1f / shown.Extent.Width, TexelY = 1f / shown.Extent.Height };
        pass.SetPipeline(pipeline);
        pass.SetBindGroup(pipeline, set);
        pass.PushConstants(pipeline, ShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
        pass.Draw(3);
    }

    private static Vector3 Linear(Color color) => new(SrgbToLinear(color.R / 255f), SrgbToLinear(color.G / 255f), SrgbToLinear(color.B / 255f));

    /// <summary>Lets the target and the levels go, once no frame in flight reads them, on a frame bloom is off.</summary>
    public void Release()
    {
        Retire();
        if (_sized is null) return;
        _retired.Add((_frame, _sized));
        _sized = null;
    }

    private static void Pass<T>(RenderContext renderContext, RenderTarget target, LoadOp load, IPipeline pipeline, IDescriptorSet set, T push)
        where T : unmanaged
    {
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, load, StoreOp.Store, ClearColor.Black with { A = 0 }));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        pass.SetPipeline(pipeline);
        pass.SetBindGroup(pipeline, set);
        pass.PushConstants(pipeline, ShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<T>(in push)));
        pass.Draw(3);
    }

    /// <summary>
    /// Blurs the HDR frame by its depth of field and the camera's movement as <paramref name="effects"/>
    /// says, in passes of their own that the composite then reads in place of the scene.
    /// </summary>
    public void Lens(RenderContext renderContext, FrameEffects effects, WindowView? view)
    {
        // The camera of the frame before, kept only from the frame before this one, so a frame after
        // others drawn without the HDR frame does not blur by a movement long past.
        Matrix4x4? last = _lastViewFrame == _frame - 1 ? _lastViewProjection : null;
        (_lastViewProjection, _lastViewFrame) = (view?.ViewProjection, _frame);
        if (_sized is not { } sized || view is null || renderContext.Device is not GraphicsDevice device) return;
        var focus = effects.FocusBlur > 0;
        var blur = effects.MotionBlur > 0 && last is not null;
        if (!focus && !blur) return;

        EnsureLens(device, sized);
        Matrix4x4.Invert(view.ViewProjection, out var inverse);
        var lens = sized.Lens!;
        var source = sized.Scene.ColorView;
        if (focus)
        {
            var push = new DofPush
            {
                InverseViewProjection = inverse,
                EyeAndFocus = new Vector4(view.Eye, effects.FocusDistance),
                Lens = new Vector4(effects.FocusRange, effects.FocusBlur, (float)sized.Extent.Width / sized.Extent.Height, sized.Extent.Height),
            };
            Pass(renderContext, lens[0], LoadOp.Clear, _dof!, LensSet(device, sized, source, exact: true), push);
            source = lens[0].ColorView;
        }
        if (blur)
        {
            // This frame's clip space to the world and on through the camera of the frame before,
            // as System.Numerics multiplies a row vector.
            var push = new BlurPush { Reprojection = inverse * last!.Value, Amount = new Vector4(effects.MotionBlur, 0.1f, 0, 0) };
            Pass(renderContext, lens[1], LoadOp.Clear, _blur!, LensSet(device, sized, source, exact: false), push);
            source = lens[1].ColorView;
        }
        _shown = source;
    }

    // The lens passes' targets, half floats with no depth at the window's size, their shaders and
    // their pipelines, made the first time one is on.
    private void EnsureLens(GraphicsDevice device, Sized sized)
    {
        _lensLayout ??= device.CreateDescriptorSetLayout(_lensBindings);
        _dofVertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _dofVertexSpv));
        _dofFragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _dofFragmentSpv));
        _blurVertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _blurVertexSpv));
        _blurFragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _blurFragmentSpv));
        sized.Lens ??= [.. Enumerable.Range(0, 2).Select(_ =>
            device.CreateRenderTarget(sized.Extent.Width, sized.Extent.Height, ImageFormat.R16G16B16A16_Float, depth: false, multisampled: false))];
        _dof ??= Pipeline(device, sized.Lens[0].RenderPass, _dofVertex, _dofFragment, _lensLayout, additive: false, Marshal.SizeOf<DofPush>());
        _blur ??= Pipeline(device, sized.Lens[0].RenderPass, _blurVertex, _blurFragment, _lensLayout, additive: false, Marshal.SizeOf<BlurPush>());
    }

    // The set a lens pass reads an image through, with the scene's depth beside it. Depth is read
    // as it is, since filtering it across an edge gives a distance between the two sides that
    // neither has, and the depth of field reads color as it is too, since a tap whose depth is the
    // background's and whose filtered color takes in a sharp thing beside it spread that thing in
    // faint copies round itself.
    private IDescriptorSet LensSet(GraphicsDevice device, Sized sized, IImageView source, bool exact)
    {
        if (sized.LensFrom.TryGetValue((source, exact), out var set)) return set;
        _depthSampler ??= device.CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        set = device.CreateDescriptorSet(_lensLayout!);
        device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(source, exact ? _depthSampler : _sampler!, 0));
        device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(sized.Scene.DepthView!, _depthSampler, 1));
        return sized.LensFrom[(source, exact)] = set;
    }

    // The composite's sets reading an image, one for each of the two adapted exposures.
    private IDescriptorSet[] CompositeSets(IGraphicsDevice gfx, Sized sized, IImageView source)
    {
        if (sized.CompositeFrom.TryGetValue(source, out var sets)) return sets;
        return sized.CompositeFrom[source] = [.. _adapted!.Select(exposure =>
        {
            var set = gfx.CreateDescriptorSet(_twoTextures!);
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(source, _sampler!, 0));
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(sized.Levels[0].ColorView, _sampler!, 1));
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(exposure.ColorView, _sampler!, 2));
            return set;
        })];
    }

    /// <summary>
    /// Measures the HDR frame and moves the exposure that follows it toward what it measured, at
    /// the pace and within the bounds <paramref name="effects"/> sets.
    /// </summary>
    public void Adapt(RenderContext renderContext, FrameEffects effects)
    {
        if (_sized is not { } sized || _measure is null || _measured is null || _adapted is null || _adaptSets is null) return;
        // All the way on the first frame and after frames without it, so a scene does not fade in
        // from the last one seen, and otherwise by the share the time since the last frame gives.
        var seconds = (float)_sinceAdapted.Elapsed.TotalSeconds;
        _sinceAdapted.Restart();
        var share = _adaptedFrame == _frame - 1 ? 1 - MathF.Exp(-Math.Min(seconds, 0.25f) * effects.AutoExposureSpeed) : 1;
        // The luminance each bound of the exposure brings to the key, as log2.
        var low = MathF.Log2(AutoExposureKey / Math.Max(effects.AutoExposureMax, 1e-3f));
        var high = MathF.Log2(AutoExposureKey / Math.Max(effects.AutoExposureMin, 1e-3f));
        var push = new ExposurePush { Mode = 0, Share = share, Low = Math.Min(low, high), High = high };
        Pass(renderContext, _measured, LoadOp.Clear, _measure, sized.Measure, push);
        var next = 1 - _adaptedLast;
        Pass(renderContext, _adapted[next], LoadOp.Clear, _measure, _adaptSets[next], push with { Mode = 1 });
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
        _measure = Pipeline(device, _measured.RenderPass, _exposureVertex, _exposureFragment, _exposureLayout!, additive: false);
        return _adapted;
    }

    // The target and levels for the window's size, made again when it changes.
    private Sized Ensure(GraphicsDevice device, RenderContext renderContext, Extent2D extent)
    {
        if (_sized is { } current && current.Extent == extent) return current;
        if (_sized is not null) _retired.Add((_frame, _sized));

        _sampler ??= device.CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        _oneTexture ??= device.CreateDescriptorSetLayout(_bloomBindings);
        _twoTextures ??= device.CreateDescriptorSetLayout(_compositeBindings);
        _fxaaLayout ??= device.CreateDescriptorSetLayout(_fxaaBindings);
        _exposureLayout ??= device.CreateDescriptorSetLayout(_exposureBindings);
        _bloomVertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _bloomVertexSpv));
        _bloomFragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _bloomFragmentSpv));
        _compositeVertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _compositeVertexSpv));
        _compositeFragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _compositeFragmentSpv));

        var scene = device.CreateRenderTarget(extent.Width, extent.Height, ImageFormat.R16G16B16A16_Float);
        // Halved each level, down to a thirty-second of the window, or fewer levels for a window
        // too small to halve that often.
        var levels = new List<RenderTarget>();
        for (int i = 1; i <= MaxLevels && Math.Min(extent.Width, extent.Height) >> i >= 2; i++)
            levels.Add(device.CreateRenderTarget(Math.Max(1, extent.Width >> i), Math.Max(1, extent.Height >> i),
                ImageFormat.R16G16B16A16_Float, depth: false, multisampled: false));
        if (levels.Count == 0)
            levels.Add(device.CreateRenderTarget(1, 1, ImageFormat.R16G16B16A16_Float, depth: false, multisampled: false));

        IDescriptorSet SetOf(IImageView view)
        {
            var set = device.CreateDescriptorSet(_oneTexture);
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(view, _sampler, 0));
            return set;
        }
        var down = levels.Select((_, i) => SetOf(i == 0 ? scene.ColorView : levels[i - 1].ColorView)).ToArray();
        var up = levels.Select(level => SetOf(level.ColorView)).ToArray();
        var adapted = Adapted(device, renderContext);
        var composite = adapted.Select(exposure =>
        {
            var set = device.CreateDescriptorSet(_twoTextures);
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(scene.ColorView, _sampler, 0));
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(levels[0].ColorView, _sampler, 1));
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(exposure.ColorView, _sampler, 2));
            return set;
        }).ToArray();
        var measure = device.CreateDescriptorSet(_exposureLayout!);
        device.UpdateDescriptorSet(measure, null, new CombinedImageSamplerBinding(scene.ColorView, _sampler, 0));
        device.UpdateDescriptorSet(measure, null, new CombinedImageSamplerBinding(adapted[0].ColorView, _sampler, 1));

        // Every level draws in the same pass, whatever its size, so the two pipelines are made once.
        _down ??= Pipeline(device, levels[0].RenderPass, _bloomVertex, _bloomFragment, _oneTexture, additive: false);
        _up ??= Pipeline(device, levels[0].RenderPass, _bloomVertex, _bloomFragment, _oneTexture, additive: true);
        return _sized = new Sized(extent, scene, [.. levels], down, up, composite, measure);
    }

    private static IPipeline Pipeline(IGraphicsDevice gfx, IRenderPass renderPass, IShader vertex, IShader fragment,
        IDescriptorSetLayout layout, bool additive, int pushSize = 16) =>
        gfx.CreateGraphicsPipeline(new GraphicsPipelineDesc(
            renderPass, vertex, fragment,
            BlendEnabled: additive,
            CullBackFace: false,
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Fragment, 0, (uint)pushSize)],
            DescriptorSetLayouts: [layout],
            Blend: BlendMode.AddColors));

    // Destroys what was let go once the frames in flight that might read it have finished.
    private void Retire()
    {
        _frame++;
        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < GpuTextures.RetireFrames) continue;
            _retired[i].Disposable.Dispose();
            _retired.RemoveAt(i);
        }
    }

    internal static float SrgbToLinear(float c) =>
        c <= 0.04045f ? c / 12.92f : MathF.Pow((Math.Max(c, 0) + 0.055f) / 1.055f, 2.4f);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, disposable) in _retired) disposable.Dispose();
        _retired.Clear();
        _sized?.Dispose();
        _down?.Dispose();
        _up?.Dispose();
        _measure?.Dispose();
        _dof?.Dispose();
        _blur?.Dispose();
        _dofVertex?.Dispose();
        _dofFragment?.Dispose();
        _blurVertex?.Dispose();
        _blurFragment?.Dispose();
        _lensLayout?.Dispose();
        _measured?.Dispose();
        if (_adapted is not null)
            foreach (var target in _adapted) target.Dispose();
        foreach (var set in _adaptSets ?? []) set.Dispose();
        _exposureVertex?.Dispose();
        _exposureFragment?.Dispose();
        _exposureLayout?.Dispose();
        foreach (var pipeline in _composites.Values.Concat(_fxaas.Values)) pipeline.Dispose();
        _fxaaVertex?.Dispose();
        _fxaaFragment?.Dispose();
        _bloomVertex?.Dispose();
        _bloomFragment?.Dispose();
        _compositeVertex?.Dispose();
        _compositeFragment?.Dispose();
        _oneTexture?.Dispose();
        _twoTextures?.Dispose();
        _fxaaLayout?.Dispose();
        _sampler?.Dispose();
        _depthSampler?.Dispose();
    }
}

/// <summary>
/// Render graph node that draws the window's scene into the HDR target when bloom is on, before
/// <see cref="BloomNode"/> spreads it and <see cref="MainPassNode"/> composites it into the window.
/// </summary>
/// <remarks>
/// The scene is the window's models and its draw list up to the last batch drawn with depth, the
/// 3D shapes inside <c>BeginMode3D</c>. What is drawn after that, a game's interface, is left to
/// <see cref="ImmediateNode"/>, which draws it into the window over the composite, so it is never
/// bloomed or tonemapped and keeps its exact colors.
/// </remarks>
public sealed class HdrSceneNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        renderWorld.Remove<BloomFrame>();
        var bloom = renderWorld.TryGet<BloomRenderer>();
        if (bloom is null) return;
        if (!BloomRenderer.IsOn(renderWorld) || renderWorld.TryGet<SwapchainTarget>() is not { } swapchain)
        {
            bloom.Release();
            return;
        }

        var split = Split(renderWorld.TryGet<DrawList>());
        if (bloom.DrawScene(renderContext, renderWorld, swapchain.Extent, split))
            renderWorld.Set(new BloomFrame(split));
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

/// <summary>Render graph node that spreads the HDR target's brightest light down and up the bloom chain, when <see cref="HdrSceneNode"/> drew it this frame.</summary>
public sealed class BloomNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<BloomFrame>() is null || renderWorld.TryGet<BloomRenderer>() is not { } renderer) return;
        var bloom = renderWorld.TryGet<BloomSettings>();
        if (bloom is { On: true }) renderer.DrawChain(renderContext, bloom.Threshold);
        var effects = renderWorld.TryGet<FrameEffects>();
        if (effects is { AutoExposure: true }) renderer.Adapt(renderContext, effects);
        // The depth of field and motion blur, run with bloom alone too, so motion blur knows the
        // camera of the frame before once it is turned on.
        renderer.Lens(renderContext, effects ?? new FrameEffects(), renderWorld.TryGet<WindowView>());
        // With FXAA the composite is drawn ahead, into the 8-bit frame FXAA reads in the window's pass.
        if (effects is { Fxaa: true }) renderer.CompositeForFxaa(renderContext, bloom, effects);
    }
}
