using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Subsurface scattering over the window's frame, the light that enters skin, wax, marble or a
/// leaf leaving it nearby: the window's meshes whose material has a subsurface radius drawn again
/// into two images of the window's size, their diffuse light and their profile
/// (<see cref="ModelRenderer.DrawSubsurface"/>), and that light spread across and then down by
/// <c>subsurface_blur.slang</c>, the pass down adding the change to the decoded frame before the
/// window's particles are drawn over it.
/// </summary>
/// <remarks>
/// <para>
/// A pixel is marked only where the scene's depth shows the scattering surface, and the spread
/// takes no light from an unmarked pixel nor across a depth edge, so every other pixel of the
/// frame is left as it was. The specular light stays where it was reflected, since only the
/// diffuse share is drawn again and spread.
/// </para>
/// <para>
/// The images are made the first frame the window draws a mesh that scatters and again when the
/// window's size changes, and those they replace are destroyed <see cref="GpuTextures.RetireFrames"/>
/// frames later, as are all of them the first frame it draws none.
/// </para>
/// </remarks>
internal sealed class SubsurfaceRenderer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Push
    {
        public Matrix4x4 InverseViewProjection;
        public Vector4 EyeAndHeight, TexelAndWay, Mode;
    }

    private readonly ReadOnlyMemory<byte> _vertexSpv, _fragmentSpv;
    private readonly DescriptorSetLayoutBinding[] _bindings;
    private IShader? _vertex, _fragment;
    private IDescriptorSetLayout? _layout;
    private ISampler? _sampler;
    private IPipeline? _across, _down;
    private Sized? _sized;
    private readonly List<(long Frame, IDisposable Disposable)> _retired = [];
    private long _frame;

    // What is made for one size of the window and one depth of the scene: the marked meshes'
    // diffuse light and profile, the light spread across, and the sets the two spreads read through.
    private sealed class Sized(Extent2D extent, IImageView depth, RenderTarget marked, RenderTarget across, IDescriptorSet acrossSet,
        IDescriptorSet downSet) : IDisposable
    {
        public Extent2D Extent { get; } = extent;
        public IImageView Depth { get; } = depth;
        public RenderTarget Marked { get; } = marked;
        public RenderTarget Across { get; } = across;
        public IDescriptorSet AcrossSet { get; } = acrossSet;
        public IDescriptorSet DownSet { get; } = downSet;

        public void Dispose()
        {
            AcrossSet.Dispose();
            DownSet.Dispose();
            Across.Dispose();
            Marked.Dispose();
        }
    }

    /// <summary>Creates the renderer from <c>subsurface_blur.slang</c>, compiled.</summary>
    public SubsurfaceRenderer(ShaderProgram blur) => (_vertexSpv, _fragmentSpv, _bindings) = (blur.Vertex, blur.Fragment, blur.LayoutOf(0));

    /// <summary>
    /// Draws the window's meshes that scatter light under their surface into the marked images,
    /// spreads their diffuse light, and adds the change to <paramref name="frame"/>, the decoded
    /// frame of linear light, reading the scene's depth from <paramref name="sceneDepth"/>; or lets
    /// the images go where the window draws none.
    /// </summary>
    public void Draw(RenderContext renderContext, RenderWorld renderWorld, IImageView sceneDepth, RenderTarget frame)
    {
        Retire();
        if (!ModelRenderer.ScattersInWindow(renderWorld) || renderContext.Device is not GraphicsDevice device
            || renderWorld.TryGet<ModelRenderer>() is not { } models || renderWorld.TryGet<WindowView>() is not { } view
            || !Matrix4x4.Invert(view.ViewProjection, out var inverse))
        {
            Release();
            return;
        }

        var sized = Ensure(device, frame, sceneDepth);
        var marked = sized.Marked;
        int drawn;
        using (var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
                   marked.RenderPass, marked.Framebuffer, marked.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(0, 0, 0, 0))))
        {
            pass.SetViewport(0, 0, marked.Extent.Width, marked.Extent.Height, 0, 1);
            pass.SetScissor(0, 0, marked.Extent.Width, marked.Extent.Height);
            drawn = models.DrawSubsurface(pass, marked.RenderPass, renderContext, renderWorld, sceneDepth);
        }
        if (drawn == 0) return;

        var push = new Push
        {
            InverseViewProjection = inverse,
            EyeAndHeight = new Vector4(view.Eye, AmbientOcclusionRenderer.HeightPerUnit(inverse)),
            TexelAndWay = new Vector4(1f / sized.Extent.Width, 1f / sized.Extent.Height, 1, 0),
        };
        Pass(renderContext, sized.Across, LoadOp.Clear, _across!, sized.AcrossSet, push);
        push.TexelAndWay = push.TexelAndWay with { Z = 0, W = 1 };
        push.Mode.X = 1;
        Pass(renderContext, frame, LoadOp.Load, _down!, sized.DownSet, push);
    }

    private void Pass(RenderContext renderContext, RenderTarget target, LoadOp load, IPipeline pipeline, IDescriptorSet set, Push push)
    {
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, load, StoreOp.Store, load == LoadOp.Clear ? new ClearColor(0, 0, 0, 0) : null));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        pass.SetPipeline(pipeline);
        pass.SetBindGroup(pipeline, set);
        pass.PushConstants(pipeline, ShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
        pass.Draw(3);
    }

    private Sized Ensure(GraphicsDevice device, RenderTarget frame, IImageView sceneDepth)
    {
        var extent = frame.Extent;
        if (_sized is { } made && made.Extent == extent && ReferenceEquals(made.Depth, sceneDepth)) return made;
        if (_sized is { } old) _retired.Add((_frame, old));

        _vertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));
        _layout ??= device.CreateDescriptorSetLayout(_bindings);
        // Every image is read texel for texel, as a tap that blended a marked texel with an
        // unmarked one would carry light across the edge between them.
        _sampler ??= device.CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        var marked = device.CreateRenderTarget(extent.Width, extent.Height, [ImageFormat.R16G16B16A16_Float, ImageFormat.R16G16B16A16_Float],
            depth: false, multisampled: false);
        var across = device.CreateRenderTarget(extent.Width, extent.Height, ImageFormat.R16G16B16A16_Float, depth: false, multisampled: false);
        device.Name(marked.ColorView.Image, "Subsurface diffuse light");
        device.Name(across.ColorView.Image, "Subsurface light spread across");
        _across ??= Pipeline(device, across.RenderPass, additive: false);
        _down ??= Pipeline(device, frame.RenderPass, additive: true);

        IDescriptorSet Set(IImageView source)
        {
            var set = device.CreateDescriptorSet(_layout);
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(marked.ColorView, _sampler, 0));
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(marked.MoreColorViews[0], _sampler, 1));
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(sceneDepth, _sampler, 2));
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(source, _sampler, 3));
            return set;
        }
        return _sized = new Sized(extent, sceneDepth, marked, across, Set(marked.ColorView), Set(across.ColorView));
    }

    private IPipeline Pipeline(GraphicsDevice device, IRenderPass renderPass, bool additive) =>
        device.CreateGraphicsPipeline(new GraphicsPipelineDesc(
            renderPass, _vertex!, _fragment!,
            BlendEnabled: additive,
            Blend: BlendMode.AddColors,
            Cull: CullMode.None,
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Fragment, 0, (uint)Marshal.SizeOf<Push>())],
            DescriptorSetLayouts: [_layout!]));

    /// <summary>
    /// Lets the images go, once no frame in flight reads them, on a frame the window's scene is not
    /// decoded or draws nothing that scatters.
    /// </summary>
    public void Skip()
    {
        Retire();
        Release();
    }

    private void Release()
    {
        if (_sized is null) return;
        _retired.Add((_frame, _sized));
        _sized = null;
    }

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

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, disposable) in _retired) disposable.Dispose();
        _retired.Clear();
        _sized?.Dispose();
        _across?.Dispose();
        _down?.Dispose();
        _layout?.Dispose();
        _sampler?.Dispose();
        _vertex?.Dispose();
        _fragment?.Dispose();
    }
}
