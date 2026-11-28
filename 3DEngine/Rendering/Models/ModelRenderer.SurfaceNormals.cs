using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

internal sealed partial class ModelRenderer
{
    // surface_normals.slang's stages and the layouts of its second set, the lighting buffer alone,
    // and its third, the depth, or empty where the renderer was made without it.
    private readonly ReadOnlyMemory<byte> _normalsVertexSpv, _normalsFragmentSpv;
    private readonly DescriptorSetLayoutBinding[] _normalsLightsBindings = [], _normalsDepthBindings = [];
    private IShader? _normalsVertex, _normalsFragment;
    private IDescriptorSetLayout? _normalsLightsLayout, _normalsDepthLayout;
    // The sets the lighting buffer is read through, a list a frame in flight whose sets a frame
    // hands out to its views in turn, with the frame the last were handed out in, its list and how
    // many of its sets it has handed out.
    private readonly List<List<IDescriptorSet>> _normalsLights = [];
    private (long Frame, int Slot, int Used) _normalsLightsAt = (-1, 0, 0);
    // The sets each depth is read through, by the depth's view, with the frame each was last read
    // in, a set no frame in flight reads retired once its depth is read no more.
    private readonly Dictionary<IImageView, (IDescriptorSet Set, long Read)> _normalsDepths = [];
    private readonly Dictionary<(IRenderPass Pass, CullMode Cull, Streams Streams), IPipeline> _normalsPipelines = [];

    /// <summary>
    /// Draws the normals the model pass shades the meshes of <paramref name="target"/> with, the
    /// window's unless another is named, into <paramref name="normals"/>, where
    /// <paramref name="depth"/>, as <see cref="DrawDepth"/> drew it through the same cameras each
    /// followed by <paramref name="back"/>, shows their surface (<c>surface_normals.slang</c>), and
    /// clears every other texel to nothing. Of the fragments that lie near the depth at a texel the
    /// one nearest it is kept, in the depth <paramref name="normals"/> holds.
    /// </summary>
    /// <remarks>
    /// A mesh drawn through a program's shader, of points or of positions alone is left out, as
    /// one that casts no shadow is from the depth, so its texels hold nothing and a pass that reads
    /// them takes the normal the depth gives. A see-through one is left out too, so the surface it
    /// lies on, which the model pass shades by its own normal and blends it over, keeps that
    /// normal.
    /// </remarks>
    internal void DrawSurfaceNormals(RenderContext renderContext, RenderWorld renderWorld, RenderTarget normals, IImageView depth, int target = 0,
        Matrix4x4? back = null)
    {
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            normals.RenderPass, normals.Framebuffer, normals.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(0, 0, 0, 0)));
        var draws = renderWorld.TryGet<ModelDrawList>();
        var meshes = renderWorld.TryGet<GpuMeshes>();
        var textures = renderWorld.TryGet<GpuTextures>();
        if (_normalsVertexSpv.IsEmpty || draws is null || draws.IsEmpty || meshes is null || textures is null) return;

        var gfx = renderContext.Device;
        BeginFrameOfSets(renderContext);
        var view = ViewBatches(target, gfx, draws, meshes, textures, renderWorld.TryGet<ShaderStore>(), renderWorld.TryGet<RenderOrigin>());
        pass.SetViewport(0, 0, normals.Extent.Width, normals.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, normals.Extent.Width, normals.Extent.Height);
        var set = NormalsDepthSet(gfx, depth);
        var lights = NormalsLightsSet(gfx, renderWorld, textures, target);
        IPipeline? pipeline = null;
        // The pass pushes its first view whatever the last pass pushed.
        _viewPushed = false;
        Frustum? frustum = null;
        foreach (ref readonly var batch in CollectionsMarshal.AsSpan(view.Batches))
        {
            // A see-through batch is one the depth draws through the masked stage though its
            // material cuts nothing out.
            if (batch.Shadow == ShadowKind.None || IsAlone(batch.Shadow) || IsMasked(batch.Shadow) && !batch.Masked || batch.Custom >= 0 || batch.Points || batch.Mesh.PositionsOnly) continue;
            if (frustum is null || !frustum.IsOf(batch.ViewProjection)) frustum = new Frustum(Placed(batch.ViewProjection), batch.ViewProjection, depth: false);
            if (!frustum.SeesAny(batch, view.Blocks)) continue;
            var streams = batch.Mesh.Colors is null ? Streams.Default : Streams.PerVertex;
            var wanted = NormalsPipeline(gfx, normals.RenderPass, renderWorld, batch.Cull, streams);
            if (!ReferenceEquals(wanted, pipeline))
            {
                pipeline = wanted;
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(pipeline, lights, index: 1);
                pass.SetBindGroup(pipeline, set, index: 2);
            }
            // Without temporal antialiasing's move where back takes it out, as the depth was drawn.
            PushView(pass, pipeline, batch.ViewProjection, back);
            pass.SetBindGroup(pipeline, batch.Set ?? MaterialSet(gfx, textures, NoDraw));
            var (colors, texcoords2) = streams == Streams.PerVertex ? (batch.Mesh.Colors!, batch.Mesh.Texcoords2!) : DefaultStreams(gfx);
            pass.SetVertexBuffer(0, [batch.Mesh.Vertices, view.Ring!, colors, texcoords2], [0, view.Offset, 0, 0]);
            if (batch.Mesh.Counted is null) pass.SetIndexBuffer(batch.Mesh.Indices, 0, batch.Mesh.IndexType);
            Count("normals", DrawSeen(pass, batch, view.Blocks, frustum));
        }
    }

    // The set surface_normals.slang reads the view's lighting buffer through, whose flags say
    // whether the materials' normal maps bend the normals, the buffer the model pass reads for the
    // view. The model pass's own set of a view is made once a frame with the occlusion and indirect
    // light the passes after the depth work out, so this pass takes the buffer alone.
    private IDescriptorSet NormalsLightsSet(IGraphicsDevice gfx, RenderWorld renderWorld, GpuTextures textures, int target)
    {
        UniformBufferBinding binding;
        if (renderWorld.TryGet<FrameLightingBinding>() is { } frame && !Unlit(renderWorld, frame, target))
            binding = LightsOf(renderWorld, frame, target).Binding;
        else
        {
            // The empty buffer the model pass reads where nothing lights the view, made with its
            // set.
            LightsSet(gfx, renderWorld, textures, target);
            binding = new UniformBufferBinding(_noLightsBuffer!, 0, 0, (ulong)LightingUboPacker.SizeBytes);
        }
        if (_lastContextFrame != _normalsLightsAt.Frame)
            _normalsLightsAt = (_lastContextFrame, (_normalsLightsAt.Slot + 1) % Math.Max(1, gfx.FramesInFlight), 0);
        while (_normalsLights.Count <= _normalsLightsAt.Slot) _normalsLights.Add([]);
        var slot = _normalsLights[_normalsLightsAt.Slot];
        _normalsLightsLayout ??= gfx.CreateDescriptorSetLayout(_normalsLightsBindings);
        if (slot.Count <= _normalsLightsAt.Used) slot.Add(gfx.CreateDescriptorSet(_normalsLightsLayout));
        var set = slot[_normalsLightsAt.Used++];
        gfx.UpdateDescriptorSet(set, binding, null);
        return set;
    }

    // The set surface_normals.slang reads depth through, texel for texel, made the first time the
    // depth is read, and those of depths not read since the frames in flight began retired.
    private IDescriptorSet NormalsDepthSet(IGraphicsDevice gfx, IImageView depth)
    {
        foreach (var (view, (stale, read)) in _normalsDepths)
            if (read < _frames - SetRingFrames && _normalsDepths.Remove(view)) _retiredShaderSets.Add((_frames, stale));
        if (_normalsDepths.TryGetValue(depth, out var made))
        {
            _normalsDepths[depth] = (made.Set, _frames);
            return made.Set;
        }
        _normalsDepthLayout ??= gfx.CreateDescriptorSetLayout(_normalsDepthBindings);
        // The subsurface pass's sampler of its depth, which reads the nearest texel as this one
        // does.
        _subsurfaceDepthSampler ??= gfx.CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        var set = gfx.CreateDescriptorSet(_normalsDepthLayout);
        gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(depth, _subsurfaceDepthSampler, 0));
        _normalsDepths[depth] = (set, _frames);
        return set;
    }

    // surface_normals.slang's pipeline, which reads the mesh's colors and second texture
    // coordinates from the mesh or the one default as the model pass does, drawing faces' backs as
    // the model pass's pipeline of the cull does. The fragment stage compares its depth with the
    // window's and writes how far apart they lie as the depth this pass tests, keeping the least
    // at each texel, and it blends nothing.
    private IPipeline NormalsPipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, CullMode cull, Streams streams)
    {
        if (_normalsPipelines.TryGetValue((renderPass, cull, streams), out var made)) return made;
        _normalsVertex ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _normalsVertexSpv));
        _normalsFragment ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _normalsFragmentSpv));
        _normalsLightsLayout ??= gfx.CreateDescriptorSetLayout(_normalsLightsBindings);
        _normalsDepthLayout ??= gfx.CreateDescriptorSetLayout(_normalsDepthBindings);
        var desc = new GraphicsPipelineDesc(
            renderPass,
            _normalsVertex,
            _normalsFragment,
            BlendEnabled: false,
            Cull: cull,
            VertexBindings:
            [
                new VertexInputBindingDesc(0, (uint)Marshal.SizeOf<ModelVertex>()),
                new VertexInputBindingDesc(1, Instance.Size, PerInstance: true),
                new VertexInputBindingDesc(2, streams == Streams.PerVertex ? 4u : 0u),
                new VertexInputBindingDesc(3, streams == Streams.PerVertex ? 8u : 0u),
            ],
            VertexAttributes: [.. Placed(6, streams, [], null, null)],
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Vertex | ShaderStageFlags.Fragment, 0, ViewPush.Size)],
            DescriptorSetLayouts: [MaterialLayout(gfx), _normalsLightsLayout, _normalsDepthLayout],
            DepthTestEnabled: true,
            DepthWriteEnabled: true,
            DepthCompareOp: CompareOp.Less,
            DrawsBacks: cull != CullMode.Back);
        return _normalsPipelines[(renderPass, cull, streams)] = renderWorld.TryGet<PipelineCache>() is { } cache
            ? cache.GetOrCreate(desc)
            : gfx.CreateGraphicsPipeline(desc);
    }

    private void DisposeSurfaceNormals()
    {
        foreach (var (set, _) in _normalsDepths.Values) set.Dispose();
        _normalsDepths.Clear();
        foreach (var set in _normalsLights.SelectMany(slot => slot)) set.Dispose();
        _normalsLights.Clear();
        _normalsLightsLayout?.Dispose();
        _normalsDepthLayout?.Dispose();
        _normalsVertex?.Dispose();
        _normalsFragment?.Dispose();
    }
}
