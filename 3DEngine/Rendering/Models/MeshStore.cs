using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// The meshes the flat API has made or loaded, by id, with what is waiting to reach the GPU and
/// what has been unloaded, until <see cref="GpuMeshesPrepare"/> takes them on the render thread.
/// </summary>
/// <remarks>
/// The same queue as <see cref="TextureStore"/>, for vertex and index buffers. Id 0 is never given
/// out, so a default <see cref="ModelMesh"/> is recognizably not loaded. Each mesh's vertices and
/// indices stay here while it is loaded, as raylib keeps a mesh's arrays beside its buffers, so a
/// mesh can be read back and drawn as wires.
/// </remarks>
internal sealed class MeshStore
{
    /// <summary>
    /// A mesh waiting to be uploaded, or what of it is written in place, its vertices where
    /// <paramref name="VerticesOnly"/> is set, its stream of the program's own where
    /// <paramref name="CustomOnly"/> is, or both where both are.
    /// </summary>
    /// <param name="Id">The mesh's id.</param>
    /// <param name="Vertices">Its vertices.</param>
    /// <param name="Indices">Three indices into the vertices for each triangle.</param>
    /// <param name="VerticesOnly">Whether only the vertices changed, so the index buffer is kept.</param>
    /// <param name="Skin">The joints and weights that pose it on the GPU, or null for a mesh that is not skinned there.</param>
    /// <param name="Streams">A color and a second texture coordinate at each vertex, which a mesh may have beside its vertices.</param>
    /// <param name="Custom">The stream of the program's own a vertex stage reads as <c>CUSTOM0</c>, or null for a mesh without one.</param>
    /// <param name="CustomOnly">Whether only that stream changed, so the mesh's other buffers are kept.</param>
    /// <param name="PositionsOnly">Whether the GPU is given the vertices' positions alone, twelve bytes a vertex, for a mesh uploaded from its positions.</param>
    public sealed record Upload(int Id, ModelVertex[] Vertices, uint[] Indices, bool VerticesOnly = false, Skin? Skin = null, Streams? Streams = null,
        MeshStream? Custom = null, bool CustomOnly = false, bool PositionsOnly = false);

    /// <summary>
    /// A mesh's colors and second texture coordinates, one a vertex each, either or both, kept in
    /// buffers of their own beside its vertices, as raylib keeps them in arrays of their own, so a
    /// mesh without them has its vertices no larger.
    /// </summary>
    public sealed record Streams(Color[]? Colors, System.Numerics.Vector2[]? Texcoords2);

    /// <summary>
    /// A skinned mesh's four joints and four weights a vertex, how many joints its skeleton has, and
    /// its morph targets, how far each moves each vertex's position and normal at full weight, two
    /// float4 a vertex, target after target.
    /// </summary>
    public sealed record Skin(ushort[] Joints, float[] Weights, int JointCount, System.Numerics.Vector4[]? Morphs = null, int MorphCount = 0);

    /// <summary>
    /// A mesh over shader buffers, its positions twelve bytes a vertex and its stream of the
    /// program's own where it has one, drawn without indices as many vertices as a shader wrote into
    /// the first four bytes of its count buffer, at most its capacity. Nothing of it is kept here,
    /// so the readers of a mesh's vertices on the CPU pass it over.
    /// </summary>
    public sealed record Counted(int Id, IBuffer Positions, (IBuffer Buffer, MeshStreamFormat Format)? Stream, IBuffer Count, uint Capacity);

    // The meshes over shader buffers, those to make on the GPU, and the boxes the program set them.
    private readonly Dictionary<int, Counted> _counted = [];
    private readonly Dictionary<int, BoundingBox> _countedBoxes = [];
    private readonly List<Counted> _countedUploads = [];
    private readonly List<(int Id, BoundingBox Box)> _boxChanges = [];
    // How many times a dispatch has bound each such mesh's buffers, which global illumination reads
    // its triangles again by.
    private readonly Dictionary<int, int> _countedVersions = [];

    private readonly Dictionary<int, Skin> _skins = [];
    private readonly Dictionary<int, (System.Numerics.Matrix4x4[] Joints, float[]? Weights)> _poses = [];

    private readonly object _gate = new();
    private readonly Dictionary<int, (ModelVertex[] Vertices, uint[] Indices)> _live = [];
    private readonly Dictionary<int, Streams> _streams = [];
    // The meshes whose colors were written since the light field last asked, which it paints again.
    private readonly HashSet<int> _recolored = [];
    // The streams of the program's own, kept apart from the colors and second texture coordinates,
    // since a mesh with those carries both and one with this stream alone pays for neither.
    private readonly Dictionary<int, MeshStream> _custom = [];
    // The meshes uploaded from their positions alone, whose vertices here hold zero normals and
    // texture coordinates for the readers of the CPU's copy and reach the GPU as positions.
    private readonly HashSet<int> _positionsOnly = [];
    private readonly List<Upload> _uploads = [];
    private readonly List<int> _removals = [];
    private int _next = 1;

    /// <summary>How many meshes are loaded.</summary>
    public int Count
    {
        get { lock (_gate) return _live.Count + _counted.Count; }
    }

    /// <summary>Queues a mesh over shader buffers, which the program's dispatches write, and returns its id.</summary>
    public int AddCounted(IBuffer positions, (IBuffer Buffer, MeshStreamFormat Format)? stream, IBuffer count, uint capacity)
    {
        lock (_gate)
        {
            var id = _next++;
            var counted = new Counted(id, positions, stream, count, capacity);
            _counted[id] = counted;
            _countedUploads.Add(counted);
            return id;
        }
    }

    /// <summary>A mesh over shader buffers, or null for any other.</summary>
    internal Counted? CountedOf(int id)
    {
        lock (_gate) return _counted.GetValueOrDefault(id);
    }

    /// <summary>Whether any mesh over shader buffers is loaded, which a dispatch asks before it marks them.</summary>
    internal bool AnyCounted
    {
        get { lock (_gate) return _counted.Count > 0; }
    }

    /// <summary>
    /// Marks the meshes over a shader buffer a dispatch bound as written, so what reads their
    /// triangles reads them again, since the CPU cannot tell what the dispatch wrote.
    /// </summary>
    internal void TouchCounted(IBuffer buffer)
    {
        lock (_gate)
            foreach (var counted in _counted.Values)
                if (ReferenceEquals(counted.Positions, buffer) || ReferenceEquals(counted.Count, buffer)
                    || counted.Stream is { } stream && ReferenceEquals(stream.Buffer, buffer))
                    _countedVersions[counted.Id] = _countedVersions.GetValueOrDefault(counted.Id) + 1;
    }

    /// <summary>How many times a dispatch has bound a mesh over shader buffers' buffers since it was made.</summary>
    internal int CountedVersion(int id)
    {
        lock (_gate) return _countedVersions.GetValueOrDefault(id);
    }

    /// <summary>
    /// Sets the box a mesh over shader buffers is culled by, in its own space, and answers whether
    /// <paramref name="id"/> names one.
    /// </summary>
    internal bool SetCountedBox(int id, BoundingBox box)
    {
        lock (_gate)
        {
            if (!_counted.ContainsKey(id)) return false;
            _countedBoxes[id] = box;
            _boxChanges.Add((id, box));
            return true;
        }
    }

    /// <summary>The box a mesh over shader buffers was given, or null for none or for another mesh.</summary>
    internal BoundingBox? CountedBoxOf(int id)
    {
        lock (_gate) return _countedBoxes.TryGetValue(id, out var box) ? box : null;
    }

    /// <summary>Unloads the meshes over a shader buffer, before it is freed, and answers how many there were.</summary>
    internal int RemoveCountedOver(IBuffer buffer)
    {
        int[] over;
        lock (_gate)
            over = [.. _counted.Values.Where(c => ReferenceEquals(c.Positions, buffer) || ReferenceEquals(c.Count, buffer)
                                                  || c.Stream is { } stream && ReferenceEquals(stream.Buffer, buffer)).Select(c => c.Id)];
        foreach (var id in over) Remove(id);
        return over.Length;
    }

    /// <summary>Hands the meshes over shader buffers to make on the GPU and the boxes set since, and empties the queues.</summary>
    internal (Counted[] Made, (int Id, BoundingBox Box)[] Boxes) TakeCounted()
    {
        lock (_gate)
        {
            var taken = (_countedUploads.ToArray(), _boxChanges.ToArray());
            _countedUploads.Clear();
            _boxChanges.Clear();
            return taken;
        }
    }

    /// <summary>
    /// Queues a mesh of triangles, with its colors and second texture coordinates and its stream of
    /// the program's own when it has them, and returns its id.
    /// </summary>
    /// <exception cref="ArgumentException">The indices are not whole triangles, one is out of range, or a stream's count differs from the vertices'.</exception>
    public int Add(ModelVertex[] vertices, uint[] indices, Streams? streams = null, MeshStream? custom = null) =>
        Add(vertices, indices, streams, custom, positionsOnly: false);

    /// <summary>
    /// Queues a mesh of triangles from its positions alone, with its stream of the program's own and
    /// its colors where given, and returns its id. The GPU is given twelve bytes a vertex, and the
    /// vertices kept here have zero normals and texture coordinates, which the light field, the
    /// glow lights, picking and bodies read as they read any mesh's.
    /// </summary>
    /// <exception cref="ArgumentException">The indices are not whole triangles, one is out of range, or a stream's count differs from the positions'.</exception>
    public int AddPositions(System.Numerics.Vector3[] positions, uint[] indices, MeshStream? custom = null, Color[]? colors = null)
    {
        var vertices = new ModelVertex[positions.Length];
        for (int i = 0; i < positions.Length; i++) vertices[i] = new ModelVertex(positions[i], Vector3.Zero, System.Numerics.Vector2.Zero);
        return Add(vertices, indices, colors is null ? null : new Streams(colors, null), custom, positionsOnly: true);
    }

    private int Add(ModelVertex[] vertices, uint[] indices, Streams? streams, MeshStream? custom, bool positionsOnly)
    {
        if (indices.Length == 0 || indices.Length % 3 != 0)
            throw new ArgumentException("A mesh needs a whole number of triangles.", nameof(indices));
        foreach (var index in indices)
            if (index >= vertices.Length)
                throw new ArgumentException($"Index {index} is past the {vertices.Length} vertices.", nameof(indices));
        if (streams?.Colors is { } colors && colors.Length != vertices.Length)
            throw new ArgumentException($"{colors.Length} colors for {vertices.Length} vertices.", nameof(streams));
        if (streams?.Texcoords2 is { } texcoords2 && texcoords2.Length != vertices.Length)
            throw new ArgumentException($"{texcoords2.Length} second texture coordinates for {vertices.Length} vertices.", nameof(streams));
        if (streams is { Colors: null, Texcoords2: null }) streams = null;
        if (custom is not null) Check(custom, vertices.Length);

        lock (_gate)
        {
            var id = _next++;
            _live.Add(id, (vertices, indices));
            if (streams is not null) _streams[id] = streams;
            if (custom is not null) _custom[id] = custom;
            if (positionsOnly) _positionsOnly.Add(id);
            _uploads.Add(new Upload(id, vertices, indices, Streams: streams, Custom: custom));
            return id;
        }
    }

    // A stream of the program's own of a whole number of its format's vertices, one a vertex.
    private static void Check(MeshStream custom, int vertices)
    {
        var size = MeshStream.SizeOf(custom.Format);
        if (custom.Data.Length % size != 0 || custom.Data.Length / size != vertices)
            throw new ArgumentException($"{custom.Data.Length} bytes of {custom.Format} are not {vertices} vertices of {size} bytes each.", nameof(custom));
    }

    /// <summary>
    /// Whether a mesh was uploaded from its positions alone, and where <paramref name="firstAsked"/>
    /// is set whether it is the first time that is asked of it so, which a warning is given once by.
    /// </summary>
    internal bool IsPositionsOnly(int id, bool firstAsked = false)
    {
        lock (_gate) return _positionsOnly.Contains(id) && (!firstAsked || _positionsAsked.Add(id));
    }

    // The meshes of positions alone a warning has been given of.
    private readonly HashSet<int> _positionsAsked = [];

    /// <summary>A loaded mesh's stream of the program's own, or null for one without it.</summary>
    internal MeshStream? CustomOf(int id)
    {
        lock (_gate) return _custom.GetValueOrDefault(id);
    }

    /// <summary>
    /// Replaces a loaded mesh's stream of the program's own with one of as many vertices, which the
    /// renderer writes into the next of a ring of buffers and uploads nothing else of the mesh.
    /// </summary>
    /// <returns>Whether <paramref name="id"/> names a loaded mesh.</returns>
    /// <exception cref="ArgumentException">The count differs from the mesh's vertices'.</exception>
    internal bool UpdateCustom(int id, MeshStream custom)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(id, out var data)) return false;
            Check(custom, data.Vertices.Length);
            _custom[id] = custom;
            // A mesh whose whole upload is still queued takes the stream with it, and one whose
            // vertices are written in place this frame has the stream written in place beside them.
            int queued = _uploads.FindIndex(u => u.Id == id);
            if (queued < 0) _uploads.Add(new Upload(id, data.Vertices, data.Indices, Custom: custom, CustomOnly: true));
            else if (_uploads[queued] is { VerticesOnly: false, CustomOnly: false } whole) _uploads[queued] = whole with { Custom = custom };
            else _uploads[queued] = _uploads[queued] with { Custom = custom, CustomOnly = true };
            return true;
        }
    }

    /// <summary>A loaded mesh's colors and second texture coordinates, or null for one without them.</summary>
    internal Streams? StreamsOf(int id)
    {
        lock (_gate) return _streams.GetValueOrDefault(id);
    }

    /// <summary>
    /// Replaces a loaded mesh's vertices, keeping its triangles, as an animated mesh does each frame.
    /// </summary>
    /// <returns>Whether <paramref name="id"/> names a loaded mesh.</returns>
    /// <exception cref="ArgumentException">The count differs from the mesh's.</exception>
    /// <remarks>
    /// The renderer writes them into a new vertex buffer and destroys the old one once no frame in
    /// flight reads it, so a frame the GPU is still drawing keeps the vertices it was given.
    /// </remarks>
    internal bool UpdateVertices(int id, ModelVertex[] vertices)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(id, out var data)) return false;
            if (vertices.Length != data.Vertices.Length)
                throw new ArgumentException($"The mesh has {data.Vertices.Length} vertices, not {vertices.Length}.", nameof(vertices));

            _live[id] = (vertices, data.Indices);
            // A mesh not yet uploaded is uploaded whole with the new vertices, and an update
            // queued earlier this frame is replaced, since only the last one is seen, keeping a
            // write of the stream of the program's own queued with it.
            int queued = _uploads.FindIndex(u => u.Id == id);
            var previous = queued >= 0 ? _uploads[queued] : null;
            var upload = new Upload(id, vertices, data.Indices, VerticesOnly: previous is null || previous.VerticesOnly || previous.CustomOnly,
                Streams: _streams.GetValueOrDefault(id), Custom: _custom.GetValueOrDefault(id), CustomOnly: previous is { CustomOnly: true });
            if (queued >= 0) _uploads[queued] = upload;
            else _uploads.Add(upload);
            return true;
        }
    }

    /// <summary>
    /// Replaces a loaded mesh's vertices and triangles, of any count, keeping its id, so whatever
    /// draws the mesh draws the new shape, as a model built from an edit mesh does after an edit.
    /// </summary>
    /// <returns>Whether <paramref name="id"/> names a loaded mesh.</returns>
    /// <remarks>
    /// The mesh is uploaded whole again, its old buffers destroyed once no frame in flight reads
    /// them. It is for a mesh of plain vertices, and a stream, colors or a skin it had are dropped.
    /// </remarks>
    /// <exception cref="ArgumentException">The indices are not whole triangles, or one is out of range.</exception>
    internal bool Replace(int id, ModelVertex[] vertices, uint[] indices)
    {
        if (indices.Length == 0 || indices.Length % 3 != 0)
            throw new ArgumentException("A mesh needs a whole number of triangles.", nameof(indices));
        foreach (var index in indices)
            if (index >= vertices.Length)
                throw new ArgumentException($"Index {index} is past the {vertices.Length} vertices.", nameof(indices));

        lock (_gate)
        {
            if (!_live.ContainsKey(id)) return false;
            _live[id] = (vertices, indices);
            _skins.Remove(id);
            _streams.Remove(id);
            _custom.Remove(id);
            _positionsOnly.Remove(id);
            _uploads.RemoveAll(u => u.Id == id);
            _uploads.Add(new Upload(id, vertices, indices));
            return true;
        }
    }

    /// <summary>Whether <paramref name="id"/> names a loaded mesh.</summary>
    public bool Contains(int id)
    {
        lock (_gate) return _live.ContainsKey(id) || _counted.ContainsKey(id);
    }

    /// <summary>A loaded mesh's vertices and triangle indices, which the caller must not change.</summary>
    /// <returns>Whether <paramref name="id"/> names a loaded mesh.</returns>
    internal bool TryGetData(int id, out ModelVertex[] vertices, out uint[] indices)
    {
        lock (_gate)
        {
            var found = _live.TryGetValue(id, out var data);
            (vertices, indices) = found ? data : ([], []);
            return found;
        }
    }

    /// <summary>Unloads a mesh. Its buffers are destroyed once no frame in flight can use them.</summary>
    public bool Remove(int id)
    {
        lock (_gate)
        {
            if (_counted.Remove(id))
            {
                _countedBoxes.Remove(id);
                _countedVersions.Remove(id);
                _countedUploads.RemoveAll(c => c.Id == id);
                _boxChanges.RemoveAll(b => b.Id == id);
                _removals.Add(id);
                return true;
            }
            if (!_live.Remove(id)) return false;
            _skins.Remove(id);
            _streams.Remove(id);
            _custom.Remove(id);
            _positionsOnly.Remove(id);
            _positionsAsked.Remove(id);
            _poses.Remove(id);
            _uploads.RemoveAll(u => u.Id == id);
            _removals.Add(id);
            return true;
        }
    }

    /// <summary>
    /// Makes a loaded mesh one the GPU poses, by four joints and weights a vertex, which uploads it
    /// again with them.
    /// </summary>
    /// <returns>Whether <paramref name="id"/> names a loaded mesh that keeps more than its positions on the GPU, which a skin needs.</returns>
    internal bool SetSkin(int id, Skin skin)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(id, out var data) || _positionsOnly.Contains(id)) return false;
            _skins[id] = skin;
            _uploads.RemoveAll(u => u.Id == id);
            _uploads.Add(new Upload(id, data.Vertices, data.Indices, Skin: skin, Streams: _streams.GetValueOrDefault(id), Custom: _custom.GetValueOrDefault(id)));
            return true;
        }
    }

    /// <summary>Gives a loaded mesh its colors and second texture coordinates, which uploads it again with them.</summary>
    /// <returns>Whether <paramref name="id"/> names a loaded mesh.</returns>
    /// <exception cref="ArgumentException">A stream's count differs from the vertices'.</exception>
    internal bool SetStreams(int id, Streams streams)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(id, out var data)) return false;
            if (streams.Colors is { } colors && colors.Length != data.Vertices.Length)
                throw new ArgumentException($"{colors.Length} colors for {data.Vertices.Length} vertices.", nameof(streams));
            if (streams.Texcoords2 is { } texcoords2 && texcoords2.Length != data.Vertices.Length)
                throw new ArgumentException($"{texcoords2.Length} second texture coordinates for {data.Vertices.Length} vertices.", nameof(streams));
            _streams[id] = streams;
            // Asked for by the light field each frame it is on, and let go past what a world holds
            // where it is off.
            if (_recolored.Count > 65536) _recolored.Clear();
            _recolored.Add(id);
            _uploads.RemoveAll(u => u.Id == id);
            _uploads.Add(new Upload(id, data.Vertices, data.Indices, Skin: _skins.GetValueOrDefault(id), Streams: streams, Custom: _custom.GetValueOrDefault(id)));
            return true;
        }
    }

    /// <summary>
    /// The meshes whose colors <see cref="SetStreams"/> wrote since this was last called, which the
    /// light field paints again and the ray scene writes into its corners, forgotten as they are given.
    /// </summary>
    internal int[] TakeRecolored()
    {
        lock (_gate)
        {
            if (_recolored.Count == 0) return [];
            var ids = _recolored.ToArray();
            _recolored.Clear();
            return ids;
        }
    }

    /// <summary>Whether a mesh is posed on the GPU.</summary>
    internal bool IsSkinned(int id)
    {
        lock (_gate) return _skins.ContainsKey(id);
    }

    /// <summary>The joints and weights mesh <paramref name="id"/> is skinned by, or null for one that is not skinned.</summary>
    internal Skin? SkinOf(int id)
    {
        lock (_gate) return _skins.GetValueOrDefault(id);
    }

    /// <summary>
    /// Poses a skinned mesh for the next frame drawn, by each joint's matrix from rest to its pose
    /// in the model's space, and its morph targets' weights when it has any. Its vertices here stay
    /// at rest, since the GPU moves them.
    /// </summary>
    internal void PoseSkin(int id, System.Numerics.Matrix4x4[] joints, float[]? morphWeights = null)
    {
        lock (_gate)
            if (_skins.ContainsKey(id)) _poses[id] = (joints, morphWeights);
    }

    /// <summary>Hands the poses set since the last call to the renderer, the last of each mesh's.</summary>
    internal void TakePoses(List<(int Id, System.Numerics.Matrix4x4[] Joints, float[]? Weights)> into)
    {
        lock (_gate)
        {
            foreach (var (id, pose) in _poses) into.Add((id, pose.Joints, pose.Weights));
            _poses.Clear();
        }
    }

    /// <summary>Hands the queued uploads and removals to the renderer and empties the queues.</summary>
    internal (Upload[] Uploads, int[] Removals) Take()
    {
        lock (_gate)
        {
            // A mesh of positions alone reaches the GPU as its positions and its own stream, its
            // colors kept here for the readers of the CPU's copy, since its shader unpacks its color
            // from the stream, and a buffer of them and one of zero second texture coordinates
            // beside it took a section of the voxel game's 32 bytes a vertex where 20 were meant.
            var taken = (_uploads.Select(u => _positionsOnly.Contains(u.Id) ? u with { PositionsOnly = true, Streams = null } : u).ToArray(), _removals.ToArray());
            _uploads.Clear();
            _removals.Clear();
            return taken;
        }
    }
}

/// <summary>The vertex and index buffers of every mesh in the <see cref="MeshStore"/>.</summary>
/// <remarks>
/// A mesh whose vertices are replaced, as an animated one is each frame, moves into a ring of
/// <see cref="GpuTextures.RetireFrames"/> and one vertex buffers kept mapped, more than there are
/// frames in flight, and each update is written into the next. Creating a buffer for each update instead
/// cost about 2 ms a mesh in allocation on an NVIDIA driver (RENDERING.md §6).
/// </remarks>
internal sealed class GpuMeshes : IDisposable
{
    /// <summary>One mesh's buffers, with the ring its vertices move through once they are replaced.</summary>
    public sealed record Entry(IBuffer Vertices, IBuffer Indices, uint IndexCount)
    {
        /// <summary>Its colors, four bytes a vertex, or null for a mesh with neither these nor second texture coordinates.</summary>
        internal IBuffer? Colors { get; init; }

        /// <summary>Its second texture coordinates, eight bytes a vertex, or null for a mesh with neither these nor colors.</summary>
        internal IBuffer? Texcoords2 { get; init; }

        internal IBuffer[]? Ring { get; init; }
        internal int Slot { get; init; }

        /// <summary>Whether <see cref="Vertices"/> holds positions alone, twelve bytes a vertex, the normal and texture coordinate read as zero.</summary>
        internal bool PositionsOnly { get; init; }

        /// <summary>Its stream of the program's own and the stream's format, or null for a mesh without one.</summary>
        internal (IBuffer Buffer, MeshStreamFormat Format)? Custom { get; init; }

        // The ring the stream moves through once it is written in place, as the vertices' does.
        internal IBuffer[]? CustomRing { get; init; }
        internal int CustomSlot { get; init; }

        /// <summary>The GPU side of a skinned mesh, whose posed vertices are <see cref="Vertices"/>, or null.</summary>
        internal GpuSkin? Skin { get; init; }

        /// <summary>
        /// The box around its positions, which a pass leaves its draws out by when the box is outside
        /// what the pass sees, or null where the CPU does not know them, as a skin the GPU poses.
        /// </summary>
        internal (Vector3 Min, Vector3 Max)? Box { get; init; }

        /// <summary>
        /// The buffer whose first four bytes a shader writes the vertex count into and the most
        /// vertices drawn, for a mesh over shader buffers, drawn without indices by an indirect draw,
        /// or null for any other. Its buffers are the program's, freed by it.
        /// </summary>
        internal (IBuffer Count, uint Capacity)? Counted { get; init; }
    }

    private readonly Dictionary<int, Entry> _entries = [];
    private readonly List<(long Frame, IDisposable Owned)> _retired = [];
    private long _frame;

    /// <summary>The skinned meshes posed for this frame, with their joints' matrices, which the skinning node records.</summary>
    internal List<(int Id, System.Numerics.Matrix4x4[] Joints, float[]? Weights)> Poses { get; } = [];

    /// <summary>The buffers of mesh <paramref name="id"/>, or <c>null</c> when it is not loaded.</summary>
    public Entry? Get(int id) => _entries.GetValueOrDefault(id);

    /// <summary>Uploads queued meshes, retires unloaded ones, and destroys what has been retired long enough.</summary>
    public void Update(IGraphicsDevice gfx, MeshStore? store)
    {
        _frame++;
        if (store is not null)
        {
            Poses.Clear();
            store.TakePoses(Poses);
            var (uploads, removals) = store.Take();
            foreach (var id in removals)
                if (_entries.Remove(id, out var gone)) Retire(gone, vertices: true);

            // A mesh over shader buffers draws them as they are, its positions in place of the
            // indices it has none of, which no draw binds.
            var (counted, boxes) = store.TakeCounted();
            foreach (var made in counted)
                _entries[made.Id] = new Entry(made.Positions, made.Positions, 0)
                    { PositionsOnly = true, Custom = made.Stream, Counted = (made.Count, made.Capacity) };
            foreach (var (id, box) in boxes)
                if (_entries.TryGetValue(id, out var boxed)) _entries[id] = boxed with { Box = (box.Min, box.Max) };

            foreach (var upload in uploads)
            {
                // A mesh uploaded from its positions is given them alone.
                var bytes = upload.PositionsOnly ? MemoryMarshal.AsBytes(PositionsOf(upload.Vertices).AsSpan()) : MemoryMarshal.AsBytes(upload.Vertices.AsSpan());
                if ((upload.VerticesOnly || upload.CustomOnly) && _entries.TryGetValue(upload.Id, out var old))
                {
                    var entry = old;
                    if (upload.VerticesOnly)
                    {
                        var (ring, slot) = Advance(gfx, entry.Ring, entry.Slot, bytes, () => Retire(entry, vertices: true, indices: false));
                        entry = entry with { Vertices = ring[slot], Ring = ring, Slot = slot, Box = entry.Skin is null ? BoxOf(upload.Vertices) : null };
                    }
                    if (upload.CustomOnly && upload.Custom is { } custom)
                    {
                        var (ring, slot) = Advance(gfx, entry.CustomRing, entry.CustomSlot, custom.Data,
                            () => { if (entry.Custom is { } held) _retired.Add((_frame, held.Buffer)); });
                        entry = entry with { Custom = (ring[slot], custom.Format), CustomRing = ring, CustomSlot = slot };
                    }
                    _entries[upload.Id] = entry;
                    continue;
                }

                if (_entries.Remove(upload.Id, out var replaced)) Retire(replaced, vertices: true);
                var indices = Buffer(gfx, MemoryMarshal.AsBytes(upload.Indices.AsSpan()), BufferUsage.Index);
                // A mesh with one of the two is given the other at its default, white or zero, so
                // the model pass reads both a vertex at a time, which measured faster than reading
                // the missing one from a buffer of a single element.
                var (colors, texcoords2) = upload.Streams is { } streams
                    ? (Buffer(gfx, streams.Colors is { } c ? MemoryMarshal.AsBytes(c.AsSpan()) : Filled(upload.Vertices.Length * 4, 255), BufferUsage.Vertex),
                       Buffer(gfx, streams.Texcoords2 is { } t ? MemoryMarshal.AsBytes(t.AsSpan()) : new byte[upload.Vertices.Length * 8], BufferUsage.Vertex))
                    : ((IBuffer?)null, (IBuffer?)null);
                (IBuffer, MeshStreamFormat)? own = upload.Custom is { } stream ? (Buffer(gfx, stream.Data, BufferUsage.Vertex), stream.Format) : null;
                // A skin the GPU poses writes its vertices into a buffer of its own.
                if (upload.Skin is { } skinData && gfx is GraphicsDevice { CanSkin: true } device)
                {
                    var skin = device.CreateSkin(upload.Vertices, skinData.Joints, skinData.Weights, skinData.JointCount, GpuTextures.RetireFrames + 1,
                        skinData.Morphs, skinData.MorphCount);
                    _entries[upload.Id] = new Entry(skin.Output, indices, (uint)upload.Indices.Length) { Skin = skin, Colors = colors, Texcoords2 = texcoords2, Custom = own };
                    continue;
                }
                _entries[upload.Id] = new Entry(Buffer(gfx, bytes, BufferUsage.Vertex), indices, (uint)upload.Indices.Length)
                    { Colors = colors, Texcoords2 = texcoords2, Custom = own, Box = BoxOf(upload.Vertices), PositionsOnly = upload.PositionsOnly };
            }
        }

        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < GpuTextures.RetireFrames) continue;
            _retired[i].Owned.Dispose();
            _retired.RemoveAt(i);
        }
    }

    // Writes replaced vertices, or a stream written in place, into the next buffer of its ring,
    // making the ring the first time after retiring what it replaces. A buffer comes round again
    // only after RetireFrames more updates, at most one a frame, by which time no frame in flight
    // reads it.
    private static (IBuffer[] Ring, int Slot) Advance(IGraphicsDevice gfx, IBuffer[]? ring, int slot, ReadOnlySpan<byte> data, Action retire)
    {
        if (ring is null || ring[0].Description.Size != (ulong)data.Length)
        {
            retire();
            ring = new IBuffer[GpuTextures.RetireFrames + 1];
            for (int i = 0; i < ring.Length; i++)
                ring[i] = gfx.CreateBuffer(new BufferDesc((ulong)data.Length, BufferUsage.Vertex, CpuAccessMode.Write));
            slot = -1;
        }

        slot = (slot + 1) % ring.Length;
        data.CopyTo(gfx.Map(ring[slot]));
        return (ring, slot);
    }

    private static Vector3[] PositionsOf(ModelVertex[] vertices)
    {
        var positions = new Vector3[vertices.Length];
        for (int i = 0; i < vertices.Length; i++) positions[i] = vertices[i].Position;
        return positions;
    }

    // The box around a mesh's positions, or null for a mesh with none.
    private static (Vector3 Min, Vector3 Max)? BoxOf(ModelVertex[] vertices)
    {
        if (vertices.Length == 0) return null;
        var (min, max) = (vertices[0].Position, vertices[0].Position);
        foreach (ref readonly var vertex in vertices.AsSpan())
        {
            min = Vector3.Min(min, vertex.Position);
            max = Vector3.Max(max, vertex.Position);
        }
        return (min, max);
    }

    // Hands a mesh's buffers to the retired list, which destroys them once no frame can read them.
    private void Retire(Entry entry, bool vertices, bool indices = true)
    {
        // A mesh over shader buffers holds the program's buffers, which it frees.
        if (entry.Counted is not null) return;
        if (vertices)
        {
            // A skin owns its vertex buffer.
            if (entry.Skin is { } skin) _retired.Add((_frame, skin));
            else if (entry.Ring is { } ring)
                foreach (var buffer in ring) _retired.Add((_frame, buffer));
            else _retired.Add((_frame, entry.Vertices));
        }
        if (indices)
        {
            _retired.Add((_frame, entry.Indices));
            if (entry.Colors is { } colors) _retired.Add((_frame, colors));
            if (entry.Texcoords2 is { } texcoords2) _retired.Add((_frame, texcoords2));
            if (entry.CustomRing is { } customRing)
                foreach (var buffer in customRing) _retired.Add((_frame, buffer));
            else if (entry.Custom is { } custom) _retired.Add((_frame, custom.Buffer));
        }
    }

    // Host-visible buffers written through a mapping. A device-local buffer behind a staging copy
    // would draw faster, which RENDERING.md lists with the move to the memory allocator.
    private static byte[] Filled(int length, byte value)
    {
        var bytes = new byte[length];
        bytes.AsSpan().Fill(value);
        return bytes;
    }

    private static IBuffer Buffer(IGraphicsDevice gfx, ReadOnlySpan<byte> data, BufferUsage usage)
    {
        var buffer = gfx.CreateBuffer(new BufferDesc((ulong)data.Length, usage, CpuAccessMode.Write));
        data.CopyTo(gfx.Map(buffer));
        gfx.Unmap(buffer);
        return buffer;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, owned) in _retired) owned.Dispose();
        foreach (var entry in _entries.Values)
        {
            if (entry.Skin is { } skin) skin.Dispose();
            else if (entry.Ring is { } ring)
                foreach (var buffer in ring) buffer.Dispose();
            else entry.Vertices.Dispose();
            entry.Indices.Dispose();
            entry.Colors?.Dispose();
            entry.Texcoords2?.Dispose();
            if (entry.CustomRing is { } customRing)
                foreach (var buffer in customRing) buffer.Dispose();
            else entry.Custom?.Buffer.Dispose();
        }
        _retired.Clear();
        _entries.Clear();
    }
}

/// <summary>Prepare system that brings <see cref="GpuMeshes"/> up to date and hands it to the render world.</summary>
internal sealed class GpuMeshesPrepare : IPrepareSystem, IDisposable
{
    private readonly GpuMeshes _meshes = new();

    /// <inheritdoc />
    public void Run(RenderWorld renderWorld, RenderContext renderContext)
    {
        _meshes.Update(renderContext.Device, renderWorld.TryGet<MeshStore>());
        renderWorld.Set(_meshes);
    }

    /// <inheritdoc />
    public void Dispose() => _meshes.Dispose();
}

/// <summary>
/// Render graph node that poses the frame's skinned meshes on the GPU, before anything that draws
/// them, the shadow map first.
/// </summary>
internal sealed class SkinningNode : INode
{
    /// <inheritdoc />
    public void Run(RenderGraphContext graphContext, RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<GpuMeshes>() is not { } meshes || renderContext.Device is not GraphicsDevice device) return;
        foreach (var (id, joints, weights) in meshes.Poses)
            if (meshes.Get(id)?.Skin is { } skin)
                device.RecordSkin(renderContext.CommandBuffer, skin, joints, weights);
    }
}
