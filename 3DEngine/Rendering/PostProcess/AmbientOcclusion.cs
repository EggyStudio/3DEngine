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
/// Each render target's depth and occlusion this frame, by the target's texture id, as
/// <see cref="WindowDepth"/> and <see cref="AmbientOcclusionImage"/> are the window's, where it draws
/// meshes through a camera of its own.
/// </summary>
internal sealed class TargetOcclusion
{
    /// <summary>Each target's depth of its meshes that cast shadows, at half its size, which its screen's probes stand on.</summary>
    public Dictionary<int, WindowDepth> Depths { get; } = [];

    /// <summary>Each target's occlusion and the sun's contact shadows, which its model pass binds.</summary>
    public Dictionary<int, AmbientOcclusionImage> Images { get; } = [];
}

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
/// <para>
/// The images are made at half the window's size the first frame it is on and again when the
/// window's size changes, and those they replace are destroyed <see cref="GpuTextures.RetireFrames"/>
/// frames later, as are all of them the first frame it is off.
/// </para>
/// <para>
/// A render target that draws meshes through a camera of its own has images of its own, worked out
/// the same way from its depth ahead of its pass (<see cref="DrawTarget"/>), and let go the frame
/// after one it is not drawn in. A reflection probe's faces are drawn without it.
/// </para>
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
    // Each render target's images and the frame it was last drawn in.
    private readonly Dictionary<int, (Sized Sized, long Drawn)> _targets = [];
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
        renderWorld.TryGet<TargetOcclusion>()?.Depths.Clear();
        renderWorld.TryGet<TargetOcclusion>()?.Images.Clear();
        // A target's images not drawn the frame before are let go.
        foreach (var (id, (stale, _)) in _targets.Where(entry => entry.Value.Drawn < _frame - 1).ToList())
        {
            _retired.Add((_frame, stale));
            _targets.Remove(id);
        }
        var settings = renderWorld.TryGet<AmbientOcclusionSettings>();
        var sun = ContactShadows(renderWorld);
        var bounces = GlobalIlluminationRenderer.CascadesIn(renderWorld) > 0;
        // A render target's images are let go only where all of it is off, since a program may draw
        // its scene into render textures alone and the window in 2D.
        var wanted = settings is { On: true } || sun is not null || bounces;
        if (!wanted) Release();
        if (!wanted || renderWorld.TryGet<WindowView>() is not { } view
            || renderWorld.TryGet<SwapchainTarget>() is not { } swapchain || renderContext.Device is not GraphicsDevice device
            || renderWorld.TryGet<ModelRenderer>() is not { } models || renderWorld.TryGet<SceneFieldBinding>() is not { } field
            || !Matrix4x4.Invert(view.ViewProjection, out var inverse))
        {
            ReleaseWindow();
            renderWorld.Remove<AmbientOcclusionImage>();
            renderWorld.Remove<WindowDepth>();
            return;
        }

        var extent = new Extent2D(Math.Max(1, swapchain.Extent.Width / 2), Math.Max(1, swapchain.Extent.Height / 2));
        var sized = _sized = Ensure(device, extent, field.Field, _sized, "the window");
        models.DrawDepth(renderContext, renderWorld, sized.Depth);
        renderWorld.Set(new WindowDepth(sized.Depth.DepthView, sized.Depth.Sampler, extent));
        // The light that bounces alone needs the depth and no occlusion pass.
        if (settings is not { On: true } && sun is null)
        {
            renderWorld.Remove<AmbientOcclusionImage>();
            return;
        }
        Occlude(renderContext, sized, inverse, view.Eye, swapchain.Extent, settings, sun);
        renderWorld.Set(new AmbientOcclusionImage(sized.Occlusion.ColorView, _sampler!));
    }

    /// <summary>
    /// Draws the depth of the meshes render target <paramref name="id"/>, of <paramref name="size"/>,
    /// draws through <paramref name="camera"/> and works out its occlusion and the sun's contact
    /// shadows from it, as the window's are, ahead of its pass, setting both in
    /// <see cref="TargetOcclusion"/> for its model pass and its screen's probes.
    /// </summary>
    public void DrawTarget(RenderContext renderContext, RenderWorld renderWorld, int id, Extent2D size, Matrix4x4 camera)
    {
        var settings = renderWorld.TryGet<AmbientOcclusionSettings>();
        var sun = ContactShadows(renderWorld);
        var bounces = GlobalIlluminationRenderer.CascadesIn(renderWorld) > 0;
        if (settings is not { On: true } && sun is null && !bounces || renderContext.Device is not GraphicsDevice device
            || renderWorld.TryGet<ModelRenderer>() is not { } models || renderWorld.TryGet<SceneFieldBinding>() is not { } field
            || !Matrix4x4.Invert(camera, out var inverse) || LightingUboPrepare.EyeOf(camera) is not { } eye)
            return;

        var extent = new Extent2D(Math.Max(1, size.Width / 2), Math.Max(1, size.Height / 2));
        var sized = Ensure(device, extent, field.Field, _targets.TryGetValue(id, out var made) ? made.Sized : null, $"render texture {id}");
        _targets[id] = (sized, _frame);
        models.DrawDepth(renderContext, renderWorld, sized.Depth, id);
        var targets = renderWorld.TryGet<TargetOcclusion>();
        if (targets is null) renderWorld.Set(targets = new TargetOcclusion());
        targets.Depths[id] = new WindowDepth(sized.Depth.DepthView, sized.Depth.Sampler, extent);
        if (settings is not { On: true } && sun is null) return;
        Occlude(renderContext, sized, inverse, eye, size, settings, sun);
        targets.Images[id] = new AmbientOcclusionImage(sized.Occlusion.ColorView, _sampler!);
    }

    // The occlusion of a view of size, through the inverse of its camera from its eye, worked out
    // into its images from the depth drawn into them, and blurred across and down.
    private void Occlude(RenderContext renderContext, Sized sized, Matrix4x4 inverse, Vector3 eye, Extent2D size,
        AmbientOcclusionSettings? settings, (Vector3 TowardSun, float Reach)? sun)
    {
        var on = settings is { On: true };
        var push = new Push
        {
            InverseViewProjection = inverse,
            EyeAndRadius = new Vector4(eye, on ? settings!.Radius : 1),
            TexelAndStrength = new Vector4(1f / sized.Extent.Width, 1f / sized.Extent.Height, on ? settings!.Intensity : 0, HeightPerUnit(inverse)),
            Mode = new Vector4(0, (float)size.Width / Math.Max(1, size.Height), on ? 1 : 0, 0),
            SunAndReach = sun is var (toward, reach) ? new Vector4(toward, reach) : Vector4.Zero,
        };
        Pass(renderContext, sized.Occlusion, sized.Occlude, push);
        push.Mode.X = 1;
        Pass(renderContext, sized.Across, sized.BlurAcross, push);
        push.Mode.X = 2;
        Pass(renderContext, sized.Occlusion, sized.BlurDown, push);
    }

    /// <summary>
    /// The share of the picture's height a unit spans a unit from the eye, through
    /// <paramref name="inverse"/>, the camera's inverse view-projection, from the middle of the
    /// picture and the middle of its top edge at the same depth, half a height apart.
    /// </summary>
    internal static float HeightPerUnit(Matrix4x4 inverse)
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

    // A view's images at extent over field, those it has where they match, or new ones in place of
    // them, named for the view.
    private Sized Ensure(GraphicsDevice device, Extent2D extent, GpuSceneField field, Sized? made, string view)
    {
        if (made is not null && made.Extent == extent && ReferenceEquals(made.Field, field)) return made;
        if (made is not null) _retired.Add((_frame, made));

        _vertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));
        _layout ??= device.CreateDescriptorSetLayout(_bindings);
        // The passes read every image at its own size through the depth's nearest sampler, texel
        // for texel, and the model pass reads the occlusion at twice its size through this one,
        // which blends between its texels rather than leaving each a square of four pixels.
        _sampler ??= device.CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        var depth = device.CreateDepthTarget(extent.Width, extent.Height);
        device.Name(depth.DepthView.Image, $"Ambient occlusion depth of {view}");
        var occlusion = device.CreateRenderTarget(extent.Width, extent.Height, ImageFormat.R8G8B8A8_UNorm, depth: false, multisampled: false);
        var across = device.CreateRenderTarget(extent.Width, extent.Height, ImageFormat.R8G8B8A8_UNorm, depth: false, multisampled: false);
        device.Name(occlusion.ColorView.Image, $"Ambient occlusion of {view}");
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
        return new Sized(extent, field, depth, occlusion, across, Set(across.ColorView), Set(occlusion.ColorView), Set(across.ColorView));
    }

    // Lets every view's images go, once no frame in flight reads them, on a frame it is off.
    private void Release()
    {
        ReleaseWindow();
        foreach (var (sized, _) in _targets.Values) _retired.Add((_frame, sized));
        _targets.Clear();
    }

    // Lets the window's images go, on a frame it is off or the window draws no meshes.
    private void ReleaseWindow()
    {
        if (_sized is not null) _retired.Add((_frame, _sized));
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
        foreach (var (sized, _) in _targets.Values) sized.Dispose();
        _targets.Clear();
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
