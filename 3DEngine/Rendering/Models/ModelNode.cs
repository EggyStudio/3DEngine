using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Engine;

/// <summary>
/// Draws the frame's <see cref="ModelDrawList"/> with <c>model.slang</c>: holds the pipeline, and
/// draws the meshes meant for one target into whichever pass is open.
/// </summary>
/// <remarks>
/// <para>
/// Draws of the model pass's own shader that share a mesh and its five maps are one instanced
/// draw. Each draw is an <see cref="Instance"/> in a vertex buffer stepped per instance, holding
/// its world matrix as a 3x4 and its material's factors, written into this frame's region of a
/// ring, and the batch binds its mesh's buffers from <see cref="GpuMeshes"/> and its maps from
/// <see cref="GpuTextures"/> once, and pushes the view-projection its draws share, which draws
/// through different cameras do not. Opaque batches are drawn in the order each first
/// appears. A translucent draw (<see cref="ModelDraw.IsTranslucent"/>) blends with what is behind
/// it, so it stays out of them and is drawn after, in the order it was recorded, batched only with
/// the draws next to it that share its mesh and set. An <see cref="InstanceGroup"/> the draw list
/// carries, which mesh entities fill, is a batch of its own after the opaque draws, its instances
/// copied into the ring as they are.
/// The frame's lights, packed by <see cref="LightingUboPrepare"/>, are bound once per pass as a
/// second descriptor set.
/// </para>
/// <para>
/// A draw whose material has a shader of the program's own is drawn with a pipeline made from that
/// shader, its vertex stage or <c>model.slang</c>'s when it has none, and a descriptor set of its
/// own holding its uniform values, copied when the draw was recorded, beside its texture. Those
/// sets come from a ring per frame in flight, reused once the GPU is done with that frame. Such a
/// draw is a batch of its own, an instance of one, unless the draws before it share its shader's
/// values, as the copies <c>DrawMeshInstanced</c> records do, which are one batch.
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
internal sealed class ModelRenderer : IDisposable
{
    /// <summary>
    /// One drawn copy of a mesh as <c>modelpass.slang</c>'s <c>ModelInstance</c> reads it from a
    /// vertex buffer stepped per instance, 96 bytes.
    /// </summary>
    /// <remarks>
    /// It holds nothing of the camera, which the batch pushes, so it is the same through every
    /// camera and light, and the frame writes and copies 96 bytes for each in place of 160.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    internal struct Instance
    {
        public const int Size = 96;

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

        /// <summary>A draw's instance, its color decoded from sRGB to linear.</summary>
        public static Instance Of(in ModelDraw draw)
        {
            var w = draw.World;
            var color = draw.Color;
            return new Instance
            {
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

    // A draw's instance, by reference, since a ModelDraw is about 200 bytes.
    private delegate T InstanceOf<T>(in ModelDraw draw) where T : unmanaged;

    // A run of instances drawn by one call. It is a mesh with a set of maps, or one draw with a
    // shader of its own, by its index in the draw list.
    private struct Batch
    {
        public GpuMeshes.Entry Mesh;
        public IDescriptorSet? Set;
        public int Custom;
        // The index of the draw list's group whose instances it draws, or -1 for draws.
        public int Group;
        public CullMode Cull;
        public bool Points;
        // World to clip space, pushed for the call, the camera's that its draws were recorded through.
        public Matrix4x4 ViewProjection;
        // What the shadow pass draws it with: nothing, a solid shadow, or one its material cuts out.
        public ShadowKind Shadow;
        public uint First;
        public uint Count;
        // A group's blocks in the call's block list, or none for draws.
        public int BlockStart;
        public int BlockCount;
    }

    private readonly ReadOnlyMemory<byte> _vertexSpv;
    private readonly ReadOnlyMemory<byte> _streamsVertexSpv;
    private readonly ReadOnlyMemory<byte> _streamsFragmentSpv;
    private IShader? _streamsVertexShader;
    private IShader? _streamsFragmentShader;
    private readonly ReadOnlyMemory<byte> _fragmentSpv;
    private readonly ReadOnlyMemory<byte> _shadowVertexSpv;
    private readonly ReadOnlyMemory<byte> _shadowMaskSpv;
    private IShader? _shadowVertexShader;
    private IShader? _shadowMaskShader;
    private IPipeline? _shadowMaskPipeline;
    private IShader? _vertexShader;
    private IShader? _fragmentShader;
    // The model pass's own pipelines, by the pass they draw in, since the window's and a target's
    // can differ in format.
    private readonly Dictionary<(IRenderPass Pass, CullMode Cull, bool Points, Streams Streams), IPipeline> _pipelines = [];

    // How a pipeline reads a mesh's colors and second texture coordinates beside its vertices: not
    // at all, a vertex at a time from the mesh, which has both where it has either, or once from a
    // buffer of one default element, for a shader of the program's own that reads them on a mesh
    // without them. The pass's own pipeline reads them only for a mesh that has them, so a mesh
    // without them draws through model.slang, reading its vertices alone.
    private enum Streams : byte { None, PerVertex, Default }

    // The inputs model.slang's stages take, the mesh's vertex and its instance into the vertex
    // stage and ModelVertexOutput into the fragment stage, past which a shader reads the streams.
    private readonly int _plainVertexInputs;
    private readonly int _plainFragmentInputs;

    // A white color and a second texture coordinate of zero, bound with a stride of 0.
    private IBuffer? _defaultColor;
    private IBuffer? _defaultTexcoords2;

    private (IBuffer Colors, IBuffer Texcoords2) DefaultStreams(IGraphicsDevice gfx)
    {
        if (_defaultColor is null)
        {
            _defaultColor = gfx.CreateBuffer(new BufferDesc(4, BufferUsage.Vertex, CpuAccessMode.Write));
            gfx.Map(_defaultColor).Fill(255);
            gfx.Unmap(_defaultColor);
            _defaultTexcoords2 = gfx.CreateBuffer(new BufferDesc(8, BufferUsage.Vertex, CpuAccessMode.Write));
            gfx.Map(_defaultTexcoords2).Clear();
            gfx.Unmap(_defaultTexcoords2);
        }
        return (_defaultColor, _defaultTexcoords2!);
    }
    private IDescriptorSetLayout? _defaultLayout;
    private IDescriptorSetLayout? _materialLayout;
    private IBuffer? _noUniforms;

    // Sets of the model pass's own draws, one per combination of five maps (a base color texture, a
    // normal map, a metallic-roughness map, an emissive map and an occlusion map) by their views and samplers,
    // with the frame each was last bound in. The factors are in each draw's instance, so a thousand
    // entities differing only in color share one set and one draw. A set unbound for RetireFrames
    // frames is freed, since no frame in flight can read it, so the views of unloaded textures do
    // not hold sets forever.
    // The samplers are in the key as well as the views, since a texture's filter changes its sampler
    // and not its view.
    private readonly Dictionary<MapsKey, (IDescriptorSet Set, long Used)> _materialSets = [];

    private readonly record struct MapsKey(
        IImageView V0, ISampler S0, IImageView V1, ISampler S1, IImageView V2, ISampler S2,
        IImageView V3, ISampler S3, IImageView V4, ISampler S4);

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

    // The frame's group segments to copy into the ring, each with where its first instance goes,
    // and where its first block goes in _blocks.
    private readonly List<(Instance[] Items, int Count, int At, InstanceGroup Group)> _copies = [];
    private readonly List<int> _copyBlocks = [];

    // A run of a group's instances in the ring and the box around them, which a view leaves out
    // when the box is outside it. A group's instances are in blocks of this many, so a view draws
    // the runs of blocks it sees, each a call, rather than every instance.
    private const int BlockSize = 64;

    private struct Block
    {
        public uint First;
        public uint Count;
        public Vector3 Min;
        public Vector3 Max;
    }

    // The blocks of the last gathered view's groups, which the view keeps a copy of.
    private Block[] _blocks = new Block[64];
    private int _blockCount;

    // Past this many instances the segments are copied on several threads, since one thread
    // writing tens of megabytes into mapped memory took most of the shadow pass's recording.
    private const int ParallelCopyInstances = 16384;
    private readonly Dictionary<(int Mesh, IDescriptorSet? Set, (CullMode Cull, bool Points) Faces, ShadowKind Shadow, Matrix4x4 ViewProjection), int> _batchOf = [];

    // Each view's batches, the window's at 0 and each render target's by its id, with where their
    // instances are and the blocks they are culled by, made once a frame by whichever of its
    // shadow and model passes comes first and drawn by both, since the shadow reads the same
    // instances through each light.
    private sealed class View
    {
        public readonly List<Batch> Batches = [];
        public IBuffer? Ring;
        public ulong Offset;
        public Block[] Blocks = new Block[64];
        public long Frame = -1;
    }

    private readonly Dictionary<int, View> _views = [];

    // The frame the point lights' faces were last drawn in, once a frame by the first view drawing
    // a shadow, since they look the same from every camera.
    private long _pointsFrame = -1;
    private readonly List<(Kind Kind, IDescriptorSet? Set)> _classified = [];

    // This frame's sets by the draws' five texture ids, cleared each frame, since an id's view can
    // change between frames when its texture is reloaded.
    private readonly Dictionary<(int, int, int, int, int), IDescriptorSet> _setByIds = [];

    /// <summary>How many sets the model pass's own draws hold, one per combination of maps in use.</summary>
    internal int MaterialSetCount => _materialSets.Count;

    /// <summary>How many draw calls the model and shadow passes recorded this frame.</summary>
    internal int DrawCalls { get; private set; }
    private long _frames;
    // The lights' sets, a list for each frame in flight, and the set each view took this frame.
    private readonly List<List<IDescriptorSet>> _lightSets = [];
    private readonly Dictionary<int, IDescriptorSet> _lightSetOf = [];
    private int _lightSlot;
    private FrameLightingBinding? _lastFrame;
    private IDescriptorSet? _noLights;
    private IBuffer? _noLightsBuffer;
    private ShadowMap? _shadowMap;
    private ShadowMap? _pointShadowMap;

    // Maps replaced by ones of another size, kept until no frame in flight reads them.
    private readonly List<(long Frame, ShadowMap Map)> _retiredMaps = [];
    private ShadowMap? _noPointShadowMap;
    private CubeMap? _environment;
    private EnvironmentMap? _environmentSource;
    private CubeMap? _noEnvironment;
    private CubeMap? _sky;
    private readonly List<(long Frame, CubeMap Cube)> _retiredCubes = [];
    private IPipeline? _shadowPipeline;
    private readonly List<Batch> _shadowBatches = [];

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

    // The modules of the program's own shaders, by ShaderStore id, and their pipelines by the pass
    // they draw in.
    private readonly Dictionary<int, (IShader Vertex, IShader Fragment, bool ReadsStreams)> _custom = [];
    private readonly Dictionary<(int Shader, IRenderPass Pass, CullMode Cull, bool Points, Streams Streams), IPipeline> _customPipelines = [];

    // Descriptor sets for draws with a shader of their own: a list per frame slot, handed out in
    // order each frame and kept for the next time the slot comes round.
    private const int SetRingFrames = GpuTextures.RetireFrames;
    private readonly List<IDescriptorSet>[] _drawSets = Enumerable.Range(0, SetRingFrames).Select(_ => new List<IDescriptorSet>()).ToArray();
    private int _drawSetSlot;
    private int _drawSetNext;
    private RenderContext? _lastContext;

    /// <summary>
    /// Creates the renderer from the compiled stages of <c>model.slang</c>, and those of
    /// <c>shadow.slang</c>: the vertex stage that draws into the shadow map, without which nothing
    /// casts a shadow, and the fragment stage that cuts a masked surface out of it, without which
    /// every shadow is solid.
    /// </summary>
    /// <param name="model">The compiled <c>model.slang</c>.</param>
    /// <param name="shadow">The compiled <c>shadow.slang</c>.</param>
    /// <param name="streams">The compiled <c>model_streams.slang</c>, for a mesh with a color or a second texture coordinate at each vertex, or none where every mesh is drawn with <c>model.slang</c>.</param>
    public ModelRenderer(ShaderProgram model, ShaderProgram shadow, ShaderProgram? streams = null)
    {
        _vertexSpv = model.Vertex;
        _streamsVertexSpv = streams?.Vertex ?? model.Vertex;
        _streamsFragmentSpv = streams?.Fragment ?? model.Fragment;
        _fragmentSpv = model.Fragment;
        _plainVertexInputs = model.InputLocations(ShaderStage.Vertex);
        _plainFragmentInputs = model.InputLocations(ShaderStage.Fragment);
        _shadowVertexSpv = shadow.Vertex;
        _shadowMaskSpv = shadow.Fragment;
        // The material and lights sets as model.slang declares them, so a binding added to
        // modelpass.slang reaches every pipeline with no layout typed here.
        _materialBindings = ShaderProgram.Merge([Uniforms], model.LayoutOf(0));
        _lightsBindings = model.LayoutOf(1);
    }

    // The buffer of a program's uniforms, at binding 0 of the material set.
    private static readonly DescriptorSetLayoutBinding Uniforms = new(0, DescriptorType.UniformBuffer, ShaderStageFlags.Vertex | ShaderStageFlags.Fragment);
    private readonly DescriptorSetLayoutBinding[] _materialBindings, _lightsBindings;

    /// <summary>
    /// Draws the meshes meant for <paramref name="target"/> into <paramref name="pass"/>, through
    /// the cameras they were recorded through, or all through <paramref name="viewProjection"/>,
    /// as a reflection probe's face draws the window's.
    /// </summary>
    public void Draw(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, RenderWorld renderWorld, int target,
        Matrix4x4? viewProjection = null, int? lights = null)
    {
        var draws = renderWorld.TryGet<ModelDrawList>();
        var meshes = renderWorld.TryGet<GpuMeshes>();
        var textures = renderWorld.TryGet<GpuTextures>();
        if (draws is null || draws.IsEmpty || meshes is null || textures is null) return;

        var gfx = renderContext.Device;
        var store = renderWorld.TryGet<ShaderStore>();
        BeginFrameOfSets(renderContext);
        RetireUnloadedShaders(store);

        var view = ViewBatches(target, gfx, draws, meshes, textures, store);
        var (batches, ring, offset, blocks) = (view.Batches, view.Ring, view.Offset, view.Blocks);

        IPipeline? pipeline = null;
        var pushed = default(Matrix4x4?);
        foreach (var batch in batches)
        {
            var draw = batch.Custom >= 0 ? draws.Draws[batch.Custom] : default;
            // A shader unloaded after the draw was recorded draws with the model pass's own.
            var program = batch.Custom >= 0 ? store?.Get(draw.Shader) : null;
            var meshStreams = batch.Mesh.Colors is null ? Streams.Default : Streams.PerVertex;
            var streams = program is null
                ? meshStreams == Streams.PerVertex ? Streams.PerVertex : Streams.None
                : CustomModules(gfx, draw.Shader, program).ReadsStreams ? meshStreams : Streams.None;
            var wanted = program is null
                ? Pipeline(gfx, renderPass, renderWorld, batch.Cull, batch.Points, streams)
                : CustomPipeline(gfx, renderPass, renderWorld, draw.Shader, program, batch.Cull, batch.Points, streams);
            if (!ReferenceEquals(wanted, pipeline))
            {
                pipeline = wanted;
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(pipeline, LightsSet(gfx, renderWorld, textures, lights ?? target), index: 1);
                pushed = null;
            }
            var through = viewProjection ?? batch.ViewProjection;
            if (pushed != through)
            {
                pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in through)));
                pushed = through;
            }

            pass.SetBindGroup(pipeline, program is null ? batch.Set ?? MaterialSet(gfx, textures, draw) : DrawSet(gfx, renderContext, renderWorld, textures, draw, program));
            if (streams == Streams.None)
                pass.SetVertexBuffer(0, [batch.Mesh.Vertices, ring!], [0, offset]);
            else
            {
                var (colors, texcoords2) = streams == Streams.PerVertex ? (batch.Mesh.Colors!, batch.Mesh.Texcoords2!) : DefaultStreams(gfx);
                pass.SetVertexBuffer(0, [batch.Mesh.Vertices, ring!, colors, texcoords2], [0, offset, 0, 0]);
            }
            pass.SetIndexBuffer(batch.Mesh.Indices, 0, IndexType.UInt32);
            DrawCalls += DrawSeen(pass, batch, blocks, through);
        }
    }

    // Draws of the pass's own shader gather by mesh and set, and a draw with a shader of its own
    // is a batch alone. With shadowKinds, a batch holds draws that cast the same kind of shadow.
    private void GatherFor(int target, IGraphicsDevice gfx, ModelDrawList draws, GpuMeshes meshes, GpuTextures textures, ShaderStore? store,
        bool shadowKinds) =>
        Gather(draws.Span, draws.Groups, meshes, (in ModelDraw draw) =>
            draw.Target != target ? (Kind.Skip, null)
            : draw.Shader != 0 && store?.Get(draw.Shader) is not null ? (Kind.Alone, null)
            : (Kind.Batched, MaterialSet(gfx, textures, draw)), keepOrderOfTranslucent: true, cullBackFaces: true, shadowKinds);

    // A view's batches and their instances, gathered and written on the first call of a frame.
    private View ViewBatches(int target, IGraphicsDevice gfx, ModelDrawList draws, GpuMeshes meshes, GpuTextures textures, ShaderStore? store)
    {
        if (!_views.TryGetValue(target, out var view))
        {
            // The views of render targets no longer drawn into are let go before another is added.
            foreach (var old in _views.Where(v => v.Key != 0 && _frames - v.Value.Frame > SetRingFrames).Select(v => v.Key).ToArray())
                _views.Remove(old);
            _views[target] = view = new View();
        }
        if (view.Frame != _frames || view.Ring is null)
        {
            GatherFor(target, gfx, draws, meshes, textures, store, shadowKinds: true);
            (view.Ring, view.Offset) = WriteInstances(gfx, draws.Span, static (in ModelDraw draw) => Instance.Of(draw), draws.Groups);
            view.Batches.Clear();
            view.Batches.AddRange(_batches);
            if (view.Blocks.Length < _blockCount) view.Blocks = new Block[Math.Max(_blockCount, view.Blocks.Length * 2)];
            Array.Copy(_blocks, view.Blocks, _blockCount);
            view.Frame = _frames;
        }
        return view;
    }

    private enum ShadowKind : byte { None, Solid, Masked }

    // What a draw is to one call. It is left out, a batch of its own, or batched with the draws
    // that share its mesh and set.
    private enum Kind { Skip, Alone, Batched }

    private delegate (Kind Kind, IDescriptorSet? Set) Classify(in ModelDraw draw);

    // Sorts this call's draws into batches, in the order each batch first appears. With
    // keepOrderOfTranslucent, a draw whose color has alpha below 255 waits until the opaque
    // batches are made and goes after them in its recorded place, joining the batch before it
    // only when that batch is translucent and shares its mesh and set. The depth pass of a shadow
    // reads no color and keeps no order. With cullBackFaces, a single-sided draw is batched apart,
    // for the pipeline that leaves its back faces out. Each group of finished instances is a batch
    // of its own after the opaque draws' batches, classified by its template.
    private void Gather(ReadOnlySpan<ModelDraw> draws, IReadOnlyList<InstanceGroup> groups, GpuMeshes meshes, Classify classify,
        bool keepOrderOfTranslucent, bool cullBackFaces = false, bool shadowKinds = false)
    {
        var masks = _shadowMaskPipeline is not null;
        // A blended surface that is clear anywhere goes through the masked stage too, which drops
        // its shadow in a pattern as dense as it is opaque.
        ShadowKind ShadowOf(in ModelDraw draw) => !shadowKinds || !draw.CastsShadow ? ShadowKind.None
            : masks && (draw.AlphaMode == MaterialAlphaMode.Mask || draw.IsTranslucent) ? ShadowKind.Masked : ShadowKind.Solid;

        _batches.Clear();
        _drawBatch.Clear();
        _batchOf.Clear();
        _classified.Clear();
        for (int i = 0; i < draws.Length; i++)
        {
            ref readonly var draw = ref draws[i];
            var (kind, set) = classify(in draw);
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
            var culled = FacesOf(in draw, cullBackFaces);
            var shadow = ShadowOf(draw);
            if (kind == Kind.Alone && i > 0 && _drawBatch[i - 1] >= 0 && SameAlone(draws, _batches[_drawBatch[i - 1]].Custom, i))
            {
                Grow(_drawBatch[i - 1]);
                _drawBatch.Add(_drawBatch[i - 1]);
                continue;
            }
            _drawBatch.Add(kind == Kind.Alone
                ? AddBatch(mesh, null, i, culled, shadow, draw.ViewProjection)
                : Join(mesh, (draw.Mesh, set, culled, shadow, draw.ViewProjection)));
        }

        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            if (group.Count == 0) continue;
            var (kind, set) = classify(in group.Template);
            if (kind != Kind.Batched || meshes.Get(group.Template.Mesh) is not { } mesh) continue;
            var index = AddBatch(mesh, set, -1, FacesOf(in group.Template, cullBackFaces), ShadowOf(group.Template), group.Template.ViewProjection);
            _batches[index] = _batches[index] with { Group = g, Count = (uint)group.Count };
        }

        var last = (Mesh: -1, Set: (IDescriptorSet?)null, Faces: (CullMode.None, false), Shadow: ShadowKind.None, ViewProjection: default(Matrix4x4));
        var lastBatch = -1;
        for (int i = 0; i < draws.Length; i++)
        {
            if (_drawBatch[i] != Translucent) continue;
            ref readonly var draw = ref draws[i];
            var (kind, set) = _classified[i];
            var mesh = meshes.Get(draw.Mesh)!;
            var culled = FacesOf(in draw, cullBackFaces);
            var shadow = ShadowOf(draw);
            if (kind == Kind.Alone)
            {
                if (i > 0 && _drawBatch[i - 1] >= 0 && SameAlone(draws, _batches[_drawBatch[i - 1]].Custom, i))
                {
                    Grow(_drawBatch[i - 1]);
                    _drawBatch[i] = _drawBatch[i - 1];
                }
                else _drawBatch[i] = AddBatch(mesh, null, i, culled, shadow, draw.ViewProjection);
                lastBatch = -1;
                continue;
            }
            if (lastBatch < 0 || last != (draw.Mesh, set, culled, shadow, draw.ViewProjection))
            {
                lastBatch = AddBatch(mesh, set, -1, culled, shadow, draw.ViewProjection);
                last = (draw.Mesh, set, culled, shadow, draw.ViewProjection);
            }
            Grow(lastBatch);
            _drawBatch[i] = lastBatch;
        }
    }

    // Whether a draw with a shader of its own is drawn with the same values as the batch's first
    // draw, as the copies DrawMeshInstanced records are, sharing one snapshot of the shader's
    // uniforms and textures, so it joins that batch as one more instance.
    private static bool SameAlone(ReadOnlySpan<ModelDraw> draws, int first, int i)
    {
        if (first < 0) return false;
        ref readonly var a = ref draws[first];
        ref readonly var b = ref draws[i];
        return a.Shader == b.Shader && a.Mesh == b.Mesh && a.Target == b.Target && a.ViewProjection == b.ViewProjection && a.DoubleSided == b.DoubleSided
            && a.CastsShadow == b.CastsShadow && a.AlphaMode == b.AlphaMode && a.IsTranslucent == b.IsTranslucent
            && a.Texture == b.Texture && a.NormalMap == b.NormalMap && a.MetallicRoughnessMap == b.MetallicRoughnessMap
            && a.EmissiveMap == b.EmissiveMap && a.OcclusionMap == b.OcclusionMap
            && ReferenceEquals(a.Uniforms, b.Uniforms) && ReferenceEquals(a.ShaderTextures, b.ShaderTextures);
    }

    // Marks a translucent draw until the opaque batches are made.
    private const int Translucent = -2;

    private int AddBatch(GpuMeshes.Entry mesh, IDescriptorSet? set, int custom, (CullMode Cull, bool Points) faces, ShadowKind shadow, in Matrix4x4 viewProjection)
    {
        _batches.Add(new Batch
        {
            Mesh = mesh, Set = set, Custom = custom, Group = -1, Cull = faces.Cull, Points = faces.Points, Shadow = shadow, ViewProjection = viewProjection,
            Count = custom >= 0 ? 1u : 0u,
        });
        return _batches.Count - 1;
    }

    private int Join(GpuMeshes.Entry mesh, (int Mesh, IDescriptorSet? Set, (CullMode Cull, bool Points) Faces, ShadowKind Shadow, Matrix4x4 ViewProjection) key)
    {
        if (!_batchOf.TryGetValue(key, out var index))
            _batchOf[key] = index = AddBatch(mesh, key.Set, -1, key.Faces, key.Shadow, key.ViewProjection);
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
    // instances together, and gives each batch the first of them, counted from the returned offset
    // in bytes. A group's batch is its instances, copied as they are.
    private (IBuffer Ring, ulong Offset) WriteInstances(IGraphicsDevice gfx, ReadOnlySpan<ModelDraw> draws, InstanceOf<Instance> instanceOf,
        IReadOnlyList<InstanceGroup> groups)
    {
        uint total = 0;
        foreach (var batch in _batches) total += batch.Count;
        var slots = (int)total;
        EnsureInstanceRoom(gfx, slots);

        var offset = (ulong)(_ringSlot * _ringCapacity + _ringCursor) * Instance.Size;
        _ringCursor += slots;
        uint first = 0;
        _filled.Clear();
        for (int b = 0; b < _batches.Count; b++)
        {
            var batch = _batches[b];
            batch.First = first;
            _batches[b] = batch;
            _filled.Add(first);
            first += batch.Count;
        }

        var instances = MemoryMarshal.Cast<byte, Instance>(gfx.Map(_instanceRing!)[(int)offset..]);
        for (int i = 0; i < _drawBatch.Count; i++)
        {
            var b = _drawBatch[i];
            if (b < 0) continue;
            instances[(int)_filled[b]++] = instanceOf(in draws[i]);
        }
        _copies.Clear();
        _copyBlocks.Clear();
        _blockCount = 0;
        var copied = 0;
        for (int b = 0; b < _batches.Count; b++)
        {
            var batch = _batches[b];
            if (batch.Group < 0) continue;
            var firstCopy = _copies.Count;
            groups[batch.Group].AddSegments(_copies, (int)batch.First);
            copied += groups[batch.Group].Count;
            batch.BlockStart = _blockCount;
            for (int c = firstCopy; c < _copies.Count; c++)
            {
                _copyBlocks.Add(_blockCount);
                _blockCount += (_copies[c].Count + BlockSize - 1) / BlockSize;
            }
            batch.BlockCount = _blockCount - batch.BlockStart;
            _batches[b] = batch;
        }
        if (_blocks.Length < _blockCount) Array.Resize(ref _blocks, Math.Max(_blockCount, _blocks.Length * 2));
        CopySegments(instances, copied);
        return (_instanceRing!, offset);
    }

    // Copies the frame's group segments into the ring, each into its own range, and finds the
    // box around each block of each, on several threads when there are many instances.
    private unsafe void CopySegments(Span<Instance> instances, int count)
    {
        if (count < ParallelCopyInstances || _copies.Count < 2)
        {
            for (int i = 0; i < _copies.Count; i++)
            {
                var (items, n, at, group) = _copies[i];
                items.AsSpan(0, n).CopyTo(instances[at..]);
                Bound(items, n, at, group.Sphere, _blocks, _copyBlocks[i]);
            }
            return;
        }

        // The ring is mapped memory, which does not move, so its address outlives the span.
        var ring = (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(instances));
        var length = instances.Length;
        var blocks = _blocks;
        Parallel.For(0, _copies.Count, i =>
        {
            var (items, n, at, group) = _copies[i];
            items.AsSpan(0, n).CopyTo(new Span<Instance>((Instance*)ring + at, length - at));
            Bound(items, n, at, group.Sphere, blocks, _copyBlocks[i]);
        });
    }

    // The boxes of a segment's blocks, each around the spheres its instances' meshes fill: the
    // mesh's sphere moved by the world matrix, its radius grown by the matrix's largest scale.
    private static void Bound(Instance[] items, int count, int at, (Vector3 Center, float Radius) sphere, Block[] blocks, int firstBlock)
    {
        for (int start = 0, b = firstBlock; start < count; start += BlockSize, b++)
        {
            var end = Math.Min(count, start + BlockSize);
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            if (float.IsPositiveInfinity(sphere.Radius))
                (min, max) = (new Vector3(float.NegativeInfinity), new Vector3(float.PositiveInfinity));
            else
                for (int i = start; i < end; i++)
                {
                    ref readonly var instance = ref items[i];
                    var c = new Vector4(sphere.Center, 1);
                    var center = new Vector3(Vector4.Dot(instance.WorldX, c), Vector4.Dot(instance.WorldY, c), Vector4.Dot(instance.WorldZ, c));
                    // The length of each of the mesh's axes once placed, the largest of which grows the sphere.
                    var x = new Vector3(instance.WorldX.X, instance.WorldY.X, instance.WorldZ.X).LengthSquared();
                    var y = new Vector3(instance.WorldX.Y, instance.WorldY.Y, instance.WorldZ.Y).LengthSquared();
                    var z = new Vector3(instance.WorldX.Z, instance.WorldY.Z, instance.WorldZ.Z).LengthSquared();
                    var radius = new Vector3(sphere.Radius * MathF.Sqrt(MathF.Max(x, MathF.Max(y, z))));
                    min = Vector3.Min(min, center - radius);
                    max = Vector3.Max(max, center + radius);
                }
            blocks[b] = new Block { First = (uint)(at + start), Count = (uint)(end - start), Min = min, Max = max };
        }
    }

    // The four side planes of a view, as (normal, distance) with the inside where the sum is not
    // negative, from a view-projection that takes row vectors to clip space (Gribb and Hartmann).
    // Near and far are left out, so a depth convention, or a shadow box's reach toward the light,
    // never leaves out what a view draws.
    internal static (Vector4 Left, Vector4 Right, Vector4 Bottom, Vector4 Top) Planes(in Matrix4x4 m)
    {
        var x = new Vector4(m.M11, m.M21, m.M31, m.M41);
        var y = new Vector4(m.M12, m.M22, m.M32, m.M42);
        var w = new Vector4(m.M14, m.M24, m.M34, m.M44);
        return (w + x, w - x, w + y, w - y);
    }

    internal static bool Outside(Vector4 plane, Vector3 min, Vector3 max)
    {
        // The corner of the box farthest along the plane's normal.
        var far = new Vector3(plane.X >= 0 ? max.X : min.X, plane.Y >= 0 ? max.Y : min.Y, plane.Z >= 0 ? max.Z : min.Z);
        return plane.X * far.X + plane.Y * far.Y + plane.Z * far.Z + plane.W < 0;
    }

    /// <summary>Whether a view through <paramref name="viewProjection"/> may see a box, by its four side planes.</summary>
    internal static bool Seen(in Matrix4x4 viewProjection, Vector3 min, Vector3 max)
    {
        var (left, right, bottom, top) = Planes(viewProjection);
        return !(Outside(left, min, max) || Outside(right, min, max) || Outside(bottom, min, max) || Outside(top, min, max));
    }

    // Draws a batch, a group's blocks a view sees as runs of calls and anything else as one, and
    // answers how many calls it made.
    private static int DrawSeen(TrackedRenderPass pass, in Batch batch, Block[] blocks, in Matrix4x4 viewProjection)
    {
        if (batch.BlockCount == 0)
        {
            pass.DrawIndexed(batch.Mesh.IndexCount, batch.Count, 0, 0, batch.First);
            return 1;
        }

        var (left, right, bottom, top) = Planes(viewProjection);
        int calls = 0;
        uint runFirst = 0, runCount = 0;
        for (int i = batch.BlockStart; i < batch.BlockStart + batch.BlockCount; i++)
        {
            ref readonly var block = ref blocks[i];
            var seen = !(Outside(left, block.Min, block.Max) || Outside(right, block.Min, block.Max)
                || Outside(bottom, block.Min, block.Max) || Outside(top, block.Min, block.Max));
            if (seen && runCount > 0 && runFirst + runCount == block.First)
            {
                runCount += block.Count;
                continue;
            }
            if (runCount > 0)
            {
                pass.DrawIndexed(batch.Mesh.IndexCount, runCount, 0, 0, runFirst);
                calls++;
            }
            (runFirst, runCount) = seen ? (block.First, block.Count) : (0u, 0u);
        }
        if (runCount > 0)
        {
            pass.DrawIndexed(batch.Mesh.IndexCount, runCount, 0, 0, runFirst);
            calls++;
        }
        return calls;
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
        (gfx as GraphicsDevice)?.Name(_instanceRing, "Model instances");
    }

    /// <summary>
    /// Draws the meshes of <paramref name="target"/>, the window's at 0, into the shadow map, as
    /// <paramref name="shadow"/>'s lights see them.
    /// </summary>
    /// <remarks>
    /// Each view drawing its own shadow draws it into the same map before its pass, so a
    /// render target's cascades follow its camera. The point lights' faces look the same from every
    /// camera and are drawn once a frame, by the first view.
    /// </remarks>
    public void DrawShadow(RenderContext renderContext, RenderWorld renderWorld, FrameShadow shadow, int target = 0)
    {
        var draws = renderWorld.TryGet<ModelDrawList>();
        var meshes = renderWorld.TryGet<GpuMeshes>();
        var textures = renderWorld.TryGet<GpuTextures>();
        if (draws is null || meshes is null || textures is null || renderContext.Device is not GraphicsDevice device) return;

        var map = ShadowMapFor(device, shadow.TileSize);
        if (!EnsureShadowPipelines(device, map.RenderPass, renderWorld)) return;
        var view = CastingBatches(renderContext, target, device, draws, meshes, textures, renderWorld);

        // One clear for the whole map, then each cascade drawn into its own tile.
        var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            map.RenderPass, map.Framebuffer, map.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(0, 0, 0, 0)));
        // The cascades in the first tiles, and the spot lights in the last, sharing it when there are several.
        for (int t = 0; t < shadow.Cascades.Count; t++)
        {
            var (x, y) = ShadowFit.TileOrigin(t, shadow.TileSize);
            pass.SetViewport(x, y, shadow.TileSize, shadow.TileSize, 0, 1);
            pass.SetScissor(x, y, (uint)shadow.TileSize, (uint)shadow.TileSize);
            DrawShadowBatches(pass, view, shadow.Cascades[t].ViewProjection);
        }
        var spots = shadow.SpotLights ?? [];
        for (int s = 0; s < spots.Count; s++)
        {
            var (x, y, size) = ShadowFit.SpotTileArea(s, spots.Count, shadow.TileSize);
            pass.SetViewport(x, y, size, size, 0, 1);
            pass.SetScissor(x, y, (uint)size, (uint)size);
            DrawShadowBatches(pass, view, spots[s].ViewProjection);
        }
        pass.EndRenderPass();

        // Each shadowed point light's six faces, a layer of the point map each for the lights that
        // matter most and a quarter of one for the rest. A layer is cleared once and each face it
        // holds drawn into its square.
        var points = shadow.PointLights ?? [];
        if (points.Count == 0 || _pointsFrame == _frames) return;
        _pointsFrame = _frames;
        var pointMap = PointShadowMap(device, shadow.PointFaceSize);
        var layers = new SortedDictionary<int, List<(int X, int Y, int Size, Matrix4x4 Face)>>();
        for (int p = 0; p < points.Count; p++)
            for (int f = 0; f < 6; f++)
            {
                var (layer, x, y, size) = ShadowFit.PointFaceArea(p, f, shadow.PointFaceSize);
                if (!layers.TryGetValue(layer, out var faces)) layers[layer] = faces = [];
                faces.Add((x, y, size, points[p].Faces[f]));
            }
        foreach (var (layer, faces) in layers)
        {
            var facePass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
                pointMap.RenderPass, pointMap.Framebuffers[layer], pointMap.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(0, 0, 0, 0)));
            foreach (var (x, y, size, face) in faces)
            {
                facePass.SetViewport(x, y, size, size, 0, 1);
                facePass.SetScissor(x, y, (uint)size, (uint)size);
                DrawShadowBatches(facePass, view, face);
            }
            facePass.EndRenderPass();
        }
    }

    // The shadow pass's pipelines, a solid one and one a material cuts out, made the first time a
    // depth-only pass draws, for the depth-only passes every shadow map and depth target share.
    private bool EnsureShadowPipelines(GraphicsDevice device, IRenderPass depthOnly, RenderWorld renderWorld)
    {
        if (_shadowVertexSpv.IsEmpty) return false;
        if (_shadowPipeline is not null) return true;
        _shadowVertexShader = device.CreateShader(new ShaderDesc(ShaderStage.Vertex, _shadowVertexSpv));
        _shadowPipeline = MakePipeline(device, depthOnly, renderWorld, _shadowVertexShader, fragment: null, shadow: true);
        if (!_shadowMaskSpv.IsEmpty)
        {
            _shadowMaskShader = device.CreateShader(new ShaderDesc(ShaderStage.Fragment, _shadowMaskSpv));
            _shadowMaskPipeline = MakePipeline(device, depthOnly, renderWorld, _shadowVertexShader, _shadowMaskShader, shadow: true);
        }
        return true;
    }

    // The view's batches, which its model pass draws after, those that cast a shadow gathered into
    // _shadowBatches as the kind of shadow their draws cast, a masked one with its maps, which its
    // fragment stage cuts it out by.
    private View CastingBatches(RenderContext renderContext, int target, GraphicsDevice device, ModelDrawList draws, GpuMeshes meshes,
        GpuTextures textures, RenderWorld renderWorld)
    {
        BeginFrameOfSets(renderContext);
        var view = ViewBatches(target, device, draws, meshes, textures, renderWorld.TryGet<ShaderStore>());
        _shadowBatches.Clear();
        foreach (var batch in view.Batches)
        {
            if (batch.Shadow == ShadowKind.None) continue;
            _shadowBatches.Add(batch.Shadow == ShadowKind.Masked && batch.Set is null
                ? batch with { Set = MaterialSet(device, textures, draws.Span[batch.Custom]) }
                : batch);
        }
        return view;
    }

    /// <summary>
    /// Draws the depth of the window's meshes that cast a shadow into <paramref name="depth"/>,
    /// through the cameras they were recorded with, ahead of the window's pass, for the ambient
    /// occlusion worked out from it.
    /// </summary>
    /// <remarks>
    /// It draws with the shadow pass's pipelines, whose depth-only pass is the same at any size, so a
    /// mesh that casts no shadow is left out, and a blended one keeps texels as often as it is opaque.
    /// </remarks>
    internal void DrawDepth(RenderContext renderContext, RenderWorld renderWorld, ShadowMap depth)
    {
        var draws = renderWorld.TryGet<ModelDrawList>();
        var meshes = renderWorld.TryGet<GpuMeshes>();
        var textures = renderWorld.TryGet<GpuTextures>();
        using var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            depth.RenderPass, depth.Framebuffers[0], depth.Extent, LoadOp.Clear, StoreOp.Store, new ClearColor(0, 0, 0, 0)));
        if (draws is null || meshes is null || textures is null || renderContext.Device is not GraphicsDevice device) return;
        if (!EnsureShadowPipelines(device, depth.RenderPass, renderWorld)) return;
        var view = CastingBatches(renderContext, 0, device, draws, meshes, textures, renderWorld);
        pass.SetViewport(0, 0, depth.Extent.Width, depth.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, depth.Extent.Width, depth.Extent.Height);

        var (ring, offset) = (view.Ring!, view.Offset);
        IPipeline? bound = null;
        Matrix4x4? pushed = null;
        foreach (var batch in _shadowBatches)
        {
            var pipeline = batch.Shadow == ShadowKind.Masked ? _shadowMaskPipeline! : _shadowPipeline!;
            if (!ReferenceEquals(pipeline, bound))
            {
                pass.SetPipeline(pipeline);
                (bound, pushed) = (pipeline, null);
            }
            if (pushed != batch.ViewProjection)
            {
                var viewProjection = batch.ViewProjection;
                pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in viewProjection)));
                pushed = viewProjection;
            }
            if (batch.Shadow == ShadowKind.Masked) pass.SetBindGroup(pipeline, batch.Set!);
            pass.SetVertexBuffer(0, [batch.Mesh.Vertices, ring], [0, offset]);
            pass.SetIndexBuffer(batch.Mesh.Indices, 0, IndexType.UInt32);
            DrawCalls += DrawSeen(pass, batch, view.Blocks, batch.ViewProjection);
        }
    }

    // The view's batches that cast a shadow, gathered into _shadowBatches, drawn as a light sees
    // them through lightViewProjection. A solid shadow reads the world matrix alone, and a masked
    // one its color and cutoff too.
    private void DrawShadowBatches(TrackedRenderPass pass, View view, Matrix4x4 lightViewProjection)
    {
        var (ring, offset) = (view.Ring!, view.Offset);
        var push = MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in lightViewProjection));
        pass.SetPipeline(_shadowPipeline!);
        pass.PushConstants(_shadowPipeline!, ShaderStageFlags.Vertex, 0, push);
        IPipeline bound = _shadowPipeline!;
        foreach (var batch in _shadowBatches)
        {
            var pipeline = batch.Shadow == ShadowKind.Masked ? _shadowMaskPipeline! : _shadowPipeline!;
            if (!ReferenceEquals(pipeline, bound))
            {
                pass.SetPipeline(pipeline);
                pass.PushConstants(pipeline, ShaderStageFlags.Vertex, 0, push);
                bound = pipeline;
            }
            if (batch.Shadow == ShadowKind.Masked) pass.SetBindGroup(pipeline, batch.Set!);
            pass.SetVertexBuffer(0, [batch.Mesh.Vertices, ring], [0, offset]);
            pass.SetIndexBuffer(batch.Mesh.Indices, 0, IndexType.UInt32);
            DrawCalls += DrawSeen(pass, batch, view.Blocks, lightViewProjection);
        }
    }

    // The point lights' faces, six layers a light, made when a point light first casts a shadow,
    // or a stand-in of two texels for the lights' set to bind before then.
    private ShadowMap PointShadowMap(GraphicsDevice device, int faceSize)
    {
        if (_pointShadowMap is { } made && made.Extent.Width == faceSize) return made;
        if (_pointShadowMap is { } old) _retiredMaps.Add((_frames, old));
        _pointShadowMap = device.CreateShadowMap((uint)faceSize, ShadowFit.PointLayers);
        device.Name(_pointShadowMap.DepthView.Image, "Point light shadow faces");
        return _pointShadowMap;
    }

    // The map of the cascades and spot lights, two tiles on a side, made again when the tile size changes.
    private ShadowMap ShadowMapFor(GraphicsDevice device, int tileSize)
    {
        if (_shadowMap is { } made && made.Extent.Width == 2 * tileSize) return made;
        if (_shadowMap is { } old) _retiredMaps.Add((_frames, old));
        _shadowMap = device.CreateShadowMap((uint)(2 * tileSize));
        device.Name(_shadowMap.DepthView.Image, "Shadow map");
        return _shadowMap;
    }

    private ShadowMap NoPointShadowMap(GraphicsDevice device) => _noPointShadowMap ??= device.CreateShadowMap(1, 2);

    // Which faces of a draw are left out and whether it is drawn as points: none for a
    // double-sided material or a pass that culls nothing, and otherwise the back or the front ones.
    private static (CullMode Cull, bool Points) FacesOf(in ModelDraw draw, bool cullBackFaces) =>
        (!cullBackFaces || draw.DoubleSided ? CullMode.None : draw.CullFront ? CullMode.Front : CullMode.Back, draw.Points);

    // The model pass's own pipeline, drawing both sides of each face or leaving the back or the
    // front ones out, filled or as points, through model_streams.slang for a mesh with streams.
    private IPipeline Pipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, CullMode cull = CullMode.None, bool points = false,
        Streams streams = Streams.None)
    {
        if (_pipelines.TryGetValue((renderPass, cull, points, streams), out var made)) return made;

        _vertexShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _vertexSpv));
        _fragmentShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _fragmentSpv));
        var (vertex, fragment) = streams == Streams.None
            ? (_vertexShader, _fragmentShader)
            : (_streamsVertexShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, _streamsVertexSpv)),
               _streamsFragmentShader ??= gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, _streamsFragmentSpv)));
        return _pipelines[(renderPass, cull, points, streams)] = MakePipeline(gfx, renderPass, renderWorld, vertex, fragment, cull, points: points,
            streams: streams);
    }

    // A material's own shader's pipeline, which leaves faces out as the model pass's own does.
    private IPipeline CustomPipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, int id, ShaderProgram program,
        CullMode cull = CullMode.None, bool points = false, Streams streams = Streams.None)
    {
        if (_customPipelines.TryGetValue((id, renderPass, cull, points, streams), out var made)) return made;

        var modules = CustomModules(gfx, id, program);
        return _customPipelines[(id, renderPass, cull, points, streams)] = MakePipeline(gfx, renderPass, renderWorld, modules.Vertex, modules.Fragment, cull,
            material: program.OwnTextures(PassTextures).Count > 0 || program.Buffers.Count > 0 ? SetsFor(gfx, id, program).Layout : null, points: points,
            streams: streams);
    }

    // A material's own shader's stages, and whether it reads a mesh's colors and second texture
    // coordinates: a vertex stage of its own that takes an input past the mesh's vertex and its
    // instance, or a fragment stage that takes ModelStreamsOutput, which then draws with
    // model_streams.slang's vertex stage where it has none of its own.
    private (IShader Vertex, IShader Fragment, bool ReadsStreams) CustomModules(IGraphicsDevice gfx, int id, ShaderProgram program)
    {
        if (_custom.TryGetValue(id, out var modules)) return modules;
        var own = program.Stages.TryGetValue(ShaderStage.Vertex, out var vertex);
        var reads = own
            ? program.InputLocations(ShaderStage.Vertex) > _plainVertexInputs
            : program.InputLocations(ShaderStage.Fragment) > _plainFragmentInputs;
        return _custom[id] = (
            gfx.CreateShader(new ShaderDesc(ShaderStage.Vertex, own ? vertex! : reads ? _streamsVertexSpv : _vertexSpv)),
            gfx.CreateShader(new ShaderDesc(ShaderStage.Fragment, program.Fragment)),
            reads);
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
            foreach (var key in _customPipelines.Keys.Where(k => k.Shader == id).ToArray()) _customPipelines.Remove(key);
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

    // A descriptor set for one draw with a shader of its own: its uniform values in this frame's
    // buffer at binding 0, and its texture at binding 1.
    private IDescriptorSet DrawSet(IGraphicsDevice gfx, RenderContext renderContext, RenderWorld renderWorld, GpuTextures textures, ModelDraw draw, ShaderProgram program)
    {
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

        if (renderContext.DynamicAllocator is not { } allocator) return set;

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
            var (view, sampler) = textures.ViewFor(gfx, id);
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(view, sampler, (uint)texture.Binding));
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

    private IPipeline MakePipeline(IGraphicsDevice gfx, IRenderPass renderPass, RenderWorld renderWorld, IShader vertex, IShader? fragment,
        CullMode cull = CullMode.None, IDescriptorSetLayout? material = null, bool shadow = false, bool points = false,
        Streams streams = Streams.None)
    {
        // The shadow pass reads the instance's first five rows, to its emission. Both push the
        // view-projection they draw through.
        var rows = shadow ? 5 : 6;
        var desc = new GraphicsPipelineDesc(
            renderPass,
            vertex,
            fragment,
            BlendEnabled: true,
            Cull: cull,
            Points: points,
            // The mesh's vertices at binding 0, and the instances at binding 1, rows of four floats
            // from location 3 in ModelInstance's order. With streams, its colors at binding 2 and
            // location 9 and its second texture coordinates at binding 3 and location 10, each a
            // vertex at a time from the mesh or the one default for every vertex.
            VertexBindings:
            [
                new VertexInputBindingDesc(0, (uint)Marshal.SizeOf<ModelVertex>()),
                new VertexInputBindingDesc(1, Instance.Size, PerInstance: true),
                .. streams == Streams.None ? Array.Empty<VertexInputBindingDesc>() :
                [
                    new VertexInputBindingDesc(2, streams == Streams.PerVertex ? 4u : 0u),
                    new VertexInputBindingDesc(3, streams == Streams.PerVertex ? 8u : 0u),
                ],
            ],
            VertexAttributes:
            [
                new VertexInputAttributeDesc(0, 0, VertexFormat.Float3, 0),
                new VertexInputAttributeDesc(1, 0, VertexFormat.Float3, 12),
                new VertexInputAttributeDesc(2, 0, VertexFormat.Float2, 24),
                .. Enumerable.Range(0, rows).Select(row => new VertexInputAttributeDesc((uint)(3 + row), 1, VertexFormat.Float4, (uint)(row * 16))),
                .. streams == Streams.None ? Array.Empty<VertexInputAttributeDesc>() :
                [
                    new VertexInputAttributeDesc(9, 2, VertexFormat.UNormR8G8B8A8, 0),
                    new VertexInputAttributeDesc(10, 3, VertexFormat.Float2, 0),
                ],
            ],
            PushConstantRanges: [new PushConstantRange(ShaderStageFlags.Vertex, 0, 64)],
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

    // The lights of this frame as one view sees them, the window's at 0, as a descriptor set: one
    // of the sets of this frame's slot of a ring, a slot per frame in flight so a set the GPU may
    // still read is never written, or a set over an empty buffer when there are no lights and no
    // environment, which the shader reads as "use the fixed light". Binding 0 holds the view's
    // lighting buffer, with its own cascades, binding 1 the shadow map when the view has a shadow,
    // and the white texture otherwise, binding 2 the environment map and binding 3 its sky, or a
    // black cube for each, and binding 4 the point lights' faces, or a stand-in, so all are always
    // valid.
    private IDescriptorSet LightsSet(IGraphicsDevice gfx, RenderWorld renderWorld, GpuTextures textures, int target)
    {
        var (white, whiteSampler) = textures.ViewFor(gfx, 0);
        if (renderWorld.TryGet<FrameLightingBinding>() is not { } frame
            || (frame.LightCount == 0 && !frame.HasEnvironment && !frame.Linear && target != ProbeCaptureLights
                && renderWorld.TryGet<BoundProbes>() is not { Slots.Count: > 0 }))
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
                gfx.UpdateDescriptorSet(_noLights, null, new CombinedImageSamplerBinding(white, whiteSampler, AmbientOcclusionBinding));
                if (EnvironmentCube(gfx, null) is { } black)
                    for (uint b = 2; b < 5 + LightingUboPacker.MaxProbes; b++)
                        if (b != 4) gfx.UpdateDescriptorSet(_noLights, null, new CombinedImageSamplerBinding(black.View, black.Sampler, b));
                if (gfx is GraphicsDevice stub)
                {
                    var none = NoPointShadowMap(stub);
                    gfx.UpdateDescriptorSet(_noLights, null, new CombinedImageSamplerBinding(none.DepthView, none.Sampler, 4));
                }
            }
            return _noLights;
        }

        // Once a frame, however many views draw models in it, the next slot's sets are handed out again.
        if (!ReferenceEquals(frame, _lastFrame))
        {
            _lastFrame = frame;
            _lightSlot = (_lightSlot + 1) % Math.Max(1, gfx.FramesInFlight);
            while (_lightSets.Count <= _lightSlot) _lightSets.Add([]);
            _lightSetOf.Clear();
        }
        if (_lightSetOf.TryGetValue(target, out var made)) return made;
        var slot = _lightSets[_lightSlot];
        if (slot.Count <= _lightSetOf.Count) slot.Add(gfx.CreateDescriptorSet(LightsLayout(gfx)));
        var set = _lightSetOf[target] = slot[_lightSetOf.Count];

        // A render target with a camera of its own has a buffer and a shadow of its own, and a
        // probe's faces the window's at the exposure they are captured at.
        var (binding, shadow) = target == ProbeCaptureLights && renderWorld.TryGet<BoundProbes>()?.CaptureBinding is { } capture
            ? (capture, renderWorld.TryGet<FrameShadow>())
            : target != 0 && renderWorld.TryGet<TargetShadows>() is { } targets && targets.ByTarget.TryGetValue(target, out var own)
            ? (own.Binding, own.Shadow)
            : (frame.Binding, renderWorld.TryGet<FrameShadow>());
        gfx.UpdateDescriptorSet(set, binding, shadow is not null && _shadowMap is { } map
            ? new CombinedImageSamplerBinding(map.DepthView, map.Sampler, 1)
            : new CombinedImageSamplerBinding(white, whiteSampler, 1));
        // The window's occlusion for the window's view, which alone has its buffer say to read it.
        gfx.UpdateDescriptorSet(set, null, target == 0 && renderWorld.TryGet<AmbientOcclusionImage>() is { } occlusion
            ? new CombinedImageSamplerBinding(occlusion.View, occlusion.Sampler, AmbientOcclusionBinding)
            : new CombinedImageSamplerBinding(white, whiteSampler, AmbientOcclusionBinding));
        if (EnvironmentCube(gfx, frame.HasEnvironment ? renderWorld.TryGet<EnvironmentMap>() : null) is { } cube)
        {
            var sky = frame.HasEnvironment ? _sky ?? cube : cube;
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(cube.View, cube.Sampler, 2));
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(sky.View, sky.Sampler, 3));
        }
        if (gfx is GraphicsDevice device)
        {
            var points = shadow is { PointLights.Count: > 0 } ? PointShadowMap(device, shadow.PointFaceSize) : NoPointShadowMap(device);
            gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(points.DepthView, points.Sampler, 4));

            // Each bound probe's cube at its slot, and the black cube past them.
            var slots = renderWorld.TryGet<BoundProbes>()?.Slots ?? [];
            for (int s = 0; s < LightingUboPacker.MaxProbes; s++)
            {
                var probeCube = s < slots.Count ? ProbeCube(device, slots[s]) : EnvironmentCube(gfx, null)!;
                gfx.UpdateDescriptorSet(set, null, new CombinedImageSamplerBinding(probeCube.View, probeCube.Sampler, (uint)(5 + s)));
            }
            ForgetProbeCubes(renderWorld);
        }
        return set;
    }

    // Where modelpass.slang binds ambientOcclusionMap in the lights' set.
    private const uint AmbientOcclusionBinding = 9;

    private IDescriptorSetLayout MaterialLayout(IGraphicsDevice gfx) => _materialLayout ??= gfx.CreateDescriptorSetLayout(_materialBindings);

    // What the particle pass borrows of this one, whose lighting it shares: the two layouts, a
    // material whose base color is a texture, white for none, and the lights of the window or of a
    // render target.
    internal IDescriptorSetLayout MaterialSetLayout(IGraphicsDevice gfx) => MaterialLayout(gfx);
    internal IDescriptorSetLayout LightsSetLayout(IGraphicsDevice gfx) => LightsLayout(gfx);
    internal IDescriptorSet TexturedMaterial(IGraphicsDevice gfx, GpuTextures textures, int texture) =>
        MaterialSet(gfx, textures, new ModelDraw(0, Matrix4x4.Identity, Matrix4x4.Identity, Color.White, texture));
    internal IDescriptorSet LightsFor(IGraphicsDevice gfx, RenderWorld renderWorld, GpuTextures textures, int target) => LightsSet(gfx, renderWorld, textures, target);

    private IDescriptorSetLayout LightsLayout(IGraphicsDevice gfx) => _defaultLayout ??= gfx.CreateDescriptorSetLayout(_lightsBindings);

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
            if (_sky is not null) _retiredCubes.Add((_frames, _sky));
            _environment = device.CreateCubeMap((uint)environment.Size, (uint)environment.MipLevels, environment.Texels);
            _sky = device.CreateCubeMap((uint)environment.SkySize, 1, environment.SkyTexels);
            device.Name(_environment.View.Image, "Environment map");
            device.Name(_sky.View.Image, "Sky");
            _environmentSource = environment;
        }
        return _environment;
    }

    // The six faces a probe is drawn into, made for the first capture and kept for the next.
    private RenderTarget[]? _probeFaces;

    // The key a probe's faces take their lights by, apart from every render target's id.
    private const int ProbeCaptureLights = int.MinValue;

    /// <summary>The width in texels of each face a probe is captured into.</summary>
    internal const int ProbeFaceSize = 64;

    // Each face's way and up, any orientation serving, since the map is read back through each
    // face's own view-projection.
    private static readonly (Vector3 Forward, Vector3 Up)[] ProbeFaceAxes =
    [
        (Vector3.UnitX, Vector3.UnitY), (-Vector3.UnitX, Vector3.UnitY), (Vector3.UnitY, -Vector3.UnitZ),
        (-Vector3.UnitY, Vector3.UnitZ), (Vector3.UnitZ, Vector3.UnitY), (-Vector3.UnitZ, Vector3.UnitY),
    ];

    /// <summary>
    /// Captures the first probe whose capture is out of date, drawing the window's meshes from its
    /// position into six faces, one face a frame, or the first render target's when the window draws
    /// none, which are read back as their frames finish and prefiltered on a worker thread. One
    /// probe at a time, and none while no meshes are drawn. A probe is captured twice, the second
    /// time with the first bound, so the metal in its room reflects the room in the capture rather
    /// than the sky.
    /// </summary>
    /// <remarks>
    /// All six faces in one frame cost it 3 to 5 ms while a level streamed rooms in, each bringing
    /// a probe to capture twice, and a face a frame costs a sixth of that.
    /// </remarks>
    public void CaptureProbes(RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<ReflectionProbes>() is not { } probes || renderContext.Device is not GraphicsDevice device) return;
        if (renderWorld.TryGet<ModelDrawList>() is not { IsEmpty: false } draws) return;
        var source = draws.WindowViewProjection is not null ? 0 : draws.Targets() is [var first, ..] ? first : (int?)null;
        if (source is null) return;
        if (_capture is null)
        {
            var next = probes.ByEntity.Values.FirstOrDefault(p => !p.Capturing && (p.Captured != p.Wanted || p.Passes < ReflectionProbes.Passes));
            if (next is null) return;
            next.Capturing = true;
            var wanted = next.Wanted;
            var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 2, 1, 0.05f, 1000);
            projection.M22 = -projection.M22;
            var viewProjections = new Matrix4x4[6];
            for (int f = 0; f < 6; f++)
                viewProjections[f] = Matrix4x4.CreateLookAt(wanted.Position, wanted.Position + ProbeFaceAxes[f].Forward, ProbeFaceAxes[f].Up) * projection;
            _capture = new ProbeCapture(next, wanted, viewProjections);
        }

        if (_probeFaces is null)
        {
            // Half floats, so a lamp or a sunlit wall comes back as bright as it was drawn.
            _probeFaces = [.. Enumerable.Range(0, 6).Select(_ => device.CreateRenderTarget(ProbeFaceSize, ProbeFaceSize, ImageFormat.R16G16B16A16_Float))];
            for (int f = 0; f < 6; f++) device.Name(_probeFaces[f].ColorView.Image, $"Reflection probe face {f}");
        }
        // Cleared as the window is, in linear light, so an opening shows what the window shows past the room.
        var clear = renderWorld.TryGet<ClearColor>() is { } windowClear
            ? new ClearColor(BloomRenderer.SrgbToLinear(windowClear.R), BloomRenderer.SrgbToLinear(windowClear.G), BloomRenderer.SrgbToLinear(windowClear.B), 1)
            : ClearColor.Black;

        var capture = _capture;
        var face = capture.Next++;
        var target = _probeFaces[face];
        var pass = renderContext.BeginTrackedRenderPass(new RenderPassDescriptor(
            target.RenderPass, target.Framebuffer, target.Extent, LoadOp.Clear, StoreOp.Store, clear));
        pass.SetViewport(0, 0, target.Extent.Width, target.Extent.Height, 0, 1);
        pass.SetScissor(0, 0, target.Extent.Width, target.Extent.Height);
        Draw(pass, target.RenderPass, renderContext, renderWorld, source.Value, capture.ViewProjections[face], ProbeCaptureLights);
        pass.EndRenderPass();
        device.RequestReadback(target.ColorView.Image, pixels =>
        {
            capture.Faces[face] = pixels;
            if (++capture.Arrived < 6) return;
            Task.Run(() =>
            {
                var map = EnvironmentMap.FromCapture(capture.Faces, ProbeFaceSize, capture.ViewProjections, capture.Wanted.Position);
                capture.Probe.Done = new ReflectionProbes.Capture(map, capture.Wanted);
            });
        });
        // The next probe waits for the next frame once this one's faces are all drawn.
        if (capture.Next == 6) _capture = null;
    }

    // A probe's capture under way: its faces drawn so far, a face a frame, and those read back.
    private sealed class ProbeCapture(ReflectionProbes.Probe probe, (Vector3 Position, Vector3 Size, int Capture) wanted, Matrix4x4[] viewProjections)
    {
        public ReflectionProbes.Probe Probe { get; } = probe;
        public (Vector3 Position, Vector3 Size, int Capture) Wanted { get; } = wanted;
        public Matrix4x4[] ViewProjections { get; } = viewProjections;
        public byte[][] Faces { get; } = new byte[6][];
        public int Next;
        public int Arrived;
    }

    private ProbeCapture? _capture;

    // Each probe's cube, uploaded when its capture is new, kept by the capture it was made from.
    private readonly Dictionary<ReflectionProbes.Probe, (EnvironmentMap Map, CubeMap Cube)> _probeCubes = [];

    private CubeMap ProbeCube(GraphicsDevice device, ReflectionProbes.Probe probe)
    {
        var map = probe.Map!;
        if (_probeCubes.TryGetValue(probe, out var made) && ReferenceEquals(made.Map, map)) return made.Cube;
        if (made.Cube is not null) _retiredCubes.Add((_frames, made.Cube));
        var cube = device.CreateCubeMap((uint)map.Size, (uint)map.MipLevels, map.Texels);
        device.Name(cube.View.Image, "Reflection probe");
        _probeCubes[probe] = (map, cube);
        return cube;
    }

    // Lets the cubes of probes gone go, once no frame in flight reads them.
    private void ForgetProbeCubes(RenderWorld renderWorld)
    {
        if (_probeCubes.Count == 0) return;
        var probes = renderWorld.TryGet<ReflectionProbes>();
        foreach (var gone in _probeCubes.Keys.Where(p => probes is null || !probes.ByEntity.ContainsValue(p)).ToArray())
        {
            _retiredCubes.Add((_frames, _probeCubes[gone].Cube));
            _probeCubes.Remove(gone);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var set in _lightSets.SelectMany(s => s)) set.Dispose();
        foreach (var sets in _drawSets)
            foreach (var set in sets) set.Dispose();
        foreach (var (vertex, fragment, _) in _custom.Values)
        {
            vertex.Dispose();
            fragment.Dispose();
        }
        foreach (var (set, _) in _materialSets.Values) set.Dispose();
        foreach (var (_, buffer) in _retiredBuffers) buffer.Dispose();
        foreach (var (_, map) in _retiredMaps) map.Dispose();
        _instanceRing?.Dispose();
        _materialSets.Clear();
        _noUniforms?.Dispose();
        _materialLayout?.Dispose();
        _noLights?.Dispose();
        _shadowMap?.Dispose();
        _pointShadowMap?.Dispose();
        _noPointShadowMap?.Dispose();
        _environment?.Dispose();
        _sky?.Dispose();
        _noEnvironment?.Dispose();
        foreach (var (_, cube) in _retiredCubes) cube.Dispose();
        foreach (var (_, cube) in _probeCubes.Values) cube.Dispose();
        if (_probeFaces is not null)
            foreach (var face in _probeFaces) face.Dispose();
        foreach (var sets in _shaderSets.Values) sets.Dispose();
        foreach (var (_, retired) in _retiredShaderSets) retired.Dispose();
        _shadowMaskShader?.Dispose();
        _shadowVertexShader?.Dispose();
        _noLightsBuffer?.Dispose();
        _defaultLayout?.Dispose();
        _fragmentShader?.Dispose();
        _vertexShader?.Dispose();
        _streamsVertexShader?.Dispose();
        _streamsFragmentShader?.Dispose();
        _noBuffer?.Dispose();
        _defaultColor?.Dispose();
        _defaultTexcoords2?.Dispose();
    }
}

/// <summary>
/// Render graph node that draws the window's share of the <see cref="ModelDrawList"/> into the
/// swapchain pass, after the ECS meshes and before the immediate shapes.
/// </summary>
internal sealed class ModelNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<ActiveSwapchainPass>() is not { } active || renderWorld.TryGet<SwapchainTarget>() is not { } swapchain) return;
        // With bloom on, the window's models were drawn into the HDR frame.
        if (renderWorld.TryGet<BloomFrame>() is not null) return;
        renderWorld.TryGet<ModelRenderer>()?.Draw(active.Pass, swapchain.RenderPass, renderContext, renderWorld, target: 0);
        renderWorld.TryGet<ParticleRenderer>()?.Draw(active.Pass, swapchain.RenderPass, renderContext, renderWorld);
    }
}
