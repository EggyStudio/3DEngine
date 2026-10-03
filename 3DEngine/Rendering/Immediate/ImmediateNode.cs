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
/// Each batch is one draw call with its transform and its shader's four values as push constants
/// and its texture, from <see cref="GpuTextures"/>, as the descriptor set. A batch recorded inside
/// <c>BeginShaderMode</c> draws with that shader's stages, from <see cref="ShaderStore"/>. Culling is off, so a shape's triangles may wind
/// either way. Render targets have render passes compatible with the window's, so the same
/// pipelines draw into both.
/// </para>
/// </remarks>
public sealed class ImmediateRenderer : IDisposable
{
    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;
    // The push block: the transform, then the four float4 values a custom shader reads.
    [StructLayout(LayoutKind.Sequential)]
    private struct Push
    {
        public Matrix4x4 Transform;
        public ShaderParams Params;
    }

    private sealed class Stages(IShader vertex, IShader fragment) : IDisposable
    {
        public IShader Vertex { get; } = vertex;
        public IShader Fragment { get; } = fragment;
        public void Dispose()
        {
            Fragment.Dispose();
            Vertex.Dispose();
        }
    }

    private Stages? _engineStages;
    private readonly Dictionary<int, Stages> _customStages = [];
    private readonly Dictionary<(int Shader, int Slot), IPipeline> _pipelines = [];
    private readonly List<(long Frame, IDisposable Stages)> _retired = [];
    private long _frame;
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
        RetireUnloadedShaders(renderWorld.TryGet<ShaderStore>());
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

            // A batch whose shader was unloaded after it was recorded draws with the engine's own.
            var pipeline = Pipeline(gfx, renderPass, renderWorld, batch);
            pass.SetPipeline(pipeline);
            pass.SetBindGroup(pipeline, textures.SetFor(gfx, batch.Texture));

            var push = new Push { Transform = batch.Transform, Params = batch.Params };
            pass.PushConstants(pipeline, ShaderStageFlags.All, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
            pass.Draw((uint)batch.VertexCount, 1, (uint)batch.FirstVertex);
        }
    }

    private IPipeline Pipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, DrawBatch batch)
    {
        var slot = (batch.Topology == PrimitiveTopology.LineList ? 2 : 0) + (batch.DepthTest ? 1 : 0);
        var stages = StagesFor(gfx, renderWorld, batch.Shader, out var shader);
        if (_pipelines.TryGetValue((shader, slot), out var existing)) return existing;

        var desc = new GraphicsPipelineDesc(
            renderPass,
            stages.Vertex,
            stages.Fragment,
            BlendEnabled: true,
            CullBackFace: false,
            VertexBindings: [new VertexInputBindingDesc(0, (uint)Marshal.SizeOf<ImmediateVertex>())],
            VertexAttributes:
            [
                new VertexInputAttributeDesc(0, 0, VertexFormat.Float3, 0),
                new VertexInputAttributeDesc(1, 0, VertexFormat.Float2, 12),
                new VertexInputAttributeDesc(2, 0, VertexFormat.UNormR8G8B8A8, 20),
            ],
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.All, 0, (uint)Marshal.SizeOf<Push>())],
            DepthTestEnabled: batch.DepthTest,
            DepthWriteEnabled: batch.DepthTest,
            DepthCompareOp: CompareOp.LessOrEqual,
            Topology: batch.Topology);

        // Custom shaders' pipelines are kept out of the shared cache, because they are destroyed
        // when the shader is unloaded and the cache would hand them out afterward.
        var pipeline = shader == 0 && renderWorld.TryGet<PipelineCache>() is { } cache
            ? cache.GetOrCreate(desc)
            : gfx.CreateGraphicsPipeline(desc);
        return _pipelines[(shader, slot)] = pipeline;
    }

    // The stages for a batch's shader, made on first use. A shader that is not loaded falls back
    // to the engine's own, and shader is set to the id the stages belong to.
    private Stages StagesFor(IGraphicsDevice gfx, RenderWorld renderWorld, int id, out int shader)
    {
        _engineStages ??= new Stages(
            gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv)),
            gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv)));

        shader = 0;
        if (id == 0) return _engineStages;
        if (_customStages.TryGetValue(id, out var custom))
        {
            shader = id;
            return custom;
        }
        if (renderWorld.TryGet<ShaderStore>()?.Get(id) is not { } program) return _engineStages;

        var vertex = program.Stages.TryGetValue(ShaderStage.Vertex, out var vs) ? vs : _vertexSpv.ToArray();
        custom = new Stages(
            gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, vertex)),
            gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, program.Fragment)));
        _customStages[id] = custom;
        shader = id;
        return custom;
    }

    // An unloaded shader's stages and pipelines may still be in use by a frame in flight, so they
    // are destroyed GpuTextures.RetireFrames frames after the shader is unloaded.
    private void RetireUnloadedShaders(ShaderStore? store)
    {
        _frame++;
        if (store is not null)
        {
            foreach (var id in store.TakeRemovals())
            {
                if (_customStages.Remove(id, out var stages)) _retired.Add((_frame, stages));
                foreach (var key in _pipelines.Keys.Where(k => k.Shader == id).ToList())
                {
                    if (_pipelines.Remove(key, out var pipeline) && pipeline is IDisposable disposable)
                        _retired.Add((_frame, disposable));
                }
            }
        }

        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < GpuTextures.RetireFrames) continue;
            _retired[i].Stages.Dispose();
            _retired.RemoveAt(i);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, disposable) in _retired) disposable.Dispose();
        foreach (var ((shader, _), pipeline) in _pipelines)
            if (shader != 0 && pipeline is IDisposable disposable) disposable.Dispose();
        foreach (var stages in _customStages.Values) stages.Dispose();
        _engineStages?.Dispose();
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
