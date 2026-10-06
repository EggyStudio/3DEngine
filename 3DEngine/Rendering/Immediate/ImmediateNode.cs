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
/// Every vertex and index of the frame is written into the per-frame buffer arena in one copy each by
/// <see cref="Upload"/>, before the graph runs, because the batches for render targets are drawn
/// by <see cref="TargetsNode"/> before the window's are drawn by <see cref="ImmediateNode"/>, and
/// both read the same buffer.
/// </para>
/// <para>
/// Each batch is one draw call with its transform and its shader's four values as push constants
/// and its texture, from <see cref="GpuTextures"/>, as the descriptor set. A batch whose shader
/// declares uniforms or textures of its own gets a set of its own instead, holding the uniforms'
/// values as they were recorded at binding 0 beside the texture, and the shader's own textures at
/// the bindings Slang gave them, from a ring kept for each frame in flight. A shader with textures
/// of its own has a descriptor layout of its own to match. A batch recorded inside
/// <c>BeginShaderMode</c> draws with that shader's stages, from <see cref="ShaderStore"/>. Culling is off, so a shape's triangles may wind
/// either way. Render targets have render passes compatible with the window's, so the same
/// pipelines draw into both.
/// </para>
/// </remarks>
internal sealed class ImmediateRenderer : IDisposable
{
    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;
    private readonly ReadOnlyMemory<byte> _linearFragmentSpv;
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
    private Stages? _linearStages;
    private readonly Dictionary<int, Stages> _customStages = [];
    private readonly Dictionary<(int Shader, int Slot, BlendMode Blend, CullMode Cull, IRenderPass Pass), IPipeline> _pipelines = [];
    private readonly List<(long Frame, IDisposable Stages)> _retired = [];
    private long _frame;
    private DynamicAllocation? _vertices;
    private DynamicAllocation? _indices;

    // Sets of batches with uniforms of their own, a list per frame slot, handed out in order each
    // frame and reused when the slot comes round, once the GPU is done with that frame. A shader
    // with textures of its own has its own layout and its own ring, by shader id.
    private readonly List<IDescriptorSet>[] _uniformSets = Enumerable.Range(0, GpuTextures.RetireFrames).Select(_ => new List<IDescriptorSet>()).ToArray();
    private int _uniformSetNext;
    private readonly Dictionary<int, ShaderSets> _shaderSets = [];

    // The texture the pass fills itself, engine.slang's, at binding 1. Any other a shader samples is its own.
    private static readonly string[] PassTextures = ["boundTexture"];

    private sealed class ShaderSets(IDescriptorSetLayout layout) : IDisposable
    {
        public IDescriptorSetLayout Layout { get; } = layout;
        public List<IDescriptorSet>[] Rings { get; } = Enumerable.Range(0, GpuTextures.RetireFrames).Select(_ => new List<IDescriptorSet>()).ToArray();
        public int Next;

        public void Dispose()
        {
            foreach (var ring in Rings)
                foreach (var set in ring) set.Dispose();
            Layout.Dispose();
        }
    }

    /// <summary>
    /// Creates the renderer from the compiled stages of <c>immediate.slang</c>, and the fragment
    /// stage of <c>immediate_linear.slang</c> that draws into the HDR frame, which the fragment stage
    /// given first stands in for when there is none.
    /// </summary>
    public ImmediateRenderer(ReadOnlyMemory<byte> vertexSpv, ReadOnlyMemory<byte> fragmentSpv, ReadOnlyMemory<byte> linearFragmentSpv = default)
    {
        _vertexSpv = vertexSpv;
        _fragmentSpv = fragmentSpv;
        _linearFragmentSpv = linearFragmentSpv.IsEmpty ? fragmentSpv : linearFragmentSpv;
    }

    /// <summary>Writes the frame's vertices and indices into the buffer arena, or forgets last frame's when there are none.</summary>
    public void Upload(RenderContext renderContext, RenderWorld renderWorld)
    {
        _vertices = null;
        _indices = null;
        RetireUnloadedShaders(renderWorld.TryGet<ShaderStore>());
        _uniformSetNext = 0;
        foreach (var sets in _shaderSets.Values) sets.Next = 0;
        var drawList = renderWorld.TryGet<DrawList>();
        if (drawList is null || drawList.Vertices.IsEmpty || renderContext.DynamicAllocator is not { } allocator) return;

        _vertices = Copy(allocator, MemoryMarshal.AsBytes(drawList.Vertices), BufferUsage.Vertex);
        _indices = Copy(allocator, MemoryMarshal.AsBytes(drawList.Indices), BufferUsage.Index);
    }

    private static DynamicAllocation Copy(DynamicBufferAllocator allocator, ReadOnlySpan<byte> bytes, BufferUsage usage)
    {
        var allocation = allocator.Allocate((ulong)bytes.Length, usage);
        bytes.CopyTo(allocator.Map(allocation));
        allocator.Unmap(allocation);
        return allocation;
    }

    /// <summary>Draws the batches meant for <paramref name="target"/> into <paramref name="pass"/>, of those from <paramref name="first"/> up to <paramref name="end"/>.</summary>
    /// <remarks>
    /// With <paramref name="linear"/> the engine's own shader decodes the colors to linear light, for
    /// the HDR frame. A shader of the program's own draws as it is, so its colors are read as linear
    /// there.
    /// </remarks>
    public void Draw(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, RenderWorld renderWorld, int target,
        int first = 0, int end = int.MaxValue, bool linear = false)
    {
        var drawList = renderWorld.TryGet<DrawList>();
        var textures = renderWorld.TryGet<GpuTextures>();
        if (drawList is null || textures is null || _vertices is not { } vertices || _indices is not { } indices) return;

        var gfx = renderContext.Device;
        var bound = false;
        ScissorRect? scissor = null;
        var batches = drawList.Batches;
        for (int i = first; i < Math.Min(end, batches.Count); i++)
        {
            var batch = batches[i];
            if (batch.Target != target) continue;
            if (!bound)
            {
                pass.SetVertexBuffer(0, [vertices.Buffer], [vertices.Offset]);
                pass.SetIndexBuffer(indices.Buffer, indices.Offset, IndexType.UInt32);
                bound = true;
            }
            if (batch.Scissor != scissor)
            {
                scissor = batch.Scissor;
                SetScissor(pass, scissor);
            }

            // A batch whose shader was unloaded after it was recorded draws with the engine's own.
            var pipeline = Pipeline(gfx, renderPass, renderWorld, batch, linear);
            pass.SetPipeline(pipeline);
            pass.SetBindGroup(pipeline, UniformSet(gfx, renderContext, renderWorld, textures, batch) ?? textures.SetFor(gfx, batch.Texture));

            var push = new Push { Transform = batch.Transform, Params = batch.Params };
            pass.PushConstants(pipeline, ShaderStageFlags.All, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
            pass.DrawIndexed((uint)batch.IndexCount, 1, (uint)batch.FirstIndex, 0, 0);
        }
        // The passes drawn after in the same render pass expect the whole target.
        if (scissor is not null) SetScissor(pass, null);
    }

    // Keeps drawing to a rectangle clipped to the pass, or to the whole of it for null.
    private static void SetScissor(TrackedRenderPass pass, ScissorRect? scissor)
    {
        var (width, height) = ((int)pass.Extent.Width, (int)pass.Extent.Height);
        if (scissor is not { } r)
        {
            pass.SetScissor(0, 0, (uint)width, (uint)height);
            return;
        }
        var x0 = Math.Clamp(r.X, 0, width);
        var y0 = Math.Clamp(r.Y, 0, height);
        var x1 = Math.Clamp(r.X + Math.Max(0, r.Width), 0, width);
        var y1 = Math.Clamp(r.Y + Math.Max(0, r.Height), 0, height);
        pass.SetScissor(x0, y0, (uint)(x1 - x0), (uint)(y1 - y0));
    }

    // A set with the batch's uniform values at binding 0, its texture at binding 1 and its shader's
    // own textures and storage buffers after, or null for a batch whose shader declares none of
    // them, which binds its texture's own set.
    private IDescriptorSet? UniformSet(IGraphicsDevice gfx, RenderContext renderContext, RenderWorld renderWorld, GpuTextures textures, DrawBatch batch)
    {
        if (batch.Shader == 0 || renderWorld.TryGet<ShaderStore>()?.Get(batch.Shader) is not { } program
            || renderContext.DynamicAllocator is not { } allocator)
            return null;
        var own = program.OwnTextures(PassTextures);
        if (program.UniformSize == 0 && own.Count == 0 && program.Buffers.Count == 0) return null;

        IDescriptorSet set;
        var slot = (int)(_frame % _uniformSets.Length);
        if (own.Count == 0 && program.Buffers.Count == 0)
        {
            var sets = _uniformSets[slot];
            if (_uniformSetNext == sets.Count) sets.Add(gfx.CreateDescriptorSet());
            set = sets[_uniformSetNext++];
        }
        else
        {
            var shaderSets = SetsFor(gfx, batch.Shader, program);
            var ring = shaderSets.Rings[slot];
            if (shaderSets.Next == ring.Count) ring.Add(gfx.CreateDescriptorSet(shaderSets.Layout));
            set = ring[shaderSets.Next++];
        }

        // A buffer at binding 0, at least one 16-byte row, unless a texture or storage buffer of the
        // shader's own has the binding, as it does in a shader with no uniforms.
        UniformBufferBinding? uniforms = null;
        if (!ZeroTaken(program, own))
        {
            var size = (ulong)Math.Max(16, program.UniformSize);
            var allocation = allocator.Allocate(size, BufferUsage.Uniform);
            var bytes = allocator.Map(allocation);
            bytes.Clear();
            batch.Uniforms?.AsSpan(0, Math.Min(batch.Uniforms.Length, bytes.Length)).CopyTo(bytes);
            allocator.Unmap(allocation);
            uniforms = new UniformBufferBinding(allocation.Buffer, 0, allocation.Offset, size);
        }

        var (view, sampler) = textures.ViewFor(gfx, batch.Texture);
        gfx.UpdateDescriptorSet(set, uniforms, new CombinedImageSamplerBinding(view, sampler, 1));
        foreach (var texture in own)
        {
            var index = IndexOf(program, texture);
            var id = batch.Textures is { } ids && index >= 0 && index < ids.Length ? ids[index] : 0;
            var (ownView, ownSampler) = textures.ViewFor(gfx, id);
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(ownView, ownSampler, (uint)texture.Binding));
        }
        BindBuffers(gfx, renderWorld, set, program, batch.Textures, ref _noBuffer);
        return set;
    }

    // Whether a texture or storage buffer of the shader's own has binding 0, which the uniform
    // buffer has otherwise.
    internal static bool ZeroTaken(ShaderProgram program, IReadOnlyList<ShaderTexture> own) =>
        own.Any(t => t.Binding == 0) || program.Buffers.Any(b => b.Binding == 0);

    // Bound in place of a storage buffer a draw was not given.
    private IBuffer? _noBuffer;

    // Points each storage buffer the shader reads at the one the draw was given, which follows its
    // textures in the draw's snapshot, or at a stand-in of 16 zero bytes, made once into stand, when
    // it was given none. The model pass binds its shaders' buffers the same way.
    internal static void BindBuffers(IGraphicsDevice gfx, RenderWorld renderWorld, IDescriptorSet set, ShaderProgram program, int[]? snapshot,
        ref IBuffer? stand)
    {
        if (program.Buffers.Count == 0) return;
        var store = renderWorld.TryGet<ShaderBufferStore>();
        for (int i = 0; i < program.Buffers.Count; i++)
        {
            var index = program.Textures.Count + i;
            var id = snapshot is { } ids && index < ids.Length ? ids[index] : 0;
            var buffer = store?.Get(id);
            if (buffer is null)
            {
                if (stand is null)
                {
                    stand = gfx.CreateBuffer(new BufferDesc(16, BufferUsage.Storage, CpuAccessMode.Write));
                    gfx.Map(stand).Clear();
                }
                buffer = stand;
            }
            gfx.UpdateDescriptorSet(set, new StorageBufferBinding(buffer, (uint)program.Buffers[i].Binding));
        }
    }

    private static int IndexOf(ShaderProgram program, ShaderTexture texture)
    {
        for (int i = 0; i < program.Textures.Count; i++)
            if (program.Textures[i] == texture) return i;
        return -1;
    }

    // The layout and ring of a shader with textures or storage buffers of its own, made on first use.
    private ShaderSets SetsFor(IGraphicsDevice gfx, int shader, ShaderProgram program)
    {
        if (_shaderSets.TryGetValue(shader, out var sets)) return sets;
        // The shader's set 0 as it declares it, with the batch's texture at 1, which the pass fills,
        // and the uniform buffer at 0 unless a texture or buffer of the shader's own took it.
        var bindings = ShaderProgram.Merge(program.LayoutOf(0),
            [new DescriptorSetLayoutBinding(1, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment)],
            [new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.Vertex | ShaderStageFlags.Fragment)]);
        return _shaderSets[shader] = new ShaderSets(gfx.CreateDescriptorSetLayout(bindings));
    }

    private IPipeline Pipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, DrawBatch batch, bool linear)
    {
        var slot = (batch.Topology == PrimitiveTopology.LineList ? 2 : 0) + (batch.DepthTest ? 1 : 0);
        // The HDR frame's pass is the only one drawn linear, so the pass in the key tells the two
        // engine stages apart.
        var stages = StagesFor(gfx, renderWorld, batch.Shader, linear, out var shader);
        if (_pipelines.TryGetValue((shader, slot, batch.Blend, batch.Cull, renderPass), out var existing)) return existing;

        var desc = new GraphicsPipelineDesc(
            renderPass,
            stages.Vertex,
            stages.Fragment,
            BlendEnabled: batch.Blend != DrawList.Replace,
            Cull: batch.Cull,
            VertexBindings: [new VertexInputBindingDesc(0, (uint)Marshal.SizeOf<ImmediateVertex>())],
            VertexAttributes:
            [
                new VertexInputAttributeDesc(0, 0, VertexFormat.Float3, 0),
                new VertexInputAttributeDesc(1, 0, VertexFormat.Float2, 12),
                new VertexInputAttributeDesc(2, 0, VertexFormat.UNormR8G8B8A8, 20),
            ],
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.All, 0, (uint)Marshal.SizeOf<Push>())],
            // A shader with textures or storage buffers of its own reads them through a layout of its own.
            DescriptorSetLayouts: shader != 0 && renderWorld.TryGet<ShaderStore>()?.Get(shader) is { } program
                                  && (program.OwnTextures(PassTextures).Count > 0 || program.Buffers.Count > 0)
                ? [SetsFor(gfx, shader, program).Layout]
                : null,
            DepthTestEnabled: batch.DepthTest,
            DepthWriteEnabled: batch.DepthTest,
            DepthCompareOp: CompareOp.LessOrEqual,
            Topology: batch.Topology,
            Blend: batch.Blend);

        // Custom shaders' pipelines are kept out of the shared cache, because they are destroyed
        // when the shader is unloaded and the cache would hand them out afterward.
        var pipeline = shader == 0 && renderWorld.TryGet<PipelineCache>() is { } cache
            ? cache.GetOrCreate(desc)
            : gfx.CreateGraphicsPipeline(desc);
        return _pipelines[(shader, slot, batch.Blend, batch.Cull, renderPass)] = pipeline;
    }

    // The stages for a batch's shader, made on first use. A shader that is not loaded falls back
    // to the engine's own, and shader is set to the id the stages belong to.
    private Stages StagesFor(IGraphicsDevice gfx, RenderWorld renderWorld, int id, bool linear, out int shader)
    {
        _engineStages ??= new Stages(
            gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv)),
            gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv)));

        shader = 0;
        if (id == 0 && linear)
            return _linearStages ??= new Stages(
                gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv)),
                gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _linearFragmentSpv)));
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
                if (_shaderSets.Remove(id, out var sets)) _retired.Add((_frame, sets));
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
        foreach (var ((shader, _, _, _, _), pipeline) in _pipelines)
            if (shader != 0 && pipeline is IDisposable disposable) disposable.Dispose();
        foreach (var stages in _customStages.Values) stages.Dispose();
        foreach (var sets in _uniformSets)
            foreach (var set in sets) set.Dispose();
        foreach (var sets in _shaderSets.Values) sets.Dispose();
        _engineStages?.Dispose();
        _linearStages?.Dispose();
        _noBuffer?.Dispose();
    }
}

/// <summary>Prepare system that uploads the frame's immediate vertices before the graph runs.</summary>
internal sealed class ImmediateUploadPrepare : IPrepareSystem
{
    /// <inheritdoc />
    public void Run(RenderWorld renderWorld, RenderContext renderContext) =>
        renderWorld.TryGet<ImmediateRenderer>()?.Upload(renderContext, renderWorld);
}

/// <summary>
/// Render graph node that draws the window's share of the <see cref="DrawList"/> into the swapchain
/// pass <see cref="MainPassNode"/> opened, after the meshes and before ImGui.
/// </summary>
internal sealed class ImmediateNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<ActiveSwapchainPass>() is not { } active || renderWorld.TryGet<SwapchainTarget>() is not { } swapchain) return;
        // With bloom on, the batches up to the split were drawn into the HDR frame.
        var first = renderWorld.TryGet<BloomFrame>()?.Split ?? 0;
        renderWorld.TryGet<ImmediateRenderer>()?.Draw(active.Pass, swapchain.RenderPass, renderContext, renderWorld, target: 0, first);
    }
}
