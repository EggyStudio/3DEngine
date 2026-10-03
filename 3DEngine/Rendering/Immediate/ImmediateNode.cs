using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Draws the frame's <see cref="DrawList"/>: holds the four pipelines (lines or triangles, depth
/// tested or not) and the frame's vertex upload, and draws the batches meant for one target into
/// whichever pass is open.
/// </summary>
/// <remarks>
/// <para>
/// Every vertex of the frame is written into the per-frame buffer arena in one copy by
/// <see cref="Upload"/>, before the graph runs, because the batches for render targets are drawn
/// by <see cref="TargetsNode"/> before the window's are drawn by <see cref="ImmediateNode"/>, and
/// both read the same buffer.
/// </para>
/// <para>
/// Each batch is one draw call with its transform as a push constant and its texture, from
/// <see cref="GpuTextures"/>, as the descriptor set. Culling is off, so a shape's triangles may wind
/// either way. Render targets have render passes compatible with the window's, so the same
/// pipelines draw into both.
/// </para>
/// </remarks>
public sealed class ImmediateRenderer : IDisposable
{
    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;
    private IShader? _vertexShader;
    private IShader? _fragmentShader;
    private readonly IPipeline?[] _pipelines = new IPipeline?[4];
    private DynamicAllocation? _vertices;

    /// <summary>Creates the renderer from the compiled stages of <c>immediate.slang</c>.</summary>
    public ImmediateRenderer(ReadOnlyMemory<byte> vertexSpv, ReadOnlyMemory<byte> fragmentSpv)
    {
        _vertexSpv = vertexSpv;
        _fragmentSpv = fragmentSpv;
    }

    /// <summary>Writes the frame's vertices into the buffer arena, or forgets last frame's when there are none.</summary>
    public void Upload(RenderContext renderContext, RenderWorld renderWorld)
    {
        _vertices = null;
        var drawList = renderWorld.TryGet<DrawList>();
        if (drawList is null || drawList.Vertices.IsEmpty || renderContext.DynamicAllocator is not { } allocator) return;

        var bytes = MemoryMarshal.AsBytes(drawList.Vertices);
        var allocation = allocator.Allocate((ulong)bytes.Length, BufferUsage.Vertex);
        bytes.CopyTo(allocator.Map(allocation));
        allocator.Unmap(allocation);
        _vertices = allocation;
    }

    /// <summary>Draws the batches meant for <paramref name="target"/> into <paramref name="pass"/>.</summary>
    public void Draw(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, RenderWorld renderWorld, int target)
    {
        var drawList = renderWorld.TryGet<DrawList>();
        var textures = renderWorld.TryGet<GpuTextures>();
        if (drawList is null || textures is null || _vertices is not { } vertices) return;

        var gfx = renderContext.Device;
        var bound = false;
        foreach (var batch in drawList.Batches)
        {
            if (batch.Target != target) continue;
            if (!bound)
            {
                pass.SetVertexBuffer(0, [vertices.Buffer], [vertices.Offset]);
                bound = true;
            }

            var pipeline = Pipeline(gfx, renderPass, renderWorld, batch);
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

/// <summary>Prepare system that uploads the frame's immediate vertices before the graph runs.</summary>
public sealed class ImmediateUploadPrepare : IPrepareSystem
{
    /// <inheritdoc />
    public void Run(RenderWorld renderWorld, RenderContext renderContext) =>
        renderWorld.TryGet<ImmediateRenderer>()?.Upload(renderContext, renderWorld);
}

/// <summary>
/// Render graph node that draws the window's share of the <see cref="DrawList"/> into the swapchain
/// pass <see cref="MainPassNode"/> opened, after the meshes and before ImGui.
/// </summary>
public sealed class ImmediateNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<ActiveSwapchainPass>() is not { } active || renderWorld.TryGet<SwapchainTarget>() is not { } swapchain) return;
        renderWorld.TryGet<ImmediateRenderer>()?.Draw(active.Pass, swapchain.RenderPass, renderContext, renderWorld, target: 0);
    }
}
