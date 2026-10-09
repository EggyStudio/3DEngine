using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

internal sealed partial class ModelRenderer
{
    // subsurface.slang's stages and the layout of its third set, the scene's depth, or empty where
    // the renderer was made without it.
    private readonly ReadOnlyMemory<byte> _subsurfaceVertexSpv, _subsurfaceFragmentSpv;
    private readonly DescriptorSetLayoutBinding[] _subsurfaceDepthBindings = [];
    private IShader? _subsurfaceVertex, _subsurfaceFragment;
    private IDescriptorSetLayout? _subsurfaceDepthLayout;
    private ISampler? _subsurfaceDepthSampler;
    // The set the scene's depth is read through, by the view it reads, made again when the scene's
    // target is made again.
    private (IImageView View, IDescriptorSet Set)? _subsurfaceDepth;
    private readonly Dictionary<(IRenderPass Pass, CullMode Cull, Streams Streams), IPipeline> _subsurfacePipelines = [];

    /// <summary>
    /// Whether the window draws a mesh this frame whose material scatters light under its surface
    /// (<see cref="ModelDraw.Subsurface"/>), a draw or a group of mesh entities.
    /// </summary>
    internal static bool ScattersInWindow(RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<ModelDrawList>() is not { } draws) return false;
        foreach (ref readonly var draw in draws.Span)
            if (draw.Target == 0 && draw.Subsurface.W > 0) return true;
        foreach (var group in draws.Groups)
            if (group.Count > 0 && group.Template.Target == 0 && group.Template.Subsurface.W > 0) return true;
        return false;
    }

    /// <summary>
    /// Draws the window's batches whose material scatters light under its surface again into
    /// <paramref name="pass"/>, of two images, each pixel's diffuse light and the material's profile,
    /// where <paramref name="sceneDepth"/>, the scene's depth, shows the surface (<c>subsurface.slang</c>).
    /// </summary>
    /// <remarks>
    /// The batches are the ones the window's model pass drew this frame, each holding draws of one
    /// profile, which a vertex buffer of one element a batch gives the call, stepped per instance
    /// with a stride of 0.
    /// </remarks>
    /// <returns>How many batches were drawn, 0 where the window draws none that scatter.</returns>
    internal int DrawSubsurface(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, RenderWorld renderWorld, IImageView sceneDepth)
    {
        var draws = renderWorld.TryGet<ModelDrawList>();
        var meshes = renderWorld.TryGet<GpuMeshes>();
        var textures = renderWorld.TryGet<GpuTextures>();
        if (_subsurfaceVertexSpv.IsEmpty || draws is null || draws.IsEmpty || meshes is null || textures is null
            || renderContext.DynamicAllocator is not { } allocator) return 0;

        var gfx = renderContext.Device;
        BeginFrameOfSets(renderContext);
        var view = ViewBatches(0, gfx, draws, meshes, textures, renderWorld.TryGet<ShaderStore>());
        static bool Scatters(in Batch batch) => batch.Subsurface.W > 0 && batch.Custom < 0 && !batch.Points;
        var marked = 0;
        foreach (var batch in view.Batches)
            if (Scatters(batch)) marked++;
        if (marked == 0) return 0;

        var profiles = allocator.Allocate((ulong)(marked * 16), BufferUsage.Vertex);
        var written = MemoryMarshal.Cast<byte, Vector4>(allocator.Map(profiles));
        var index = 0;
        foreach (var batch in view.Batches)
            if (Scatters(batch)) written[index++] = batch.Subsurface;
        allocator.Unmap(profiles);

        var depth = SubsurfaceDepthSet(gfx, sceneDepth);
        IPipeline? pipeline = null;
        var pushed = default(Matrix4x4?);
        index = 0;
        foreach (var batch in view.Batches)
        {
            if (!Scatters(batch)) continue;
            var streams = batch.Mesh.Colors is null ? Streams.Default : Streams.PerVertex;
            var wanted = SubsurfacePipeline(gfx, renderPass, renderWorld, batch.Cull, streams);
            if (!ReferenceEquals(wanted, pipeline))
            {
                pipeline = wanted;
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(pipeline, LightsSet(gfx, renderWorld, textures, 0), index: 1);
                pass.SetBindGroup(pipeline, depth, index: 2);
                pushed = null;
            }
            if (pushed != batch.ViewProjection)
            {
                pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in batch.ViewProjection)));
                pushed = batch.ViewProjection;
            }
            pass.SetBindGroup(pipeline, batch.Set ?? MaterialSet(gfx, textures, default));
            var (colors, texcoords2) = streams == Streams.PerVertex ? (batch.Mesh.Colors!, batch.Mesh.Texcoords2!) : DefaultStreams(gfx);
            pass.SetVertexBuffer(0, [batch.Mesh.Vertices, view.Ring!, colors, texcoords2, profiles.Buffer],
                [0, view.Offset, 0, 0, profiles.Offset + (ulong)(index++ * 16)]);
            pass.SetIndexBuffer(batch.Mesh.Indices, 0, IndexType.UInt32);
            DrawCalls += DrawSeen(pass, batch, view.Blocks, batch.ViewProjection);
        }
        return marked;
    }

    // The set subsurface.slang reads the scene's depth through, texel for texel.
    private IDescriptorSet SubsurfaceDepthSet(IGraphicsDevice gfx, IImageView sceneDepth)
    {
        if (_subsurfaceDepth is { } made && ReferenceEquals(made.View, sceneDepth)) return made.Set;
        if (_subsurfaceDepth is { } old) _retiredShaderSets.Add((_frames, old.Set));
        _subsurfaceDepthLayout ??= gfx.CreateDescriptorSetLayout(_subsurfaceDepthBindings);
        _subsurfaceDepthSampler ??= gfx.CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        var set = gfx.CreateDescriptorSet(_subsurfaceDepthLayout);
        gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(sceneDepth, _subsurfaceDepthSampler, 0));
        _subsurfaceDepth = (sceneDepth, set);
        return set;
    }

    // subsurface.slang's pipeline, which reads the mesh's colors and second texture coordinates
    // from the mesh or the one default, and the batch's profile from a fifth binding stepped per
    // instance with a stride of 0, at location 11. It draws with no depth, the fragment stage
    // comparing its own with the scene's, and blends nothing.
    private IPipeline SubsurfacePipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, CullMode cull, Streams streams)
    {
        if (_subsurfacePipelines.TryGetValue((renderPass, cull, streams), out var made)) return made;
        _subsurfaceVertex ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _subsurfaceVertexSpv));
        _subsurfaceFragment ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _subsurfaceFragmentSpv));
        _subsurfaceDepthLayout ??= gfx.CreateDescriptorSetLayout(_subsurfaceDepthBindings);
        var desc = new GraphicsPipelineDesc(
            renderPass,
            _subsurfaceVertex,
            _subsurfaceFragment,
            BlendEnabled: false,
            Cull: cull,
            VertexBindings:
            [
                new VertexInputBindingDesc(0, (uint)Marshal.SizeOf<ModelVertex>()),
                new VertexInputBindingDesc(1, Instance.Size, PerInstance: true),
                new VertexInputBindingDesc(2, streams == Streams.PerVertex ? 4u : 0u),
                new VertexInputBindingDesc(3, streams == Streams.PerVertex ? 8u : 0u),
                new VertexInputBindingDesc(4, 0, PerInstance: true),
            ],
            VertexAttributes: [.. Placed(6, streams, [], null, null), new VertexInputAttributeDesc(11, 4, VertexFormat.Float4, 0)],
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Vertex, 0, 64)],
            DescriptorSetLayouts: [MaterialLayout(gfx), LightsLayout(gfx), _subsurfaceDepthLayout],
            DepthTestEnabled: false,
            DepthWriteEnabled: false);
        return _subsurfacePipelines[(renderPass, cull, streams)] = renderWorld.TryGet<PipelineCache>() is { } cache
            ? cache.GetOrCreate(desc)
            : gfx.CreateGraphicsPipeline(desc);
    }

    private void DisposeSubsurface()
    {
        _subsurfaceDepth?.Set.Dispose();
        _subsurfaceDepth = null;
        _subsurfaceDepthSampler?.Dispose();
        _subsurfaceDepthLayout?.Dispose();
        _subsurfaceVertex?.Dispose();
        _subsurfaceFragment?.Dispose();
    }
}
