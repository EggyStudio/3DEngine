using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Draws the frame's <see cref="ModelDrawList"/> with <c>model.slang</c>: holds the pipeline, and
/// draws the meshes meant for one target into whichever pass is open.
/// </summary>
/// <remarks>
/// <para>
/// Each draw binds its mesh's buffers from <see cref="GpuMeshes"/> and its texture from
/// <see cref="GpuTextures"/>, and pushes its transform, its world matrix as a 3x4 and its color.
/// The frame's lights, packed by <see cref="LightingUboPrepare"/>, are bound once per pass as a
/// second descriptor set.
/// </para>
/// <para>
/// A draw whose material has a shader of the program's own is drawn with a pipeline made from that
/// shader, its vertex stage or <c>model.slang</c>'s when it has none, and a descriptor set of its
/// own holding its uniform values, copied when the draw was recorded, beside its texture. Those
/// sets come from a ring per frame in flight, reused once the GPU is done with that frame.
/// </para>
/// <para>
/// The frame's directional shadow is drawn by <see cref="DrawShadow"/> into a <see cref="ShadowMap"/>
/// with <c>model.slang</c>'s vertex stage and no fragment stage, so a model shader with a vertex
/// stage of its own casts the shadow of its mesh as it was before that stage moved it. The map
/// is bound beside the lights, or the white texture in its place in a frame with no shadow.
/// </para>
/// <para>
/// The pipelines do not cull, because a model loaded from a file may wind its
/// triangles either way, and the cost is small next to drawing a back face wrong.
/// </para>
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
    }

    /// <summary>A draw's material factors as <c>modelpass.slang</c>'s <c>MaterialFactors</c> block lays them out, 48 bytes.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct MaterialFactors(Vector4 Color, Vector4 Emission, float Metallic, float Roughness, float NormalScale, float OcclusionStrength)
    {
        public const int Size = 48;

        /// <summary>A draw's factors, its color decoded from sRGB to linear.</summary>
        public static MaterialFactors Of(in ModelDraw draw)
        {
            static float Linear(byte value)
            {
                var c = value / 255f;
                return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
            }
            var color = draw.Color;
            // No map, no bending, which also keeps the shader from reading the white texture in
            // its place as a normal.
            return new MaterialFactors(
                new Vector4(Linear(color.R), Linear(color.G), Linear(color.B), color.A / 255f),
                new Vector4(draw.Emission, 0),
                draw.Metallic, draw.Roughness, draw.NormalMap == 0 ? 0 : draw.NormalScale, draw.OcclusionStrength);
        }
    }

    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;
    private IShader? _vertexShader;
    private IShader? _fragmentShader;
    private IPipeline? _pipeline;
    private IDescriptorSetLayout? _defaultLayout;
    private IDescriptorSetLayout? _materialLayout;
    private IBuffer? _noUniforms;

    // Sets of the model pass's own draws, one per material, which is its five maps (a base color
    // texture, a normal map, a metallic-roughness map, an emissive map and an occlusion map) by
    // their views and its factors, with a buffer of the factors written once, since the key fixes
    // them, and the frame the set was last bound in. A set unbound for RetireFrames frames is freed
    // with its buffer, since no frame in flight can read it, so the views of unloaded textures and
    // the materials of past frames do not hold sets forever.
    private readonly Dictionary<((IImageView, IImageView, IImageView, IImageView, IImageView) Maps, MaterialFactors Factors),
        (IDescriptorSet Set, IBuffer Factors, long Used)> _materialSets = [];
    private long _frames;
    private readonly List<IDescriptorSet> _lightSets = [];
    private int _lightSet;
    private FrameLightingBinding? _lastFrame;
    private IDescriptorSet? _noLights;
    private IBuffer? _noLightsBuffer;
    private ShadowMap? _shadowMap;
    private IPipeline? _shadowPipeline;

    // Pipelines of the program's own shaders, by ShaderStore id, with the modules they were made from.
    private readonly Dictionary<int, (IShader Vertex, IShader Fragment, IPipeline Pipeline)> _custom = [];

    // Descriptor sets for draws with a shader of their own: a list per frame slot, handed out in
    // order each frame and kept for the next time the slot comes round.
    private const int SetRingFrames = GpuTextures.RetireFrames;
    private readonly List<IDescriptorSet>[] _drawSets = Enumerable.Range(0, SetRingFrames).Select(_ => new List<IDescriptorSet>()).ToArray();
    private int _drawSetSlot;
    private int _drawSetNext;
    private RenderContext? _lastContext;

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
        var store = renderWorld.TryGet<ShaderStore>();
        BeginFrameOfSets(renderContext);
        RetireUnloadedShaders(store);

        foreach (var draw in draws.Draws)
        {
            // A mesh unloaded after its draw was recorded is skipped.
            if (draw.Target != target || meshes.Get(draw.Mesh) is not { } mesh) continue;

            // A shader unloaded after the draw was recorded draws with the model pass's own.
            var program = draw.Shader != 0 ? store?.Get(draw.Shader) : null;
            var wanted = program is null
                ? Pipeline(gfx, renderPass, renderWorld)
                : CustomPipeline(gfx, renderPass, renderWorld, draw.Shader, program);
            if (!ReferenceEquals(wanted, pipeline))
            {
                pipeline = wanted;
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(pipeline, LightsSet(gfx, renderWorld, textures), index: 1);
            }

            pass.SetBindGroup(pipeline, program is null
                ? MaterialSet(gfx, textures, draw)
                : DrawSet(gfx, renderContext, textures, draw, program));
            pass.SetVertexBuffer(0, [mesh.Vertices], [0]);
            pass.SetIndexBuffer(mesh.Indices, 0, IndexType.UInt32);

            var w = draw.World;
            var push = new Push
            {
                Transform = w * draw.ViewProjection,
                WorldX = new Vector4(w.M11, w.M21, w.M31, w.M41),
                WorldY = new Vector4(w.M12, w.M22, w.M32, w.M42),
                WorldZ = new Vector4(w.M13, w.M23, w.M33, w.M43),
            };
            pass.PushConstants(pipeline, ShaderStageFlags.All, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
            pass.DrawIndexed(mesh.IndexCount);
        }
    }

    /// <summary>Draws the window's meshes into the shadow map, as <paramref name="shadow"/>'s light sees them.</summary>
    public void DrawShadow(RenderContext renderContext, RenderWorld renderWorld, FrameShadow shadow)
    {
        var draws = renderWorld.TryGet<ModelDrawList>();
        var meshes = renderWorld.TryGet<GpuMeshes>();
        if (draws is null || meshes is null || renderContext.Device is not GraphicsDevice device) return;

        var map = _shadowMap ??= device.CreateShadowMap(ShadowFit.MapSize);
        if (_shadowPipeline is null)
        {
            _vertexShader ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
            _shadowPipeline = MakePipeline(device, map.RenderPass, renderWorld, _vertexShader, fragment: null);
        }

        var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            map.RenderPass, map.Framebuffer, map.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(0, 0, 0, 0)));
        pass.SetViewport(0, 0, map.Extent.Width, map.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, map.Extent.Width, map.Extent.Height);
        pass.SetPipeline(_shadowPipeline);

        foreach (var draw in draws.Draws)
        {
            if (draw.Target != 0 || meshes.Get(draw.Mesh) is not { } mesh) continue;
            pass.SetVertexBuffer(0, [mesh.Vertices], [0]);
            pass.SetIndexBuffer(mesh.Indices, 0, IndexType.UInt32);
            var push = new Push { Transform = draw.World * shadow.ViewProjection };
            pass.PushConstants(_shadowPipeline, ShaderStageFlags.All, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Push>(in push)));
            pass.DrawIndexed(mesh.IndexCount);
        }
        pass.EndRenderPass();
    }

    private IPipeline Pipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld)
    {
        if (_pipeline is not null) return _pipeline;

        _vertexShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragmentShader = gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));
        return _pipeline = MakePipeline(gfx, renderPass, renderWorld, _vertexShader, _fragmentShader);
    }

    private IPipeline CustomPipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, int id, ShaderProgram program)
    {
        if (_custom.TryGetValue(id, out var made)) return made.Pipeline;

        var vertex = gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex,
            program.Stages.TryGetValue(ShaderStage.Vertex, out var own) ? own : _vertexSpv));
        var fragment = gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, program.Fragment));
        var pipeline = MakePipeline(gfx, renderPass, renderWorld, vertex, fragment);
        _custom[id] = (vertex, fragment, pipeline);
        return pipeline;
    }

    // Frees what was made for shaders the program has unloaded. Their pipelines belong to the
    // PipelineCache, and a pipeline does not need its modules once it is made.
    private void RetireUnloadedShaders(ShaderStore? store)
    {
        if (_custom.Count == 0) return;
        foreach (var id in _custom.Keys.Where(id => store?.Get(id) is null).ToArray())
        {
            _custom[id].Vertex.Dispose();
            _custom[id].Fragment.Dispose();
            _custom.Remove(id);
        }
    }

    // Moves to the next slot of draw sets once a frame, however many targets draw in it. Each
    // frame has a RenderContext of its own.
    private void BeginFrameOfSets(RenderContext renderContext)
    {
        if (ReferenceEquals(renderContext, _lastContext)) return;
        _lastContext = renderContext;
        _drawSetSlot = (_drawSetSlot + 1) % SetRingFrames;
        _drawSetNext = 0;

        _frames++;
        if (_materialSets.Count == 0) return;
        foreach (var (key, (set, factors, used)) in _materialSets.ToArray())
            if (_frames - used > SetRingFrames)
            {
                set.Dispose();
                factors.Dispose();
                _materialSets.Remove(key);
            }
    }

    // The set of a draw with the model pass's own shader, made once per material.
    private IDescriptorSet MaterialSet(IGraphicsDevice gfx, GpuTextures textures, ModelDraw draw)
    {
        var maps = Maps(gfx, textures, draw);
        var factors = MaterialFactors.Of(draw);
        var key = ((maps[0].View, maps[1].View, maps[2].View, maps[3].View, maps[4].View), factors);
        if (_materialSets.TryGetValue(key, out var known))
        {
            _materialSets[key] = known with { Used = _frames };
            return known.Set;
        }

        var buffer = gfx.CreateBuffer(new BufferDesc(MaterialFactors.Size, BufferUsage.Uniform, CpuAccessMode.Write));
        MemoryMarshal.Write(gfx.Map(buffer), in factors);
        gfx.Unmap(buffer);

        var set = gfx.CreateDescriptorSet(MaterialLayout(gfx));
        WriteMaterial(gfx, set, NoUniforms(gfx), new UniformBufferBinding(buffer, 6, 0, MaterialFactors.Size), maps);
        _materialSets[key] = (set, buffer, _frames);
        return set;
    }

    // A draw's five maps in binding order, the colors through views that decode sRGB.
    private static (IImageView View, ISampler Sampler)[] Maps(IGraphicsDevice gfx, GpuTextures textures, in ModelDraw draw) =>
    [
        textures.ViewFor(gfx, draw.Texture, srgb: true),
        textures.ViewFor(gfx, draw.NormalMap),
        textures.ViewFor(gfx, draw.MetallicRoughnessMap),
        textures.ViewFor(gfx, draw.EmissiveMap, srgb: true),
        textures.ViewFor(gfx, draw.OcclusionMap),
    ];

    // Binding 0's uniforms, the maps at bindings 1 to 5 and the factors at binding 6. One buffer
    // and one sampler per call is what the device's update takes, so they go in one call each.
    private static void WriteMaterial(IGraphicsDevice gfx, IDescriptorSet set, UniformBufferBinding uniforms, UniformBufferBinding factors,
        (IImageView View, ISampler Sampler)[] maps)
    {
        for (int i = 0; i < maps.Length; i++)
            gfx.UpdateDescriptorSet(set, i == 0 ? uniforms : i == 1 ? factors : null,
                new CombinedImageSamplerBinding(maps[i].View, maps[i].Sampler, (uint)(i + 1)));
    }

    // Sixteen zero bytes for the uniforms binding of the model pass's own draws, which its shader
    // does not read.
    private UniformBufferBinding NoUniforms(IGraphicsDevice gfx)
    {
        if (_noUniforms is null)
        {
            _noUniforms = gfx.CreateBuffer(new BufferDesc(16, BufferUsage.Uniform, CpuAccessMode.Write));
            gfx.Map(_noUniforms).Clear();
            gfx.Unmap(_noUniforms);
        }
        return new UniformBufferBinding(_noUniforms, 0, 0, 16);
    }

    // A descriptor set for one draw with a shader of its own: its uniform values in this frame's
    // buffer at binding 0, and its texture at binding 1.
    private IDescriptorSet DrawSet(IGraphicsDevice gfx, RenderContext renderContext, GpuTextures textures, ModelDraw draw, ShaderProgram program)
    {
        var sets = _drawSets[_drawSetSlot];
        if (_drawSetNext == sets.Count) sets.Add(gfx.CreateDescriptorSet(MaterialLayout(gfx)));
        var set = sets[_drawSetNext++];

        if (renderContext.DynamicAllocator is not { } allocator) return set;

        // At least one 16-byte row, so binding 0 holds a buffer whether the shader declares uniforms or not.
        var size = (ulong)Math.Max(16, program.UniformSize);
        var allocation = allocator.Allocate(size, BufferUsage.Uniform);
        var bytes = allocator.Map(allocation);
        bytes.Clear();
        draw.Uniforms?.AsSpan(0, Math.Min(draw.Uniforms.Length, bytes.Length)).CopyTo(bytes);
        allocator.Unmap(allocation);

        var factors = MaterialFactors.Of(draw);
        var block = allocator.Allocate(MaterialFactors.Size, BufferUsage.Uniform);
        MemoryMarshal.Write(allocator.Map(block), in factors);
        allocator.Unmap(block);

        WriteMaterial(gfx, set, new UniformBufferBinding(allocation.Buffer, 0, allocation.Offset, size),
            new UniformBufferBinding(block.Buffer, 6, block.Offset, MaterialFactors.Size), Maps(gfx, textures, draw));
        return set;
    }

    private IPipeline MakePipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, IShader vertex, IShader? fragment)
    {
        var desc = new GraphicsPipelineDesc(
            renderPass,
            vertex,
            fragment,
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
            // The material's set, with uniforms at binding 0, its five maps and its factors after, then the
            // frame's lights at binding 0 of the second and the shadow map at binding 1.
            DescriptorSetLayouts: [MaterialLayout(gfx), DefaultLayout(gfx)],
            DepthTestEnabled: true,
            DepthWriteEnabled: true,
            DepthCompareOp: CompareOp.LessOrEqual);

        return renderWorld.TryGet<PipelineCache>() is { } cache
            ? cache.GetOrCreate(desc)
            : gfx.CreateGraphicsPipeline(desc);
    }

    // The lights of this frame as a descriptor set: one of a ring, a set per frame in flight so a
    // set the GPU may still read is never written, or a set over an empty buffer when there are no
    // lights, which the shader reads as "use the fixed light". Binding 1 holds the shadow map when
    // the frame has a shadow, and the white texture otherwise, so it is always valid.
    private IDescriptorSet LightsSet(IGraphicsDevice gfx, RenderWorld renderWorld, GpuTextures textures)
    {
        var (white, whiteSampler) = textures.ViewFor(gfx, 0);
        if (renderWorld.TryGet<FrameLightingBinding>() is not { LightCount: > 0 } frame)
        {
            if (_noLights is null)
            {
                _noLightsBuffer = gfx.CreateBuffer(new BufferDesc((ulong)LightingUboPacker.SizeBytes, BufferUsage.Uniform, CpuAccessMode.Write));
                var span = gfx.Map(_noLightsBuffer);
                span.Clear();
                gfx.Unmap(_noLightsBuffer);
                _noLights = gfx.CreateDescriptorSet();
                gfx.UpdateDescriptorSet(_noLights, new UniformBufferBinding(_noLightsBuffer, 0, 0, (ulong)LightingUboPacker.SizeBytes),
                    new CombinedImageSamplerBinding(white, whiteSampler, 1));
            }
            return _noLights;
        }

        // Once a frame, however many targets draw models in it.
        if (!ReferenceEquals(frame, _lastFrame))
        {
            _lastFrame = frame;
            if (_lightSets.Count < gfx.FramesInFlight) _lightSets.Add(gfx.CreateDescriptorSet());
            _lightSet = (_lightSet + 1) % _lightSets.Count;
            var shadow = renderWorld.TryGet<FrameShadow>() is not null ? _shadowMap : null;
            gfx.UpdateDescriptorSet(_lightSets[_lightSet], frame.Binding, shadow is null
                ? new CombinedImageSamplerBinding(white, whiteSampler, 1)
                : new CombinedImageSamplerBinding(shadow.DepthView, shadow.Sampler, 1));
        }
        return _lightSets[_lightSet];
    }

    private IDescriptorSetLayout MaterialLayout(IGraphicsDevice gfx) => _materialLayout ??= gfx.CreateDescriptorSetLayout(
    [
        new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.Vertex | ShaderStageFlags.Fragment),
        new DescriptorSetLayoutBinding(1, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment),
        new DescriptorSetLayoutBinding(2, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment),
        new DescriptorSetLayoutBinding(3, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment),
        new DescriptorSetLayoutBinding(4, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment),
        new DescriptorSetLayoutBinding(5, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment),
        new DescriptorSetLayoutBinding(6, DescriptorType.UniformBuffer, ShaderStageFlags.Vertex | ShaderStageFlags.Fragment),
    ]);

    private IDescriptorSetLayout DefaultLayout(IGraphicsDevice gfx) => _defaultLayout ??= gfx.CreateDescriptorSetLayout(
    [
        new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.Vertex | ShaderStageFlags.Fragment),
        new DescriptorSetLayoutBinding(1, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment),
    ]);

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var set in _lightSets) set.Dispose();
        foreach (var sets in _drawSets)
            foreach (var set in sets) set.Dispose();
        foreach (var (vertex, fragment, _) in _custom.Values)
        {
            vertex.Dispose();
            fragment.Dispose();
        }
        foreach (var (set, factors, _) in _materialSets.Values)
        {
            set.Dispose();
            factors.Dispose();
        }
        _materialSets.Clear();
        _noUniforms?.Dispose();
        _materialLayout?.Dispose();
        _noLights?.Dispose();
        _shadowMap?.Dispose();
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
