namespace Engine;

/// <summary>
/// What a device holds at a moment, which a program left running reads at intervals to see that
/// none of it climbs.
/// </summary>
/// <param name="Buffers">Buffers made and not yet destroyed, the rings' and the retired lists' among them.</param>
/// <param name="Images">Images made and not yet destroyed, textures, render targets and cube maps.</param>
/// <param name="DescriptorSets">Descriptor sets from the device's pools not yet given back.</param>
/// <param name="Pipelines">Graphics pipelines not yet destroyed.</param>
/// <param name="MemoryBlocks">Device memory allocations the carved blocks hold.</param>
/// <param name="BlockBytes">The bytes of those blocks.</param>
/// <param name="UsedBytes">The bytes of those blocks handed out to buffers and images.</param>
internal readonly record struct DeviceUsage(int Buffers, int Images, int DescriptorSets, int Pipelines,
    int MemoryBlocks, long BlockBytes, long UsedBytes);

/// <summary>Where the engine made a buffer and what it is for, a line of the device's census.</summary>
/// <param name="File">The file that asked for it, as the compiler gave its path.</param>
/// <param name="Line">The line of <paramref name="File"/> that asked for it.</param>
/// <param name="Usage">What it is for.</param>
internal readonly record struct BufferMaker(string File, int Line, BufferUsage Usage);

/// <summary>How many buffers alive one place made for one use, and their bytes.</summary>
/// <param name="Maker">Where they were made and what they are for.</param>
/// <param name="Count">How many are alive.</param>
/// <param name="Bytes">Their bytes together.</param>
internal readonly record struct BufferCount(BufferMaker Maker, int Count, long Bytes);

internal sealed partial class GraphicsDevice
{
    private int _liveBuffers, _liveImages, _liveDescriptorSets, _livePipelines;

    // The buffers alive by where each was made and what it is for, which a soak reads beside the
    // count to name what climbs (`memory.buffers`), as Manor's buffers did past the soak's bound
    // with nothing else of it growing.
    private readonly Dictionary<BufferMaker, (int Count, long Bytes)> _bufferCensus = [];

    private void Census(BufferMaker made, int count, long bytes)
    {
        lock (_bufferCensus)
        {
            var (held, size) = _bufferCensus.GetValueOrDefault(made);
            if (held + count == 0)
                _bufferCensus.Remove(made);
            else
                _bufferCensus[made] = (held + count, size + bytes);
        }
    }

    /// <summary>The buffers alive by where each was made and what it is for, the most first.</summary>
    public IReadOnlyList<BufferCount> BufferCensus()
    {
        lock (_bufferCensus)
            return [.. _bufferCensus.Select(entry => new BufferCount(entry.Key, entry.Value.Count, entry.Value.Bytes))
                .OrderByDescending(entry => entry.Count).ThenBy(entry => entry.Maker.File).ThenBy(entry => entry.Maker.Line)];
    }

    /// <summary>What the device holds now.</summary>
    public DeviceUsage Usage
    {
        get
        {
            int blocks = 0;
            long blockBytes = 0, used = 0;
            lock (_memoryGate)
                foreach (var block in _memoryBlocks.Values.SelectMany(b => b))
                {
                    blocks++;
                    blockBytes += (long)block.Size;
                    used += (long)block.Size - block.Free.Sum(f => (long)f.Size);
                }
            return new DeviceUsage(Volatile.Read(ref _liveBuffers), Volatile.Read(ref _liveImages),
                Volatile.Read(ref _liveDescriptorSets), Volatile.Read(ref _livePipelines), blocks, blockBytes, used);
        }
    }
}
