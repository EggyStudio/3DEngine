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
    // and where its first block goes in _blocks.
    private readonly List<(Instance[] Items, int Count, int At, InstanceGroup Group)> _copies = [];
    private readonly List<int> _copyBlocks = [];

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

    // Past this many instances the segments are copied on several threads, since one thread
    // writing tens of megabytes into mapped memory took most of the shadow pass's recording.
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
        ViewBatches(0, renderContext.Device, draws, meshes, textures, renderWorld.TryGet<ShaderStore>());
    }

    // Writes each batched draw's instance into this frame's region of the ring, a batch's
    // instances together, and gives each batch the first of them, counted from the returned offset
    // in bytes. A group's batch is its instances, copied as they are. A batch of draws is in
    // blocks as a group is, each block around its draws' boxes, so every pass leaves out the draws
    // it cannot see, unless the batch's shader is the program's own, which may move its vertices
    // past the mesh's box, or the mesh's box is not known, as a skin the GPU poses is not.
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
        _drawBlocks.Clear();
        _blockCount = 0;
        for (int b = 0; b < _batches.Count; b++)
        {
            var batch = _batches[b];
            batch.First = first;
            var box = batch.Group < 0 && batch.Custom < 0 && batch.Count > 0 ? batch.Mesh.Box : null;
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
            instances[(int)slot] = instanceOf(in draws[i]);
            var (block, from, min, max) = _drawBlocks[b];
            if (block >= 0) Enclose(ref _blocks[block + (int)((slot - from) / BlockSize)], draws[i].World, min, max);
        }
        _copies.Clear();
        _copyBlocks.Clear();
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
