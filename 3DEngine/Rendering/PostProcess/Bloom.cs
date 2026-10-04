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

    private readonly ReadOnlyMemory<byte> _bloomVertexSpv, _bloomFragmentSpv, _compositeVertexSpv, _compositeFragmentSpv;
    private IShader? _bloomVertex, _bloomFragment, _compositeVertex, _compositeFragment;
    private IDescriptorSetLayout? _oneTexture, _twoTextures;
    private ISampler? _sampler;
    private IPipeline? _down, _up, _composite;
    private IRenderPass? _compositePass;
    private Sized? _sized;
    private readonly List<(long Frame, IDisposable Disposable)> _retired = [];
    private long _frame;

    // What is made for one size of the window: the HDR target, the levels, and the sets each pass
    // reads its source through.
    private sealed class Sized(Extent2D extent, RenderTarget scene, RenderTarget[] levels, IDescriptorSet[] down, IDescriptorSet[] up,
        IDescriptorSet composite) : IDisposable
    {
        public Extent2D Extent { get; } = extent;
        public RenderTarget Scene { get; } = scene;
        public RenderTarget[] Levels { get; } = levels;
        public IDescriptorSet[] Down { get; } = down;
        public IDescriptorSet[] Up { get; } = up;
        public IDescriptorSet Composite { get; } = composite;

        public void Dispose()
        {
            foreach (var set in Down) set.Dispose();
            foreach (var set in Up) set.Dispose();
            Composite.Dispose();
            foreach (var level in Levels) level.Dispose();
            Scene.Dispose();
        }
    }

    /// <summary>Creates the renderer from the compiled stages of <c>bloom.slang</c> and <c>composite.slang</c>.</summary>
    public BloomRenderer(ReadOnlyMemory<byte> bloomVertex, ReadOnlyMemory<byte> bloomFragment,
        ReadOnlyMemory<byte> compositeVertex, ReadOnlyMemory<byte> compositeFragment)
    {
        _bloomVertexSpv = bloomVertex;
        _bloomFragmentSpv = bloomFragment;
        _compositeVertexSpv = compositeVertex;
        _compositeFragmentSpv = compositeFragment;
    }

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
        var sized = Ensure(device, extent);
        var clear = renderWorld.TryGet<ClearColor>() is { } set ? set : ClearColor.Black;
        var linear = new ClearColor(SrgbToLinear(clear.R), SrgbToLinear(clear.G), SrgbToLinear(clear.B), clear.A);

        var target = sized.Scene;
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, LoadOp.Clear, StoreOp.Store, linear));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        renderWorld.TryGet<ModelRenderer>()?.Draw(pass, target.RenderPass, renderContext, renderWorld, target: 0);
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

    /// <summary>Draws the HDR target with <paramref name="intensity"/> of the bloom added, tonemapped and encoded, over the whole of <paramref name="pass"/>, the window's.</summary>
    public void Composite(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, float intensity)
    {
        if (_sized is not { } sized) return;
        var gfx = renderContext.Device;
        if (_composite is null || !Equals(_compositePass, renderPass))
        {
            if (_composite is not null) _retired.Add((_frame, _composite));
            _composite = Pipeline(gfx, renderPass, _compositeVertex!, _compositeFragment!, _twoTextures!, additive: false);
            _compositePass = renderPass;
        }
        var first = sized.Levels[0].Extent;
        var push = new Push { TexelX = 1f / first.Width, TexelY = 1f / first.Height, Threshold = intensity / sized.Levels.Length };
        pass.SetPipeline(_composite);
        pass.SetBindGroup(_composite, sized.Composite);
        pass.PushConstants(_composite, ShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
        pass.Draw(3);
    }

    /// <summary>Lets the target and the levels go, once no frame in flight reads them, on a frame bloom is off.</summary>
    public void Release()
    {
        Retire();
        if (_sized is null) return;
        _retired.Add((_frame, _sized));
        _sized = null;
    }

    private static void Pass(RenderContext renderContext, RenderTarget target, LoadOp load, IPipeline pipeline, IDescriptorSet set, Push push)
    {
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, load, StoreOp.Store, ClearColor.Black with { A = 0 }));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        pass.SetPipeline(pipeline);
        pass.SetBindGroup(pipeline, set);
        pass.PushConstants(pipeline, ShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
        pass.Draw(3);
    }

    // The target and levels for the window's size, made again when it changes.
    private Sized Ensure(GraphicsDevice device, Extent2D extent)
    {
        if (_sized is { } current && current.Extent == extent) return current;
        if (_sized is not null) _retired.Add((_frame, _sized));

        _sampler ??= device.CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        _oneTexture ??= device.CreateDescriptorSetLayout([new DescriptorSetLayoutBinding(0, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment)]);
        _twoTextures ??= device.CreateDescriptorSetLayout([
            new DescriptorSetLayoutBinding(0, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment),
            new DescriptorSetLayoutBinding(1, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment)]);
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
        var composite = device.CreateDescriptorSet(_twoTextures);
        device.UpdateDescriptorSet(composite, null, new CombinedImageSamplerBinding(scene.ColorView, _sampler, 0));
        device.UpdateDescriptorSet(composite, null, new CombinedImageSamplerBinding(levels[0].ColorView, _sampler, 1));

        // Every level draws in the same pass, whatever its size, so the two pipelines are made once.
        _down ??= Pipeline(device, levels[0].RenderPass, _bloomVertex, _bloomFragment, _oneTexture, additive: false);
        _up ??= Pipeline(device, levels[0].RenderPass, _bloomVertex, _bloomFragment, _oneTexture, additive: true);
        return _sized = new Sized(extent, scene, [.. levels], down, up, composite);
    }

    private static IPipeline Pipeline(IGraphicsDevice gfx, IRenderPass renderPass, IShader vertex, IShader fragment,
        IDescriptorSetLayout layout, bool additive) =>
        gfx.CreateGraphicsPipeline(new GraphicsPipelineDesc(
            renderPass, vertex, fragment,
            BlendEnabled: additive,
            CullBackFace: false,
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Fragment, 0, (uint)Marshal.SizeOf<Push>())],
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

    private static float SrgbToLinear(float c) =>
        c <= 0.04045f ? c / 12.92f : MathF.Pow((Math.Max(c, 0) + 0.055f) / 1.055f, 2.4f);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, disposable) in _retired) disposable.Dispose();
        _retired.Clear();
        _sized?.Dispose();
        _down?.Dispose();
        _up?.Dispose();
        _composite?.Dispose();
        _bloomVertex?.Dispose();
        _bloomFragment?.Dispose();
        _compositeVertex?.Dispose();
        _compositeFragment?.Dispose();
        _oneTexture?.Dispose();
        _twoTextures?.Dispose();
        _sampler?.Dispose();
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
        if (renderWorld.TryGet<BloomSettings>() is not { On: true } || renderWorld.TryGet<SwapchainTarget>() is not { } swapchain)
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
        if (renderWorld.TryGet<BloomFrame>() is null || renderWorld.TryGet<BloomSettings>() is not { } settings) return;
        renderWorld.TryGet<BloomRenderer>()?.DrawChain(renderContext, settings.Threshold);
    }
}
