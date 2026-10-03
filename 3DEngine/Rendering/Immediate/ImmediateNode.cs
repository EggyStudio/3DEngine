using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Render graph node that draws the frame's <see cref="DrawList"/> into the swapchain pass
/// <see cref="MainPassNode"/> opened, after the meshes and before ImGui.
/// </summary>
/// <remarks>
/// <para>
/// Every vertex of the frame is written into the per-frame buffer arena in one copy, and each
/// <see cref="DrawBatch"/> is one draw call with its transform as a push constant and its texture
/// as the descriptor set, from <see cref="GpuTextures"/>. Untextured shapes sample a white texture of one pixel, so one shader and
/// four pipelines (lines or triangles, depth tested or not) draw everything. Culling is off, so a
/// shape's triangles may wind either way.
/// </para>
/// </remarks>
public sealed class ImmediateNode : INode, IDisposable
{
    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;

    private IShader? _vertexShader;
    private IShader? _fragmentShader;
    private readonly IPipeline?[] _pipelines = new IPipeline?[4];

    /// <summary>Creates the node from the compiled stages of <c>immediate.slang</c>.</summary>
    public ImmediateNode(ReadOnlyMemory<byte> vertexSpv, ReadOnlyMemory<byte> fragmentSpv)
    {
        _vertexSpv = vertexSpv;
        _fragmentSpv = fragmentSpv;
    }

    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        var gfx = renderContext.Device;
        var drawList = renderWorld.TryGet<DrawList>();
        if (drawList is null || drawList.Batches.Count == 0) return;

        var activePass = renderWorld.TryGet<ActiveSwapchainPass>();
        var swapchainTarget = renderWorld.TryGet<SwapchainTarget>();
        var allocator = renderContext.DynamicAllocator;
        var textures = renderWorld.TryGet<GpuTextures>();
        if (activePass is null || swapchainTarget is null || allocator is null || textures is null) return;

        var vertices = MemoryMarshal.AsBytes(drawList.Vertices);
        var allocation = allocator.Allocate((ulong)vertices.Length, BufferUsage.Vertex);
        vertices.CopyTo(allocator.Map(allocation));
        allocator.Unmap(allocation);

        var pass = activePass.Pass;
        pass.SetVertexBuffer(0, [allocation.Buffer], [allocation.Offset]);

        foreach (var batch in drawList.Batches)
        {
            var pipeline = Pipeline(gfx, swapchainTarget.RenderPass, renderWorld, batch);
            pass.SetPipeline(pipeline);

            pass.SetBindGroup(pipeline, textures.SetFor(gfx, batch.Texture));

            var transform = batch.Transform;
            pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0,
                MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in transform)));
            pass.Draw((uint)batch.VertexCount, 1, (uint)batch.FirstVertex);
        }
    }

    private IPipeline Pipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, DrawBatch batch)
    {
        var slot = (batch.Topology == PrimitiveTopology.LineList ? 2 : 0) + (batch.DepthTest ? 1 : 0);
        if (_pipelines[slot] is { } existing) return existing;

        _vertexShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragmentShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));

        var desc = new GraphicsPipelineDesc(
            renderPass,
            _vertexShader,
            _fragmentShader,
            BlendEnabled: true,
            CullBackFace: false,
            VertexBindings: [new VertexInputBindingDesc(0, (uint)Marshal.SizeOf<ImmediateVertex>())],
            VertexAttributes:
            [
                new VertexInputAttributeDesc(0, 0, VertexFormat.Float3, 0),
                new VertexInputAttributeDesc(1, 0, VertexFormat.Float2, 12),
                new VertexInputAttributeDesc(2, 0, VertexFormat.UNormR8G8B8A8, 20),
            ],
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Vertex, 0, 64)],
            DepthTestEnabled: batch.DepthTest,
            DepthWriteEnabled: batch.DepthTest,
            DepthCompareOp: CompareOp.LessOrEqual,
            Topology: batch.Topology);

        var pipeline = renderWorld.TryGet<PipelineCache>() is { } cache
            ? cache.GetOrCreate(desc)
            : gfx.CreateGraphicsPipeline(desc);
        return _pipelines[slot] = pipeline;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _fragmentShader?.Dispose();
        _vertexShader?.Dispose();
    }
}
