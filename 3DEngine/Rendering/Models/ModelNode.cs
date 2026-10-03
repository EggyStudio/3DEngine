using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Draws the frame's <see cref="ModelDrawList"/> with <c>model.slang</c>: holds the pipeline, and
/// draws the meshes meant for one target into whichever pass is open.
/// </summary>
/// <remarks>
/// Each draw binds its mesh's buffers from <see cref="GpuMeshes"/> and its texture from
/// <see cref="GpuTextures"/>, and pushes its transform, its world matrix as a 3x4 and its color.
/// The frame's lights, packed by <see cref="LightingUboPrepare"/>, are bound once per pass as a
/// second descriptor set. The pipeline does not cull, because a model loaded from a file may wind its
/// triangles either way, and the cost is small next to drawing a back face wrong.
/// </remarks>
public sealed class ModelRenderer : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Push
    {
        public Matrix4x4 Transform;
        public Vector4 WorldX;
        public Vector4 WorldY;
        public Vector4 WorldZ;
        public Vector4 Color;
    }

    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;
    private IShader? _vertexShader;
    private IShader? _fragmentShader;
    private IPipeline? _pipeline;
    private IDescriptorSetLayout? _defaultLayout;
    private readonly List<IDescriptorSet> _lightSets = [];
    private int _lightSet;
    private FrameLightingBinding? _lastFrame;
    private IDescriptorSet? _noLights;
    private IBuffer? _noLightsBuffer;

    /// <summary>Creates the renderer from the compiled stages of <c>model.slang</c>.</summary>
    public ModelRenderer(ReadOnlyMemory<byte> vertexSpv, ReadOnlyMemory<byte> fragmentSpv)
    {
        _vertexSpv = vertexSpv;
        _fragmentSpv = fragmentSpv;
    }

    /// <summary>Draws the meshes meant for <paramref name="target"/> into <paramref name="pass"/>.</summary>
    public void Draw(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, RenderWorld renderWorld, int target)
    {
        var draws = renderWorld.TryGet<ModelDrawList>();
        var meshes = renderWorld.TryGet<GpuMeshes>();
        var textures = renderWorld.TryGet<GpuTextures>();
        if (draws is null || draws.Draws.Count == 0 || meshes is null || textures is null) return;

        var gfx = renderContext.Device;
        IPipeline? pipeline = null;

        foreach (var draw in draws.Draws)
        {
            // A mesh unloaded after its draw was recorded is skipped.
            if (draw.Target != target || meshes.Get(draw.Mesh) is not { } mesh) continue;

            if (pipeline is null)
            {
                pipeline = Pipeline(gfx, renderPass, renderWorld);
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(pipeline, LightsSet(gfx, renderWorld), index: 1);
            }

            pass.SetBindGroup(pipeline, textures.SetFor(gfx, draw.Texture));
            pass.SetVertexBuffer(0, [mesh.Vertices], [0]);
            pass.SetIndexBuffer(mesh.Indices, 0, IndexType.UInt32);

            var w = draw.World;
            var push = new Push
            {
                Transform = w * draw.ViewProjection,
                WorldX = new Vector4(w.M11, w.M21, w.M31, w.M41),
                WorldY = new Vector4(w.M12, w.M22, w.M32, w.M42),
                WorldZ = new Vector4(w.M13, w.M23, w.M33, w.M43),
                Color = draw.Color.ToVector4(),
            };
            pass.PushConstants(pipeline, ShaderStageFlags.All, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
            pass.DrawIndexed(mesh.IndexCount);
        }
    }

    private IPipeline Pipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld)
    {
        if (_pipeline is not null) return _pipeline;

        _vertexShader = gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragmentShader = gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));

        var desc = new GraphicsPipelineDesc(
            renderPass,
            _vertexShader,
            _fragmentShader,
            BlendEnabled: true,
            CullBackFace: false,
            VertexBindings: [new VertexInputBindingDesc(0, (uint)Marshal.SizeOf<ModelVertex>())],
            VertexAttributes:
            [
                new VertexInputAttributeDesc(0, 0, VertexFormat.Float3, 0),
                new VertexInputAttributeDesc(1, 0, VertexFormat.Float3, 12),
                new VertexInputAttributeDesc(2, 0, VertexFormat.Float2, 24),
            ],
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.All, 0, (uint)Marshal.SizeOf<Push>())],
            // Both sets have the device's default layout: the texture at binding 1 of the first,
            // and the frame's lights at binding 0 of the second.
            DescriptorSetLayouts: [DefaultLayout(gfx), DefaultLayout(gfx)],
            DepthTestEnabled: true,
            DepthWriteEnabled: true,
            DepthCompareOp: CompareOp.LessOrEqual);

        _pipeline = renderWorld.TryGet<PipelineCache>() is { } cache
            ? cache.GetOrCreate(desc)
            : gfx.CreateGraphicsPipeline(desc);
        return _pipeline;
    }

    // The lights of this frame as a descriptor set: one of a ring, a set per frame in flight so a
    // set the GPU may still read is never written, or a set over an empty buffer when there are no
    // lights, which the shader reads as "use the fixed light".
    private IDescriptorSet LightsSet(IGraphicsDevice gfx, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<FrameLightingBinding>() is not { LightCount: > 0 } frame)
        {
            if (_noLights is null)
            {
                _noLightsBuffer = gfx.CreateBuffer(new BufferDesc((ulong)LightingUboPacker.SizeBytes, BufferUsage.Uniform, CpuAccessMode.Write));
                var span = gfx.Map(_noLightsBuffer);
                span.Clear();
                gfx.Unmap(_noLightsBuffer);
                _noLights = gfx.CreateDescriptorSet();
                gfx.UpdateDescriptorSet(_noLights, new UniformBufferBinding(_noLightsBuffer, 0, 0, (ulong)LightingUboPacker.SizeBytes), samplerBinding: null);
            }
            return _noLights;
        }

        // Once a frame, however many targets draw models in it.
        if (!ReferenceEquals(frame, _lastFrame))
        {
            _lastFrame = frame;
            if (_lightSets.Count < gfx.FramesInFlight) _lightSets.Add(gfx.CreateDescriptorSet());
            _lightSet = (_lightSet + 1) % _lightSets.Count;
            gfx.UpdateDescriptorSet(_lightSets[_lightSet], frame.Binding, samplerBinding: null);
        }
        return _lightSets[_lightSet];
    }

    private IDescriptorSetLayout DefaultLayout(IGraphicsDevice gfx) => _defaultLayout ??= gfx.CreateDescriptorSetLayout(
    [
        new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.Vertex | ShaderStageFlags.Fragment),
        new DescriptorSetLayoutBinding(1, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment),
    ]);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var set in _lightSets) set.Dispose();
        _noLights?.Dispose();
        _noLightsBuffer?.Dispose();
        _defaultLayout?.Dispose();
        _fragmentShader?.Dispose();
        _vertexShader?.Dispose();
    }
}

/// <summary>
/// Render graph node that draws the window's share of the <see cref="ModelDrawList"/> into the
/// swapchain pass, after the ECS meshes and before the immediate shapes.
/// </summary>
public sealed class ModelNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<ActiveSwapchainPass>() is not { } active || renderWorld.TryGet<SwapchainTarget>() is not { } swapchain) return;
        renderWorld.TryGet<ModelRenderer>()?.Draw(active.Pass, swapchain.RenderPass, renderContext, renderWorld, target: 0);
    }
}
