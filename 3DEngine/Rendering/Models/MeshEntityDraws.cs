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
/// Runs in <see cref="Stage.Render"/> and draws through the first entity with a
/// <see cref="Camera"/>. Without one, mesh entities are not drawn, as before.
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
/// world matrix and camera, and the model pass copies each group as it is and draws it as one
/// batch (RENDERING.md section 6).
/// </para>
/// <para>
/// Past 4,096 entities they are recorded in chunks of that many, each on a thread of its own and
/// into buffers of its own. An entity that needs its mesh uploaded or its look built is left by its
/// chunk and recorded after the chunks, on the calling thread, since those change what the other
/// chunks read.
/// </para>
/// </remarks>
public sealed class MeshEntityDraws
{
    private readonly Dictionary<Vector3[], int> _meshes = new(ReferenceEqualityComparer.Instance);
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

    // The groups opaque entities are written into, by what their instances share, kept from frame
    // to frame.
    private readonly List<InstanceGroup> _groups = [];
    private readonly Dictionary<GroupKey, int> _groupOf = [];

    private readonly record struct GroupKey(int Mesh, int Texture, int NormalMap, int MetallicRoughnessMap, int EmissiveMap,
        int OcclusionMap, bool DoubleSided, MaterialAlphaMode AlphaMode);

    // Entities are recorded in chunks of this many, a thread each, once there is more than one.
    private const int ChunkSize = 4096;
    private Chunk[] _chunks = [];

    // What a chunk gathers apart from the others: the positions arrays it drew, the mesh of the
    // entity before, its translucent draws, the entities it left for after, and its instances, a
    // buffer for each group. The buffers and their counts are the chunk's own, since threads
    // counting into objects that lie side by side wait on each other's writes to the same line.
    private sealed class Chunk
    {
        public readonly HashSet<Vector3[]> Seen = new(ReferenceEqualityComparer.Instance);
        public readonly List<(float Distance, ModelDraw Draw)> Translucent = [];
        public readonly List<int> Deferred = [];
        public Vector3[]? LastPositions;
        public int LastMesh;
        public Writer[] Writers = [];

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
        world.TryGetResource<Assets<Texture>>(out var assets);
        ForgetReloadedTextures(world, textures);

        _frame++;
        _seen.Clear();
        _translucent.Clear();
        _sorted.Clear();
        if (_colors.Count > 4096) _colors.Clear();
        if (ecs.Count<Mesh>() > 0 && FirstCamera(world, ecs) is { } camera)
        {
            var frame = new Frame(ecs.GetStorePublic<Mesh>(), ecs.GetStorePublic<Material>(), ecs.GetStorePublic<GlobalTransform>(),
                ecs.GetStorePublic<Transform>(), camera.ViewProjection, camera.Eye, meshes, assets, textures);
            var count = frame.Meshes.Count;
            var entities = frame.Meshes.EntitiesArray;
            var last = 0;
            for (int i = 0; i < count; i++) last = Math.Max(last, entities[i]);
            if (last >= _kept.Length) Array.Resize(ref _kept, Math.Max(last + 1, _kept.Length * 2));

            // A chunk of entities a thread, each written into a segment of its own of each group,
            // and one segment more for the entities a chunk defers, written after.
            var chunks = Math.Max(1, (count + ChunkSize - 1) / ChunkSize);
            if (_chunks.Length < chunks + 1) Array.Resize(ref _chunks, chunks + 1);
            for (int c = 0; c <= chunks; c++) (_chunks[c] ??= new Chunk()).Clear();
            for (int c = 0; c <= chunks; c++) _chunks[c].Begin(_groups.Count);

            if (chunks == 1) RecordChunk(0, count, frame);
            else Parallel.For(0, chunks, c => RecordChunk(c, Math.Min(count, (c + 1) * ChunkSize), frame));

            // What a chunk could not do on its thread, uploading a mesh or building a look.
            var late = _chunks[chunks];
            for (int c = 0; c < chunks; c++)
                foreach (var dense in _chunks[c].Deferred)
                    Place(dense, late, canBuild: true, frame);

            for (int c = 0; c <= chunks; c++)
            {
                _seen.UnionWith(_chunks[c].Seen);
                _translucent.AddRange(_chunks[c].Translucent);
            }
            for (int g = 0; g < _groups.Count; g++)
            {
                var group = _groups[g];
                group.Clear();
                for (int c = 0; c <= chunks; c++)
                    if (g < _chunks[c].Writers.Length) group.Add(_chunks[c].Writers[g].Items, _chunks[c].Writers[g].Count);
                if (group.Count == 0) continue;
                group.Template = group.Template with { ViewProjection = camera.ViewProjection };
                draws.AddGroup(group);
            }
            if (_lookCount > MaxLooks || _groups.Count > MaxLooks) Forget();

            _translucent.Sort(static (a, b) => b.Distance.CompareTo(a.Distance));
            foreach (var (_, draw) in _translucent) _sorted.Add(draw);
            draws.AddRange(CollectionsMarshal.AsSpan(_sorted));
        }

        // A mesh no entity drew this frame was despawned or replaced, and its id may be given out again.
        if (_meshes.Count > _seen.Count)
        {
            foreach (var positions in _meshes.Keys.Where(p => !_seen.Contains(p)).ToArray())
            {
                meshes.Remove(_meshes[positions]);
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
        _generation++;
    }

    // What a frame's entities are recorded from, the stores read once rather than looked up by
    // type for each entity.
    private readonly record struct Frame(EcsWorld.ComponentStore<Mesh> Meshes, EcsWorld.ComponentStore<Material> Materials,
        EcsWorld.ComponentStore<GlobalTransform> Globals, EcsWorld.ComponentStore<Transform> Locals, Matrix4x4 ViewProjection,
        Vector3 Eye, MeshStore MeshStore, Assets<Texture>? Assets, TextureStore Textures);

    // The entities of a chunk, from its first dense index in the mesh store up to end.
    private void RecordChunk(int chunk, int end, in Frame frame)
    {
        var state = _chunks[chunk];
        for (int dense = chunk * ChunkSize; dense < end; dense++)
            if (!Place(dense, state, canBuild: false, frame))
                state.Deferred.Add(dense);
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

        var placed = frame.Globals.TryGet(entity, out var global) ? global.Matrix
            : frame.Locals.TryGet(entity, out var local) ? TransformPropagation.ToMatrix(local)
            : Matrix4x4.Identity;
        if (look.Group < 0)
        {
            state.Translucent.Add((Vector3.DistanceSquared(placed.Translation, frame.Eye), look.Draw with { World = placed, ViewProjection = frame.ViewProjection }));
            return true;
        }

        // As ModelRenderer.Instance.Of writes them, the factors kept.
        ref var instance = ref state.Next(look.Group);
        instance = look.Instance;
        instance.Transform = placed * frame.ViewProjection;
        instance.WorldX = new Vector4(placed.M11, placed.M21, placed.M31, placed.M41);
        instance.WorldY = new Vector4(placed.M12, placed.M22, placed.M32, placed.M42);
        instance.WorldZ = new Vector4(placed.M13, placed.M23, placed.M33, placed.M43);
        return true;
    }

    // The look of a mesh and material, built the first time an entity is drawn with them.
    private int LookFor(int mesh, in Material material, Assets<Texture>? assets, TextureStore textures)
    {
        if (_lookOf.TryGetValue((mesh, material), out var index)) return index;
        if (_lookCount == _looks.Length) Array.Resize(ref _looks, _looks.Length * 2);
        _looks[_lookCount] = Build(mesh, material, assets, textures);
        return _lookOf[(mesh, material)] = _lookCount++;
    }

    // A look from a material, its draw with the world matrix and camera left for the frame.
    private Look Build(int mesh, in Material material, Assets<Texture>? assets, TextureStore textures)
    {
        var pending = false;
        int Texture(Handle<Texture> handle)
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
            Instance = ModelRenderer.Instance.Of(draw, Matrix4x4.Identity),
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
        _groups.Add(new InstanceGroup { Template = draw });
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

    /// <summary>World to clip space through the first camera entity, as <see cref="CameraExtract"/> builds it, and where that camera is.</summary>
    internal static (Matrix4x4 ViewProjection, Vector3 Eye)? FirstCamera(World world, EcsWorld ecs)
    {
        foreach (var (entity, camera) in ecs.Query<Camera>())
        {
            // The window's shape, or with no window (an offscreen run) the size the config asked
            // for, which is what the frames are drawn at. Assuming square there stretched every
            // mesh entity in an offscreen capture while the 2D drawing beside it was right.
            var (width, height) = world.TryGetResource<AppWindow>(out var window)
                ? (window.Sdl.Width, window.Sdl.Height)
                : world.TryGetResource<Config>(out var config) ? (config.WindowData.Width, config.WindowData.Height) : (1, 1);
            var aspect = height > 0 ? (float)width / height : 1f;
            var (view, projection) = CameraExtract.Matrices(ecs, entity, camera, aspect);
            return (view * projection, TransformPropagation.WorldMatrix(ecs, entity).Translation);
        }
        return null;
    }

    private int TextureFor(Handle<Texture> handle, Assets<Texture>? assets, TextureStore textures)
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
        var events = world.ReadAssetEvents<Texture>();
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
