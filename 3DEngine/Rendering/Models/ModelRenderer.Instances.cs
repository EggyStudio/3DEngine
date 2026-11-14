using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Engine;

internal sealed partial class ModelRenderer
{
    // Every draw's instance, a region per frame slot, kept mapped. Each frame writes its instances
    // into its own region, which the GPU finished reading RetireFrames frames ago, the shadow pass's
    // and every target's one after another.
    private IBuffer? _instanceRing;
    private int _ringCapacity;
    private int _ringSlot;
    private int _ringCursor;
    private readonly List<(long Frame, IBuffer Buffer)> _retiredBuffers = [];

    // The frame's group segments to copy into the ring, each with where its first instance goes,
    // where its first block goes in _blocks, and the quantum of its mesh's packed positions.
    private readonly List<(Instance[] Items, int Count, int At, InstanceGroup Group)> _copies = [];
    private readonly List<int> _copyBlocks = [];
    private readonly List<float> _copyQuanta = [];

    // Each batch of draws' first block and first instance and its mesh's box, or a block of -1 for
    // a batch whose draws are not in blocks.
    private readonly List<(int Block, uint First, Vector3 Min, Vector3 Max)> _drawBlocks = [];

    // A run of instances in the ring and the box around them, which a view leaves out when the box
    // is outside it. A batch's instances are in blocks of this many, so a view draws the runs of
    // blocks it sees, each a call, rather than every instance.
    private const int BlockSize = 64;

    private struct Block
    {
        public uint First;
        public uint Count;
        public Vector3 Min;
        public Vector3 Max;
        // Whether a pass of the frame drew the block, the camera's, a cascade's or a light's.
        public bool Drawn;
    }

    /// <summary>
    /// The instances the last frame copied into the ring for its groups and those of them in blocks
    /// some pass drew, the camera's, a shadow cascade's or a light's, over every view, read between
    /// frames, so what copying the blocks no pass drew costs can be weighed.
    /// </summary>
    internal (long Copied, long Drawn) BlocksLastFrame()
    {
        long copied = 0, drawn = 0;
        foreach (var view in _views.Values)
            foreach (var batch in view.Batches)
            {
                if (batch.Group < 0) continue;
                for (int i = batch.BlockStart; i < batch.BlockStart + batch.BlockCount; i++)
                {
                    copied += view.Blocks[i].Count;
                    if (view.Blocks[i].Drawn) drawn += view.Blocks[i].Count;
                }
            }
        return (copied, drawn);
    }

    // The blocks of the last gathered view's batches, which the view keeps a copy of.
    private Block[] _blocks = new Block[64];
    private int _blockCount;

    // Past this many instances the segments are copied on several threads, since every pass that
    // draws them waits for the copy, and one thread writing so many into mapped memory holds up the
    // shadow pass's recording.
    private const int ParallelCopyInstances = 16384;

    /// <summary>
    /// Gathers the window's batches and writes their instances for this frame, which every pass
    /// drawing the window's meshes then reads, so the cost is the frame's once whichever draws first.
    /// </summary>
    internal void GatherWindow(RenderContext renderContext, RenderWorld renderWorld)
    {
        if (renderWorld.TryGet<ModelDrawList>() is not { IsEmpty: false } draws || renderWorld.TryGet<GpuMeshes>() is not { } meshes
            || renderWorld.TryGet<GpuTextures>() is not { } textures)
            return;
        BeginFrameOfSets(renderContext);
        ViewBatches(0, renderContext.Device, draws, meshes, textures, renderWorld.TryGet<ShaderStore>(), renderWorld.TryGet<RenderOrigin>());
    }

    // Writes each batched draw's instance into this frame's region of the ring, a batch's
    // instances together, and gives each batch the first of them, counted from the returned offset
    // in bytes. A group's batch is its instances, copied as they are. A batch of draws is in
    // blocks of BlockSize, each block around its draws' boxes, so every pass leaves out the draws
    // it cannot see, unless the batch's shader is the program's own, which may move its vertices
    // past the mesh's box, or the mesh's box is not known, as a skin the GPU poses is not.
    private (IBuffer Ring, ulong Offset) WriteInstances(IGraphicsDevice gfx, ReadOnlySpan<ModelDraw> draws, InstanceOf<Instance> instanceOf,
        IReadOnlyList<InstanceGroup> groups, ShaderStore? store)
    {
        uint total = 0;
        foreach (var batch in _batches) total += batch.Count;
        var slots = (int)total;
        EnsureInstanceRoom(gfx, slots);

        var offset = (ulong)(_ringSlot * _ringCapacity + _ringCursor) * Instance.Size;
        _ringCursor += slots;
        uint first = 0;
        _filled.Clear();
        _drawBlocks.Clear();
        _blockCount = 0;
        for (int b = 0; b < _batches.Count; b++)
        {
            var batch = _batches[b];
            batch.First = first;
            // A batch of a shader of the program's own is drawn whole in every pass, since its vertex
            // stage may move the vertices past the mesh's box, unless the program said the stage
            // keeps them (SetShaderKeepsPositions).
            var kept = batch.Custom < 0 || store?.KeepsPositions(draws[batch.Custom].Shader) == true;
            var box = batch.Group < 0 && kept && batch.Count > 0 ? batch.Mesh.Box : null;
            batch.BlockStart = _blockCount;
            batch.BlockCount = box is null ? 0 : (int)((batch.Count + BlockSize - 1) / BlockSize);
            _drawBlocks.Add(box is { } around ? (_blockCount, first, around.Min, around.Max) : (-1, 0, default, default));
            _blockCount += batch.BlockCount;
            _batches[b] = batch;
            _filled.Add(first);
            first += batch.Count;
        }
        if (_blocks.Length < _blockCount) Array.Resize(ref _blocks, Math.Max(_blockCount, _blocks.Length * 2));
        for (int b = 0; b < _batches.Count; b++)
        {
            if (_drawBlocks[b].Block < 0) continue;
            var (start, end) = (_batches[b].First, _batches[b].First + _batches[b].Count);
            for (uint at = start, k = (uint)_drawBlocks[b].Block; at < end; at += BlockSize, k++)
                _blocks[k] = new Block { First = at, Count = Math.Min(BlockSize, end - at), Min = new Vector3(float.MaxValue), Max = new Vector3(float.MinValue) };
        }

        var instances = MemoryMarshal.Cast<byte, Instance>(gfx.Map(_instanceRing!)[(int)offset..]);
        for (int i = 0; i < _drawBatch.Count; i++)
        {
            var b = _drawBatch[i];
            if (b < 0) continue;
            var slot = _filled[b]++;
            instances[(int)slot] = Placed(Folded(instanceOf(in draws[i]), _batches[b].Mesh.Quantum));
            var (block, from, min, max) = _drawBlocks[b];
            if (block >= 0) Enclose(ref _blocks[block + (int)((slot - from) / BlockSize)], _origin.Against(draws[i].World), min, max);
        }
        _copies.Clear();
        _copyBlocks.Clear();
        _copyQuanta.Clear();
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
                _copyQuanta.Add(batch.Mesh.Quantum);
                _blockCount += (_copies[c].Count + BlockSize - 1) / BlockSize;
            }
            batch.BlockCount = _blockCount - batch.BlockStart;
            _batches[b] = batch;
        }
        if (_blocks.Length < _blockCount) Array.Resize(ref _blocks, Math.Max(_blockCount, _blocks.Length * 2));
        CopySegments(instances, copied);
        return (_instanceRing!, offset);
    }

    // The place the frame's instances and views are taken against (RenderOrigin), and the frame's
    // draws, which hold the eyes its views were made from.
    private RenderOrigin _origin;
    private ModelDrawList? _draws;

    // An instance taken against the frame's origin as it is written into the ring, which is mapped
    // memory the GPU reads and is never read back.
    private Instance Placed(Instance instance) => _origin == RenderOrigin.Zero ? instance : Against(instance, _origin);

    // An instance whose translation is taken against the origin, worked in doubles.
    internal static Instance Against(Instance instance, RenderOrigin origin)
    {
        instance.WorldX.W = (float)(instance.WorldX.W - origin.X);
        instance.WorldY.W = (float)(instance.WorldY.W - origin.Y);
        instance.WorldZ.W = (float)(instance.WorldZ.W - origin.Z);
        return instance;
    }

    // The views worked against the frame's origin this frame, by their view-projection and by that
    // with the move of clip space after it as pushed, each worked once a frame.
    private readonly Dictionary<Matrix4x4, Matrix4x4> _placedViews = [];
    private readonly Dictionary<(Matrix4x4 ViewProjection, Matrix4x4? After), ViewPush> _pushes = [];

    // A view's view-projection against the frame's origin, from its eye where the frame noted one,
    // which the passes push and find the planes they leave blocks out by from.
    private Matrix4x4 Placed(in Matrix4x4 viewProjection)
    {
        if (!_placedViews.TryGetValue(viewProjection, out var placed))
            _placedViews[viewProjection] = placed = _origin.Seen(viewProjection, _draws?.EyeOf(viewProjection));
        return placed;
    }

    // Pushes a view-projection that takes a place against the frame's origin already, a light's
    // fitted against it, as it is, with the origin.
    private void PushPlaced(TrackedRenderPass pass, IPipeline pipeline, in Matrix4x4 viewProjection)
    {
        var push = new ViewPush { ViewProjection = viewProjection, Origin = new Vector4(_origin.AsVector3, 0) };
        pass.PushConstants(pipeline, ShaderStageFlags.Vertex | ShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<ViewPush>(in push)));
    }

    // Pushes a view's view-projection against the frame's origin, from its eye where the frame
    // noted one, with a move of clip space after it where one is given, and the origin.
    private void PushView(TrackedRenderPass pass, IPipeline pipeline, in Matrix4x4 viewProjection, Matrix4x4? after = null)
    {
        if (!_pushes.TryGetValue((viewProjection, after), out var push))
            _pushes[(viewProjection, after)] = push = _origin.Push(viewProjection, _draws?.EyeOf(viewProjection), after);
        pass.PushConstants(pipeline, ShaderStageFlags.Vertex | ShaderStageFlags.Fragment, 0, MemoryMarshal.AsBytes(new ReadOnlySpan<ViewPush>(in push)));
    }

    // An instance of a mesh whose positions are packed integers (PositionPacking), the rows that
    // turn its position scaled by the integers' quantum, a power of two, and its translation as it
    // is, so the vertex stage's position is the float the mesh's own positions give, bit for bit.
    internal static Instance Folded(Instance instance, float quantum)
    {
        if (quantum == 1) return instance;
        instance.WorldX = new Vector4(instance.WorldX.X * quantum, instance.WorldX.Y * quantum, instance.WorldX.Z * quantum, instance.WorldX.W);
        instance.WorldY = new Vector4(instance.WorldY.X * quantum, instance.WorldY.Y * quantum, instance.WorldY.Z * quantum, instance.WorldY.W);
        instance.WorldZ = new Vector4(instance.WorldZ.X * quantum, instance.WorldZ.Y * quantum, instance.WorldZ.Z * quantum, instance.WorldZ.W);
        return instance;
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
                Write(items.AsSpan(0, n), instances[at..], _copyQuanta[i], _origin);
                Bound(items, n, at, group.Sphere, _blocks, _copyBlocks[i], _origin);
            }
            return;
        }

        // The ring is mapped memory, which does not move, so its address outlives the span.
        var ring = (nint)Unsafe.AsPointer(ref MemoryMarshal.GetReference(instances));
        var length = instances.Length;
        var (blocks, origin) = (_blocks, _origin);
        Parallel.For(0, _copies.Count, i =>
        {
            var (items, n, at, group) = _copies[i];
            Write(items.AsSpan(0, n), new Span<Instance>((Instance*)ring + at, length - at), _copyQuanta[i], origin);
            Bound(items, n, at, group.Sphere, blocks, _copyBlocks[i], origin);
        });
    }

    // A group's instances written into the ring, each of a packed mesh scaled and each taken against
    // the origin as it is written, after their blocks' boxes were found from them as they were, and
    // copied whole where neither changes them, the ring never read back.
    private static void Write(ReadOnlySpan<Instance> items, Span<Instance> ring, float quantum, RenderOrigin origin)
    {
        if (quantum == 1 && origin == RenderOrigin.Zero)
        {
            items.CopyTo(ring);
            return;
        }
        for (int k = 0; k < items.Length; k++)
        {
            var instance = quantum == 1 ? items[k] : Folded(items[k], quantum);
            ring[k] = origin == RenderOrigin.Zero ? instance : Against(instance, origin);
        }
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
}
