using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// The part of a pixel the window's camera is moved by each frame where temporal antialiasing is
/// on, eight places of Halton's sequence in bases 2 and 3 in turn, which <c>BeginMode3D</c> and the
/// window's camera entity add to their projection.
/// </summary>
internal static class TemporalJitter
{
    /// <summary>How many places the camera is moved to in turn.</summary>
    public const int Count = 8;

    /// <summary>The move of frame <paramref name="frame"/>, in pixels, each axis between -0.5 and 0.5.</summary>
    public static Vector2 Pixels(ulong frame)
    {
        var index = (int)(frame % Count) + 1;
        return new Vector2(Halton(index, 2) - 0.5f, Halton(index, 3) - 0.5f);
    }

    /// <summary>
    /// The move of this frame for a window of <paramref name="width"/> by <paramref name="height"/>
    /// pixels, as a matrix that moves clip space after the projection, or none where temporal
    /// antialiasing is off or the window has no size.
    /// </summary>
    public static Matrix4x4 Shift(World world, int width, int height)
    {
        if (!world.TryGetResource<FrameEffects>(out var effects) || !effects.Temporal || width <= 0 || height <= 0
            || !world.TryGetResource<Time>(out var time))
            return Matrix4x4.Identity;
        var pixels = Pixels(time.FrameCount);
        return Matrix4x4.CreateTranslation(2 * pixels.X / width, 2 * pixels.Y / height, 0);
    }

    /// <summary>
    /// Keeps the move given to the window's camera this frame, so the extract takes the camera without
    /// it for the history's reprojection.
    /// </summary>
    public static void Moved(World world, Matrix4x4 shift)
    {
        var moved = world.GetOrInsertResource(static () => new WindowJitter());
        (moved.Shift, moved.Frame) = (shift, world.TryGetResource<Time>(out var time) ? time.FrameCount : 0);
    }

    // The radical inverse of index in base, the point of Halton's sequence there.
    private static float Halton(int index, int @base)
    {
        float result = 0, fraction = 1;
        while (index > 0)
        {
            fraction /= @base;
            result += fraction * (index % @base);
            index /= @base;
        }
        return result;
    }
}

/// <summary>The move the window's camera was given and the frame it was given in (<see cref="TemporalJitter"/>).</summary>
internal sealed class WindowJitter
{
    /// <summary>The move, a matrix after the projection, or none.</summary>
    public Matrix4x4 Shift { get; set; } = Matrix4x4.Identity;

    /// <summary>The frame it was given in.</summary>
    public ulong Frame { get; set; }

    /// <summary>The move given in frame <paramref name="frame"/>, or none where it was given in another.</summary>
    public Matrix4x4 In(ulong frame) => Frame == frame ? Shift : Matrix4x4.Identity;
}

/// <summary>
/// Temporal antialiasing of the window's HDR frame, which resolves its decoded light against the
/// history the frames before left, found through the camera of the frame before without its move,
/// held to the colors of each pixel's neighbors and blended a part at a time (<c>taa.slang</c>),
/// and draws the result back into the decoded frame, ahead of bloom, the exposure and the lens.
/// </summary>
/// <remarks>
/// The two histories, half floats at the window's size each with the frame's depth beside it, are
/// written in turn, made the first frame it is on and again when the window's size changes, and
/// those they replace destroyed <see cref="GpuTextures.RetireFrames"/> frames later. A frame after
/// one it did not resolve starts the history again from the frame's own light. The push block is
/// 112 bytes, under the 128 every device gives.
/// </remarks>
internal sealed class TemporalRenderer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Push
    {
        public Matrix4x4 Reprojection;
        public Vector4 TexelAndBlend, Mode, Exposure;
    }

    /// <summary>
    /// The most frames a pixel that moved is held to, fewer the faster it moved, so the blend takes
    /// at least an eighth of each frame's light there, as the camera's moves are eight.
    /// </summary>
    internal const float Moving = 8;

    /// <summary>The least share of each frame's light the blend takes, where a pixel has held still.</summary>
    internal const float Still = 1f / 32;

    private readonly ReadOnlyMemory<byte> _vertexSpv, _fragmentSpv;
    private readonly DescriptorSetLayoutBinding[] _bindings, _exposureBindings;
    private IShader? _vertex, _fragment;
    private IDescriptorSetLayout? _layout, _exposureLayout;
    // The set reading each adapted exposure the passes have been given, and the white texel's.
    private readonly Dictionary<IImageView, IDescriptorSet> _exposures = [];
    private ISampler? _nearest, _linear;
    private IPipeline? _resolve, _copy;
    private Sized? _sized;
    private readonly List<(long Frame, IDisposable Disposable)> _retired = [];
    private long _frame;
    private long _resolved = long.MinValue;
    // The window's camera the frame before was resolved through.
    private WindowView? _last;

    /// <summary>Reads the frame before's camera in a world a program moved by a whole step, so its history is reprojected as before.</summary>
    public void Move(WorldStep step) => _last = _last?.Moved(step);

    // The histories for one decoded frame, and the sets each pass reads through.
    private sealed class Sized(RenderTarget frame, RenderTarget[] histories, IDescriptorSet[] resolve, IDescriptorSet first,
        IDescriptorSet[] copy) : IDisposable
    {
        public RenderTarget Frame { get; } = frame;
        public RenderTarget[] Histories { get; } = histories;
        // The resolve writing each history and reading the other, or reading the frame alone, and
        // the copy reading each.
        public IDescriptorSet[] Resolve { get; } = resolve;
        public IDescriptorSet First { get; } = first;
        public IDescriptorSet[] Copy { get; } = copy;
        public int Written { get; set; }

        public void Dispose()
        {
            foreach (var set in Resolve) set.Dispose();
            First.Dispose();
            foreach (var set in Copy) set.Dispose();
            foreach (var history in Histories) history.Dispose();
        }
    }

    /// <summary>Creates the renderer from <c>taa.slang</c>, compiled.</summary>
    public TemporalRenderer(ShaderProgram taa) =>
        (_vertexSpv, _fragmentSpv, _bindings, _exposureBindings) = (taa.Vertex, taa.Fragment, taa.LayoutOf(0), taa.LayoutOf(1));

    /// <summary>
    /// Resolves <paramref name="frame"/>, the window's decoded light and depth this frame, against
    /// the history through <paramref name="view"/>, and draws the result back into it, or lets the
    /// histories go where temporal antialiasing is off. <paramref name="adapted"/> is the texel of
    /// the exposure that follows the scene as it adapted the frame before, where it does.
    /// </summary>
    public void Resolve(RenderContext renderContext, RenderWorld renderWorld, RenderTarget? frame, WindowView? view, IImageView? adapted = null)
    {
        _frame++;
        Retire();
        if (renderWorld.TryGet<FrameEffects>() is not { Temporal: true } effects || frame is null || view is null
            || renderContext.Device is not GraphicsDevice device)
        {
            Release();
            return;
        }
        var sized = _sized = Ensure(device, frame, _sized);
        // This frame's clip space to the frame before's, from both cameras' eyes, so the product
        // holds no float of the world's size and reads the same against any origin.
        var reprojection = _last is not null && _resolved == _frame - 1 ? view.To(_last) : null;
        var held = reprojection is not null;
        var next = 1 - sized.Written;
        var push = new Push
        {
            // None where the camera has not moved, which the product of the two would give only
            // within its rounding, and a still pixel would read as moved.
            Reprojection = held && !view.Same(_last!) ? reprojection!.Value : Matrix4x4.Identity,
            TexelAndBlend = new Vector4(1f / frame.Extent.Width, 1f / frame.Extent.Height, Still, held ? 1 : 0),
            // The camera's move this frame in pixels, from clip space's two units across the frame.
            Mode = new Vector4(0, Moving, view.Jitter.M41 * frame.Extent.Width / 2, view.Jitter.M42 * frame.Extent.Height / 2),
            Exposure = new Vector4(effects.Exposure, adapted is null ? 0 : 1, effects.AutoExposureKey, 0),
        };
        var exposure = ExposureSet(device, renderWorld, adapted);
        Pass(renderContext, _resolve!, sized.Histories[next], held ? sized.Resolve[next] : sized.First, exposure, push, LoadOp.Clear);
        push.Mode.X = 1;
        // Drawn back over the decoded frame's light, its depth kept.
        Pass(renderContext, _copy!, sized.Frame, sized.Copy[next], exposure, push, LoadOp.Load);
        (sized.Written, _resolved, _last) = (next, _frame, view);
    }

    // The set reading the adapted exposure, or the white texel where none is given, which the
    // pass does not read but must bind.
    private IDescriptorSet ExposureSet(GraphicsDevice device, RenderWorld renderWorld, IImageView? adapted)
    {
        var (view, sampler) = adapted is not null ? (adapted, _nearest!) : renderWorld.TryGet<GpuTextures>()!.ViewFor(device, 0);
        if (_exposures.TryGetValue(view, out var set)) return set;
        set = device.CreateDescriptorSet(_exposureLayout!);
        device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(view, sampler, 0));
        return _exposures[view] = set;
    }

    private static void Pass(RenderContext renderContext, IPipeline pipeline, RenderTarget target, IDescriptorSet set, IDescriptorSet exposure,
        Push push, LoadOp load)
    {
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, load, StoreOp.Store, new ClearColor(0, 0, 0, 1)));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        pass.SetPipeline(pipeline);
        pass.SetBindGroup(pipeline, set);
        pass.SetBindGroup(pipeline, exposure, 1);
        pass.PushConstants(pipeline, ShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
        pass.Draw(3);
    }

    // The histories for frame, those made where they match it, or new ones in place of them.
    private Sized Ensure(GraphicsDevice device, RenderTarget frame, Sized? made)
    {
        if (made is not null && ReferenceEquals(made.Frame, frame)) return made;
        if (made is not null) _retired.Add((_frame, made));
        _resolved = long.MinValue;

        _vertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));
        _layout ??= device.CreateDescriptorSetLayout(_bindings);
        _exposureLayout ??= device.CreateDescriptorSetLayout(_exposureBindings);
        _nearest ??= device.CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        _linear ??= device.CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        // Each with a depth of its own, which the resolve writes the frame's into, so the frame
        // after finds what stood nearest where its points were.
        RenderTarget[] histories = [.. Enumerable.Range(0, 2).Select(i =>
        {
            var history = device.CreateRenderTarget(frame.Extent.Width, frame.Extent.Height, ImageFormat.R16G16B16A16_Float, depth: true, multisampled: false);
            device.Name(history.ColorView.Image, $"Temporal antialiasing history {i}");
            return history;
        })];
        var push = (uint)Marshal.SizeOf<Push>();
        _resolve ??= device.CreateGraphicsPipeline(new GraphicsPipelineDesc(histories[0].RenderPass, _vertex, _fragment,
            BlendEnabled: false, Cull: CullMode.None, DepthTestEnabled: true, DepthWriteEnabled: true, DepthCompareOp: CompareOp.Always,
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Fragment, 0, push)], DescriptorSetLayouts: [_layout, _exposureLayout]));
        _copy ??= device.CreateGraphicsPipeline(new GraphicsPipelineDesc(frame.RenderPass, _vertex, _fragment,
            BlendEnabled: false, Cull: CullMode.None, DepthTestEnabled: false, DepthWriteEnabled: false,
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Fragment, 0, push)], DescriptorSetLayouts: [_layout, _exposureLayout]));

        // The copy, drawn into the frame, writes no depth and binds the history's in place of the
        // frame's, which is the pass's own, and the first resolve reads no history and binds the
        // frame in its place.
        IDescriptorSet Set(IImageView read, IImageView depth, RenderTarget? history)
        {
            var set = device.CreateDescriptorSet(_layout);
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(read, _nearest, 0));
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(depth, _nearest, 1));
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(history?.ColorView ?? read, _linear, 2));
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(history?.DepthView ?? depth, _nearest, 3));
            return set;
        }
        var depth = frame.DepthView!;
        return new Sized(frame, histories,
            [Set(frame.ColorView, depth, histories[1]), Set(frame.ColorView, depth, histories[0])],
            Set(frame.ColorView, depth, null),
            [.. histories.Select(history => Set(history.ColorView, history.DepthView!, history))]);
    }

    // Lets the histories go, once no frame in flight reads them, on a frame it is off.
    private void Release()
    {
        if (_sized is not null) _retired.Add((_frame, _sized));
        _sized = null;
        _resolved = long.MinValue;
    }

    // Destroys what was let go once the frames in flight that might read it have finished.
    private void Retire()
    {
        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < GpuTextures.RetireFrames) continue;
            _retired[i].Disposable.Dispose();
            _retired.RemoveAt(i);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, disposable) in _retired) disposable.Dispose();
        _retired.Clear();
        _sized?.Dispose();
        foreach (var set in _exposures.Values) set.Dispose();
        _exposures.Clear();
        _resolve?.Dispose();
        _copy?.Dispose();
        _layout?.Dispose();
        _exposureLayout?.Dispose();
        _nearest?.Dispose();
        _linear?.Dispose();
        _vertex?.Dispose();
        _fragment?.Dispose();
    }
}
