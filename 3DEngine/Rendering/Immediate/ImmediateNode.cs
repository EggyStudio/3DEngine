using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Render graph node that draws the frame's <see cref="DrawList"/> into the swapchain pass
/// <see cref="MainPassNode"/> opened, after the meshes and before ImGui, and owns the GPU side of
/// every texture in the <see cref="TextureStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Every vertex of the frame is written into the per-frame buffer arena in one copy, and each
/// <see cref="DrawBatch"/> is one draw call with its transform as a push constant and its texture
/// as the descriptor set. Untextured shapes sample a white texture of one pixel, so one shader and
/// four pipelines (lines or triangles, depth tested or not) draw everything. Culling is off, so a
/// shape's triangles may wind either way.
/// </para>
/// <para>
/// A texture that is unloaded or replaced may still be read by a frame the GPU has not finished,
/// so its objects are kept for <see cref="RetireFrames"/> frames before they are destroyed. A
/// replaced texture gets new objects rather than having its image written over for the same
/// reason.
/// </para>
/// </remarks>
public sealed class ImmediateNode : INode, IDisposable
{
    // One more than the device's frames in flight, so a frame that was recording when the texture
    // was retired has also finished.
    private const int RetireFrames = 4;

    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;

    private IShader? _vertexShader;
    private IShader? _fragmentShader;
    private readonly IPipeline?[] _pipelines = new IPipeline?[4];

    private readonly Dictionary<int, GpuTexture> _textures = [];
    private readonly List<(long Frame, IDisposable[] Objects)> _retired = [];
    private long _frame;

    private sealed record GpuTexture(IImage Image, IImageView View, ISampler Sampler, IDescriptorSet Set);

    /// <summary>Creates the node from the compiled stages of <c>immediate.slang</c>.</summary>
    public ImmediateNode(ReadOnlyMemory<byte> vertexSpv, ReadOnlyMemory<byte> fragmentSpv)
    {
        _vertexSpv = vertexSpv;
        _fragmentSpv = fragmentSpv;
    }

    /// <summary>How many textures have GPU objects, the white one included.</summary>
    public int GpuTextureCount => _textures.Count;

    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        var gfx = renderContext.Device;
        _frame++;
        ApplyTextureChanges(gfx, renderWorld.TryGet<TextureStore>());
        DestroyRetired(all: false);

        var drawList = renderWorld.TryGet<DrawList>();
        if (drawList is null || drawList.Batches.Count == 0) return;

        var activePass = renderWorld.TryGet<ActiveSwapchainPass>();
        var swapchainTarget = renderWorld.TryGet<SwapchainTarget>();
        var allocator = renderContext.DynamicAllocator;
        if (activePass is null || swapchainTarget is null || allocator is null) return;

        var vertices = MemoryMarshal.AsBytes(drawList.Vertices);
        var allocation = allocator.Allocate((ulong)vertices.Length, BufferUsage.Vertex);
        vertices.CopyTo(allocator.Map(allocation));
        allocator.Unmap(allocation);

        var pass = activePass.Pass;
        pass.SetVertexBuffer(0, [allocation.Buffer], [allocation.Offset]);

        var white = White(gfx);
        foreach (var batch in drawList.Batches)
        {
            var pipeline = Pipeline(gfx, swapchainTarget.RenderPass, renderWorld, batch);
            pass.SetPipeline(pipeline);

            // A texture unloaded after its draw was recorded draws white rather than failing.
            var texture = batch.Texture != 0 && _textures.TryGetValue(batch.Texture, out var t) ? t : white;
            pass.SetBindGroup(pipeline, texture.Set);

            var transform = batch.Transform;
            pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0,
                MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in transform)));
            pass.Draw((uint)batch.VertexCount, 1, (uint)batch.FirstVertex);
        }
    }

    private void ApplyTextureChanges(IGraphicsDevice gfx, TextureStore? store)
    {
        if (store is null) return;
        var (uploads, removals) = store.Take();

        foreach (var id in removals)
            if (_textures.Remove(id, out var gone))
                Retire(gone.Set, gone.Sampler, gone.View, gone.Image);

        foreach (var upload in uploads)
        {
            _textures.TryGetValue(upload.Id, out var existing);

            if (upload.Rgba is null)
            {
                // Only the filter changed, so the image stays and the sampler and set are new.
                if (existing is null) continue;
                var sampler = CreateSampler(gfx, upload.Filter);
                var set = CreateSet(gfx, existing.View, sampler);
                _textures[upload.Id] = existing with { Sampler = sampler, Set = set };
                Retire(existing.Set, existing.Sampler);
                continue;
            }

            _textures[upload.Id] = Create(gfx, upload.Rgba, upload.Width, upload.Height, upload.Filter);
            if (existing is not null)
                Retire(existing.Set, existing.Sampler, existing.View, existing.Image);
        }
    }

    private GpuTexture White(IGraphicsDevice gfx)
    {
        if (_textures.TryGetValue(0, out var white)) return white;
        return _textures[0] = Create(gfx, [255, 255, 255, 255], 1, 1, TextureFilter.Point);
    }

    private static GpuTexture Create(IGraphicsDevice gfx, byte[] rgba, int width, int height, TextureFilter filter)
    {
        var image = gfx.CreateImage(new ImageDesc(
            new Extent2D((uint)width, (uint)height),
            ImageFormat.R8G8B8A8_UNorm,
            ImageUsage.Sampled | ImageUsage.TransferDst));
        gfx.UploadTexture2D(image, rgba, (uint)width, (uint)height, 4);
        var view = gfx.CreateImageView(image);
        var sampler = CreateSampler(gfx, filter);
        return new GpuTexture(image, view, sampler, CreateSet(gfx, view, sampler));
    }

    private static ISampler CreateSampler(IGraphicsDevice gfx, TextureFilter filter)
    {
        var f = filter == TextureFilter.Point ? SamplerFilter.Nearest : SamplerFilter.Linear;
        return gfx.CreateSampler(new SamplerDesc(f, f,
            SamplerAddressMode.Repeat, SamplerAddressMode.Repeat, SamplerAddressMode.Repeat));
    }

    private static IDescriptorSet CreateSet(IGraphicsDevice gfx, IImageView view, ISampler sampler)
    {
        var set = gfx.CreateDescriptorSet();
        gfx.UpdateDescriptorSet(set, uniformBinding: null, new CombinedImageSamplerBinding(view, sampler, 1));
        return set;
    }

    private void Retire(params IDisposable[] objects) => _retired.Add((_frame, objects));

    private void DestroyRetired(bool all)
    {
        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (!all && _frame - _retired[i].Frame < RetireFrames) continue;
            foreach (var o in _retired[i].Objects) o.Dispose();
            _retired.RemoveAt(i);
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
        DestroyRetired(all: true);
        foreach (var texture in _textures.Values)
        {
            texture.Set.Dispose();
            texture.Sampler.Dispose();
            texture.View.Dispose();
            texture.Image.Dispose();
        }
        _textures.Clear();
        _fragmentShader?.Dispose();
        _vertexShader?.Dispose();
    }
}
