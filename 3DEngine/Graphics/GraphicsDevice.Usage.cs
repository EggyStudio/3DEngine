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

internal sealed partial class GraphicsDevice
{
    private int _liveBuffers, _liveImages, _liveDescriptorSets, _livePipelines;

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
