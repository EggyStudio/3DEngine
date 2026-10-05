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
/// on the first frame no entity draws them. A material's base color texture is copied from the
/// asset store into <see cref="TextureStore"/> once it has loaded, and copied again when the asset
/// is reloaded. Only the first mip level of an RGBA8 texture is copied, and the GPU makes the rest.
/// </para>
/// <para>
/// An entity whose material blends, with alpha below 1 in its albedo or base color texture, shows
/// what is behind it, so it is recorded after the opaque ones, from the farthest from the camera
/// to the nearest, which the model pass keeps.
/// </para>
/// <para>
/// Each entity's draw is kept from frame to frame and built again only when its mesh or material
/// differs from what it was built from, a texture it names was still loading, or a texture or
/// mesh was replaced. An opaque entity's instance is then written straight into the
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
internal sealed class MeshEntityDraws
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

    // Each entity's look as last found, by entity id. A kept look of an older generation was found
    // before the looks were forgotten. Twelve bytes, since a frame reads every entity's.
    private Kept[] _kept = [];
    private int _generation = 1;
    private long _frame;

    private struct Kept
    {
        public int Generation;
        public int Mesh;
        public int Look;
    }

    // Each mesh and material entities are drawn with, built once: the draw with the world matrix
    // and camera left to the frame, the instance with the material's factors, and the group it is
    // written into. Entities share a few, so the comparison each makes a frame reads one already in
    // the cache, where a draw and a material kept for each entity cost a frame several misses each.
    private Look[] _looks = new Look[16];
    private int _lookCount;
    private readonly Dictionary<(int Mesh, Material Material), int> _lookOf = [];

    private struct Look
    {
        // Compared rather than relying on change marks, since a material replaced by Add marks none.
        public Material Material;
        public ModelDraw Draw;
        public ModelRenderer.Instance Instance;
        // Its group in _groups, or -1 for a translucent draw.
        public int Group;
        // A texture it names was still loading, so it is built again on a later frame.
        public bool Pending;
        public long Built;
    }

    // What the groups opaque entities are written into share, by index, kept from frame to frame,
    // and each camera's groups, the same index for the same mesh and maps, which hold the same
    // instances under a template of the camera's own.
    private readonly List<ModelDraw> _groups = [];
    private readonly Dictionary<GroupKey, int> _groupOf = [];
    private readonly List<List<InstanceGroup>> _cameraGroups = [];

    private readonly record struct GroupKey(int Mesh, int Texture, int NormalMap, int MetallicRoughnessMap, int EmissiveMap,
        int OcclusionMap, bool DoubleSided, MaterialAlphaMode AlphaMode);

    // Entities are recorded in chunks of this many, a thread each, once there is more than one.
    private const int ChunkSize = 4096;
    private Chunk[] _chunks = [];

    // What a chunk gathers apart from the others: the positions arrays it drew, the mesh of the
    // entity before, its translucent draws placed in the world, the entities it left for after,
    // and its instances, a buffer for each group. The buffers and their counts are the chunk's
    // own, since threads counting into objects that lie side by side wait on each other's writes
    // to the same line.
    private sealed class Chunk
    {
        public readonly HashSet<Vector3[]> Seen = new(ReferenceEqualityComparer.Instance);
        public readonly List<ModelDraw> Translucent = [];
        public readonly List<int> Deferred = [];
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
            Translucent.Clear();
            Deferred.Clear();
            LastPositions = null;
            Waiting = false;
            Keepable = false;
        }
    }

    // A look or a group past this many is a sign of materials made anew each frame, as a color
    // that flashes, and every look and group is forgotten rather than kept growing.
    private const int MaxLooks = 4096;

    /// <summary>How many meshes are uploaded for entities.</summary>
    public int MeshCount => _meshes.Count;

    /// <summary>How many texture assets are copied for entities.</summary>
    public int TextureCount => _textures.Count;

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
        _translucent.Clear();
        _sorted.Clear();
        if (_colors.Count > 4096) _colors.Clear();
        if (ecs.Count<Mesh>() > 0 && Cameras(world, ecs) is { Count: > 0 } cameras)
        {
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
            var counts = (count, ecs.Count<Material>(), ecs.Count<GlobalTransform>(), ecs.Count<Transform>());
            var keep = counts == _lastCounts;
            _lastCounts = counts;
            var frame = new Frame(ecs.GetStorePublic<Mesh>(), ecs.GetStorePublic<Material>(), ecs.GetStorePublic<GlobalTransform>(),
                ecs.GetStorePublic<Transform>(), meshes, assets, textures, ChangeTicks.Since(ecs.FrameStart), keep);
            Gather(frame, count);
            for (int index = 0; index < cameras.Count; index++)
                DrawThrough(index, cameras[index].ViewProjection, cameras[index].Eye, cameras[index].Target, draws);
            if (_lookCount > MaxLooks || _groups.Count > MaxLooks) Forget();
        }

        // A mesh no entity drew this frame was despawned or replaced, and its id may be given out again.
        if (_meshes.Count > _seen.Count)
        {
            foreach (var positions in _meshes.Keys.Where(p => !_seen.Contains(p)).ToArray())
            {
                meshes.Remove(_meshes[positions]);
                _spheres.Remove(_meshes[positions]);
                _meshes.Remove(positions);
            }
            Forget();
        }
    }

    // Forgets every look and group, after a mesh or texture they name was replaced or once there
    // are too many, and with them every entity's kept look, which is found again on its next frame.
    // The groups recorded this frame stay in the draw list until it is cleared.
    private void Forget()
    {
        _lookCount = 0;
        _lookOf.Clear();
        _groups.Clear();
        _groupOf.Clear();
        _cameraGroups.Clear();
        _generation++;
    }

    // How many chunks the frame's entities were gathered in, the deferred ones' after them.
    private int _chunkCount;

    // How many chunks kept what they gathered the frame before, this frame.
    private int _chunksKept;

    /// <summary>How many chunks of entities kept what they gathered the frame before, in the last frame, since none of their entities changed.</summary>
    internal int ChunksKept => _chunksKept;

    // The mesh, material, global and local transform stores' counts last frame.
    private (int, int, int, int) _lastCounts = (-1, -1, -1, -1);

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

        for (int c = 0; c <= chunks; c++) _seen.UnionWith(_chunks[c].Seen);
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

        _translucent.Clear();
        _sorted.Clear();
        for (int c = 0; c <= chunks; c++)
            foreach (var draw in _chunks[c].Translucent)
                _translucent.Add((Vector3.DistanceSquared(draw.World.Translation, eye), draw));
        _translucent.Sort(static (a, b) => b.Distance.CompareTo(a.Distance));
        foreach (var (_, draw) in _translucent) _sorted.Add(draw with { ViewProjection = viewProjection, Target = target });
        draws.AddRange(CollectionsMarshal.AsSpan(_sorted));
    }

    // What a frame's entities are recorded from, the stores read once rather than looked up by
    // type for each entity.
    // Since is the tick after which a write counts as changed, the system's last run, taken on the
    // main thread for the chunks' threads, and Keep whether a chunk may be kept from the last frame.
    private readonly record struct Frame(EcsWorld.ComponentStore<Mesh> Meshes, EcsWorld.ComponentStore<Material> Materials,
        EcsWorld.ComponentStore<GlobalTransform> Globals, EcsWorld.ComponentStore<Transform> Locals,
        MeshStore MeshStore, Assets<TextureAsset>? Assets, TextureStore Textures, long Since = 0, bool Keep = false);

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
    // with nothing they are drawn by changed since, so what it gathered is what it would again.
    private bool Unchanged(Chunk state, int start, int end, in Frame frame)
    {
        if (!state.Keepable || state.Start != start || state.End != end || state.Generation != _generation) return false;
        for (int dense = start; dense < end; dense++)
        {
            var entity = frame.Meshes.EntityByDenseIndex(dense);
            if (entity != state.Entities[dense - start]
                || Touched(frame.Meshes, entity, frame.Since) || Touched(frame.Materials, entity, frame.Since)
                || Touched(frame.Globals, entity, frame.Since) || Touched(frame.Locals, entity, frame.Since))
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
        if (mesh.Positions is not { Length: >= 3 } positions) return true;
        var entity = frame.Meshes.EntityByDenseIndex(dense);
        var materialAt = frame.Materials.DenseIndexOf(entity);
        if (materialAt < 0) return true;
        ref readonly var material = ref frame.Materials.ComponentRefByDenseIndex(materialAt);

        // Entities spawned together usually share one positions array, so the mesh of the entity
        // before is checked first.
        if (!ReferenceEquals(positions, state.LastPositions))
        {
            if (!_meshes.TryGetValue(positions, out var found))
            {
                if (!canBuild) return false;
                _meshes[positions] = found = frame.MeshStore.Add(Vertices(mesh), Sequence(positions.Length / 3 * 3));
                _spheres[found] = Sphere(positions);
            }
            state.LastPositions = positions;
            state.LastMesh = found;
            state.Seen.Add(positions);
        }
        var id = state.LastMesh;

        ref var kept = ref _kept[entity];
        if (kept.Generation != _generation || kept.Mesh != id || !_looks[kept.Look].Material.Equals(material))
        {
            if (!canBuild) return false;
            kept = new Kept { Generation = _generation, Mesh = id, Look = LookFor(id, material, frame.Assets, frame.Textures) };
        }
        ref var look = ref _looks[kept.Look];
        if (look.Pending && look.Built != _frame)
        {
            if (!canBuild) return false;
            look = Build(id, look.Material, frame.Assets, frame.Textures);
        }
        // A look waiting on a texture is built again each frame until it has it, which may change
        // its group, so its chunk is gathered again too.
        if (look.Pending) state.Waiting = true;

        var placed = frame.Globals.TryGet(entity, out var global) ? global.Matrix
            : frame.Locals.TryGet(entity, out var local) ? TransformPropagation.ToMatrix(local)
            : Matrix4x4.Identity;
        if (look.Group < 0)
        {
            state.Translucent.Add(look.Draw with { World = placed });
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

    // The sphere around a mesh's positions, at the middle of their box.
    private static (Vector3 Center, float Radius) Sphere(Vector3[] positions)
    {
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var p in positions)
        {
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
        }
        var center = (min + max) / 2;
        var radius = 0f;
        foreach (var p in positions) radius = MathF.Max(radius, Vector3.DistanceSquared(p, center));
        return (center, MathF.Sqrt(radius));
    }

    // The look of a mesh and material, built the first time an entity is drawn with them.
    private int LookFor(int mesh, in Material material, Assets<TextureAsset>? assets, TextureStore textures)
    {
        if (_lookOf.TryGetValue((mesh, material), out var index)) return index;
        if (_lookCount == _looks.Length) Array.Resize(ref _looks, _looks.Length * 2);
        _looks[_lookCount] = Build(mesh, material, assets, textures);
        return _lookOf[(mesh, material)] = _lookCount++;
    }

    // A look from a material, its draw with the world matrix and camera left for the frame.
    private Look Build(int mesh, in Material material, Assets<TextureAsset>? assets, TextureStore textures)
    {
        var pending = false;
        int Texture(Handle<TextureAsset> handle)
        {
            var id = TextureFor(handle, assets, textures);
            pending |= handle.IsValid && !_textures.ContainsKey(handle.Id);
            return id;
        }

        var baseColor = Texture(material.BaseColorTexture);
        var draw = new ModelDraw(
            mesh,
            Matrix4x4.Identity,
            Matrix4x4.Identity,
            Encoded(material.Albedo),
            baseColor,
            Metallic: material.MetallicFactor,
            Roughness: material.RoughnessFactor,
            NormalMap: Texture(material.NormalTexture),
            NormalScale: material.NormalScale,
            MetallicRoughnessMap: Texture(material.MetallicRoughnessTexture),
            Emission: material.EmissiveFactor,
            EmissiveMap: Texture(material.EmissiveTexture),
            OcclusionMap: Texture(material.OcclusionTexture),
            OcclusionStrength: material.OcclusionStrength,
            AlphaMode: material.AlphaMode,
            AlphaCutoff: material.AlphaCutoff,
            TextureTranslucent: baseColor != 0 && textures.IsTranslucent(baseColor),
            DoubleSided: material.DoubleSided);
        return new Look
        {
            Material = material,
            Draw = draw,
            Instance = ModelRenderer.Instance.Of(draw),
            Group = draw.IsTranslucent ? -1 : GroupOf(draw),
            Pending = pending,
            Built = _frame,
        };
    }

    // The group of an opaque draw's mesh, maps, sides and alpha mode, made the first time one is drawn.
    private int GroupOf(in ModelDraw draw)
    {
        var key = new GroupKey(draw.Mesh, draw.Texture, draw.NormalMap, draw.MetallicRoughnessMap, draw.EmissiveMap,
            draw.OcclusionMap, draw.DoubleSided, draw.AlphaMode);
        if (_groupOf.TryGetValue(key, out var index)) return index;
        _groups.Add(draw);
        return _groupOf[key] = _groups.Count - 1;
    }

    // Albedo is linear, and a draw's color is sRGB-encoded bytes, as the flat API's are.
    private Color Encoded(Vector4 albedo)
    {
        if (_colors.TryGetValue(albedo, out var known)) return known;
        var c = Vector4.Clamp(albedo, Vector4.Zero, Vector4.One);
        c = new Vector4(LinearToSrgb(c.X), LinearToSrgb(c.Y), LinearToSrgb(c.Z), c.W) * 255 + new Vector4(0.5f);
        return _colors[albedo] = new Color((byte)c.X, (byte)c.Y, (byte)c.Z, (byte)c.W);
    }

    private static float LinearToSrgb(float c) =>
        c <= 0.0031308f ? c * 12.92f : 1.055f * MathF.Pow(c, 1 / 2.4f) - 0.055f;

    // The cameras mesh entities are drawn through: the first without a render texture, into the
    // window, then each with one, into it, whose target is cleared to its background unless the
    // program clears it itself.
    internal static List<(Matrix4x4 ViewProjection, Vector3 Eye, int Target)> Cameras(World world, EcsWorld ecs)
    {
        var cameras = new List<(Matrix4x4, Vector3, int)>();
        var window = false;
        foreach (var (entity, camera) in ecs.Query<Camera>())
        {
            var target = camera.Target.IsValid ? camera.Target.Texture.Id : 0;
            if (target == 0 && window) continue;
            var (viewProjection, eye) = Through(world, ecs, entity, camera);
            var seen = (viewProjection, eye, target);
            if (target == 0)
            {
                window = true;
                cameras.Insert(0, seen);
            }
            else
            {
                cameras.Add(seen);
                world.TryGetResource<DrawList>(out var drawList);
                drawList?.UseTarget(target, camera.Background);
            }
        }
        if (!window && world.TryGetResource<Mode3DCamera>(out var flat) && flat.ViewProjection is { } flatViewProjection)
            cameras.Insert(0, (flatViewProjection, flat.Eye, 0));
        return cameras;
    }

    // A camera entity's view-projection and eye. The shape is its render texture's, or the
    // window's, or with no window (an offscreen run) the size the config asked for, which is what
    // the frames are drawn at. Assuming square there stretched every mesh entity in an offscreen
    // capture while the 2D drawing beside it was right.
    private static (Matrix4x4 ViewProjection, Vector3 Eye) Through(World world, EcsWorld ecs, int entity, in Camera camera)
    {
        var (width, height) = camera.Target.IsValid ? (camera.Target.Texture.Width, camera.Target.Texture.Height)
            : world.TryGetResource<AppWindow>(out var appWindow) ? (appWindow.Sdl.Width, appWindow.Sdl.Height)
            : world.TryGetResource<OffscreenSurface>(out var surface) ? ((int)surface.Size.Width, (int)surface.Size.Height)
            : world.TryGetResource<Config>(out var config) ? (config.WindowData.Width, config.WindowData.Height) : (1, 1);
        var aspect = height > 0 ? (float)width / height : 1f;
        var (view, projection) = CameraExtract.Matrices(ecs, entity, camera, aspect);
        return (view * projection, TransformPropagation.WorldMatrix(ecs, entity).Translation);
    }

    /// <summary>
    /// The camera the window is drawn through, as <see cref="Cameras"/> gives it first, the first
    /// camera entity without a render texture or the frame's <c>BeginMode3D</c>, or null for none.
    /// </summary>
    internal static (Matrix4x4 ViewProjection, Vector3 Eye)? WindowCamera(World world, EcsWorld? ecs)
    {
        if (ecs is not null)
            foreach (var (entity, camera) in ecs.Query<Camera>())
                if (!camera.Target.IsValid) return Through(world, ecs, entity, camera);
        return world.TryGetResource<Mode3DCamera>(out var flat) && flat.ViewProjection is { } viewProjection ? (viewProjection, flat.Eye) : null;
    }

    private int TextureFor(Handle<TextureAsset> handle, Assets<TextureAsset>? assets, TextureStore textures)
    {
        if (!handle.IsValid) return 0;
        if (_textures.TryGetValue(handle.Id, out var id)) return id;
        if (assets is null || !assets.TryGet(handle, out var texture)) return 0; // still loading

        if (texture.Format != TextureFormat.Rgba8)
        {
            _textures[handle.Id] = 0;
            return 0;
        }
        var bytes = texture.Width * texture.Height * 4;
        var pixels = texture.Pixels.Length == bytes ? texture.Pixels : texture.Pixels[..bytes];
        return _textures[handle.Id] = textures.Add(pixels, texture.Width, texture.Height, mipmaps: true);
    }

    private void ForgetReloadedTextures(World world, TextureStore textures)
    {
        if (_textures.Count == 0) return;
        var events = world.ReadAssetEvents<TextureAsset>();
        for (int i = 0; i < events.Count; i++)
        {
            if (events[i].Kind is not (AssetEventKind.Modified or AssetEventKind.Removed)) continue;
            if (!_textures.Remove(events[i].Id, out var id)) continue;
            if (id != 0) textures.Remove(id);
            Forget();
        }
    }

    private static ModelVertex[] Vertices(in Mesh mesh)
    {
        var count = mesh.Positions.Length / 3 * 3;
        var vertices = new ModelVertex[count];
        for (int i = 0; i < count; i += 3)
        {
            var (a, b, c) = (mesh.Positions[i], mesh.Positions[i + 1], mesh.Positions[i + 2]);
            // A triangle with no normals of its own is lit flat, by the normal of its face.
            var face = Vector3.Cross(b - a, c - a);
            face = face.LengthSquared() > 0 ? Vector3.Normalize(face) : Vector3.UnitY;
            for (int k = 0; k < 3; k++)
            {
                var n = mesh.Normals is { } normals && i + k < normals.Length ? normals[i + k] : face;
                var uv = mesh.Uvs is { } uvs && i + k < uvs.Length ? uvs[i + k] : Vector2.Zero;
                vertices[i + k] = new ModelVertex(mesh.Positions[i + k], n, uv);
            }
        }
        return vertices;
    }

    private static uint[] Sequence(int count)
    {
        var indices = new uint[count];
        for (uint i = 0; i < count; i++) indices[i] = i;
        return indices;
    }
}

/// <summary>
/// The camera of the first <c>BeginMode3D</c> of the frame drawn into the window, a world resource
/// <see cref="MeshEntityDraws"/> draws mesh entities through when no <see cref="Camera"/> entity
/// draws the window, cleared as each frame begins.
/// </summary>
internal sealed class Mode3DCamera
{
    /// <summary>The camera's view and projection, or null when no <c>BeginMode3D</c> has drawn into the window this frame.</summary>
    public Matrix4x4? ViewProjection { get; set; }

    /// <summary>Where the camera is, which the mesh entities are sorted from.</summary>
    public Vector3 Eye { get; set; }
}
