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
    /// <param name="PositionsOnly">Whether the GPU is given the vertices' positions alone, twelve bytes a vertex or six where they lie on a grid (<see cref="PositionPacking"/>), for a mesh uploaded from its positions.</param>
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
    /// A mesh over shader buffers, its positions twelve bytes a vertex, its stream of the program's
    /// own and its colors, four bytes a vertex, where it has them, drawn without indices from vertex
    /// <see cref="First"/> as many vertices as a shader wrote into the uint at <see cref="CountAt"/>
    /// of its count buffer, at most its capacity. Nothing of it is kept here, so the readers of a
    /// mesh's vertices on the CPU pass it over.
    /// </summary>
    public sealed record Counted(int Id, IBuffer Positions, (IBuffer Buffer, MeshStreamFormat Format)? Stream, IBuffer Count, uint Capacity,
        IBuffer? Colors = null, uint First = 0, uint CountAt = 0);

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
    public int AddCounted(IBuffer positions, (IBuffer Buffer, MeshStreamFormat Format)? stream, IBuffer count, uint capacity,
        IBuffer? colors = null, uint first = 0, uint countAt = 0)
    {
        lock (_gate)
        {
            var id = _next++;
            var counted = new Counted(id, positions, stream, count, capacity, colors, first, countAt);
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
                    || ReferenceEquals(counted.Colors, buffer) || counted.Stream is { } stream && ReferenceEquals(stream.Buffer, buffer))
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
    /// its colors where given, and returns its id. The GPU is given twelve bytes a vertex, or six
    /// where the positions lie on a grid (<see cref="PositionPacking"/>), and the vertices kept
    /// here have zero normals and texture coordinates, which the light field, the glow lights,
    /// picking and bodies read as they read any mesh's.
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

    /// <summary>
    /// Gives a loaded mesh its colors and second texture coordinates, which uploads it again with
    /// them, or for a mesh of positions alone, whose colors the GPU holds none of, keeps them here
    /// for the light field and the glow lights and leaves its buffers as they are.
    /// </summary>
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
            // A write of the stream queued this frame stays a write in place, as a voxel game's
            // section relit by its stream and its colors is, where uploading it again whole took
            // its positions, indices and stream to the GPU each time.
            if (_positionsOnly.Contains(id)) return true;
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
