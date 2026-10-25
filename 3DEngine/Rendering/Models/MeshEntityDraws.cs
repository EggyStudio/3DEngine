using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// Records every entity with a <see cref="Mesh"/> and a <see cref="Material"/> into the frame's
/// <see cref="ModelDrawList"/>, so mesh entities are drawn by the same pass as <c>DrawModel</c>.
/// </summary>
/// <remarks>
/// <para>
/// Runs in <see cref="Stage.Render"/> and draws into the window through the first entity with a
/// <see cref="Camera"/> that has no render texture, and into each camera's render texture through
/// it. With no camera entity for the window, they are drawn into it through the camera of the first
/// <c>BeginMode3D</c> of the frame drawn there (<see cref="Mode3DCamera"/>), so a program on the flat
/// API that loads a scene sees its models. With neither, mesh entities are not drawn.
/// </para>
/// <para>
/// Entities are gathered in chunks of 4096, a thread each, and a chunk none of whose entities' mesh,
/// material or transforms changed since the last frame keeps what it gathered then, so a level
/// standing still costs a check of each entity's change ticks rather than its instance written
/// again. A write the change detection does not see, through a store's raw array, leaves an
/// entity where it was drawn until something marks it.
/// </para>
/// <para>
/// A mesh's arrays are uploaded to <see cref="MeshStore"/> the first time they are drawn and freed
/// on the first frame no entity holds them, drawn or not, and a mesh the program uploaded is drawn
/// as it is and never freed here. A material's base color texture is copied from the
/// asset store into <see cref="TextureStore"/> once it has loaded, and copied again when the asset
/// is reloaded. Only the first mip level of an RGBA8 texture is copied, and the GPU makes the rest.
/// </para>
/// <para>
/// An entity whose material blends, with alpha below 1 in its albedo or base color texture, shows
/// what is behind it, so it is recorded after the opaque ones, from the farthest from the camera
/// to the nearest, which the model pass keeps.
/// </para>
/// <para>
/// An entity whose material names a program's shader is recorded as a draw of its own, as
/// <c>DrawMesh</c> records one, with the shader's values as the program last set them, copied once
/// a frame for every entity drawn with it. One whose <see cref="Visible"/> is false is drawn nowhere
/// and keeps its mesh, and one whose <see cref="ShadowOnly"/> is true is drawn into the shadow maps
/// alone, each a bool the entity's chunk is gathered again for when it changes. A material whose
/// <see cref="Material.CastsShadows"/> or <see cref="Material.BouncesLight"/> is false leaves the
/// entity out of the shadow maps and the scene's field, or the field alone, as a
/// <see cref="ModelMaterial"/>'s do a mesh drawn by a call. The entities
/// recorded in a frame are the frame profile's <c>entities.drawn</c>, before each camera's frustum
/// leaves out those it cannot see.
/// </para>
/// <para>
/// Each entity's draw is kept from frame to frame and built again only when its mesh or material
/// differs from what it was built from, a texture it names was still loading, or a texture was
/// replaced. A mesh no entity holds lets go of its own looks and groups alone, so a voxel game's
/// section built again as a block changes leaves the other entities' looks and chunks as they were. An opaque entity's instance is then written straight into the
/// <see cref="InstanceGroup"/> of its mesh and maps, from the kept instance with the frame's
/// world matrix, and the model pass copies each group as it is and draws it as one batch
/// (RENDERING.md section 6). An instance holds nothing of the camera, so the entities are gathered
/// once a frame, and each camera's groups hold the same instances.
/// </para>
/// <para>
/// Past 4,096 entities they are recorded in chunks of that many, each on a thread of its own and
/// into buffers of its own. An entity that needs its mesh uploaded or its look built is left by its
/// chunk and recorded after the chunks, on the calling thread, since those change what the other
/// chunks read.
/// </para>
/// </remarks>
internal sealed partial class MeshEntityDraws
{
    private readonly Dictionary<Vector3[], int> _meshes = new(ReferenceEqualityComparer.Instance);

    // The sphere around each mesh's positions, by mesh id, which a group carries for the pass to
    // cull its instances by.
    private readonly Dictionary<int, (Vector3 Center, float Radius)> _spheres = [];
    private readonly HashSet<Vector3[]> _seen = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<AssetId, int> _textures = [];
    private readonly List<(float Distance, ModelDraw Draw)> _translucent = [];

    // The frame's translucent draws in order, farthest first, handed to the draw list under one lock.
    private readonly List<ModelDraw> _sorted = [];

    // Each albedo's sRGB bytes, since entities share a few materials and three powers an entity
    // cost a measurable part of the frame (RENDERING.md section 6). Forgotten past a few thousand.
    private readonly Dictionary<Vector4, Color> _colors = [];

    // Each entity's look as last found, by entity id, with the stamp its slot had then. A kept look
    // of an older generation was found before the looks were forgotten, and one whose slot's stamp
    // has moved on was let go with its mesh, its slot perhaps another's since. Sixteen bytes, since
    // a frame reads every entity's.
    private Kept[] _kept = [];
    private int _generation = 1;
    private long _frame;

    private struct Kept
    {
        public int Generation;
        public int Mesh;
        public int Look;
        public int Stamp;
    }

    // The look and group slots let go with their meshes, used again before the arrays grow, and
    // the stamps a look slot takes each time it is taken or let go.
    private readonly Stack<int> _freeLooks = new();
    private readonly Stack<int> _freeGroups = new();
    private int _stamps;

    // The meshes the program uploaded that entities held this frame and the frame before, by id,
    // with the bounds each sphere was made from, and the ids any entity holds, gathered as a
    // frame lets go of the looks of the rest.
    private readonly HashSet<int> _uploadedSeen = [];
    private HashSet<int> _uploadedLast = [];
    private readonly Dictionary<int, BoundingBox> _uploadedBounds = [];
    private readonly HashSet<int> _live = [];

    // Each mesh and material entities are drawn with, built once, the draw with the world matrix
    // and camera left to the frame, the instance with the material's factors, and the group it is
    // written into. Entities share a few, so the comparison each makes a frame reads one already in
    // the cache, where a draw and a material kept for each entity cost a frame several misses each.
    private Look[] _looks = new Look[16];
    private int _lookCount;
    private readonly Dictionary<(int Mesh, Material Material, bool ShadowOnly), int> _lookOf = [];

    private struct Look
    {
        // Compared rather than relying on change marks, since a material replaced by Add marks none.
        public Material Material;
        public bool ShadowOnly;
        public ModelDraw Draw;
        public ModelRenderer.Instance Instance;
        // Its group in _groups, or TranslucentLook or ShadedLook for a draw recorded alone.
        public int Group;
        // A texture it names was still loading, so it is built again on a later frame.
        public bool Pending;
        public long Built;
        // Taken from _stamps as the slot is taken or let go, and whether it is let go.
        public int Stamp;
        public bool Free;
    }

    // What the groups opaque entities are written into share, by index, kept from frame to frame,
    // and each camera's groups, the same index for the same mesh and maps, which hold the same
    // instances under a template of the camera's own.
    private readonly List<ModelDraw> _groups = [];
    private readonly Dictionary<GroupKey, int> _groupOf = [];
    private readonly List<List<InstanceGroup>> _cameraGroups = [];

    private readonly record struct GroupKey(int Mesh, int Texture, int NormalMap, int MetallicRoughnessMap, int EmissiveMap,
        int OcclusionMap, bool DoubleSided, MaterialAlphaMode AlphaMode, Vector4 Subsurface, bool ShadowOnly, bool CastsShadow, bool BouncesLight);

    // A look's group for a translucent draw, sorted far to near, and for one with a program's
    // shader, which no group draws, each recorded alone.
    private const int TranslucentLook = -1;
    private const int ShadedLook = -2;

    // The shader store of the world last recorded, and the values of each shader its entities are
    // drawn with this frame, copied once for them all.
    private ShaderStore? _shaders;
    private readonly Dictionary<int, (byte[]? Uniforms, int[]? Textures)> _values = [];

    // How many entities the frame recorded, every chunk's, kept ones included.
    private int _drawn;

    /// <summary>How many mesh entities the last frame recorded, before any camera's frustum.</summary>
    internal int Drawn => _drawn;

    // Entities are recorded in chunks of this many, a thread each, once there is more than one.
    private const int ChunkSize = 4096;
    private Chunk[] _chunks = [];

    // What a chunk gathers apart from the others, the positions arrays it drew, the mesh of the
    // entity before, its translucent draws placed in the world, the entities it left for after,
    // and its instances, a buffer for each group. The buffers and their counts are the chunk's
    // own, since threads counting into objects that lie side by side wait on each other's writes
    // to the same line.
    private sealed class Chunk
    {
        public readonly HashSet<Vector3[]> Seen = new(ReferenceEqualityComparer.Instance);
        public readonly HashSet<int> UploadedSeen = [];
        public int LastUploaded;
        public readonly List<ModelDraw> Translucent = [];
        public readonly List<ModelDraw> Shaded = [];
        public readonly List<int> Deferred = [];
        public int Placed;
        public Vector3[]? LastPositions;
        public int LastMesh;
        public Writer[] Writers = [];

        // What the chunk was recorded from, which a later frame keeps it by when none of its
        // entities changed. That is its range of dense indices, the entity at each, the generation
        // of the looks it used, and whether it could be kept at all, which it cannot when it left
        // an entity to the pass after it or drew one whose look waited on a texture.
        public int Start = -1, End = -1;
        public int[] Entities = [];
        public long Generation = -1;
        public bool Keepable;
        public bool Waiting;

        // Padded to a cache line, since the chunks' arrays of writers are allocated one after
        // another and every entity counts into one.
        public struct Writer
        {
            public ModelRenderer.Instance[] Items;
            public int Count;
#pragma warning disable CS0169 // Never read, the padding.
            private long _pad0, _pad1, _pad2, _pad3, _pad4, _pad5;
#pragma warning restore CS0169
        }

        // Empties the buffers for a frame of this many groups.
        public void Begin(int groups)
        {
            if (Writers.Length < groups) Array.Resize(ref Writers, groups);
            for (int i = 0; i < Writers.Length; i++) Writers[i].Count = 0;
        }

        // The next instance of a group, in a buffer grown as it fills. A group made after the
        // chunks began, by the pass after them, grows the array.
        public ref ModelRenderer.Instance Next(int group)
        {
            if (group >= Writers.Length) Array.Resize(ref Writers, Math.Max(group + 1, Writers.Length * 2));
            ref var writer = ref Writers[group];
            if (writer.Items is null) writer.Items = new ModelRenderer.Instance[64];
            else if (writer.Count == writer.Items.Length) Array.Resize(ref writer.Items, writer.Items.Length * 2);
            return ref writer.Items[writer.Count++];
        }

        public void Clear()
        {
            Seen.Clear();
            UploadedSeen.Clear();
            LastUploaded = 0;
            Translucent.Clear();
            Shaded.Clear();
            Deferred.Clear();
            Placed = 0;
            LastPositions = null;
            Waiting = false;
            Keepable = false;
        }
    }

    /// <summary>How many meshes are uploaded for entities.</summary>
    public int MeshCount => _meshes.Count;

    /// <summary>How many texture assets are copied for entities.</summary>
    public int TextureCount => _textures.Count;

    /// <summary>How many looks are kept, each a mesh and material some entity was drawn with, those let go with their meshes left out.</summary>
    internal int LookCount => _lookCount - _freeLooks.Count;

    /// <summary>The system, for <see cref="Stage.Render"/>.</summary>
    public static void Run(World world)
    {
        if (!world.TryGetResource<EcsWorld>(out var ecs) || !world.TryGetResource<ModelDrawList>(out var draws)) return;
        var self = world.GetOrInsertResource(static () => new MeshEntityDraws());
        self.Record(world, ecs, draws);
    }

    private void Record(World world, EcsWorld ecs, ModelDrawList draws)
    {
        var meshes = world.Resource<MeshStore>();
        var textures = world.Resource<TextureStore>();
        world.TryGetResource<Assets<TextureAsset>>(out var assets);
        ForgetReloadedTextures(world, textures);

        _frame++;
        _seen.Clear();
        _uploadedSeen.Clear();
        _translucent.Clear();
        _sorted.Clear();
        _values.Clear();
        _drawn = 0;
        _shaders = world.TryGetResource<ShaderStore>(out var shaders) ? shaders : null;
        if (_colors.Count > 4096) _colors.Clear();
        // With no mesh entity nothing holds a mesh, and with some every one they hold is seen as
        // they are gathered.
        var gathered = ecs.Count<Mesh>() == 0;
        if (ecs.Count<Mesh>() > 0 && Cameras(world, ecs) is { Count: > 0 } cameras)
        {
            gathered = true;
            var count = ecs.GetStorePublic<Mesh>().Count;
            var entities = ecs.GetStorePublic<Mesh>().EntitiesArray;
            var last = 0;
            for (int i = 0; i < count; i++) last = Math.Max(last, entities[i]);
            if (last >= _kept.Length) Array.Resize(ref _kept, Math.Max(last + 1, _kept.Length * 2));

            // The entities are gathered once, since an instance holds nothing of the camera, and each
            // camera draws the same instances.
            // Chunks are kept from the last frame only while no entity was added, removed or given or
            // relieved of a material or transform, which the stores' counts would show, and a write
            // counts from this system's last run, which the chunks' threads cannot ask for themselves.
            var counts = (count, ecs.Count<Material>(), ecs.Count<GlobalTransform>(), ecs.Count<Transform>(), ecs.Count<Visible>(), ecs.Count<ShadowOnly>());
            var keep = counts == _lastCounts;
            _lastCounts = counts;
            var frame = new Frame(ecs.GetStorePublic<Mesh>(), ecs.GetStorePublic<Material>(), ecs.GetStorePublic<GlobalTransform>(),
                ecs.GetStorePublic<Transform>(), ecs.GetStorePublic<Visible>(), ecs.GetStorePublic<ShadowOnly>(), meshes, assets, textures,
                _shaders, ChangeTicks.Since(ecs.FrameStart), keep);
            Gather(frame, count);
            if (world.TryGetResource<FrameEffects>(out var effects) && effects is { MotionBlur: > 0, MotionBlurObjects: true })
                RecordMoving(frame, count, draws);
            for (int index = 0; index < cameras.Count; index++)
                DrawThrough(index, cameras[index].ViewProjection, cameras[index].Eye, cameras[index].Target, draws);
        }

        // A frame with mesh entities and no camera to gather them for saw none held and lets none go.
        if (gathered) Release(meshes);
        if (world.TryGetResource<FrameProfile>(out var profile)) profile.Set("entities.drawn", _drawn);
    }

    // Frees the arrays' meshes no entity holds any longer, despawned or replaced, whose ids may be
    // given out again, and lets go of the looks and groups of every mesh no entity holds, those
    // freed and those the program uploaded that no entity names, so a voxel game's section built
    // again as a block changes leaves no look behind. Forgetting every look, as a freed mesh did,
    // built every entity's again on the main thread the next frame.
    private void Release(MeshStore meshes)
    {
        var dropped = false;
        if (_meshes.Count > _seen.Count)
        {
            foreach (var positions in _meshes.Keys.Where(p => !_seen.Contains(p)).ToArray())
            {
                meshes.Remove(_meshes[positions]);
                _spheres.Remove(_meshes[positions]);
                _meshes.Remove(positions);
            }
            dropped = true;
        }
        if (!_uploadedSeen.SetEquals(_uploadedLast))
        {
            _uploadedLast.Clear();
            _uploadedLast.UnionWith(_uploadedSeen);
            dropped = true;
        }
        if (!dropped) return;

        _live.Clear();
        foreach (var id in _meshes.Values) _live.Add(id);
        _live.UnionWith(_uploadedSeen);
        foreach (var id in _uploadedBounds.Keys.Where(id => !_live.Contains(id)).ToArray())
        {
            _uploadedBounds.Remove(id);
            _spheres.Remove(id);
        }
        for (int l = 0; l < _lookCount; l++)
        {
            ref var look = ref _looks[l];
            if (look.Free || _live.Contains(look.Draw.Mesh)) continue;
            _lookOf.Remove((look.Draw.Mesh, look.Material, look.ShadowOnly));
            look = new Look { Free = true, Stamp = ++_stamps };
            _freeLooks.Push(l);
        }
        for (int g = 0; g < _groups.Count; g++)
        {
            var draw = _groups[g];
            if (draw.Mesh == 0 || _live.Contains(draw.Mesh)) continue;
            _groupOf.Remove(KeyOf(draw));
            _groups[g] = default;
            _freeGroups.Push(g);
        }
    }

    // Each entity's world matrix when it was last drawn, with the frame and the mesh it was drawn
    // with, by entity id, which per-object motion blur finds the moving entities by.
    private Matrix4x4[] _placed = [];
    private long[] _placedFrame = [];
    private int[] _placedMesh = [];

    // Records each mesh entity whose world matrix differs from the frame before's, with that matrix,
    // for per-object motion blur, which alone reads them, so a frame without it pays nothing here.
    // An entity first drawn this frame, or with another mesh than the frame before, has no movement.
    // The entities are taken in ranges of a chunk's size, a thread each, each into lists of its own
    // and each writing only its own entities' places.
    private void RecordMoving(Frame frame, int count, ModelDrawList draws)
    {
        if (_placed.Length < _kept.Length)
        {
            var from = _placed.Length;
            Array.Resize(ref _placed, _kept.Length);
            Array.Resize(ref _placedFrame, _kept.Length);
            Array.Resize(ref _placedMesh, _kept.Length);
            // Never drawn, which no frame's number before this one matches.
            Array.Fill(_placedFrame, -2, from, _kept.Length - from);
        }
        var parts = (count + ChunkSize - 1) / ChunkSize;
        draws.Moving.EnsureParts(parts);
        if (parts == 1) RecordMovingRange(frame, 0, count, draws.Moving[0]);
        else Parallel.For(0, parts, part => RecordMovingRange(frame, part * ChunkSize, Math.Min(count, (part + 1) * ChunkSize), draws.Moving[part]));
    }

    private void RecordMovingRange(in Frame frame, int start, int end, MovingDraws.Part into)
    {
        var entities = frame.Meshes.EntitiesArray;
        // Entities of one mesh are mostly side by side, so the list of the last one's mesh is kept.
        var (lastMesh, list) = (-1, (List<MovingInstance>?)null);
        for (int i = start; i < end; i++)
        {
            var entity = entities[i];
            var kept = _kept[entity];
            // One drawn into the shadow maps alone is seen nowhere to blur.
            if (kept.Generation != _generation || _looks[kept.Look].ShadowOnly) continue;
            var placed = frame.Globals.TryGet(entity, out var global) ? global.Matrix
                : frame.Locals.TryGet(entity, out var local) ? TransformPropagation.ToMatrix(local)
                : Matrix4x4.Identity;
            if (_placedFrame[entity] == _frame - 1 && _placedMesh[entity] == kept.Mesh && _placed[entity] != placed)
            {
                if (kept.Mesh != lastMesh) (lastMesh, list) = (kept.Mesh, into.Of(kept.Mesh));
                into.Add(list!, new MovingInstance(placed, _placed[entity]));
            }
            (_placed[entity], _placedFrame[entity], _placedMesh[entity]) = (placed, _frame, kept.Mesh);
        }
    }

    // Forgets every look and group, after a texture they name was replaced, and with them every
    // entity's kept look, which is found again on its next frame. The groups recorded this frame
    // stay in the draw list until it is cleared.
    private void Forget()
    {
        _lookCount = 0;
        _lookOf.Clear();
        _groups.Clear();
        _groupOf.Clear();
        _cameraGroups.Clear();
        _freeLooks.Clear();
        _freeGroups.Clear();
        _generation++;
    }

    // How many chunks the frame's entities were gathered in, the deferred ones' after them.
    private int _chunkCount;

    // How many chunks kept what they gathered the frame before, this frame.
    private int _chunksKept;

    /// <summary>How many chunks of entities kept what they gathered the frame before, in the last frame, since none of their entities changed.</summary>
    internal int ChunksKept => _chunksKept;

    // The mesh, material, global and local transform, visibility and shadow-only stores' counts last frame.
    private (int, int, int, int, int, int) _lastCounts = (-1, -1, -1, -1, -1, -1);

    // Every entity's instance or translucent draw, written into the chunks.
    private void Gather(in Frame frame, int count)
    {
        // A chunk of entities a thread, each written into a segment of its own of each group,
        // and one segment more for the entities a chunk defers, written after.
        var chunks = _chunkCount = Math.Max(1, (count + ChunkSize - 1) / ChunkSize);
        _chunksKept = 0;
        if (_chunks.Length < chunks + 1) Array.Resize(ref _chunks, chunks + 1);
        for (int c = 0; c <= chunks; c++) _chunks[c] ??= new Chunk();
        // The chunks clear themselves unless they keep what they gathered, and the one the
        // deferred entities are placed in always does.
        _chunks[chunks].Clear();
        _chunks[chunks].Begin(_groups.Count);
        _chunks[chunks].Keepable = false;
        _chunks[chunks].Start = -1;

        var local = frame;
        if (chunks == 1) RecordChunk(0, count, local);
        else Parallel.For(0, chunks, c => RecordChunk(c, Math.Min(count, (c + 1) * ChunkSize), local));

        // What a chunk could not do on its thread, uploading a mesh or building a look.
        var late = _chunks[chunks];
        for (int c = 0; c < chunks; c++)
            foreach (var dense in _chunks[c].Deferred)
                Place(dense, late, canBuild: true, frame);

        for (int c = 0; c <= chunks; c++)
        {
            _seen.UnionWith(_chunks[c].Seen);
            _uploadedSeen.UnionWith(_chunks[c].UploadedSeen);
            _drawn += _chunks[c].Placed;
        }
    }

    // The gathered entities through one camera, into its target, the window's at 0, the opaque
    // ones as groups over the chunks' instances and the translucent ones farthest first.
    private void DrawThrough(int camera, in Matrix4x4 viewProjection, Vector3 eye, int target, ModelDrawList draws)
    {
        var chunks = _chunkCount;
        while (_cameraGroups.Count <= camera) _cameraGroups.Add([]);
        var groups = _cameraGroups[camera];
        while (groups.Count < _groups.Count) groups.Add(new InstanceGroup());
        for (int g = 0; g < _groups.Count; g++)
        {
            var group = groups[g];
            group.Clear();
            for (int c = 0; c <= chunks; c++)
                if (g < _chunks[c].Writers.Length) group.Add(_chunks[c].Writers[g].Items, _chunks[c].Writers[g].Count);
            if (group.Count == 0) continue;
            group.Template = _groups[g] with { ViewProjection = viewProjection, Target = target };
            group.Sphere = _spheres.TryGetValue(group.Template.Mesh, out var sphere) ? sphere : (Vector3.Zero, float.PositiveInfinity);
            draws.AddGroup(group);
        }

        _sorted.Clear();
        for (int c = 0; c <= chunks; c++)
            foreach (var draw in _chunks[c].Shaded)
                _sorted.Add(WithValues(draw) with { ViewProjection = viewProjection, Target = target });
        draws.AddRange(CollectionsMarshal.AsSpan(_sorted));

        _translucent.Clear();
        _sorted.Clear();
        for (int c = 0; c <= chunks; c++)
            foreach (var draw in _chunks[c].Translucent)
                _translucent.Add((Vector3.DistanceSquared(draw.World.Translation, eye), draw));
        _translucent.Sort(static (a, b) => b.Distance.CompareTo(a.Distance));
        foreach (var (_, draw) in _translucent) _sorted.Add(WithValues(draw) with { ViewProjection = viewProjection, Target = target });
        draws.AddRange(CollectionsMarshal.AsSpan(_sorted));
    }

    // What a frame's entities are recorded from, the stores read once rather than looked up by
    // type for each entity.
    // Since is the tick after which a write counts as changed, the system's last run, taken on the
    // main thread for the chunks' threads, and Keep whether a chunk may be kept from the last frame.
    private readonly record struct Frame(EcsWorld.ComponentStore<Mesh> Meshes, EcsWorld.ComponentStore<Material> Materials,
        EcsWorld.ComponentStore<GlobalTransform> Globals, EcsWorld.ComponentStore<Transform> Locals,
        EcsWorld.ComponentStore<Visible> Visibles, EcsWorld.ComponentStore<ShadowOnly> ShadowOnlys,
        MeshStore MeshStore, Assets<TextureAsset>? Assets, TextureStore Textures, ShaderStore? Shaders, long Since = 0, bool Keep = false);

    // The entities of a chunk, from its first dense index in the mesh store up to end, or what it
    // gathered the frame before when none of them changed, as in a level standing still.
    private void RecordChunk(int chunk, int end, in Frame frame)
    {
        var state = _chunks[chunk];
        var start = chunk * ChunkSize;
        if (frame.Keep && Unchanged(state, start, end, frame))
        {
            Interlocked.Increment(ref _chunksKept);
            return;
        }

        state.Clear();
        state.Begin(_groups.Count);
        for (int dense = start; dense < end; dense++)
            if (!Place(dense, state, canBuild: false, frame))
                state.Deferred.Add(dense);

        if (state.Entities.Length < end - start) state.Entities = new int[ChunkSize];
        for (int dense = start; dense < end; dense++) state.Entities[dense - start] = frame.Meshes.EntityByDenseIndex(dense);
        (state.Start, state.End, state.Generation) = (start, end, _generation);
        state.Keepable = state.Deferred.Count == 0 && !state.Waiting;
    }

    // Whether an entity's component was written or given to it after a tick.
    private static bool Touched<T>(EcsWorld.ComponentStore<T> store, int entity, long since) =>
        store.ChangedAfter(entity, since) || store.AddedAfter(entity, since);

    // Whether a chunk's entities are the ones it gathered last frame, at the same dense indices,
    // with nothing they are drawn by changed since, so it would gather the same again.
    private bool Unchanged(Chunk state, int start, int end, in Frame frame)
    {
        if (!state.Keepable || state.Start != start || state.End != end || state.Generation != _generation) return false;
        for (int dense = start; dense < end; dense++)
        {
            var entity = frame.Meshes.EntityByDenseIndex(dense);
            if (entity != state.Entities[dense - start]
                || Touched(frame.Meshes, entity, frame.Since) || Touched(frame.Materials, entity, frame.Since)
                || Touched(frame.Globals, entity, frame.Since) || Touched(frame.Locals, entity, frame.Since)
                || Touched(frame.Visibles, entity, frame.Since) || Touched(frame.ShadowOnlys, entity, frame.Since))
                return false;
        }
        return true;
    }

    // Writes one entity's instance into its group's segment, or its draw among the translucent
    // ones, answering false when it needs a mesh uploaded or a look built, which only a call with
    // canBuild does, since those change what the chunks on other threads read.
    private bool Place(int dense, Chunk state, bool canBuild, in Frame frame)
    {
        ref readonly var mesh = ref frame.Meshes.ComponentRefByDenseIndex(dense);
        var entity = frame.Meshes.EntityByDenseIndex(dense);
        int id;
        if (mesh.Uploaded.IsValid)
        {
            // A mesh the program uploaded is drawn as it is, its sphere made from its bounds, again
            // where the program uploaded another under the same id.
            id = mesh.Uploaded.Id;
            if (id != state.LastUploaded)
            {
                if (!_uploadedBounds.TryGetValue(id, out var bounds) || bounds != mesh.Uploaded.Bounds)
                {
                    if (!canBuild) return false;
                    _uploadedBounds[id] = mesh.Uploaded.Bounds;
                    _spheres[id] = Sphere([mesh.Uploaded.Bounds.Min, mesh.Uploaded.Bounds.Max]);
                }
                state.LastUploaded = id;
                state.UploadedSeen.Add(id);
            }
        }
        else
        {
            if (mesh.Positions is not { Length: >= 3 } positions) return true;
            // Entities spawned together usually share one positions array, so the mesh of the entity
            // before is checked first. The array is held whether or not the entity is drawn, so it
            // is seen before anything else is asked of the entity.
            if (!ReferenceEquals(positions, state.LastPositions))
            {
                state.Seen.Add(positions);
                if (!_meshes.TryGetValue(positions, out var found))
                {
                    if (!canBuild) return false;
                    _meshes[positions] = found = frame.MeshStore.Add(Vertices(mesh), Indices(mesh));
                    _spheres[found] = Sphere(positions);
                }
                state.LastPositions = positions;
                state.LastMesh = found;
            }
            id = state.LastMesh;
        }
        // One not drawn keeps the mesh seen above and forgets its look, so motion blur finds it
        // nowhere, and finds it again once it is drawn.
        if (frame.Visibles.TryGet(entity, out var visible) && !visible.Value)
        {
            _kept[entity].Generation = 0;
            return true;
        }
        var shadowOnly = frame.ShadowOnlys.TryGet(entity, out var alone) && alone.Value;
        var materialAt = frame.Materials.DenseIndexOf(entity);
        if (materialAt < 0) return true;
        ref readonly var material = ref frame.Materials.ComponentRefByDenseIndex(materialAt);

        ref var kept = ref _kept[entity];
        if (kept.Generation != _generation || kept.Mesh != id || _looks[kept.Look].Stamp != kept.Stamp || _looks[kept.Look].ShadowOnly != shadowOnly
            || !_looks[kept.Look].Material.Equals(material))
        {
            // A look another entity of the mesh and material made is found on any thread, since the
            // looks are only read while the chunks run, and only a look not yet made waits for the
            // pass after them.
            if (!_lookOf.TryGetValue((id, material, shadowOnly), out var found))
            {
                if (!canBuild) return false;
                found = LookFor(id, material, shadowOnly, frame);
            }
            kept = new Kept { Generation = _generation, Mesh = id, Look = found, Stamp = _looks[found].Stamp };
        }
        ref var look = ref _looks[kept.Look];
        if (look.Pending && look.Built != _frame)
        {
            if (!canBuild) return false;
            var stamp = look.Stamp;
            look = Build(id, look.Material, look.ShadowOnly, frame);
            look.Stamp = stamp;
        }
        // A look waiting on a texture is built again each frame until it has it, which may change
        // its group, so its chunk is gathered again too.
        if (look.Pending) state.Waiting = true;

        var placed = frame.Globals.TryGet(entity, out var global) ? global.Matrix
            : frame.Locals.TryGet(entity, out var local) ? TransformPropagation.ToMatrix(local)
            : Matrix4x4.Identity;
        state.Placed++;
        if (look.Group < 0)
        {
            (look.Group == ShadedLook ? state.Shaded : state.Translucent).Add(look.Draw with { World = placed });
            return true;
        }

        // As ModelRenderer.Instance.Of writes them, the factors kept.
        ref var instance = ref state.Next(look.Group);
        instance = look.Instance;
        instance.WorldX = new Vector4(placed.M11, placed.M21, placed.M31, placed.M41);
        instance.WorldY = new Vector4(placed.M12, placed.M22, placed.M32, placed.M42);
        instance.WorldZ = new Vector4(placed.M13, placed.M23, placed.M33, placed.M43);
        return true;
    }
}
