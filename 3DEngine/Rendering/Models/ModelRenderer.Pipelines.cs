using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// Which build of the model pass draws a frame, each leaving out what a frame may not hold, which
/// lavapipe compiles as it first draws with each pipeline, the whole, the one without the glow
/// lights' marches, half the fragment stage, and the one without global illumination and the
/// traced reflection besides, a fifth of it.
/// </summary>
internal enum ModelBuild
{
    /// <summary>The whole, for a frame with glow lights.</summary>
    Whole,

    /// <summary>Without the glow lights, for a frame with global illumination and none of them (<c>NO_GLOW_LIGHTS</c>).</summary>
    Lampless,

    /// <summary>Without global illumination, the traced reflection and the glow lights, for a frame without it (<c>NO_BOUNCE</c>).</summary>
    Unbounced,
}

internal sealed partial class ModelRenderer
{
    // Which faces of a draw are left out, whether it is drawn as points, blended, writes its depth
    // and cuts nothing out, no faces for a double-sided material or a pass that culls nothing and
    // otherwise the back or the front ones.
    private static (CullMode Cull, bool Points, bool Blend, bool Depth, bool Solid) FacesOf(in ModelDraw draw, bool cullBackFaces) =>
        (!cullBackFaces || draw.DoubleSided ? CullMode.None : draw.CullFront ? CullMode.Front : CullMode.Back, draw.Points, draw.ColorBlend, draw.DepthWrite,
            draw.AlphaMode == MaterialAlphaMode.Opaque);

    // The model pass's own pipeline, drawing both sides of each face or leaving the back or the
    // front ones out, filled or as points, blended or written as they are, writing its depth or
    // not, through model_streams.slang for a mesh with streams, with the build given where it was
    // made and the whole where it was not, reading positions alone for a mesh uploaded so, and
    // with its alpha test folded away for a material that cuts nothing out.
    private IPipeline? Pipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, CullMode cull = CullMode.None, bool points = false,
        bool blend = true, Streams streams = Streams.None, bool depth = true, ModelBuild build = ModelBuild.Whole, bool positions = false, bool solid = false)
    {
        var (spvs, shaders) = streams == Streams.None ? (_fragmentSpvs, _fragmentShaders) : (_streamsFragmentSpvs, _streamsFragmentShaders);
        if (spvs[(int)build] is null) build = ModelBuild.Whole;
        if (_pipelines.TryGetValue((renderPass, cull, points, blend, streams, depth, build, positions, solid), out var kept)) return kept;

        _vertexShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        var vertex = streams == Streams.None
            ? _vertexShader
            : _streamsVertexShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _streamsVertexSpv));
        var fragment = shaders[(int)build] ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, spvs[(int)build]!));
        var made = MakePipeline(gfx, renderPass, renderWorld, vertex, fragment, cull, points: points, blend: blend, streams: streams, depth: depth,
            positions: positions, solid: solid, waitingAs: (renderPass, cull, points, blend, streams, depth, build, positions, solid));
        if (made is not null) _pipelines[(renderPass, cull, points, blend, streams, depth, build, positions, solid)] = made;
        return made;
    }

    // A material's own shader's pipeline, which leaves faces out as the model pass's own does, its
    // color held to an eight-bit frame's where it draws into the HDR frame, and CUSTOM0 fed as the
    // mesh gives it.
    private IPipeline? CustomPipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, int id, ShaderProgram program,
        CullMode cull = CullMode.None, bool points = false, bool blend = true, Streams streams = Streams.None, bool depth = true, bool held = false,
        StreamFeed? feed = null, bool positions = false, bool solid = false)
    {
        if (_customPipelines.TryGetValue((id, renderPass, cull, points, blend, streams, depth, held, feed, positions, solid), out var kept)) return kept;

        var modules = CustomModules(gfx, id, program);
        var fragment = held
            ? _heldFragments.TryGetValue(id, out var heldFragment) ? heldFragment
                : _heldFragments[id] = gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, ShaderProgram.HeldToEightBits(program.Fragment)))
            : modules.Fragment;
        // A vertex stage of the shader's own is fed the inputs it takes and no others, which a
        // stage that reads less than the instance's every row would leave unread, each by its
        // semantic where the reflection names them and otherwise at the location it reads.
        var own = program.Stages.TryGetValue(ShaderStage.Vertex, out var vertex);
        var made = MakePipeline(gfx, renderPass, renderWorld, modules.Vertex, fragment, cull,
            material: program.OwnTextures(PassTextures).Count > 0 || program.Buffers.Count > 0 ? SetsFor(gfx, id, program).Layout : null, points: points,
            blend: blend, streams: streams, depth: depth, inputs: own ? ShaderProgram.InputLocationSet(vertex) : null,
            named: own ? program.VertexInputs : [], shaderName: program.Name, feed: feed, positions: positions, solid: solid,
            waitingAs: (id, renderPass, cull, points, blend, streams, depth, held, feed, positions, solid));
        if (made is not null) _customPipelines[(id, renderPass, cull, points, blend, streams, depth, held, feed, positions, solid)] = made;
        return made;
    }

    // A material's own shader's stages, whether it reads a mesh's colors and second texture
    // coordinates, a vertex stage of its own that takes an input past the mesh's vertex and its
    // instance, or a fragment stage that takes ModelStreamsOutput, which then draws with
    // model_streams.slang's vertex stage where it has none of its own, and the input its vertex
    // stage reads a mesh's stream of the program's own by, CUSTOM0, where it reads one.
    private (IShader Vertex, IShader Fragment, bool ReadsStreams, ShaderInput? Custom0) CustomModules(IGraphicsDevice gfx, int id, ShaderProgram program)
    {
        if (_custom.TryGetValue(id, out var modules)) return modules;
        var own = program.Stages.TryGetValue(ShaderStage.Vertex, out var vertex);
        var reads = own
            ? program.VertexInputs.Count > 0
                ? program.VertexInputs.Any(input => input.Semantic is "COLOR0" or "TEXCOORD1")
                : program.InputLocations(ShaderStage.Vertex) > _plainVertexInputs
            : program.InputLocations(ShaderStage.Fragment) > _plainFragmentInputs;
        ShaderInput? custom0 = own && program.VertexInputs.FirstOrDefault(input => input.Semantic == "CUSTOM0") is { Semantic: not null } found ? found : null;
        return _custom[id] = (
            gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, own ? vertex! : reads ? _streamsVertexSpv : _vertexSpv)),
            gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, program.Fragment)),
            reads,
            custom0);
    }

    // Frees what was made for shaders the program has unloaded. Their pipelines belong to the
    // PipelineCache, and a pipeline does not need its modules once it is made, so those the cache's
    // workers make are waited for first.
    private void RetireUnloadedShaders(ShaderStore? store, RenderWorld renderWorld)
    {
        if (_custom.Count == 0) return;
        foreach (var id in _custom.Keys.Where(id => store?.Get(id) is null).ToArray())
        {
            renderWorld.TryGet<PipelineCache>()?.WaitForMaking();
            _custom[id].Vertex.Dispose();
            _custom[id].Fragment.Dispose();
            _custom.Remove(id);
            if (_heldFragments.Remove(id, out var heldFragment)) heldFragment.Dispose();
            if (_shadowFragments.Remove(id, out var shadowFragment)) shadowFragment.Dispose();
            foreach (var key in _customPipelines.Keys.Where(k => k.Shader == id).ToArray()) _customPipelines.Remove(key);
            foreach (var key in _ownShadowPipelines.Keys.Where(k => k.Shader == id).ToArray()) _ownShadowPipelines.Remove(key);
            // Its sets may still be read by a frame in flight.
            if (_shaderSets.Remove(id, out var sets)) _retiredShaderSets.Add((_frames, sets));
        }
    }

    // Moves to the next slot of draw sets once a frame, however many targets draw in it. Each
    // frame has a RenderContext of its own.
    private void BeginFrameOfSets(RenderContext renderContext)
    {
        if (ReferenceEquals(renderContext, _lastContext)) return;
        _lastContext = renderContext;
        DrawCalls = 0;
        PipelinesBound = 0;
        _callsByPass.Clear();
        _setByIds.Clear();
        _drawSetsByLook.Clear();
        DrawSetsWritten = 0;
        _drawSetSlot = (_drawSetSlot + 1) % SetRingFrames;
        _drawSetNext = 0;
        foreach (var sets in _shaderSets.Values) sets.Next = 0;

        _frames++;
        _ringSlot = (int)(_frames % SetRingFrames);
        _ringCursor = 0;
        for (int i = _retiredBuffers.Count - 1; i >= 0; i--)
            if (_frames - _retiredBuffers[i].Frame > SetRingFrames)
            {
                _retiredBuffers[i].Buffer.Dispose();
                _retiredBuffers.RemoveAt(i);
            }
        for (int i = _retiredMaps.Count - 1; i >= 0; i--)
            if (_frames - _retiredMaps[i].Frame > SetRingFrames)
            {
                _retiredMaps[i].Map.Dispose();
                _retiredMaps.RemoveAt(i);
            }
        for (int i = _retiredShaderSets.Count - 1; i >= 0; i--)
            if (_frames - _retiredShaderSets[i].Frame > SetRingFrames)
            {
                _retiredShaderSets[i].Retired.Dispose();
                _retiredShaderSets.RemoveAt(i);
            }
        for (int i = _retiredCubes.Count - 1; i >= 0; i--)
            if (_frames - _retiredCubes[i].Frame > SetRingFrames)
            {
                _retiredCubes[i].Cube.Dispose();
                _retiredCubes.RemoveAt(i);
            }
        if (_materialSets.Count == 0) return;
        foreach (var (key, (set, used)) in _materialSets.ToArray())
            if (_frames - used > SetRingFrames)
            {
                set.Dispose();
                _materialSets.Remove(key);
            }
    }

    // The set of a draw with the model pass's own shader, made once per combination of maps. Within
    // a frame the same five texture ids give the same views, so most draws find their set by the
    // ids without looking the views up.
    private IDescriptorSet MaterialSet(IGraphicsDevice gfx, GpuTextures textures, in ModelDraw draw)
    {
        var ids = (draw.Texture, draw.NormalMap, draw.MetallicRoughnessMap, draw.EmissiveMap, draw.OcclusionMap);
        if (_setByIds.TryGetValue(ids, out var found)) return found;
        return _setByIds[ids] = MaterialSetByViews(gfx, textures, draw);
    }

    private IDescriptorSet MaterialSetByViews(IGraphicsDevice gfx, GpuTextures textures, ModelDraw draw)
    {
        var maps = Maps(gfx, textures, draw);
        var key = new MapsKey(maps[0].View, maps[0].Sampler, maps[1].View, maps[1].Sampler, maps[2].View, maps[2].Sampler,
            maps[3].View, maps[3].Sampler, maps[4].View, maps[4].Sampler);
        if (_materialSets.TryGetValue(key, out var known))
        {
            _materialSets[key] = known with { Used = _frames };
            return known.Set;
        }

        var set = gfx.CreateDescriptorSet(MaterialLayout(gfx));
        WriteMaterial(gfx, set, NoUniforms(gfx), maps);
        _materialSets[key] = (set, _frames);
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

    // Binding 0's uniforms and the maps at bindings 1 to 5. One buffer and one sampler per call is
    // what the device's update takes, so the uniforms go with the first map.
    private static void WriteMaterial(IGraphicsDevice gfx, IDescriptorSet set, UniformBufferBinding? uniforms,
        (IImageView View, ISampler Sampler)[] maps)
    {
        for (int i = 0; i < maps.Length; i++)
            gfx.UpdateDescriptorSet(set, i == 0 ? uniforms : null,
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

    // This frame's sets of the draws with a shader of their own, by the shader, the values and the
    // shader's own textures the draw was given and its five maps, so the draws of one look share a
    // set across the frame's passes. Made and written whole for each draw in each pass, 2.8 to 3.2
    // µs a set, the voxel game's sections, every one holding the same values, took 2,041 to 2,047
    // sets a frame at 12 columns, 5.6 to 6.5 ms of the four passes' recording on the main thread.
    // An entity's values are one copy a frame for every entity drawn with the shader and a call's a
    // copy of its own, so the copies themselves tell the values apart.
    private readonly Dictionary<(int Shader, byte[]? Values, int[]? Own, (int, int, int, int, int) Maps), IDescriptorSet> _drawSetsByLook = [];

    /// <summary>How many sets this frame's draws with a shader of their own were written into, one a look.</summary>
    internal int DrawSetsWritten { get; private set; }

    // A descriptor set for the draws with a shader of their own of one look, its uniform values in
    // this frame's buffer at binding 0 and its texture at binding 1.
    private IDescriptorSet DrawSet(IGraphicsDevice gfx, RenderContext renderContext, RenderWorld renderWorld, GpuTextures textures, ModelDraw draw, ShaderProgram program)
    {
        var look = (draw.Shader, draw.Uniforms, draw.ShaderTextures, (draw.Texture, draw.NormalMap, draw.MetallicRoughnessMap, draw.EmissiveMap, draw.OcclusionMap));
        if (_drawSetsByLook.TryGetValue(look, out var shared)) return shared;
        var own = program.OwnTextures(PassTextures);
        IDescriptorSet set;
        if (own.Count == 0 && program.Buffers.Count == 0)
        {
            var sets = _drawSets[_drawSetSlot];
            if (_drawSetNext == sets.Count) sets.Add(gfx.CreateDescriptorSet(MaterialLayout(gfx)));
            set = sets[_drawSetNext++];
        }
        else
        {
            var shaderSets = SetsFor(gfx, draw.Shader, program);
            var ring = shaderSets.Rings[_drawSetSlot];
            if (shaderSets.Next == ring.Count) ring.Add(gfx.CreateDescriptorSet(shaderSets.Layout));
            set = ring[shaderSets.Next++];
        }

        _drawSetsByLook[look] = set;
        if (renderContext.DynamicAllocator is not { } allocator) return set;
        DrawSetsWritten++;

        // A buffer at binding 0, at least one 16-byte row, unless a texture or storage buffer of the
        // shader's own has the binding, as it does in a shader with no uniforms.
        UniformBufferBinding? uniforms = null;
        if (!ImmediateRenderer.ZeroTaken(program, own))
        {
            var size = (ulong)Math.Max(16, program.UniformSize);
            var allocation = allocator.Allocate(size, BufferUsage.Uniform);
            var bytes = allocator.Map(allocation);
            bytes.Clear();
            draw.Uniforms?.AsSpan(0, Math.Min(draw.Uniforms.Length, bytes.Length)).CopyTo(bytes);
            allocator.Unmap(allocation);
            uniforms = new UniformBufferBinding(allocation.Buffer, 0, allocation.Offset, size);
        }

        WriteMaterial(gfx, set, uniforms, Maps(gfx, textures, draw));
        foreach (var texture in own)
        {
            var index = -1;
            for (int i = 0; i < program.Textures.Count && index < 0; i++)
                if (program.Textures[i] == texture) index = i;
            var id = draw.ShaderTextures is { } ids && index >= 0 && index < ids.Length ? ids[index] : 0;
            var (view, sampler) = textures.ViewFor(gfx, id, cube: texture.Cube);
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(view, sampler, (uint)texture.Binding, texture.Type));
        }
        ImmediateRenderer.BindBuffers(gfx, renderWorld, set, program, draw.ShaderTextures, ref _noBuffer);
        return set;
    }

    // Bound in place of a storage buffer a draw was not given.
    private IBuffer? _noBuffer;

    // The material layout and ring of a shader with textures or storage buffers of its own, made on first use.
    private ShaderSets SetsFor(IGraphicsDevice gfx, int shader, ShaderProgram program)
    {
        if (_shaderSets.TryGetValue(shader, out var sets)) return sets;
        // The shader's own set 0 as it declares it, with the engine's material bindings the pass
        // fills, and the uniform buffer at 0 unless a texture or buffer of the shader's own took it.
        var bindings = ShaderProgram.Merge(program.LayoutOf(0), _materialBindings.Where(b => b.Binding != 0), [Uniforms]);
        return _shaderSets[shader] = new ShaderSets(gfx.CreateDescriptorSetLayout(bindings));
    }

    // The model pass's streams by the semantics its shaders name them by, the mesh's vertex, the
    // instance's rows from location 3 in ModelInstance's order, and with streams the mesh's colors
    // and second texture coordinates.
    private static readonly string[] InstanceRows = ["INSTANCE_WORLDX0", "INSTANCE_WORLDY0", "INSTANCE_WORLDZ0", "INSTANCE_COLOR0", "INSTANCE_EMISSION0", "INSTANCE_FACTORS0"];

    // The attributes a pipeline of the pass is fed, the stage's inputs placed by their semantics
    // where it names them, with a warning once for a stage that reads one no stream gives.
    private VertexInputAttributeDesc[] Placed(int rows, Streams streams, IReadOnlyList<ShaderInput> named, IReadOnlySet<int>? inputs, string? shaderName,
        StreamFeed? feed = null, bool positions = false, bool solid = false)
    {
        VertexStream[] given =
        [
            new("POSITION0", new VertexInputAttributeDesc(0, 0, VertexFormat.Float3, 0)),
            // A mesh of positions alone has its normal and texture coordinate read as zero from
            // binding 6, at a stride of 0.
            new("NORMAL0", positions ? new VertexInputAttributeDesc(1, 6, VertexFormat.Float3, 0) : new VertexInputAttributeDesc(1, 0, VertexFormat.Float3, 12)),
            new("TEXCOORD0", positions ? new VertexInputAttributeDesc(2, 6, VertexFormat.Float2, 12) : new VertexInputAttributeDesc(2, 0, VertexFormat.Float2, 24)),
            .. Enumerable.Range(0, rows).Select(row => new VertexStream(InstanceRows[row], new VertexInputAttributeDesc((uint)(3 + row), 1, VertexFormat.Float4, (uint)(row * 16)))),
            .. streams == Streams.None ? Array.Empty<VertexStream>() :
            [
                new("COLOR0", new VertexInputAttributeDesc(9, 2, VertexFormat.UNormR8G8B8A8, 0)),
                new("TEXCOORD1", new VertexInputAttributeDesc(10, 3, VertexFormat.Float2, 0)),
            ],
            // The stream of the program's own at binding 5, past the subsurface pass's binding 4,
            // location 13 standing where a stage's inputs are not named.
            .. feed is { } fed ? [new VertexStream("CUSTOM0", new VertexInputAttributeDesc(13, 5, fed.Format, 0))] : Array.Empty<VertexStream>(),
        ];
        var placed = VertexStream.Placed(given, named, inputs, out var missing);
        if (missing.Length > 0 && shaderName is not null && _unfed.Add(shaderName))
            Logger.Warn($"'{shaderName}': its vertex stage takes {string.Join(", ", missing)}, which no stream of a model gives. " +
                        "A model's vertices give POSITION, NORMAL, TEXCOORD0, COLOR0, TEXCOORD1, a ModelInstance and the mesh's own stream as CUSTOM0.");
        return placed;
    }

    // The shaders whose missing inputs were warned of, each once.
    private readonly HashSet<string> _unfed = [];
    private static readonly ILogger Logger = Log.Category("Engine.Models");

    // Whether the pass being drawn takes its pipelines from worker threads and leaves out what they
    // have not made yet, and whether it left any out, as the view mirrored in a plane does.
    private bool _waitless;

    /// <summary>Whether the last pass drawn without waiting left out a batch whose pipeline was still being made.</summary>
    internal bool PipelinesPending { get; private set; }

    // The description each pipeline a worker makes was first asked for by, by the pass's own key,
    // asked for again by the same one, since a description holds its vertex inputs as arrays made
    // for it, which a description made again does not equal.
    private readonly Dictionary<object, GraphicsPipelineDesc> _waiting = [];

    // A pass's pipeline, or none while a worker makes it for a pass that waits for none.
    private IPipeline? MakePipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, IShader vertex, IShader? fragment,
        CullMode cull = CullMode.None, IDescriptorSetLayout? material = null, bool shadow = false, bool points = false,
        Streams streams = Streams.None, bool blend = true, bool depth = true, IReadOnlySet<int>? inputs = null, IReadOnlyList<ShaderInput>? named = null,
        string? shaderName = null, StreamFeed? feed = null, bool positions = false, bool solid = false, bool clamp = false, object? waitingAs = null)
    {
        // The shadow pass reads the instance's first five rows, to its emission. Both push the
        // view-projection they draw through.
        var rows = shadow ? 5 : 6;
        var desc = new GraphicsPipelineDesc(
            renderPass,
            vertex,
            fragment,
            BlendEnabled: blend,
            Cull: cull,
            Points: points,
            Solid: solid,
            // The mesh's vertices at binding 0, and the instances at binding 1, rows of four floats
            // from location 3 in ModelInstance's order. With streams, its colors at binding 2 and
            // location 9 and its second texture coordinates at binding 3 and location 10, each a
            // vertex at a time from the mesh or the one default for every vertex, and its stream of
            // the program's own at binding 5 the same way. A mesh of positions alone gives twelve
            // bytes a vertex at binding 0, its normal and texture coordinate zeros at binding 6.
            VertexBindings:
            [
                new VertexInputBindingDesc(0, positions ? 12u : (uint)Marshal.SizeOf<ModelVertex>()),
                new VertexInputBindingDesc(1, Instance.Size, PerInstance: true),
                .. streams == Streams.None ? Array.Empty<VertexInputBindingDesc>() :
                [
                    new VertexInputBindingDesc(2, streams == Streams.PerVertex ? 4u : 0u),
                    new VertexInputBindingDesc(3, streams == Streams.PerVertex ? 8u : 0u),
                ],
                .. feed is { } fed ? [new VertexInputBindingDesc(5, fed.Stride)] : Array.Empty<VertexInputBindingDesc>(),
                .. positions ? [new VertexInputBindingDesc(6, 0)] : Array.Empty<VertexInputBindingDesc>(),
            ],
            VertexAttributes: Placed(rows, streams, named ?? [], inputs, shaderName, feed, positions),
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Vertex, 0, 64)],
            // The material's set, with uniforms at binding 0 and its five maps after, then the
            // frame's lights at binding 0 of the second and the shadow map at binding 1.
            DescriptorSetLayouts: [material ?? MaterialLayout(gfx), LightsLayout(gfx)],
            DepthTestEnabled: true,
            DepthWriteEnabled: depth,
            DepthCompareOp: CompareOp.LessOrEqual,
            DepthClamp: clamp,
            DrawsBacks: cull != CullMode.Back);

        if (renderWorld.TryGet<PipelineCache>() is not { } cache) return gfx.CreateGraphicsPipeline(desc);
        if (!_waitless || waitingAs is null) return cache.GetOrCreate(desc);
        if (_waiting.TryGetValue(waitingAs, out var asked)) desc = asked;
        var made = cache.GetOrBegin(desc);
        if (made is null) _waiting[waitingAs] = desc;
        else _waiting.Remove(waitingAs);
        return made;
    }
}
