using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>How strongly the surfaces around a point close off its light from all around, a world resource the renderer reads each frame.</summary>
/// <seealso cref="Engine3D.SetAmbientOcclusion"/>
internal sealed class AmbientOcclusionSettings
{
    /// <summary>How strongly it darkens, 0 for not at all, which draws no pass for it.</summary>
    public float Intensity { get; set; }

    /// <summary>How far from a surface, in world units, the surfaces that close it off are looked for.</summary>
    public float Radius { get; set; } = 1;

    /// <summary>Whether it is worked out this frame.</summary>
    public bool On => Intensity > 0 && Radius > 0;
}

/// <summary>
/// The window's ambient occlusion this frame in red and the share of the sun's light its contact
/// shadows let through in green, which the model pass binds for the window's view.
/// </summary>
internal sealed record AmbientOcclusionImage(IImageView View, ISampler Sampler);

/// <summary>
/// The depth of the window's meshes that cast shadows at half the window's size this frame, which
/// the occlusion is worked out from and the light that bounces places its screen's probes on.
/// </summary>
internal sealed record WindowDepth(IImageView View, ISampler Sampler, Extent2D Extent);

/// <summary>
/// The window's ambient occlusion: the depth of its meshes drawn at half the window's size ahead of
/// its pass, the occlusion worked out from it, and two passes that blur it along each axis without
/// crossing an edge (<c>ao.slang</c>). The model pass multiplies its ambient, environment and
/// probe light by it, and not the light of lights, which reaches a corner as well as a wall.
/// Where the scene's distance field is built, the occlusion pass also reads the occlusion from it
/// and traces the sun's light through it, the share that arrives in the image's green channel,
/// which the model pass multiplies the shadowed directional light by, so the pass runs for the
/// sun's contact shadows alone where the occlusion is off.
/// </summary>
/// <remarks>
/// The images are made at half the window's size the first frame it is on and again when the
/// window's size changes, and those they replace are destroyed <see cref="GpuTextures.RetireFrames"/>
/// frames later, as are all of them the first frame it is off.
/// </remarks>
internal sealed class AmbientOcclusionRenderer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Push
    {
        public Matrix4x4 InverseViewProjection;
        public Vector4 EyeAndRadius, TexelAndStrength, Mode, SunAndReach;
    }

    /// <summary>
    /// How many cells of the finest cascade of the scene's distance field the sun's contact
    /// shadows are traced across.
    /// </summary>
    internal const int ContactCells = 32;

    private readonly ReadOnlyMemory<byte> _vertexSpv, _fragmentSpv;
    private readonly DescriptorSetLayoutBinding[] _bindings;
    private IShader? _vertex, _fragment;
    private IDescriptorSetLayout? _layout;
    private ISampler? _sampler;
    private IPipeline? _pipeline;
    private Sized? _sized;
    private readonly List<(long Frame, IDisposable Disposable)> _retired = [];
    private long _frame;

    // What is made for one size of the window: the depth, the occlusion and the image between the
    // blurs, and the set each of the three passes reads through.
    private sealed class Sized(Extent2D extent, GpuSceneField field, ShadowMap depth, RenderTarget occlusion, RenderTarget across,
        IDescriptorSet occlude, IDescriptorSet blurAcross, IDescriptorSet blurDown) : IDisposable
    {
        public Extent2D Extent { get; } = extent;
        public GpuSceneField Field { get; } = field;
        public ShadowMap Depth { get; } = depth;
        public RenderTarget Occlusion { get; } = occlusion;
        public RenderTarget Across { get; } = across;
        public IDescriptorSet Occlude { get; } = occlude;
        public IDescriptorSet BlurAcross { get; } = blurAcross;
        public IDescriptorSet BlurDown { get; } = blurDown;

        public void Dispose()
        {
            Occlude.Dispose();
            BlurAcross.Dispose();
            BlurDown.Dispose();
            Across.Dispose();
            Occlusion.Dispose();
            Depth.Dispose();
        }
    }

    /// <summary>Creates the renderer from <c>ao.slang</c>, compiled.</summary>
    public AmbientOcclusionRenderer(ShaderProgram ao) => (_vertexSpv, _fragmentSpv, _bindings) = (ao.Vertex, ao.Fragment, ao.LayoutOf(0));

    /// <summary>
    /// The way toward the frame's shadowed directional light, the sun, and how far its contact
    /// shadows reach, where the scene's distance field is built and there is such a light, which
    /// the model pass reads the image's green channel for.
    /// </summary>
    internal static (Vector3 TowardSun, float Reach)? ContactShadows(RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<SceneFieldSettings>() is not { On: true } field || renderWorld.TryGet<FrameShadow>() is not { } shadow
            || renderWorld.TryGet<RenderLights>() is not { } lights || shadow.Light < 0 || shadow.Light >= lights.All.Count
            || lights.All[shadow.Light].Kind != LightKind.Directional)
            return null;
        return (-lights.All[shadow.Light].Direction, ContactCells * field.CellSize);
    }

    /// <summary>
    /// Draws the window's depth and works out its occlusion as <see cref="AmbientOcclusionSettings"/> say,
    /// and the sun's contact shadows where the scene's distance field is built, setting
    /// <see cref="AmbientOcclusionImage"/> for the model pass, or lets the images go and removes it
    /// when both are off or the window has no camera.
    /// </summary>
    public void Draw(RenderContext renderContext, RenderWorld renderWorld)
    {
        Retire();
        var settings = renderWorld.TryGet<AmbientOcclusionSettings>();
        var sun = ContactShadows(renderWorld);
        var bounces = GlobalIlluminationRenderer.CascadesIn(renderWorld) > 0;
        if (settings is not { On: true } && sun is null && !bounces || renderWorld.TryGet<WindowView>() is not { } view
            || renderWorld.TryGet<SwapchainTarget>() is not { } swapchain || renderContext.Device is not GraphicsDevice device
            || renderWorld.TryGet<ModelRenderer>() is not { } models || renderWorld.TryGet<SceneFieldBinding>() is not { } field
            || !Matrix4x4.Invert(view.ViewProjection, out var inverse))
        {
            Release();
            renderWorld.Remove<AmbientOcclusionImage>();
            renderWorld.Remove<WindowDepth>();
            return;
        }

        var extent = new Extent2D(Math.Max(1, swapchain.Extent.Width / 2), Math.Max(1, swapchain.Extent.Height / 2));
        var sized = Ensure(device, extent, field.Field);
        models.DrawDepth(renderContext, renderWorld, sized.Depth);
        renderWorld.Set(new WindowDepth(sized.Depth.DepthView, sized.Depth.Sampler, extent));
        // The light that bounces alone needs the depth and no occlusion pass.
        if (settings is not { On: true } && sun is null)
        {
            renderWorld.Remove<AmbientOcclusionImage>();
            return;
        }

        var on = settings is { On: true };
        var push = new Push
        {
            InverseViewProjection = inverse,
            EyeAndRadius = new Vector4(view.Eye, on ? settings!.Radius : 1),
            TexelAndStrength = new Vector4(1f / extent.Width, 1f / extent.Height, on ? settings!.Intensity : 0, HeightPerUnit(inverse)),
            Mode = new Vector4(0, (float)swapchain.Extent.Width / Math.Max(1, swapchain.Extent.Height), on ? 1 : 0, 0),
            SunAndReach = sun is var (toward, reach) ? new Vector4(toward, reach) : Vector4.Zero,
        };
        Pass(renderContext, sized.Occlusion, sized.Occlude, push);
        push.Mode.X = 1;
        Pass(renderContext, sized.Across, sized.BlurAcross, push);
        push.Mode.X = 2;
        Pass(renderContext, sized.Occlusion, sized.BlurDown, push);
        renderWorld.Set(new AmbientOcclusionImage(sized.Occlusion.ColorView, _sampler!));
    }

    // The share of the picture's height a unit spans a unit from the eye, from the middle of the
    // picture and the middle of its top edge at the same depth, half a height apart.
    private static float HeightPerUnit(Matrix4x4 inverse)
    {
        Vector3 At(float x, float y)
        {
            var p = Vector4.Transform(new Vector4(x, y, 0.5f, 1), inverse);
            return new Vector3(p.X, p.Y, p.Z) / p.W;
        }
        var near = Vector4.Transform(new Vector4(0, 0, 0, 1), inverse);
        var eye = new Vector3(near.X, near.Y, near.Z) / near.W;
        var middle = At(0, 0);
        var halfHeight = Vector3.Distance(At(0, 1), middle);
        return halfHeight > 1e-6f ? Vector3.Distance(middle, eye) / (2 * halfHeight) : 1;
    }

    private void Pass(RenderContext renderContext, RenderTarget target, IDescriptorSet set, Push push)
    {
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(1, 1, 1, 1)));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        pass.SetPipeline(_pipeline!);
        pass.SetBindGroup(_pipeline!, set);
        pass.PushConstants(_pipeline!, ShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
        pass.Draw(3);
    }

    private Sized Ensure(GraphicsDevice device, Extent2D extent, GpuSceneField field)
    {
        if (_sized is { } made && made.Extent == extent && ReferenceEquals(made.Field, field)) return made;
        if (_sized is { } old) _retired.Add((_frame, old));

        _vertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));
        _layout ??= device.CreateDescriptorSetLayout(_bindings);
        // The passes read every image at its own size through the depth's nearest sampler, texel
        // for texel, and the model pass reads the occlusion at twice its size through this one,
        // which blends between its texels rather than leaving each a square of four pixels.
        _sampler ??= device.CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        var depth = device.CreateDepthTarget(extent.Width, extent.Height);
        device.Name(depth.DepthView.Image, "Ambient occlusion depth");
        var occlusion = device.CreateRenderTarget(extent.Width, extent.Height, ImageFormat.R8G8B8A8_UNorm, depth: false, multisampled: false);
        var across = device.CreateRenderTarget(extent.Width, extent.Height, ImageFormat.R8G8B8A8_UNorm, depth: false, multisampled: false);
        device.Name(occlusion.ColorView.Image, "Ambient occlusion");
        _pipeline ??= device.CreateGraphicsPipeline(new GraphicsPipelineDesc(
            occlusion.RenderPass, _vertex, _fragment,
            BlendEnabled: false,
            Cull: CullMode.None,
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Fragment, 0, (uint)Marshal.SizeOf<Push>())],
            DescriptorSetLayouts: [_layout]));

        IDescriptorSet Set(IImageView read)
        {
            var set = device.CreateDescriptorSet(_layout);
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(depth.DepthView, depth.Sampler, 0));
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(read, depth.Sampler, 1));
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(field.View, field.Sampler, 2));
            device.UpdateDescriptorSet(set, new UniformBufferBinding(field.Info, 3, 0, GpuSceneField.InfoBytes), null);
            return set;
        }
        return _sized = new Sized(extent, field, depth, occlusion, across, Set(across.ColorView), Set(occlusion.ColorView), Set(across.ColorView));
    }

    // Lets the images go, once no frame in flight reads them, on a frame it is off.
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
        _pipeline?.Dispose();
        _layout?.Dispose();
        _sampler?.Dispose();
        _vertex?.Dispose();
        _fragment?.Dispose();
    }
}

/// <summary>Render graph node that works out the window's ambient occlusion ahead of every pass that lights its meshes.</summary>
internal sealed class AmbientOcclusionNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) =>
        renderWorld.TryGet<AmbientOcclusionRenderer>()?.Draw(renderContext, renderWorld);
}
