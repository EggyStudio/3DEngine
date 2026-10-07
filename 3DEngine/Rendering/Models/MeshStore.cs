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
    /// <summary>A mesh waiting to be uploaded, or only its vertices when <paramref name="VerticesOnly"/> is set.</summary>
    /// <param name="Id">The mesh's id.</param>
    /// <param name="Vertices">Its vertices.</param>
    /// <param name="Indices">Three indices into the vertices for each triangle.</param>
    /// <param name="VerticesOnly">Whether only the vertices changed, so the index buffer is kept.</param>
    /// <param name="Skin">The joints and weights that pose it on the GPU, or null for a mesh that is not skinned there.</param>
    /// <param name="Streams">A color and a second texture coordinate at each vertex, which a mesh may have beside its vertices.</param>
    public sealed record Upload(int Id, ModelVertex[] Vertices, uint[] Indices, bool VerticesOnly = false, Skin? Skin = null, Streams? Streams = null);

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

    private readonly Dictionary<int, Skin> _skins = [];
    private readonly Dictionary<int, (System.Numerics.Matrix4x4[] Joints, float[]? Weights)> _poses = [];

    private readonly object _gate = new();
    private readonly Dictionary<int, (ModelVertex[] Vertices, uint[] Indices)> _live = [];
    private readonly Dictionary<int, Streams> _streams = [];
    private readonly List<Upload> _uploads = [];
    private readonly List<int> _removals = [];
    private int _next = 1;

    /// <summary>How many meshes are loaded.</summary>
    public int Count
    {
        get { lock (_gate) return _live.Count; }
    }

    /// <summary>Queues a mesh of triangles, with its colors and second texture coordinates when it has them, and returns its id.</summary>
    /// <exception cref="ArgumentException">The indices are not whole triangles, one is out of range, or a stream's count differs from the vertices'.</exception>
    public int Add(ModelVertex[] vertices, uint[] indices, Streams? streams = null)
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

        lock (_gate)
        {
            var id = _next++;
            _live.Add(id, (vertices, indices));
            if (streams is not null) _streams[id] = streams;
            _uploads.Add(new Upload(id, vertices, indices, Streams: streams));
            return id;
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
            // queued earlier this frame is replaced, since only the last one is seen.
            int queued = _uploads.FindIndex(u => u.Id == id);
            var upload = new Upload(id, vertices, data.Indices, VerticesOnly: queued < 0 || _uploads[queued].VerticesOnly, Streams: _streams.GetValueOrDefault(id));
            if (queued >= 0) _uploads[queued] = upload;
            else _uploads.Add(upload);
            return true;
        }
    }

    /// <summary>Whether <paramref name="id"/> names a loaded mesh.</summary>
    public bool Contains(int id)
    {
        lock (_gate) return _live.ContainsKey(id);
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
            if (!_live.Remove(id)) return false;
            _skins.Remove(id);
            _streams.Remove(id);
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
    /// <returns>Whether <paramref name="id"/> names a loaded mesh.</returns>
    internal bool SetSkin(int id, Skin skin)
    {
        lock (_gate)
        {
            if (!_live.TryGetValue(id, out var data)) return false;
            _skins[id] = skin;
            _uploads.RemoveAll(u => u.Id == id);
            _uploads.Add(new Upload(id, data.Vertices, data.Indices, Skin: skin, Streams: _streams.GetValueOrDefault(id)));
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
            _uploads.RemoveAll(u => u.Id == id);
            _uploads.Add(new Upload(id, data.Vertices, data.Indices, Skin: _skins.GetValueOrDefault(id), Streams: streams));
            return true;
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
            var taken = (_uploads.ToArray(), _removals.ToArray());
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

        /// <summary>The GPU side of a skinned mesh, whose posed vertices are <see cref="Vertices"/>, or null.</summary>
        internal GpuSkin? Skin { get; init; }
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

            foreach (var upload in uploads)
            {
                var bytes = MemoryMarshal.AsBytes(upload.Vertices.AsSpan());
                if (upload.VerticesOnly && _entries.TryGetValue(upload.Id, out var old))
                {
                    _entries[upload.Id] = Advance(gfx, old, bytes);
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
                // A skin the GPU poses writes its vertices into a buffer of its own.
                if (upload.Skin is { } skinData && gfx is GraphicsDevice { CanSkin: true } device)
                {
                    var skin = device.CreateSkin(upload.Vertices, skinData.Joints, skinData.Weights, skinData.JointCount, GpuTextures.RetireFrames + 1,
                        skinData.Morphs, skinData.MorphCount);
                    _entries[upload.Id] = new Entry(skin.Output, indices, (uint)upload.Indices.Length) { Skin = skin, Colors = colors, Texcoords2 = texcoords2 };
                    continue;
                }
                _entries[upload.Id] = new Entry(Buffer(gfx, bytes, BufferUsage.Vertex), indices, (uint)upload.Indices.Length)
                    { Colors = colors, Texcoords2 = texcoords2 };
            }
        }

        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < GpuTextures.RetireFrames) continue;
            _retired[i].Owned.Dispose();
            _retired.RemoveAt(i);
        }
    }

    // Writes replaced vertices into the next buffer of the mesh's ring, making the ring the first
    // time. A buffer comes round again only after RetireFrames more updates, at most one a frame,
    // by which time no frame in flight reads it.
    private Entry Advance(IGraphicsDevice gfx, Entry entry, ReadOnlySpan<byte> vertices)
    {
        var ring = entry.Ring;
        if (ring is null || ring[0].Description.Size != (ulong)vertices.Length)
        {
            Retire(entry, vertices: true, indices: false);
            ring = new IBuffer[GpuTextures.RetireFrames + 1];
            for (int i = 0; i < ring.Length; i++)
                ring[i] = gfx.CreateBuffer(new BufferDesc((ulong)vertices.Length, BufferUsage.Vertex, CpuAccessMode.Write));
            entry = entry with { Ring = ring, Slot = -1 };
        }

        var slot = (entry.Slot + 1) % ring.Length;
        vertices.CopyTo(gfx.Map(ring[slot]));
        return entry with { Vertices = ring[slot], Slot = slot };
    }

    // Hands a mesh's buffers to the retired list, which destroys them once no frame can read them.
    private void Retire(Entry entry, bool vertices, bool indices = true)
    {
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
