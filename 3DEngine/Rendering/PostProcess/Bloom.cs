using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>How much light past a threshold spreads into the pixels around it, a world resource the renderer reads each frame.</summary>
/// <seealso cref="Engine3D.SetBloom"/>
internal sealed class BloomSettings
{
    /// <summary>How much of the spread light is added to the frame, 0 for none.</summary>
    public float Intensity { get; set; }

    /// <summary>The brightness in linear light past which a pixel blooms, 1 unless set, so only what is brighter than white does.</summary>
    public float Threshold { get; set; } = 1;

    /// <summary>Whether the bloom chain runs this frame.</summary>
    public bool On => Intensity > 0;
}

/// <summary>
/// The frame's scene, drawn into the HDR target, which <see cref="HdrSceneNode"/> sets for the
/// nodes after it and removes on a frame the window shows no scene.
/// </summary>
/// <param name="Split">
/// The index in the <see cref="DrawList"/> of the first batch drawn into the window after the
/// composite rather than into the HDR target, the one after the window's last batch with depth.
/// </param>
internal sealed record BloomFrame(int Split);

/// <summary>
/// The HDR frame and its bloom: a half-float target the window's scene is drawn into every frame it
/// shows one, a chain of half-float levels down to a thirty-second of the window that spread its
/// brightest light, and the pass that adds them and tonemaps the result into the window.
/// </summary>
/// <remarks>
/// <para>
/// The scene holds its light sRGB-encoded and carried on past 1, as the model pass and the 2D passes
/// write it, so its blending and multisampling are done on encoded light as raylib's are and a
/// program's shader returns its color encoded wherever it draws. The composite decodes it, and
/// where bloom, an exposure that follows the scene, the lens passes or the reflections of light
/// that bounces read the scene, a pass decodes it once into an image of linear light they all read.
/// </para>
/// <para>
/// The target and the levels are made at the window's size the first frame the window shows a
/// scene and again when the window's size changes, and those they replace are destroyed
/// <see cref="GpuTextures.RetireFrames"/> frames later, once no frame in flight reads them. They
/// are let go the same way the first frame it shows none.
/// </para>
/// <para>
/// The first level keeps the light past <see cref="BloomSettings.Threshold"/>, and each level is
/// added back onto the one above it on the way up, so the first ends up holding the light spread
/// over every size, the levels' sum. The composite divides it by their number, so an intensity of 1
/// adds as much light as was past the threshold, spread.
/// </para>
/// </remarks>
internal sealed class BloomRenderer : IDisposable
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
    private readonly MotionVelocity _velocity;
    // The pass that decodes the scene into linear light with its depth, and the particles' after it.
    private readonly ReadOnlyMemory<byte> _decodeVertexSpv, _decodeFragmentSpv;
    private readonly DescriptorSetLayoutBinding[] _decodeBindings;
    private IShader? _decodeVertex, _decodeFragment;
    private IDescriptorSetLayout? _decodeLayout;
    private IPipeline? _decode;
    // The image the composite reads this frame, the scene, its decoded light or the last lens
    // pass's, whether it holds linear light, and the camera of the frame before with the frame it
    // was seen in, which motion blur measures movement from.
    private IImageView? _shown;
    private bool _shownLinear;
    // Whether the engine's curve bends this frame's light, or gives way to a clamp, as Bends says.
    private bool _bends;
    // Bevy's tables by the curve that looks light up in each, read the first time it is chosen, and
    // the texel bound in their place for the other curves.
    private readonly Dictionary<Tonemap, VolumeTexture> _tables = [];
    private VolumeTexture? _noTable;
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
    private sealed class Sized(Extent2D extent, RenderTarget scene, RenderTarget[] levels, IDescriptorSet?[] down, IDescriptorSet[] up,
        int depthGeneration) : IDisposable
    {
        // The window's depth the scene draws into, by how many times it had been made.
        public int DepthGeneration { get; } = depthGeneration;

        // The composite's sets by the image they read, the scene or a pass's, and the curve's table,
        // the two lens passes' targets once one is on, and their sets by the image they read with
        // the depth.
        public Dictionary<(IImageView Source, IImageView Table), IDescriptorSet[]> CompositeFrom { get; } = [];
        public RenderTarget[]? Lens { get; set; }
        public Dictionary<(IImageView Source, bool Exact, IImageView? Velocity), IDescriptorSet> LensFrom { get; } = [];

        // The moving entities' movement, drawn with per-object blur on, and the set it reads the
        // scene's depth through, made the first frame one moves.
        public RenderTarget? Velocity { get; set; }
        public IDescriptorSet? VelocityDepth { get; set; }

        // The 8-bit frame the composite draws into for FXAA to read, made the first frame FXAA is on.
        public RenderTarget? Shown { get; set; }
        public IDescriptorSet? ShownSet { get; set; }

        // The scene decoded to linear light with its depth, and the set the decoding reads the scene
        // and its depth through, made the first frame a pass reads it.
        public RenderTarget? Linear { get; set; }
        public IDescriptorSet? Decode { get; set; }

        public Extent2D Extent { get; } = extent;
        public RenderTarget Scene { get; } = scene;
        public RenderTarget[] Levels { get; } = levels;
        // The sets each level is drawn down from, the first's reading the decoded scene and made with it.
        public IDescriptorSet?[] Down { get; } = down;
        public IDescriptorSet[] Up { get; } = up;
        // The set the frame is measured through, reading the decoded scene and made with it.
        public IDescriptorSet? Measure { get; set; }

        public void Dispose()
        {
            foreach (var set in Down) set?.Dispose();
            foreach (var set in Up) set.Dispose();
            foreach (var sets in CompositeFrom.Values)
                foreach (var set in sets) set.Dispose();
            foreach (var set in LensFrom.Values) set.Dispose();
            if (Lens is not null)
                foreach (var target in Lens) target.Dispose();
            VelocityDepth?.Dispose();
            Velocity?.Dispose();
            Measure?.Dispose();
            ShownSet?.Dispose();
            Shown?.Dispose();
            Decode?.Dispose();
            Linear?.Dispose();
            foreach (var level in Levels) level.Dispose();
            Scene.Dispose();
        }
    }

    /// <summary>
    /// Creates the renderer from <c>bloom.slang</c>, <c>composite.slang</c>, <c>fxaa.slang</c>,
    /// <c>exposure.slang</c>, <c>dof.slang</c>, <c>motion_blur.slang</c>, <c>velocity.slang</c> and
    /// <c>decode.slang</c>, compiled.
    /// </summary>
    public BloomRenderer(ShaderProgram bloom, ShaderProgram composite, ShaderProgram fxaa, ShaderProgram exposure,
        ShaderProgram dof, ShaderProgram motionBlur, ShaderProgram velocity, ShaderProgram decode)
    {
        (_decodeVertexSpv, _decodeFragmentSpv, _decodeBindings) = (decode.Vertex, decode.Fragment, decode.LayoutOf(0));
        // One layout for both lens passes, the blur's reading the moving entities' movement too.
        (_dofVertexSpv, _dofFragmentSpv, _lensBindings) = (dof.Vertex, dof.Fragment, ShaderProgram.Merge(dof.LayoutOf(0), motionBlur.LayoutOf(0)));
        _velocity = new MotionVelocity(velocity);
        (_blurVertexSpv, _blurFragmentSpv) = (motionBlur.Vertex, motionBlur.Fragment);
        (_exposureVertexSpv, _exposureFragmentSpv, _exposureBindings) = (exposure.Vertex, exposure.Fragment, exposure.LayoutOf(0));
        (_bloomVertexSpv, _bloomFragmentSpv, _bloomBindings) = (bloom.Vertex, bloom.Fragment, bloom.LayoutOf(0));
        (_compositeVertexSpv, _compositeFragmentSpv, _compositeBindings) = (composite.Vertex, composite.Fragment, composite.LayoutOf(0));
        (_fxaaVertexSpv, _fxaaFragmentSpv, _fxaaBindings) = (fxaa.Vertex, fxaa.Fragment, fxaa.LayoutOf(0));
    }

    /// <summary>
    /// Whether the window shows a scene this frame, a mesh, a particle or a shape drawn with depth
    /// inside <c>BeginMode3D</c>, which is drawn into the HDR target and tonemapped into the window.
    /// A frame of 2D alone is drawn straight into the window, as it looks the same either way.
    /// </summary>
    public static bool ShowsScene(RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<ModelDrawList>()?.WindowViewProjection is not null
            || renderWorld.TryGet<ParticleRenderer>()?.DrawsInWindow(renderWorld) == true) return true;
        foreach (var batch in renderWorld.TryGet<DrawList>()?.Batches ?? [])
            if (batch.Target == 0 && batch.DepthTest) return true;
        return false;
    }

    /// <summary>
    /// Whether a pass besides the composite reads the scene's light this frame, bloom, an exposure
    /// that follows the scene, the depth of field or motion blur, or light bouncing, whose
    /// reflections read the scene of the frame before, or the window draws particles, which are
    /// drawn over it, so the scene is decoded for them once.
    /// </summary>
    public static bool ReadsLight(RenderWorld renderWorld) =>
        renderWorld.TryGet<BloomSettings>() is { On: true } || renderWorld.TryGet<ParticleRenderer>()?.DrawsInWindow(renderWorld) == true
        || renderWorld.TryGet<FrameEffects>() is { AutoExposure: true } or { FocusBlur: > 0 } or { MotionBlur: > 0 }
        || GlobalIlluminationRenderer.CascadesIn(renderWorld) > 0;

    /// <summary>
    /// Whether the engine's curve bends the scene's light past its knee this frame, as it does
    /// wherever light past white may come about: a light, a sky, a reflection probe, light that
    /// bounces, particles, bloom or an exposure. A scene with none of these is drawn as raylib draws
    /// one, its colors as they are, and the curve gives way to a clamp, which leaves every color at
    /// or under white as it is.
    /// </summary>
    public static bool Bends(RenderWorld renderWorld) =>
        renderWorld.TryGet<RenderLights>() is { All.Count: > 0 } || renderWorld.TryGet<EnvironmentMap>() is not null
        || renderWorld.TryGet<BoundProbes>() is { Slots.Count: > 0 } || GlobalIlluminationRenderer.CascadesIn(renderWorld) > 0
        || renderWorld.TryGet<ParticleRenderer>()?.DrawsInWindow(renderWorld) == true
        || renderWorld.TryGet<BloomSettings>() is { On: true }
        || renderWorld.TryGet<FrameEffects>() is { AutoExposure: true } or { Exposure: not 1 };

    /// <summary>
    /// Draws the window's models, and its draw list's batches before <paramref name="split"/>, into
    /// the HDR target, cleared to the clear color, which is made or remade first at
    /// <paramref name="extent"/>. The scene writes its light sRGB-encoded, carried on past 1, so the
    /// clear color and every 2D color are drawn as they are, and a shader of the program's own has
    /// its color held to what an eight-bit frame keeps of it, so it blends as it does in the
    /// window. The window's particles are drawn over it once <see cref="Decode"/> has decoded it.
    /// </summary>
    /// <returns>False on a device that cannot make the target, which leaves the frame to be drawn without it.</returns>
    public bool DrawScene(RenderContext renderContext, RenderWorld renderWorld, Extent2D extent, int split)
    {
        if (renderContext.Device is not GraphicsDevice device) return false;
        Retire();
        var sized = Ensure(device, renderContext, extent);
        (_shown, _shownLinear, _bends) = (sized.Scene.ColorView, false, Bends(renderWorld));
        var clear = renderWorld.TryGet<ClearColor>() is { } set ? set : ClearColor.Black;

        var target = sized.Scene;
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, LoadOp.Clear, StoreOp.Store, clear));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        renderWorld.TryGet<ModelRenderer>()?.Draw(pass, target.RenderPass, renderContext, renderWorld, target: 0, held: true);
        renderWorld.TryGet<ImmediateRenderer>()?.Draw(pass, target.RenderPass, renderContext, renderWorld, target: 0, end: split, held: true);
        return true;
    }

    /// <summary>
    /// Decodes the scene this frame drew into an image of linear light with the scene's depth,
    /// made the first time, and draws the window's particles over it, so they add and lay their
    /// light over the scene's in linear light. The bloom chain, the exposure, the lens passes and
    /// the composite then read it in the scene's place.
    /// </summary>
    /// <returns>The decoded image, or null before the scene is drawn.</returns>
    public IImageView? Decode(RenderContext renderContext, RenderWorld renderWorld)
    {
        if (_sized is not { } sized || renderContext.Device is not GraphicsDevice device) return null;
        var linear = EnsureLinear(device, sized);
        _decode ??= device.CreateGraphicsPipeline(new GraphicsPipelineDesc(
            linear.RenderPass, _decodeVertex!, _decodeFragment!,
            Cull: CullMode.None,
            DescriptorSetLayouts: [_decodeLayout!],
            DepthTestEnabled: true,
            DepthWriteEnabled: true,
            DepthCompareOp: CompareOp.Always));
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            linear.RenderPass, linear.Framebuffer, linear.Extent, LoadOp.Clear, StoreOp.Store, ClearColor.Black));
        pass.SetViewport(0, 0, linear.Extent.Width, linear.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, linear.Extent.Width, linear.Extent.Height);
        pass.SetPipeline(_decode);
        pass.SetBindGroup(_decode, sized.Decode!);
        pass.Draw(3);
        renderWorld.TryGet<ParticleRenderer>()?.Draw(pass, linear.RenderPass, renderContext, renderWorld);
        (_shown, _shownLinear) = (linear.ColorView, true);
        return linear.ColorView;
    }

    /// <summary>Spreads the HDR target's light down the levels and back up into the first one.</summary>
    public void DrawChain(RenderContext renderContext, float threshold)
    {
        if (_sized is not { Linear: not null } sized || _down is null || _up is null) return;
        var levels = sized.Levels;
        for (int i = 0; i < levels.Length; i++)
        {
            var source = i == 0 ? sized.Scene.Extent : levels[i - 1].Extent;
            Pass(renderContext, levels[i], LoadOp.Clear, _down, sized.Down[i]!,
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
    /// Draws the HDR target into the whole of <paramref name="pass"/>, decoded, with the bloom
    /// added, scaled by the exposure, brought under 1 by the curve, graded, encoded and vignetted as
    /// <paramref name="bloom"/> and <paramref name="effects"/> say.
    /// </summary>
    public void Composite(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, BloomSettings? bloom, FrameEffects? effects)
    {
        if (_sized is not { } sized || renderContext.Device is not GraphicsDevice device) return;
        if (!_composites.TryGetValue(renderPass, out var pipeline))
            _composites[renderPass] = pipeline = Pipeline(renderContext.Device, renderPass, _compositeVertex!, _compositeFragment!, _twoTextures!,
                additive: false, Marshal.SizeOf<CompositePush>());
        var first = sized.Levels[0].Extent;
        effects ??= new FrameEffects();
        var tint = Linear(effects.Tint);
        var curve = effects.Tonemap == Tonemap.Engine && !_bends ? Tonemap.Clamp : effects.Tonemap;
        var push = new CompositePush
        {
            Bloom = new Vector4(1f / first.Width, 1f / first.Height, bloom is { On: true } && _shownLinear ? bloom.Intensity / sized.Levels.Length : 0,
                effects.Exposure),
            Grade = new Vector4(effects.Contrast, effects.Saturation, (float)curve, effects.Vignette),
            Tint = new Vector4(tint, Math.Min(effects.VignetteRadius, 0.99f)),
            Exposure = new Vector4(effects.AutoExposure && _adaptedFrame == _frame ? 1 : 0, AutoExposureKey, _shownLinear ? 0 : 1, 0),
        };
        pass.SetPipeline(pipeline);
        pass.SetBindGroup(pipeline, CompositeSets(device, sized, _shown ?? sized.Scene.ColorView, TableFor(device, curve))[_adaptedLast]);
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
    /// Blurs the HDR frame's decoded light by its depth of field and the camera's movement as
    /// <paramref name="effects"/> says, and by each moving mesh entity's own with per-object blur
    /// on, in passes of their own that the composite then reads in place of the scene.
    /// </summary>
    public void Lens(RenderContext renderContext, RenderWorld renderWorld, FrameEffects effects, WindowView? view)
    {
        // The camera of the frame before, kept only from the frame before this one, so a frame after
        // others drawn without the HDR frame does not blur by a movement long past.
        Matrix4x4? last = _lastViewFrame == _frame - 1 ? _lastViewProjection : null;
        (_lastViewProjection, _lastViewFrame) = (view?.ViewProjection, _frame);
        if (_sized is not { Linear: { } linear } sized || !_shownLinear || view is null || renderContext.Device is not GraphicsDevice device) return;
        var focus = effects.FocusBlur > 0;
        var blur = effects.MotionBlur > 0 && last is not null;
        if (!focus && !blur) return;

        EnsureLens(device, sized);
        Matrix4x4.Invert(view.ViewProjection, out var inverse);
        var lens = sized.Lens!;
        var source = linear.ColorView;
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
            // The mesh entities that moved, each drawn as its own movement, where per-object blur is on.
            var own = false;
            if (effects.MotionBlurObjects && renderWorld.TryGet<ModelDrawList>() is { Moving.Count: > 0 } models
                && renderWorld.TryGet<GpuMeshes>() is { } meshes)
            {
                EnsureVelocity(device, sized);
                _velocity.Draw(renderContext, device, sized.Velocity!, sized.VelocityDepth!, meshes, models.Moving, view.ViewProjection, last!.Value);
                own = true;
            }
            var push = new BlurPush { Reprojection = inverse * last!.Value, Amount = new Vector4(effects.MotionBlur, 0.1f, own ? 1 : 0, 0) };
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
    // The moving entities' movement is read at binding 2 once it has been drawn, and the image read
    // in its place before then, which the blur is told not to read.
    private IDescriptorSet LensSet(GraphicsDevice device, Sized sized, IImageView source, bool exact)
    {
        var velocity = sized.Velocity?.ColorView;
        if (sized.LensFrom.TryGetValue((source, exact, velocity), out var set)) return set;
        _depthSampler ??= DepthSampler(device);
        set = device.CreateDescriptorSet(_lensLayout!);
        device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(source, exact ? _depthSampler : _sampler!, 0));
        device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(sized.Scene.DepthView!, _depthSampler, 1));
        device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(velocity ?? source, _depthSampler, 2));
        return sized.LensFrom[(source, exact, velocity)] = set;
    }

    private static ISampler DepthSampler(GraphicsDevice device) => device.CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
        SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

    // The image the moving entities' movement is drawn into, the HDR frame's size, and the set its
    // pass reads the scene's depth through, made the first frame an entity moves with it on.
    private void EnsureVelocity(GraphicsDevice device, Sized sized)
    {
        if (sized.Velocity is not null) return;
        _depthSampler ??= DepthSampler(device);
        sized.Velocity = device.CreateRenderTarget(sized.Extent.Width, sized.Extent.Height, ImageFormat.R16G16B16A16_Float, depth: false, multisampled: false);
        sized.VelocityDepth = device.CreateDescriptorSet(_velocity.Layout(device));
        device.UpdateDescriptorSet(sized.VelocityDepth, null, new CombinedImageSamplerBinding(sized.Scene.DepthView!, _depthSampler, 0));
    }

    // The composite's sets reading an image and a curve's table, one for each of the two adapted exposures.
    private IDescriptorSet[] CompositeSets(IGraphicsDevice gfx, Sized sized, IImageView source, VolumeTexture table)
    {
        if (sized.CompositeFrom.TryGetValue((source, table.View), out var sets)) return sets;
        return sized.CompositeFrom[(source, table.View)] = [.. _adapted!.Select(exposure =>
        {
            var set = gfx.CreateDescriptorSet(_twoTextures!);
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(source, _sampler!, 0));
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(sized.Levels[0].ColorView, _sampler!, 1));
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(exposure.ColorView, _sampler!, 2));
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(table.View, table.Sampler, 3));
            return set;
        })];
    }

    // The table a curve looks light up in, read and uploaded the first time the curve is chosen,
    // or a texel standing in for a curve worked out without one.
    private VolumeTexture TableFor(GraphicsDevice device, Tonemap curve)
    {
        if (TonemapTables.FileOf(curve) is null)
            return _noTable ??= device.CreateVolumeTexture(1, Vortice.Vulkan.VkFormat.R16G16B16A16Sfloat, new byte[8]);
        if (_tables.TryGetValue(curve, out var made)) return made;
        var table = TonemapTables.Load(curve);
        return _tables[curve] = device.CreateVolumeTexture(table.Size, table.Format, table.Texels);
    }

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
        var share = _adaptedFrame == _frame - 1 ? 1 - MathF.Exp(-Math.Min(seconds, 0.25f) * effects.AutoExposureSpeed) : 1;
        // The luminance each bound of the exposure brings to the key, as log2.
        var low = MathF.Log2(AutoExposureKey / Math.Max(effects.AutoExposureMax, 1e-3f));
        var high = MathF.Log2(AutoExposureKey / Math.Max(effects.AutoExposureMin, 1e-3f));
        var push = new ExposurePush { Mode = 0, Share = share, Low = Math.Min(low, high), High = high };
        Pass(renderContext, _measured, LoadOp.Clear, _measure, measured, push);
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
        if (_sized is { } current && current.Extent == extent && current.DepthGeneration == device.WindowDepthGeneration) return current;
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

        // Drawn into the window's own multisampled depth, which the window's pass after the
        // composite clears and draws nothing into with depth.
        var scene = device.CreateRenderTargetOnWindowDepth(ImageFormat.R16G16B16A16_Float);
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
        var down = levels.Select((_, i) => i == 0 ? null : SetOf(levels[i - 1].ColorView)).ToArray();
        var up = levels.Select(level => SetOf(level.ColorView)).ToArray();
        Adapted(device, renderContext);

        // Every level draws in the same pass, whatever its size, so the two pipelines are made once.
        _down ??= Pipeline(device, levels[0].RenderPass, _bloomVertex, _bloomFragment, _oneTexture, additive: false);
        _up ??= Pipeline(device, levels[0].RenderPass, _bloomVertex, _bloomFragment, _oneTexture, additive: true);
        return _sized = new Sized(extent, scene, [.. levels], down, up, device.WindowDepthGeneration);
    }

    // The image the scene is decoded into and the sets that read it, the first level's and the
    // measure's, made the first frame a pass reads the scene's light.
    private RenderTarget EnsureLinear(GraphicsDevice device, Sized sized)
    {
        if (sized.Linear is { } made) return made;
        // With a depth of its own, which the decoding writes the scene's into, so the particles
        // drawn over it are hidden behind what the scene drew.
        var linear = sized.Linear = device.CreateRenderTarget(sized.Extent.Width, sized.Extent.Height, ImageFormat.R16G16B16A16_Float,
            depth: true, multisampled: false);
        IDescriptorSet SetOf(IDescriptorSetLayout layout, IImageView view)
        {
            var set = device.CreateDescriptorSet(layout);
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(view, _sampler!, 0));
            return set;
        }
        _depthSampler ??= DepthSampler(device);
        _decodeLayout ??= device.CreateDescriptorSetLayout(_decodeBindings);
        _decodeVertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _decodeVertexSpv));
        _decodeFragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _decodeFragmentSpv));
        sized.Decode = device.CreateDescriptorSet(_decodeLayout);
        device.UpdateDescriptorSet(sized.Decode, null, new CombinedImageSamplerBinding(sized.Scene.ColorView, _depthSampler, 0));
        device.UpdateDescriptorSet(sized.Decode, null, new CombinedImageSamplerBinding(sized.Scene.DepthView!, _depthSampler, 1));
        sized.Down[0] = SetOf(_oneTexture!, linear.ColorView);
        sized.Measure = SetOf(_exposureLayout!, linear.ColorView);
        device.UpdateDescriptorSet(sized.Measure, null, new CombinedImageSamplerBinding(_adapted![0].ColorView, _sampler!, 1));
        return linear;
    }

    private static IPipeline Pipeline(IGraphicsDevice gfx, IRenderPass renderPass, IShader vertex, IShader fragment,
        IDescriptorSetLayout layout, bool additive, int pushSize = 16) =>
        gfx.CreateGraphicsPipeline(new GraphicsPipelineDesc(
            renderPass, vertex, fragment,
            BlendEnabled: additive,
            Cull: CullMode.None,
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
        _velocity.Dispose();
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
        foreach (var table in _tables.Values) table.Dispose();
        _noTable?.Dispose();
        _decode?.Dispose();
        _decodeVertex?.Dispose();
        _decodeFragment?.Dispose();
        _decodeLayout?.Dispose();
        _sampler?.Dispose();
        _depthSampler?.Dispose();
    }
}
