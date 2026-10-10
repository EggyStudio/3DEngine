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
internal sealed partial class ModelRenderer : IDisposable
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
        internal static readonly float[] Linear = Enumerable.Range(0, 256).Select(value =>
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
        public bool Blend;
        public bool Depth;
        // World to clip space, pushed for the call, the camera's that its draws were recorded through.
        public Matrix4x4 ViewProjection;
        // The pixels its draws are kept to, or null for the whole target.
        public ScissorRect? Scissor;
        // What the shadow pass draws it with: nothing, a solid shadow, or one its material cuts out.
        public ShadowKind Shadow;
        // The shares of the radius each color travels under the surface and the radius, where its
        // draws scatter light under it (ModelDraw.Subsurface), or zero, a batch holding one alone.
        public Vector4 Subsurface;
        // How thick its draws' parts are at most where they scatter (ModelDraw.SubsurfaceThickness), or 0.
        public float Thickness;
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
    private readonly Dictionary<(IRenderPass Pass, CullMode Cull, bool Points, bool Blend, Streams Streams, bool Depth), IPipeline> _pipelines = [];

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

    // This call's batches, which batch each draw went into (-1 for none), and the batch of each
    // mesh and set, kept between calls so a frame allocates nothing once they have grown.
    private readonly List<Batch> _batches = [];
    private readonly List<int> _drawBatch = [];
    private readonly List<uint> _filled = [];

    private readonly Dictionary<(int Mesh, IDescriptorSet? Set, (CullMode Cull, bool Points, bool Blend, bool Depth) Faces, ShadowKind Shadow, Matrix4x4 ViewProjection, ScissorRect? Scissor, Vector4 Subsurface, float Thickness), int> _batchOf = [];

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
    // The shadow last drawn into the map this frame, which a view sharing it does not draw again,
    // and how many shadows the map was drawn with this frame.
    private FrameShadow? _drawnShadow;
    private long _shadowsFrame = -1;

    /// <summary>How many times the shadow map was drawn this frame, once for each shadow views did not share.</summary>
    internal int ShadowMapsDrawn { get; private set; }
    private readonly List<(Kind Kind, IDescriptorSet? Set)> _classified = [];

    // This frame's sets by the draws' five texture ids, cleared each frame, since an id's view can
    // change between frames when its texture is reloaded.
    private readonly Dictionary<(int, int, int, int, int), IDescriptorSet> _setByIds = [];

    /// <summary>How many sets the model pass's own draws hold, one per combination of maps in use.</summary>
    internal int MaterialSetCount => _materialSets.Count;

    /// <summary>How many draw calls the model and shadow passes recorded this frame.</summary>
    internal int DrawCalls { get; private set; }

    // This frame's draw calls by pass, read between frames as DrawCalls is.
    private readonly Dictionary<string, int> _callsByPass = [];

    /// <summary>How many draw calls each pass recorded this frame, the camera's, the depth's, each cascade's and the lights'.</summary>
    internal IReadOnlyDictionary<string, int> CallsByPass => _callsByPass;

    private void Count(string pass, int calls)
    {
        DrawCalls += calls;
        _callsByPass[pass] = _callsByPass.GetValueOrDefault(pass) + calls;
    }
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
    // The environment map filtered on the GPU and its sky, made in the frame that first sees the map.
    private FilteredCube? _environment;
    private FilteredCube? _sky;
    private EnvironmentMap? _environmentSource;
    private CubeMap? _noEnvironment;
    // Cubes, probes' maps, the environment's image and the descriptor sets of filters let go, each
    // freed once no frame in flight reads it.
    private readonly List<(long Frame, IDisposable Cube)> _retiredCubes = [];
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
    // A program's shader's fragment stage with its color held to an eight-bit frame's, for the HDR frame.
    private readonly Dictionary<int, IShader> _heldFragments = [];
    private readonly Dictionary<(int Shader, IRenderPass Pass, CullMode Cull, bool Points, bool Blend, Streams Streams, bool Depth, bool Held), IPipeline> _customPipelines = [];

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
    /// <param name="subsurface">The compiled <c>subsurface.slang</c>, which draws the window's meshes that scatter light under their surface again for <see cref="SubsurfaceRenderer"/>, or none where no light scatters.</param>
    public ModelRenderer(ShaderProgram model, ShaderProgram shadow, ShaderProgram? streams = null, ShaderProgram? subsurface = null)
    {
        if (subsurface is not null)
            (_subsurfaceVertexSpv, _subsurfaceFragmentSpv, _subsurfaceDepthBindings) = (subsurface.Vertex, subsurface.Fragment, subsurface.LayoutOf(2));
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
    /// as a reflection probe's face draws the window's, with a shader of the program's own's color
    /// held to what an eight-bit frame keeps of it where <paramref name="held"/>, as the HDR frame
    /// asks (<see cref="ShaderProgram.HeldToEightBits"/>).
    /// </summary>
    public void Draw(TrackedRenderPass pass, IRenderPass renderPass, RenderContext renderContext, RenderWorld renderWorld, int target,
        Matrix4x4? viewProjection = null, int? lights = null, bool held = false)
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
        // The scissor the pass was begun with is the whole target, which a batch kept to some of it
        // changes, and a face of a probe, drawn through a camera of its own, keeps.
        ScissorRect? scissored = null;
        var (frustum, culledThrough) = (default(Frustum), default(Matrix4x4?));
        foreach (ref readonly var batch in CollectionsMarshal.AsSpan(batches))
        {
            var through = viewProjection ?? batch.ViewProjection;
            if (culledThrough != through) (frustum, culledThrough) = (new Frustum(through, depth: false), through);
            if (!frustum.SeesAny(batch, blocks)) continue;
            var keptTo = viewProjection is null ? batch.Scissor : null;
            if (keptTo != scissored)
            {
                if (keptTo is { } rect) pass.SetScissor(rect.X, rect.Y, (uint)Math.Max(0, rect.Width), (uint)Math.Max(0, rect.Height));
                else pass.SetScissor(0, 0, pass.Extent.Width, pass.Extent.Height);
                scissored = keptTo;
            }
            var draw = batch.Custom >= 0 ? draws.Draws[batch.Custom] : default;
            // A shader unloaded after the draw was recorded draws with the model pass's own.
            var program = batch.Custom >= 0 ? store?.Get(draw.Shader) : null;
            var meshStreams = batch.Mesh.Colors is null ? Streams.Default : Streams.PerVertex;
            var streams = program is null
                ? meshStreams == Streams.PerVertex ? Streams.PerVertex : Streams.None
                : CustomModules(gfx, draw.Shader, program).ReadsStreams ? meshStreams : Streams.None;
            var wanted = program is null
                ? Pipeline(gfx, renderPass, renderWorld, batch.Cull, batch.Points, batch.Blend, streams, batch.Depth)
                : CustomPipeline(gfx, renderPass, renderWorld, draw.Shader, program, batch.Cull, batch.Points, batch.Blend, streams, batch.Depth, held);
            if (!ReferenceEquals(wanted, pipeline))
            {
                pipeline = wanted;
                pass.SetPipeline(pipeline);
                pass.SetBindGroup(pipeline, LightsSet(gfx, renderWorld, textures, lights ?? target), index: 1);
                pushed = null;
            }
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
            Count(target == 0 ? "camera" : "targets", DrawSeen(pass, batch, blocks, frustum));
        }
        if (scissored is not null) pass.SetScissor(0, 0, pass.Extent.Width, pass.Extent.Height);
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
        // By the shader and not its pipeline, which the first pass to draw a shadow or a depth makes,
        // since the frame's batches are gathered before either.
        var masks = !_shadowMaskSpv.IsEmpty;
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
                ? AddBatch(mesh, null, i, culled, shadow, draw.ViewProjection, draw.Scissor)
                : Join(mesh, (draw.Mesh, set, culled, shadow, draw.ViewProjection, draw.Scissor, draw.Subsurface, ThicknessOf(in draw))));
        }

        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            if (group.Count == 0) continue;
            var (kind, set) = classify(in group.Template);
            if (kind != Kind.Batched || meshes.Get(group.Template.Mesh) is not { } mesh) continue;
            var index = AddBatch(mesh, set, -1, FacesOf(in group.Template, cullBackFaces), ShadowOf(group.Template), group.Template.ViewProjection, group.Template.Scissor,
                group.Template.Subsurface, ThicknessOf(in group.Template));
            _batches[index] = _batches[index] with { Group = g, Count = (uint)group.Count };
        }

        var last = (Mesh: -1, Set: (IDescriptorSet?)null, Faces: (CullMode.None, false, true, true), Shadow: ShadowKind.None, ViewProjection: default(Matrix4x4), Scissor: (ScissorRect?)null);
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
                else _drawBatch[i] = AddBatch(mesh, null, i, culled, shadow, draw.ViewProjection, draw.Scissor);
                lastBatch = -1;
                continue;
            }
            if (lastBatch < 0 || last != (draw.Mesh, set, culled, shadow, draw.ViewProjection, draw.Scissor))
            {
                lastBatch = AddBatch(mesh, set, -1, culled, shadow, draw.ViewProjection, draw.Scissor);
                last = (draw.Mesh, set, culled, shadow, draw.ViewProjection, draw.Scissor);
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
        return a.Shader == b.Shader && a.Mesh == b.Mesh && a.Target == b.Target && a.ViewProjection == b.ViewProjection && a.Scissor == b.Scissor && a.DoubleSided == b.DoubleSided
            && a.CastsShadow == b.CastsShadow && a.AlphaMode == b.AlphaMode && a.IsTranslucent == b.IsTranslucent
            && a.Texture == b.Texture && a.NormalMap == b.NormalMap && a.MetallicRoughnessMap == b.MetallicRoughnessMap
            && a.EmissiveMap == b.EmissiveMap && a.OcclusionMap == b.OcclusionMap
            && a.CullFront == b.CullFront && a.Points == b.Points && a.ColorBlend == b.ColorBlend && a.DepthWrite == b.DepthWrite
            && ReferenceEquals(a.Uniforms, b.Uniforms) && ReferenceEquals(a.ShaderTextures, b.ShaderTextures);
    }

    // Marks a translucent draw until the opaque batches are made.
    private const int Translucent = -2;

    private int AddBatch(GpuMeshes.Entry mesh, IDescriptorSet? set, int custom, (CullMode Cull, bool Points, bool Blend, bool Depth) faces, ShadowKind shadow, in Matrix4x4 viewProjection,
        ScissorRect? scissor, Vector4 subsurface = default, float thickness = 0)
    {
        _batches.Add(new Batch
        {
            Mesh = mesh, Set = set, Custom = custom, Group = -1, Cull = faces.Cull, Points = faces.Points, Blend = faces.Blend, Depth = faces.Depth, Shadow = shadow, ViewProjection = viewProjection,
            Scissor = scissor, Subsurface = subsurface, Thickness = thickness,
            Count = custom >= 0 ? 1u : 0u,
        });
        return _batches.Count - 1;
    }

    // A draw's parts' thickness where it scatters, so draws that differ only where nothing scatters share a batch.
    private static float ThicknessOf(in ModelDraw draw) => draw.Subsurface.W > 0 ? draw.SubsurfaceThickness : 0;

    private int Join(GpuMeshes.Entry mesh, (int Mesh, IDescriptorSet? Set, (CullMode Cull, bool Points, bool Blend, bool Depth) Faces, ShadowKind Shadow, Matrix4x4 ViewProjection, ScissorRect? Scissor, Vector4 Subsurface, float Thickness) key)
    {
        if (!_batchOf.TryGetValue(key, out var index))
            _batchOf[key] = index = AddBatch(mesh, key.Set, -1, key.Faces, key.Shadow, key.ViewProjection, key.Scissor, key.Subsurface, key.Thickness);
        Grow(index);
        return index;
    }

    private void Grow(int index)
    {
        var batch = _batches[index];
        batch.Count++;
        _batches[index] = batch;
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
        foreach (var fragment in _heldFragments.Values) fragment.Dispose();
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
        _noBounce?.Dispose();
        _noScreen?.Dispose();
        _noGiLights?.Dispose();
        _noRays?.Dispose();
        _lightLinear?.Dispose();
        _lightNearest?.Dispose();
        foreach (var (_, cube) in _retiredCubes) cube.Dispose();
        foreach (var map in _probeMaps) map.Dispose();
        _noIrradiance?.Dispose();
        if (_probeFaces is not null)
            foreach (var face in _probeFaces) face.Dispose();
        foreach (var sets in _shaderSets.Values) sets.Dispose();
        foreach (var (_, retired) in _retiredShaderSets) retired.Dispose();
        DisposeSubsurface();
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
        // The window's models were drawn into the HDR frame, on every device that makes one.
        if (renderWorld.TryGet<BloomFrame>() is not null) return;
        renderWorld.TryGet<ModelRenderer>()?.Draw(active.Pass, swapchain.RenderPass, renderContext, renderWorld, target: 0);
        renderWorld.TryGet<ParticleRenderer>()?.Draw(active.Pass, swapchain.RenderPass, renderContext, renderWorld);
    }
}

/// <summary>
/// Render graph node that gathers the window's batches and writes their instances, once a frame,
/// ahead of the depth, the shadows and the model pass that draw them.
/// </summary>
internal sealed class ModelBatchesNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld) =>
        renderWorld.TryGet<ModelRenderer>()?.GatherWindow(renderContext, renderWorld);
}
