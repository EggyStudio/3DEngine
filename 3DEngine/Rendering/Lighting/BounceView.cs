using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>What <see cref="BounceViewRenderer"/> draws of the light that bounces, as <c>gi.show</c> names it.</summary>
internal enum BounceView
{
    /// <summary>Nothing, the window as it is.</summary>
    None,

    /// <summary>The screen's probes as tiles over the picture, each a dot of the light it holds.</summary>
    Tiles,

    /// <summary>The light each screen probe's rays brought, a block a probe.</summary>
    Light,

    /// <summary>Each screen probe's light blended with its neighbors' and the frame before's, as the model pass reads it.</summary>
    Filtered,

    /// <summary>How much of each screen probe's light the frame before's gave, red for none to green for the most.</summary>
    History,

    /// <summary>The light a cascade's rays brought, each layer of its probes a square of their octahedrons.</summary>
    Rays,

    /// <summary>A cascade's light merged with the cascades above, laid out as <see cref="Rays"/> is.</summary>
    Merged,

    /// <summary>A cascade's probes as small cubes in the scene, each face the light the probe gathers from that side.</summary>
    Probes,

    /// <summary>The window's light against a reference, red where the frame is brighter and blue where it is darker.</summary>
    Difference,
}

/// <summary>
/// Draws what the light that bounces holds where <see cref="GlobalIlluminationSettings.Shown"/>
/// names a view: a cascade's probes as cubes into the window's scene
/// (<c>gi_probes.slang</c>, <see cref="DrawProbes"/>), and every other view over the window after
/// its shapes and before ImGui (<c>gi_view.slang</c>, <see cref="Draw"/>), as the
/// <c>gi.show</c> command and <see cref="Engine3D.DrawBounceWindow"/> set it.
/// </summary>
/// <remarks>
/// <para>
/// The cubes are drawn in the scene's own pass, after its meshes, so the scene hides them as it
/// hides any mesh and they are lit by nothing but their own faces. Drawn through the immediate
/// pass they would be a batch with depth after the program's own, which would move the line
/// between the scene and the interface drawn over it.
/// </para>
/// <para>
/// The view's images change from frame to frame, a cascade's or the screen's probes, which are
/// made again when the window's size changes, so each frame writes one of a ring of sets that no
/// frame in flight still reads.
/// </para>
/// </remarks>
internal sealed class BounceViewRenderer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ViewPush
    {
        public uint Mode, Probes, Texels, Unused;
        public Vector4 Grid, Window;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProbesPush
    {
        public Matrix4x4 ViewProjection;
        public uint Cascade, Probes, Unused0, Unused1;
        public Vector4 Size;
    }

    // A cube's half side as a share of its cascade's probes' spacing.
    private const float CubeSize = 0.12f;

    private readonly ShaderProgram _view, _probes;
    private IShader? _viewVertex, _viewFragment, _probesVertex, _probesFragment;
    private IDescriptorSetLayout? _viewLayout, _probesLayout;
    private ISampler? _sampler;
    private IPipeline? _viewPipeline, _probesPipeline;
    private IRenderPass? _viewPass, _probesPass;
    private readonly IDescriptorSet?[] _sets = new IDescriptorSet?[GpuTextures.RetireFrames];
    private (GpuIllumination Probes, GpuSceneField Field, IDescriptorSet Set)? _probesSet;
    private (int Version, IImage Image, IImageView View)? _reference;
    private readonly List<(long Frame, IDisposable Disposable)> _retired = [];
    private long _frame;

    /// <summary>Creates the renderer from <c>gi_view.slang</c> and <c>gi_probes.slang</c>, compiled.</summary>
    public BounceViewRenderer(ShaderProgram view, ShaderProgram probes) => (_view, _probes) = (view, probes);

    /// <summary>
    /// Draws the view shown into the window's open pass, where one is shown that is drawn over the
    /// window and light bounces this frame; the screen's views where the window has screen probes,
    /// and the difference where a reference of the window's size was given.
    /// </summary>
    public void Draw(RenderContext renderContext, RenderWorld renderWorld)
    {
        Retire();
        if (renderWorld.TryGet<GlobalIlluminationSettings>() is not { Shown: not (BounceView.None or BounceView.Probes) } settings
            || renderWorld.TryGet<IlluminationBinding>() is not { } binding || renderWorld.TryGet<BloomFrame>() is null
            || renderWorld.TryGet<BloomRenderer>()?.Decoded is not { } decoded
            || renderWorld.TryGet<ActiveSwapchainPass>() is not { } active || renderWorld.TryGet<SwapchainTarget>() is not { } swapchain
            || renderContext.Device is not GraphicsDevice device)
            return;
        var shown = settings.Shown;
        var screen = binding.Screen;
        if (shown is BounceView.Tiles or BounceView.Light or BounceView.Filtered or BounceView.History && screen is null) return;
        var reference = shown == BounceView.Difference ? Reference(device, settings, decoded.Extent) : null;
        if (shown == BounceView.Difference && reference is null) return;

        _viewVertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _view.Vertex));
        _viewFragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _view.Fragment));
        _viewLayout ??= device.CreateDescriptorSetLayout(_view.LayoutOf(0));
        _sampler ??= device.CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        if (_viewPipeline is null || !Equals(_viewPass, swapchain.RenderPass))
        {
            if (_viewPipeline is not null) _retired.Add((_frame, _viewPipeline));
            _viewPass = swapchain.RenderPass;
            // Blended, as the tiles' edges lie over the picture half seen through.
            _viewPipeline = device.CreateGraphicsPipeline(new GraphicsPipelineDesc(_viewPass, _viewVertex, _viewFragment, BlendEnabled: true,
                Cull: CullMode.None,
                PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Fragment, 0, (uint)Marshal.SizeOf<ViewPush>())],
                DescriptorSetLayouts: [_viewLayout]));
        }

        var probes = binding.Probes;
        var cascade = Math.Clamp(settings.ShownCascade, 0, probes.Cascades - 1);
        var slot = (int)(_frame % _sets.Length);
        var set = _sets[slot] ??= device.CreateDescriptorSet(_viewLayout);
        var plane = screen is null ? decoded.ColorView : shown == BounceView.Light ? screen.IrradianceView : screen.BlendedView;
        device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(plane, _sampler, 0));
        device.BindProbeVolume(set, 1, probes, cascade, merged: shown != BounceView.Rays);
        device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(decoded.ColorView, _sampler, 2));
        device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(reference ?? decoded.ColorView, _sampler, 3));

        var push = new ViewPush
        {
            Mode = (uint)shown, Probes = (uint)probes.Probes, Texels = (uint)probes.Texels[cascade],
            Grid = new Vector4(screen?.Tile ?? 1, 0, 0, 0), Window = new Vector4(active.Extent.Width, active.Extent.Height, 0, 0),
        };
        var pass = active.Pass;
        pass.SetViewport(0, 0, active.Extent.Width, active.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, active.Extent.Width, active.Extent.Height);
        pass.SetPipeline(_viewPipeline);
        pass.SetBindGroup(_viewPipeline, set);
        pass.PushConstants(_viewPipeline, ShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<ViewPush>(in push)));
        pass.Draw(3);
    }

    /// <summary>
    /// Draws the cascade shown's probes as cubes into <paramref name="pass"/>, the window's scene,
    /// where <see cref="BounceView.Probes"/> is shown and light bounces this frame.
    /// </summary>
    public void DrawProbes(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<GlobalIlluminationSettings>() is not { Shown: BounceView.Probes } settings
            || renderWorld.TryGet<IlluminationBinding>() is not { } binding || renderWorld.TryGet<WindowView>() is not { } view
            || renderContext.Device is not GraphicsDevice device)
            return;

        _probesVertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _probes.Vertex));
        _probesFragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _probes.Fragment));
        _probesLayout ??= device.CreateDescriptorSetLayout(_probes.LayoutOf(0));
        _sampler ??= device.CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        if (_probesPipeline is null || !Equals(_probesPass, renderPass))
        {
            if (_probesPipeline is not null) _retired.Add((_frame, _probesPipeline));
            _probesPass = renderPass;
            _probesPipeline = device.CreateGraphicsPipeline(new GraphicsPipelineDesc(_probesPass, _probesVertex, _probesFragment, Cull: CullMode.None,
                PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Vertex, 0, (uint)Marshal.SizeOf<ProbesPush>())],
                DescriptorSetLayouts: [_probesLayout], DepthTestEnabled: true, DepthWriteEnabled: true));
        }
        if (_probesSet is not { } made || !ReferenceEquals(made.Probes, binding.Probes) || !ReferenceEquals(made.Field, binding.Field))
        {
            if (_probesSet is { } old) _retired.Add((_frame, old.Set));
            var set = device.CreateDescriptorSet(_probesLayout);
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(binding.Probes.CubesView, _sampler, 0, DescriptorType.SampledImage));
            device.UpdateDescriptorSet(set, new UniformBufferBinding(binding.Field.Info, 1, 0, GpuSceneField.InfoBytes), null);
            _probesSet = (binding.Probes, binding.Field, set);
        }

        var p = binding.Probes.Probes;
        var push = new ProbesPush
        {
            ViewProjection = view.ViewProjection,
            Cascade = (uint)Math.Clamp(settings.ShownCascade, 0, binding.Probes.Cascades - 1), Probes = (uint)p,
            Size = new Vector4(GlobalIlluminationRenderer.ProbeSpacing, CubeSize, 0, 0),
        };
        pass.SetPipeline(_probesPipeline);
        pass.SetBindGroup(_probesPipeline, _probesSet.Value.Set);
        pass.PushConstants(_probesPipeline, ShaderStageFlags.Vertex, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<ProbesPush>(in push)));
        pass.Draw(36, (uint)(p * p * p));
    }

    // The reference's light as an image, uploaded again when another is given, or null where none
    // is given or its size is not the window's.
    private IImageView? Reference(GraphicsDevice device, GlobalIlluminationSettings settings, Extent2D extent)
    {
        if (settings.Reference is not { } given || given.Width != extent.Width || given.Height != extent.Height) return null;
        if (_reference is { } held && held.Version == given.Version) return held.View;
        if (_reference is { } old)
        {
            _retired.Add((_frame, old.View));
            _retired.Add((_frame, old.Image));
        }
        var texels = new Half[given.Width * given.Height * 4];
        for (int i = 0; i < given.Width * given.Height; i++)
        {
            texels[i * 4] = (Half)given.Light[i * 3];
            texels[i * 4 + 1] = (Half)given.Light[i * 3 + 1];
            texels[i * 4 + 2] = (Half)given.Light[i * 3 + 2];
            texels[i * 4 + 3] = (Half)1;
        }
        var size = new Extent2D((uint)given.Width, (uint)given.Height);
        var image = device.CreateImage(new ImageDesc(size, ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.TransferDst));
        device.Name(image, "Light that bounces, the reference shown");
        device.UploadTexture2D(image, MemoryMarshal.AsBytes(texels.AsSpan()), size.Width, size.Height, 8);
        var view = device.CreateImageView(image);
        _reference = (given.Version, image, view);
        return view;
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
        foreach (var set in _sets) set?.Dispose();
        _probesSet?.Set.Dispose();
        _reference?.View.Dispose();
        _reference?.Image.Dispose();
        _viewPipeline?.Dispose();
        _probesPipeline?.Dispose();
        _viewLayout?.Dispose();
        _probesLayout?.Dispose();
        _sampler?.Dispose();
        _viewVertex?.Dispose();
        _viewFragment?.Dispose();
        _probesVertex?.Dispose();
        _probesFragment?.Dispose();
    }
}

/// <summary>Render graph node that draws the view of the light that bounces asked for over the window.</summary>
internal sealed class BounceViewNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) =>
        renderWorld.TryGet<BounceViewRenderer>()?.Draw(renderContext, renderWorld);
}
