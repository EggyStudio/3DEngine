using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Draws the frame's <see cref="ModelDrawList"/> with <c>model.slang</c>: holds the pipeline, and
/// draws the meshes meant for one target into whichever pass is open.
/// </summary>
/// <remarks>
/// <para>
/// Draws of the model pass's own shader that share a mesh and its five maps are one instanced
/// draw. Each draw is an <see cref="Instance"/> in a vertex buffer stepped per instance, holding
/// its transform, its world matrix as a 3x4 and its material's factors, written into this frame's
/// region of a ring, and the batch binds its mesh's buffers from <see cref="GpuMeshes"/> and its
/// maps from <see cref="GpuTextures"/> once. Opaque batches are drawn in the order each first
/// appears. A translucent draw (<see cref="ModelDraw.IsTranslucent"/>) blends with what is behind
/// it, so it stays out of them and is drawn after, in the order it was recorded, batched only with
/// the draws next to it that share its mesh and set.
/// The frame's lights, packed by <see cref="LightingUboPrepare"/>, are bound once per pass as a
/// second descriptor set.
/// </para>
/// <para>
/// A draw whose material has a shader of the program's own is drawn with a pipeline made from that
/// shader, its vertex stage or <c>model.slang</c>'s when it has none, and a descriptor set of its
/// own holding its uniform values, copied when the draw was recorded, beside its texture. Those
/// sets come from a ring per frame in flight, reused once the GPU is done with that frame. Such a
/// draw is a batch of its own, an instance of one.
/// </para>
/// <para>
/// The frame's directional shadow is drawn by <see cref="DrawShadow"/> into a <see cref="ShadowMap"/>,
/// each cascade into a tile of it, with <c>model.slang</c>'s vertex stage and no fragment stage, a batch per mesh, so a model shader with a vertex
/// stage of its own casts the shadow of its mesh as it was before that stage moved it. The map
/// is bound beside the lights, or the white texture in its place in a frame with no shadow.
/// </para>
/// <para>
/// A material draws both sides of each face unless it is single-sided, as a glTF file can say,
/// whose draws are batched apart and drawn by a pipeline that leaves the back faces out. Both
/// sides is the default because a model from a format that does not say may wind its triangles
/// either way, and the back of a double-sided face is lit by its normal turned toward the viewer.
/// </para>
/// </remarks>
public sealed class ModelRenderer : IDisposable
{
    /// <summary>
    /// One drawn copy of a mesh as <c>modelpass.slang</c>'s <c>ModelInstance</c> reads it from a
    /// vertex buffer stepped per instance, 160 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Instance
    {
        public const int Size = 160;

        /// <summary>Model to clip space, read by its rows.</summary>
        public Matrix4x4 Transform;
        /// <summary>The world matrix's columns, the rows of a 3x4.</summary>
        public Vector4 WorldX, WorldY, WorldZ;
        /// <summary>The material's color, linear.</summary>
        public Vector4 Color;
        /// <summary>The light the material gives off, linear, in xyz.</summary>
        public Vector4 Emission;
        /// <summary>Metallic, roughness, normal scale and occlusion strength.</summary>
        public Vector4 Factors;

        // Each sRGB byte's linear value, since three powers a draw cost more than the rest of it.
        private static readonly float[] Linear = Enumerable.Range(0, 256).Select(value =>
        {
            var c = value / 255f;
            return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        }).ToArray();

        /// <summary>A draw through <paramref name="viewProjection"/>, its color decoded from sRGB to linear.</summary>
        public static Instance Of(in ModelDraw draw, in Matrix4x4 viewProjection)
        {
            var w = draw.World;
            var color = draw.Color;
            return new Instance
            {
                Transform = w * viewProjection,
                WorldX = new Vector4(w.M11, w.M21, w.M31, w.M41),
                WorldY = new Vector4(w.M12, w.M22, w.M32, w.M42),
                WorldZ = new Vector4(w.M13, w.M23, w.M33, w.M43),
                Color = new Vector4(Linear[color.R], Linear[color.G], Linear[color.B], color.A / 255f),
                // w carries the alpha mode to the fragment stage. Below zero ignores alpha, above
                // zero is a mask's cutoff, and zero blends.
                Emission = new Vector4(draw.Emission, draw.AlphaMode switch
                {
                    MaterialAlphaMode.Opaque => -1,
                    MaterialAlphaMode.Mask => Math.Max(draw.AlphaCutoff, 1e-6f),
                    _ => 0,
                }),
                // No map, no bending, which also keeps the shader from reading the white texture
                // in its place as a normal.
                Factors = new Vector4(draw.Metallic, draw.Roughness, draw.NormalMap == 0 ? 0 : draw.NormalScale, draw.OcclusionStrength),
            };
        }
    }

    // A run of instances drawn by one call. It is a mesh with a set of maps, or one draw with a
    // shader of its own, by its index in the draw list.
    private struct Batch
    {
        public GpuMeshes.Entry Mesh;
        public IDescriptorSet? Set;
        public int Custom;
        public bool Culled;
        public uint First;
        public uint Count;
    }

    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;
    private readonly ReadOnlyMemory<byte> _shadowMaskSpv;
    private IShader? _shadowMaskShader;
    private IPipeline? _shadowMaskPipeline;
    private IShader? _vertexShader;
    private IShader? _fragmentShader;
    private IPipeline? _pipeline;
    private IPipeline? _culledPipeline;
    private IDescriptorSetLayout? _defaultLayout;
    private IDescriptorSetLayout? _materialLayout;
    private IBuffer? _noUniforms;

    // Sets of the model pass's own draws, one per combination of five maps (a base color texture, a
    // normal map, a metallic-roughness map, an emissive map and an occlusion map) by their views,
    // with the frame each was last bound in. The factors are in each draw's instance, so a thousand
    // entities differing only in color share one set and one draw. A set unbound for RetireFrames
    // frames is freed, since no frame in flight can read it, so the views of unloaded textures do
    // not hold sets forever.
    private readonly Dictionary<(IImageView, IImageView, IImageView, IImageView, IImageView), (IDescriptorSet Set, long Used)> _materialSets = [];

    // Every draw's instance, a region per frame slot, kept mapped. Each frame writes its instances
    // into its own region, which the GPU finished reading RetireFrames frames ago, the shadow pass's
    // and every target's one after another.
    private IBuffer? _instanceRing;
    private int _ringCapacity;
    private int _ringSlot;
    private int _ringCursor;
    private readonly List<(long Frame, IBuffer Buffer)> _retiredBuffers = [];

    // This call's batches, which batch each draw went into (-1 for none), and the batch of each
    // mesh and set, kept between calls so a frame allocates nothing once they have grown.
    private readonly List<Batch> _batches = [];
    private readonly List<int> _drawBatch = [];
    private readonly List<uint> _filled = [];
    private readonly Dictionary<(int Mesh, IDescriptorSet? Set, bool Culled), int> _batchOf = [];
    private readonly List<(Kind Kind, IDescriptorSet? Set)> _classified = [];

    // This frame's sets by the draws' five texture ids, cleared each frame, since an id's view can
    // change between frames when its texture is reloaded.
    private readonly Dictionary<(int, int, int, int, int), IDescriptorSet> _setByIds = [];

    /// <summary>How many sets the model pass's own draws hold, one per combination of maps in use.</summary>
    internal int MaterialSetCount => _materialSets.Count;

    /// <summary>How many draw calls the model and shadow passes recorded this frame.</summary>
    internal int DrawCalls { get; private set; }
    private long _frames;
    private readonly List<IDescriptorSet> _lightSets = [];
    private int _lightSet;
    private FrameLightingBinding? _lastFrame;
    private IDescriptorSet? _noLights;
    private IBuffer? _noLightsBuffer;
    private ShadowMap? _shadowMap;
    private CubeMap? _environment;
    private EnvironmentMap? _environmentSource;
    private CubeMap? _noEnvironment;
    private readonly List<(long Frame, CubeMap Cube)> _retiredCubes = [];
    private IPipeline? _shadowPipeline;

    // A shader with textures of its own past the material's five maps has a material layout of its
    // own with them added, and its own ring of sets, by ShaderStore id.
    private const int MaterialBindings = 5;

    // The maps the pass fills itself, modelpass.slang's, at bindings 1 to 5. Any other texture a
    // shader samples is its own.
    private static readonly string[] PassTextures = ["boundTexture", "normalMap", "metallicRoughnessMap", "emissiveMap", "occlusionMap"];
    private readonly Dictionary<int, ShaderSets> _shaderSets = [];
    private readonly List<(long Frame, IDisposable Retired)> _retiredShaderSets = [];

    private sealed class ShaderSets(IDescriptorSetLayout layout) : IDisposable
    {
        public IDescriptorSetLayout Layout { get; } = layout;
        public List<IDescriptorSet>[] Rings { get; } = Enumerable.Range(0, SetRingFrames).Select(_ => new List<IDescriptorSet>()).ToArray();
        public int Next;

        public void Dispose()
        {
            foreach (var ring in Rings)
                foreach (var set in ring) set.Dispose();
            Layout.Dispose();
        }
    }

    // Pipelines of the program's own shaders, by ShaderStore id, with the modules they were made from.
    private readonly Dictionary<int, (IShader Vertex, IShader Fragment, IPipeline Pipeline)> _custom = [];

    // Descriptor sets for draws with a shader of their own: a list per frame slot, handed out in
    // order each frame and kept for the next time the slot comes round.
    private const int SetRingFrames = GpuTextures.RetireFrames;
    private readonly List<IDescriptorSet>[] _drawSets = Enumerable.Range(0, SetRingFrames).Select(_ => new List<IDescriptorSet>()).ToArray();
    private int _drawSetSlot;
    private int _drawSetNext;
    private RenderContext? _lastContext;

    /// <summary>
    /// Creates the renderer from the compiled stages of <c>model.slang</c>, and the fragment stage
    /// of <c>shadowmask.slang</c> that cuts a masked surface out of the shadow, or none to draw
    /// every shadow solid.
    /// </summary>
    public ModelRenderer(ReadOnlyMemory<byte> vertexSpv, ReadOnlyMemory<byte> fragmentSpv, ReadOnlyMemory<byte> shadowMaskSpv = default)
    {
        _vertexSpv = vertexSpv;
        _fragmentSpv = fragmentSpv;
        _shadowMaskSpv = shadowMaskSpv;
    }

    /// <summary>Draws the meshes meant for <paramref name="target"/> into <paramref name="pass"/>.</summary>
    public void Draw(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, RenderWorld renderWorld, int target)
    {
        var draws = renderWorld.TryGet<ModelDrawList>();
        var meshes = renderWorld.TryGet<GpuMeshes>();
        var textures = renderWorld.TryGet<GpuTextures>();
        if (draws is null || draws.Draws.Count == 0 || meshes is null || textures is null) return;

        var gfx = renderContext.Device;
        var store = renderWorld.TryGet<ShaderStore>();
        BeginFrameOfSets(renderContext);
        RetireUnloadedShaders(store);

        // Draws of the pass's own shader gather by mesh and set, and a draw with a shader of its
        // own is a batch alone.
        Gather(draws.Draws, meshes, draw =>
            draw.Target != target ? (Kind.Skip, null)
            : draw.Shader != 0 && store?.Get(draw.Shader) is not null ? (Kind.Alone, null)
            : (Kind.Batched, MaterialSet(gfx, textures, draw)), keepOrderOfTranslucent: true, cullBackFaces: true);
        var ring = WriteInstances(gfx, draws.Draws, static draw => Instance.Of(draw, draw.ViewProjection));

        IPipeline? pipeline = null;
        foreach (var batch in _batches)
        {
            var draw = batch.Custom >= 0 ? draws.Draws[batch.Custom] : default;
            // A shader unloaded after the draw was recorded draws with the model pass's own.
            var program = batch.Custom >= 0 ? store?.Get(draw.Shader) : null;
            var wanted = program is null
                ? Pipeline(gfx, renderPass, renderWorld, batch.Culled)
                : CustomPipeline(gfx, renderPass, renderWorld, draw.Shader, program);
            if (!ReferenceEquals(wanted, pipeline))
            {
                pipeline = wanted;
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(pipeline, LightsSet(gfx, renderWorld, textures), index: 1);
            }

            pass.SetBindGroup(pipeline, program is null ? batch.Set ?? MaterialSet(gfx, textures, draw) : DrawSet(gfx, renderContext, textures, draw, program));
            pass.SetVertexBuffer(0, [batch.Mesh.Vertices, ring], [0, 0]);
            pass.SetIndexBuffer(batch.Mesh.Indices, 0, IndexType.UInt32);
            pass.DrawIndexed(batch.Mesh.IndexCount, batch.Count, 0, 0, batch.First);
            DrawCalls++;
        }
    }

    // What a draw is to one call. It is left out, a batch of its own, or batched with the draws
    // that share its mesh and set.
    private enum Kind { Skip, Alone, Batched }

    // Sorts this call's draws into batches, in the order each batch first appears. With
    // keepOrderOfTranslucent, a draw whose color has alpha below 255 waits until the opaque
    // batches are made and goes after them in its recorded place, joining the batch before it
    // only when that batch is translucent and shares its mesh and set. The depth pass of a shadow
    // reads no color and keeps no order. With cullBackFaces, a single-sided draw is batched apart,
    // for the pipeline that leaves its back faces out.
    private void Gather(IReadOnlyList<ModelDraw> draws, GpuMeshes meshes, Func<ModelDraw, (Kind Kind, IDescriptorSet? Set)> classify,
        bool keepOrderOfTranslucent, bool cullBackFaces = false)
    {
        _batches.Clear();
        _drawBatch.Clear();
        _batchOf.Clear();
        _classified.Clear();
        for (int i = 0; i < draws.Count; i++)
        {
            var draw = draws[i];
            var (kind, set) = classify(draw);
            _classified.Add((kind, set));
            // A mesh unloaded after its draw was recorded is skipped.
            if (kind == Kind.Skip || meshes.Get(draw.Mesh) is not { } mesh)
            {
                _drawBatch.Add(-1);
                continue;
            }
            if (keepOrderOfTranslucent && draw.IsTranslucent)
            {
                _drawBatch.Add(Translucent);
                continue;
            }
            var culled = cullBackFaces && !draw.DoubleSided;
            _drawBatch.Add(kind == Kind.Alone ? AddBatch(mesh, null, i, culled) : Join(mesh, (draw.Mesh, set, culled)));
        }

        var last = (Mesh: -1, Set: (IDescriptorSet?)null, Culled: false);
        var lastBatch = -1;
        for (int i = 0; i < draws.Count; i++)
        {
            if (_drawBatch[i] != Translucent) continue;
            var draw = draws[i];
            var (kind, set) = _classified[i];
            var mesh = meshes.Get(draw.Mesh)!;
            var culled = cullBackFaces && !draw.DoubleSided;
            if (kind == Kind.Alone)
            {
                _drawBatch[i] = AddBatch(mesh, null, i, culled);
                lastBatch = -1;
                continue;
            }
            if (lastBatch < 0 || last != (draw.Mesh, set, culled))
            {
                lastBatch = AddBatch(mesh, set, -1, culled);
                last = (draw.Mesh, set, culled);
            }
            Grow(lastBatch);
            _drawBatch[i] = lastBatch;
        }
    }

    // Marks a translucent draw until the opaque batches are made.
    private const int Translucent = -2;

    private int AddBatch(GpuMeshes.Entry mesh, IDescriptorSet? set, int custom, bool culled)
    {
        _batches.Add(new Batch { Mesh = mesh, Set = set, Custom = custom, Culled = culled, Count = custom >= 0 ? 1u : 0u });
        return _batches.Count - 1;
    }

    private int Join(GpuMeshes.Entry mesh, (int Mesh, IDescriptorSet? Set, bool Culled) key)
    {
        if (!_batchOf.TryGetValue(key, out var index))
            _batchOf[key] = index = AddBatch(mesh, key.Set, -1, key.Culled);
        Grow(index);
        return index;
    }

    private void Grow(int index)
    {
        var batch = _batches[index];
        batch.Count++;
        _batches[index] = batch;
    }

    // Writes each batched draw's instance into this frame's region of the ring, a batch's
    // instances together, and gives each batch the first of them. Returns the ring they are in.
    private IBuffer WriteInstances(IGraphicsDevice gfx, IReadOnlyList<ModelDraw> draws, Func<ModelDraw, Instance> instanceOf)
    {
        uint total = 0;
        foreach (var batch in _batches) total += batch.Count;
        EnsureInstanceRoom(gfx, (int)total);

        var first = (uint)(_ringSlot * _ringCapacity + _ringCursor);
        _ringCursor += (int)total;
        _filled.Clear();
        for (int b = 0; b < _batches.Count; b++)
        {
            var batch = _batches[b];
            batch.First = first;
            _batches[b] = batch;
            _filled.Add(first);
            first += batch.Count;
        }

        var instances = MemoryMarshal.Cast<byte, Instance>(gfx.Map(_instanceRing!));
        for (int i = 0; i < _drawBatch.Count; i++)
        {
            var b = _drawBatch[i];
            if (b < 0) continue;
            var draw = draws[i];
            instances[(int)_filled[b]++] = instanceOf(draw);
        }
        return _instanceRing!;
    }

    // The ring of instances, with room for a region per frame slot of at least this call's
    // instances past those the frame has written already. A ring outgrown is replaced by one twice
    // the size, the old one kept until no frame in flight reads it, and the frame's earlier
    // commands keep the old one bound.
    private void EnsureInstanceRoom(IGraphicsDevice gfx, int instances)
    {
        if (_instanceRing is not null && _ringCursor + instances <= _ringCapacity) return;

        if (_instanceRing is not null) _retiredBuffers.Add((_frames, _instanceRing));
        _ringCapacity = Math.Max(Math.Max(1024, _ringCapacity * 2), _ringCursor + instances);
        _instanceRing = gfx.CreateBuffer(new BufferDesc((ulong)(SetRingFrames * _ringCapacity * Instance.Size), BufferUsage.Vertex, CpuAccessMode.Write));
    }

    /// <summary>Draws the window's meshes into the shadow map, as <paramref name="shadow"/>'s light sees them.</summary>
    public void DrawShadow(RenderContext renderContext, RenderWorld renderWorld, FrameShadow shadow)
    {
        var draws = renderWorld.TryGet<ModelDrawList>();
        var meshes = renderWorld.TryGet<GpuMeshes>();
        var textures = renderWorld.TryGet<GpuTextures>();
        if (draws is null || meshes is null || textures is null || renderContext.Device is not GraphicsDevice device) return;

        var map = _shadowMap ??= device.CreateShadowMap(ShadowFit.AtlasSize);
        if (_shadowPipeline is null)
        {
            _vertexShader ??= device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
            _shadowPipeline = MakePipeline(device, map.RenderPass, renderWorld, _vertexShader, fragment: null);
            if (!_shadowMaskSpv.IsEmpty)
            {
                _shadowMaskShader = device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _shadowMaskSpv));
                _shadowMaskPipeline = MakePipeline(device, map.RenderPass, renderWorld, _vertexShader, _shadowMaskShader);
            }
        }

        // The window's draws gather by mesh alone, since a solid shadow reads no material, and a
        // masked draw by its maps too, which its fragment stage cuts it out by.
        BeginFrameOfSets(renderContext);
        var masks = _shadowMaskPipeline is not null;
        Gather(draws.Draws, meshes, draw => draw.Target != 0 ? (Kind.Skip, null)
            : masks && draw.AlphaMode == MaterialAlphaMode.Mask ? (Kind.Batched, MaterialSet(device, textures, draw))
            : (Kind.Batched, null), keepOrderOfTranslucent: false);

        // One clear for the whole map, then each cascade drawn into its own tile.
        var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            map.RenderPass, map.Framebuffer, map.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(0, 0, 0, 0)));
        pass.SetPipeline(_shadowPipeline);
        IPipeline bound = _shadowPipeline;
        var tiles = shadow.Cascades.Count + (shadow.SpotLight >= 0 ? 1 : 0);
        for (int t = 0; t < tiles; t++)
        {
            // The cascades in the first tiles, and the spot light in its own.
            var spot = t == shadow.Cascades.Count;
            var (x, y) = ShadowFit.TileOrigin(spot ? ShadowFit.SpotTile : t);
            pass.SetViewport(x, y, ShadowFit.TileSize, ShadowFit.TileSize, 0, 1);
            pass.SetScissor(x, y, ShadowFit.TileSize, ShadowFit.TileSize);

            // A solid shadow reads the transform alone, and a masked one its color and cutoff too.
            var lightViewProjection = spot ? shadow.SpotViewProjection : shadow.Cascades[t].ViewProjection;
            var ring = WriteInstances(device, draws.Draws, draw => masks && draw.AlphaMode == MaterialAlphaMode.Mask
                ? Instance.Of(draw, lightViewProjection)
                : new Instance { Transform = draw.World * lightViewProjection });
            foreach (var batch in _batches)
            {
                var pipeline = batch.Set is null ? _shadowPipeline : _shadowMaskPipeline!;
                if (!ReferenceEquals(pipeline, bound))
                {
                    pass.SetPipeline(pipeline);
                    bound = pipeline;
                }
                if (batch.Set is not null) pass.SetBindGroup(pipeline, batch.Set);
                pass.SetVertexBuffer(0, [batch.Mesh.Vertices, ring], [0, 0]);
                pass.SetIndexBuffer(batch.Mesh.Indices, 0, IndexType.UInt32);
                pass.DrawIndexed(batch.Mesh.IndexCount, batch.Count, 0, 0, batch.First);
                DrawCalls++;
            }
        }
        pass.EndRenderPass();
    }

    // The model pass's own pipeline, drawing both sides of each face or leaving the back ones out.
    private IPipeline Pipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, bool culled = false)
    {
        if ((culled ? _culledPipeline : _pipeline) is { } made) return made;

        _vertexShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragmentShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));
        var pipeline = MakePipeline(gfx, renderPass, renderWorld, _vertexShader, _fragmentShader, culled);
        return culled ? _culledPipeline = pipeline : _pipeline = pipeline;
    }

    private IPipeline CustomPipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, int id, ShaderProgram program)
    {
        if (_custom.TryGetValue(id, out var made)) return made.Pipeline;

        var vertex = gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex,
            program.Stages.TryGetValue(ShaderStage.Vertex, out var own) ? own : _vertexSpv));
        var fragment = gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, program.Fragment));
        var ownTextures = program.OwnTextures(PassTextures);
        var pipeline = MakePipeline(gfx, renderPass, renderWorld, vertex, fragment,
            material: ownTextures.Count > 0 ? SetsFor(gfx, id, ownTextures).Layout : null);
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
        _setByIds.Clear();
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
    private IDescriptorSet MaterialSet(IGraphicsDevice gfx, GpuTextures textures, ModelDraw draw)
    {
        var ids = (draw.Texture, draw.NormalMap, draw.MetallicRoughnessMap, draw.EmissiveMap, draw.OcclusionMap);
        if (_setByIds.TryGetValue(ids, out var found)) return found;
        return _setByIds[ids] = MaterialSetByViews(gfx, textures, draw);
    }

    private IDescriptorSet MaterialSetByViews(IGraphicsDevice gfx, GpuTextures textures, ModelDraw draw)
    {
        var maps = Maps(gfx, textures, draw);
        var key = (maps[0].View, maps[1].View, maps[2].View, maps[3].View, maps[4].View);
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

    // A descriptor set for one draw with a shader of its own: its uniform values in this frame's
    // buffer at binding 0, and its texture at binding 1.
    private IDescriptorSet DrawSet(IGraphicsDevice gfx, RenderContext renderContext, GpuTextures textures, ModelDraw draw, ShaderProgram program)
    {
        var own = program.OwnTextures(PassTextures);
        IDescriptorSet set;
        if (own.Count == 0)
        {
            var sets = _drawSets[_drawSetSlot];
            if (_drawSetNext == sets.Count) sets.Add(gfx.CreateDescriptorSet(MaterialLayout(gfx)));
            set = sets[_drawSetNext++];
        }
        else
        {
            var shaderSets = SetsFor(gfx, draw.Shader, own);
            var ring = shaderSets.Rings[_drawSetSlot];
            if (shaderSets.Next == ring.Count) ring.Add(gfx.CreateDescriptorSet(shaderSets.Layout));
            set = ring[shaderSets.Next++];
        }

        if (renderContext.DynamicAllocator is not { } allocator) return set;

        // A buffer at binding 0, at least one 16-byte row, unless a texture of the shader's own has
        // the binding, as it does in a shader with no uniforms.
        UniformBufferBinding? uniforms = null;
        if (program.UniformSize > 0 || !own.Any(t => t.Binding == 0))
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
            var (view, sampler) = textures.ViewFor(gfx, id);
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(view, sampler, (uint)texture.Binding));
        }
        return set;
    }

    // The material layout and ring of a shader with textures of its own, made on first use.
    private ShaderSets SetsFor(IGraphicsDevice gfx, int shader, IReadOnlyList<ShaderTexture> own)
    {
        if (_shaderSets.TryGetValue(shader, out var sets)) return sets;
        // The uniform buffer at 0 unless a texture of the shader's own took it.
        DescriptorSetLayoutBinding[] bindings =
        [
            .. own.Any(t => t.Binding == 0) ? [] : new[] { new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.Vertex | ShaderStageFlags.Fragment) },
            .. Enumerable.Range(1, MaterialBindings).Select(b => new DescriptorSetLayoutBinding((uint)b, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment)),
            .. own.Select(t => new DescriptorSetLayoutBinding((uint)t.Binding, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment)),
        ];
        return _shaderSets[shader] = new ShaderSets(gfx.CreateDescriptorSetLayout(bindings));
    }

    private IPipeline MakePipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, IShader vertex, IShader? fragment,
        bool culled = false, IDescriptorSetLayout? material = null)
    {
        var desc = new GraphicsPipelineDesc(
            renderPass,
            vertex,
            fragment,
            BlendEnabled: true,
            CullBackFace: culled,
            // The mesh's vertices at binding 0, and the instances at binding 1, ten rows of four
            // floats at locations 3 to 12 in ModelInstance's order.
            VertexBindings:
            [
                new VertexInputBindingDesc(0, (uint)Marshal.SizeOf<ModelVertex>()),
                new VertexInputBindingDesc(1, Instance.Size, PerInstance: true),
            ],
            VertexAttributes:
            [
                new VertexInputAttributeDesc(0, 0, VertexFormat.Float3, 0),
                new VertexInputAttributeDesc(1, 0, VertexFormat.Float3, 12),
                new VertexInputAttributeDesc(2, 0, VertexFormat.Float2, 24),
                .. Enumerable.Range(0, 10).Select(row => new VertexInputAttributeDesc((uint)(3 + row), 1, VertexFormat.Float4, (uint)(row * 16))),
            ],
            // The material's set, with uniforms at binding 0 and its five maps after, then the
            // frame's lights at binding 0 of the second and the shadow map at binding 1.
            DescriptorSetLayouts: [material ?? MaterialLayout(gfx), LightsLayout(gfx)],
            DepthTestEnabled: true,
            DepthWriteEnabled: true,
            DepthCompareOp: CompareOp.LessOrEqual);

        return renderWorld.TryGet<PipelineCache>() is { } cache
            ? cache.GetOrCreate(desc)
            : gfx.CreateGraphicsPipeline(desc);
    }

    // The lights of this frame as a descriptor set: one of a ring, a set per frame in flight so a
    // set the GPU may still read is never written, or a set over an empty buffer when there are no
    // lights and no environment, which the shader reads as "use the fixed light". Binding 1 holds
    // the shadow map when the frame has a shadow, and the white texture otherwise, and binding 2
    // the environment map or a black cube, so both are always valid.
    private IDescriptorSet LightsSet(IGraphicsDevice gfx, RenderWorld renderWorld, GpuTextures textures)
    {
        var (white, whiteSampler) = textures.ViewFor(gfx, 0);
        if (renderWorld.TryGet<FrameLightingBinding>() is not { } frame || (frame.LightCount == 0 && !frame.HasEnvironment))
        {
            if (_noLights is null)
            {
                _noLightsBuffer = gfx.CreateBuffer(new BufferDesc((ulong)LightingUboPacker.SizeBytes, BufferUsage.Uniform, CpuAccessMode.Write));
                var span = gfx.Map(_noLightsBuffer);
                span.Clear();
                gfx.Unmap(_noLightsBuffer);
                _noLights = gfx.CreateDescriptorSet(LightsLayout(gfx));
                gfx.UpdateDescriptorSet(_noLights, new UniformBufferBinding(_noLightsBuffer, 0, 0, (ulong)LightingUboPacker.SizeBytes),
                    new CombinedImageSamplerBinding(white, whiteSampler, 1));
                if (EnvironmentCube(gfx, null) is { } black)
                    gfx.UpdateDescriptorSet(_noLights, null, new CombinedImageSamplerBinding(black.View, black.Sampler, 2));
            }
            return _noLights;
        }

        // Once a frame, however many targets draw models in it.
        if (!ReferenceEquals(frame, _lastFrame))
        {
            _lastFrame = frame;
            if (_lightSets.Count < gfx.FramesInFlight) _lightSets.Add(gfx.CreateDescriptorSet(LightsLayout(gfx)));
            _lightSet = (_lightSet + 1) % _lightSets.Count;
            var shadow = renderWorld.TryGet<FrameShadow>() is not null ? _shadowMap : null;
            gfx.UpdateDescriptorSet(_lightSets[_lightSet], frame.Binding, shadow is null
                ? new CombinedImageSamplerBinding(white, whiteSampler, 1)
                : new CombinedImageSamplerBinding(shadow.DepthView, shadow.Sampler, 1));
            if (EnvironmentCube(gfx, frame.HasEnvironment ? renderWorld.TryGet<EnvironmentMap>() : null) is { } cube)
                gfx.UpdateDescriptorSet(_lightSets[_lightSet], null, new CombinedImageSamplerBinding(cube.View, cube.Sampler, 2));
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
    ]);

    private IDescriptorSetLayout LightsLayout(IGraphicsDevice gfx) => _defaultLayout ??= gfx.CreateDescriptorSetLayout(
    [
        new DescriptorSetLayoutBinding(0, DescriptorType.UniformBuffer, ShaderStageFlags.Vertex | ShaderStageFlags.Fragment),
        new DescriptorSetLayoutBinding(1, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment),
        new DescriptorSetLayoutBinding(2, DescriptorType.CombinedImageSampler, ShaderStageFlags.Fragment),
    ]);

    // The cube of the environment map, uploaded when the map is new, or a black cube of one texel
    // for none. A cube replaced is kept for RetireFrames frames, since a frame in flight may read it.
    private CubeMap? EnvironmentCube(IGraphicsDevice gfx, EnvironmentMap? environment)
    {
        if (gfx is not GraphicsDevice device) return null;
        if (environment is null)
            return _noEnvironment ??= device.CreateCubeMap(1, 1, new Half[6 * 4]);

        if (!ReferenceEquals(environment, _environmentSource))
        {
            if (_environment is not null) _retiredCubes.Add((_frames, _environment));
            _environment = device.CreateCubeMap((uint)environment.Size, (uint)environment.MipLevels, environment.Texels);
            _environmentSource = environment;
        }
        return _environment;
    }

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
        foreach (var (set, _) in _materialSets.Values) set.Dispose();
        foreach (var (_, buffer) in _retiredBuffers) buffer.Dispose();
        _instanceRing?.Dispose();
        _materialSets.Clear();
        _noUniforms?.Dispose();
        _materialLayout?.Dispose();
        _noLights?.Dispose();
        _shadowMap?.Dispose();
        _environment?.Dispose();
        _noEnvironment?.Dispose();
        foreach (var (_, cube) in _retiredCubes) cube.Dispose();
        foreach (var sets in _shaderSets.Values) sets.Dispose();
        foreach (var (_, retired) in _retiredShaderSets) retired.Dispose();
        _shadowMaskShader?.Dispose();
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
