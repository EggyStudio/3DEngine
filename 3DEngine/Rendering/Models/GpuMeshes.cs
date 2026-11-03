using System.Numerics;
using System.Runtime.InteropServices;

namespace Engine;

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

        /// <summary>The width of each index in <see cref="Indices"/>, two bytes for a mesh of 65,536 vertices or fewer and four for a larger one.</summary>
        internal IndexType IndexType { get; init; } = IndexType.UInt32;

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
        /// The buffer a shader writes the vertex count into, the most vertices drawn, the first vertex
        /// and the count's byte in the buffer, for a mesh over shader buffers, drawn without indices
        /// by an indirect draw, or null for any other. Its buffers are the program's, freed by it.
        /// </summary>
        internal (IBuffer Count, uint Capacity, uint First, uint CountOffset)? Counted { get; init; }
    }

    private readonly Dictionary<int, Entry> _entries = [];
    private readonly List<(long Frame, IDisposable Owned)> _retired = [];
    private long _frame;

    // Zero second texture coordinates for the meshes over shader buffers that have colors, which
    // the model pass binds beside the colors, one buffer for them all as long as the furthest vertex
    // any of them draws, since such a mesh may lie anywhere in a large buffer.
    private IBuffer? _zeros;

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
                {
                    PositionsOnly = true, Custom = made.Stream, Counted = (made.Count, made.Capacity, made.First, made.CountAt * 4),
                    Colors = made.Colors, Texcoords2 = made.Colors is null ? null : Zeros(gfx, made.First + made.Capacity),
                };
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
                // Two bytes an index where every index a mesh has fits in them, half the bytes a draw
                // reads and the upload carries, as a voxel game's sections of fewer than 65,536
                // vertices each are, the CPU keeping its four-byte ones for picking, bodies and the
                // scene's field.
                var (indices, indexType) = upload.Vertices.Length <= 65536
                    ? (Buffer(gfx, MemoryMarshal.AsBytes(Narrowed(upload.Indices).AsSpan()), BufferUsage.Index), IndexType.UInt16)
                    : (Buffer(gfx, MemoryMarshal.AsBytes(upload.Indices.AsSpan()), BufferUsage.Index), IndexType.UInt32);
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
                    _entries[upload.Id] = new Entry(skin.Output, indices, (uint)upload.Indices.Length)
                        { Skin = skin, Colors = colors, Texcoords2 = texcoords2, Custom = own, IndexType = indexType };
                    continue;
                }
                _entries[upload.Id] = new Entry(Buffer(gfx, bytes, BufferUsage.Vertex), indices, (uint)upload.Indices.Length)
                    { Colors = colors, Texcoords2 = texcoords2, Custom = own, Box = BoxOf(upload.Vertices), PositionsOnly = upload.PositionsOnly, IndexType = indexType };
            }
        }

        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < GpuTextures.RetireFrames) continue;
            _retired[i].Owned.Dispose();
            _retired.RemoveAt(i);
        }
    }

    // A mesh's indices as two bytes each, every one under 65,536.
    private static ushort[] Narrowed(uint[] indices)
    {
        var narrow = new ushort[indices.Length];
        for (int i = 0; i < indices.Length; i++) narrow[i] = (ushort)indices[i];
        return narrow;
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

    // The zero second texture coordinates for vertices up to that many, made larger where a mesh
    // reaches further and given to every mesh that held the smaller, which is retired.
    private IBuffer Zeros(IGraphicsDevice gfx, uint vertices)
    {
        var bytes = (ulong)Math.Max(1u, vertices) * 8;
        if (_zeros is { } held && held.Description.Size >= bytes) return held;
        var made = Buffer(gfx, new byte[Math.Max(bytes, (_zeros?.Description.Size ?? 0) * 2)], BufferUsage.Vertex);
        if (_zeros is { } old)
        {
            foreach (var (id, entry) in _entries.ToArray())
                if (ReferenceEquals(entry.Texcoords2, old)) _entries[id] = entry with { Texcoords2 = made };
            _retired.Add((_frame, old));
        }
        return _zeros = made;
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

    // A buffer written once, in the device's own memory where the device is Vulkan's, since one in
    // memory the CPU sees is read across the bus by every pass that draws it
    // (GraphicsDevice.CreateFilledBuffer).
    private static IBuffer Buffer(IGraphicsDevice gfx, ReadOnlySpan<byte> data, BufferUsage usage)
    {
        if (gfx is GraphicsDevice device) return device.CreateFilledBuffer(data, usage);
        var buffer = gfx.CreateBuffer(new BufferDesc((ulong)data.Length, usage, CpuAccessMode.Write));
        data.CopyTo(gfx.Map(buffer));
        gfx.Unmap(buffer);
        return buffer;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, owned) in _retired) owned.Dispose();
        _zeros?.Dispose();
        _zeros = null;
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
