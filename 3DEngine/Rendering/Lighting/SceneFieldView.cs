using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Draws one cascade of the scene's distance field over the window as the field holds the scene
/// (<c>field_view.slang</c>), after the window's shapes and before ImGui, while
/// <see cref="SceneFieldSettings.Shown"/> names a cascade, as the <c>field.show</c> command sets it.
/// </summary>
internal sealed class SceneFieldViewRenderer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Push
    {
        public Matrix4x4 InverseViewProjection;
        public Vector4 EyeAndCascade;
    }

    private readonly ReadOnlyMemory<byte> _vertexSpv, _fragmentSpv;
    private readonly DescriptorSetLayoutBinding[] _bindings;
    private IShader? _vertex, _fragment;
    private IDescriptorSetLayout? _layout;
    private IPipeline? _pipeline;
    private IRenderPass? _pass;
    private (GpuSceneField Field, IDescriptorSet Set)? _set;
    private readonly List<(long Frame, IDisposable Disposable)> _retired = [];
    private long _frame;

    /// <summary>Creates the renderer from <c>field_view.slang</c>, compiled.</summary>
    public SceneFieldViewRenderer(ShaderProgram view) => (_vertexSpv, _fragmentSpv, _bindings) = (view.Vertex, view.Fragment, view.LayoutOf(0));

    /// <summary>Draws the cascade shown into the window's open pass, where one is shown and the field is built.</summary>
    public void Draw(RenderContext renderContext, RenderWorld renderWorld)
    {
        _frame++;
        for (int i = _retired.Count - 1; i >= 0; i--)
            if (_frame - _retired[i].Frame >= GpuTextures.RetireFrames)
            {
                _retired[i].Disposable.Dispose();
                _retired.RemoveAt(i);
            }
        if (renderWorld.TryGet<SceneFieldSettings>() is not { On: true, Shown: >= 0 } settings
            || renderWorld.TryGet<SceneFieldBinding>() is not { On: true } binding || renderWorld.TryGet<WindowView>() is not { } view
            || renderWorld.TryGet<ActiveSwapchainPass>() is not { } active || renderWorld.TryGet<SwapchainTarget>() is not { } swapchain
            || renderContext.Device is not GraphicsDevice device || !Matrix4x4.Invert(view.ViewProjection, out var inverse))
            return;

        _vertex ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragment ??= device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));
        _layout ??= device.CreateDescriptorSetLayout(_bindings);
        if (_pipeline is null || !Equals(_pass, swapchain.RenderPass))
        {
            if (_pipeline is not null) _retired.Add((_frame, _pipeline));
            _pass = swapchain.RenderPass;
            _pipeline = device.CreateGraphicsPipeline(new GraphicsPipelineDesc(_pass, _vertex, _fragment, Cull: CullMode.None,
                PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Fragment, 0, (uint)Marshal.SizeOf<Push>())],
                DescriptorSetLayouts: [_layout]));
        }
        if (_set is not { } made || !ReferenceEquals(made.Field, binding.Field))
        {
            if (_set is { } old) _retired.Add((_frame, old.Set));
            var set = device.CreateDescriptorSet(_layout);
            device.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(binding.Field.View, binding.Field.Sampler, 0));
            device.UpdateDescriptorSet(set, new UniformBufferBinding(binding.Field.Info, 1, 0, GpuSceneField.InfoBytes), null);
            _set = (binding.Field, set);
        }

        var pass = active.Pass;
        var push = new Push { InverseViewProjection = inverse, EyeAndCascade = new Vector4(view.Eye, settings.Shown) };
        pass.SetViewport(0, 0, active.Extent.Width, active.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, active.Extent.Width, active.Extent.Height);
        pass.SetPipeline(_pipeline);
        pass.SetBindGroup(_pipeline, _set.Value.Set);
        pass.PushConstants(_pipeline, ShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
        pass.Draw(3);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, disposable) in _retired) disposable.Dispose();
        _retired.Clear();
        _set?.Set.Dispose();
        _pipeline?.Dispose();
        _layout?.Dispose();
        _vertex?.Dispose();
        _fragment?.Dispose();
    }
}

/// <summary>Render graph node that draws the cascade of the scene's distance field asked for over the window.</summary>
internal sealed class SceneFieldViewNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) =>
        renderWorld.TryGet<SceneFieldViewRenderer>()?.Draw(renderContext, renderWorld);
}
