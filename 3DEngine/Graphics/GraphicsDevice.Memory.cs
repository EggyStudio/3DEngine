using Vortice.Vulkan;

namespace Engine;

/// <summary>A range of device memory a buffer or image is bound to, carved from a larger block.</summary>
/// <param name="Memory">The block's memory, which the resource is bound to at <paramref name="Offset"/>.</param>
/// <param name="Offset">Where the range starts in the block, in bytes.</param>
/// <param name="Size">How many bytes the range holds.</param>
/// <param name="Mapped">The range's first byte as the CPU sees it, or zero for memory the CPU cannot see.</param>
/// <param name="Block">The block the range belongs to, which takes it back.</param>
internal sealed record MemorySlice(VkDeviceMemory Memory, ulong Offset, ulong Size, nint Mapped, object Block);

/// <summary>
/// The device had no memory for an allocation, which says how many bytes were asked and how many
/// the device had free where its driver says.
/// </summary>
internal sealed class DeviceMemoryException(ulong asked, ulong? free)
    : InvalidOperationException($"The GPU had no memory for {asked / 1048576.0:0} MB, with {(free is { } f ? $"{f / 1048576.0:0} MB" : "an amount its driver does not say")} of it free.")
{
    /// <summary>How many bytes were asked.</summary>
    public ulong Asked { get; } = asked;

    /// <summary>How many bytes of the device's own memory were free, or null where its driver does not say.</summary>
    public ulong? Free { get; } = free;
}

internal sealed unsafe partial class GraphicsDevice
{
    // Whether the driver says how much of each memory heap the program may use and uses.
    private bool _knowsMemoryBudget;

    /// <summary>
    /// How many bytes of the device's own memory this program may still take, the budget the driver
    /// gives each of its heaps less what the program uses of it, summed over the heaps local to the
    /// device, or null where the driver does not say.
    /// </summary>
    /// <remarks>
    /// The budget counts what other programs hold, so an example started beside an editor holding
    /// 3.5 of a GPU's 8 GB reads what is left to it.
    /// </remarks>
    // Memory as the info asks, or where the device has none a DeviceMemoryException saying how much
    // was asked and how much is free, which a caller that can take less or go without catches.
    private VkDeviceMemory AllocateSaying(VkMemoryAllocateInfo* info)
    {
        var made = _deviceApi.vkAllocateMemory(info, null, out VkDeviceMemory memory);
        if (made is VkResult.ErrorOutOfDeviceMemory or VkResult.ErrorOutOfHostMemory)
            throw new DeviceMemoryException(info->allocationSize, FreeDeviceMemory());
        made.CheckResult();
        return memory;
    }

    internal ulong? FreeDeviceMemory()
    {
        if (!_knowsMemoryBudget) return null;
        var budget = new VkPhysicalDeviceMemoryBudgetPropertiesEXT();
        var properties = new VkPhysicalDeviceMemoryProperties2 { pNext = &budget };
        _instanceApi.vkGetPhysicalDeviceMemoryProperties2(_physicalDevice, &properties);
        ulong free = 0;
        for (int h = 0; h < (int)properties.memoryProperties.memoryHeapCount; h++)
            if ((properties.memoryProperties.memoryHeaps[h].flags & VkMemoryHeapFlags.DeviceLocal) != 0 && budget.heapBudget[h] > budget.heapUsage[h])
                free += budget.heapBudget[h] - budget.heapUsage[h];
        return free;
    }

    // Buffers and images are carved out of blocks of device memory rather than given an allocation
    // each, since drivers limit how many allocations a device holds (4,096 on many Windows
    // drivers), which a game of a few thousand meshes and textures would pass. Buffers and images
    // keep to blocks of their own, so the granularity Vulkan asks between linear and optimal
    // resources side by side in one allocation never applies. A request past half a block gets a
    // block of its own, freed with it.
    private const ulong MemoryBlockSize = 64UL << 20;

    private sealed class MemoryBlock
    {
        public required VkDeviceMemory Memory;
        public required ulong Size;
        public nint Mapped;
        public bool Dedicated;
        public int Slices;

        // The ranges not handed out, by offset, neighbors merged.
        public readonly List<(ulong Offset, ulong Size)> Free = [];
    }

    private readonly Lock _memoryGate = new();
    private readonly Dictionary<(uint Type, bool Image), List<MemoryBlock>> _memoryBlocks = [];

    /// <summary>How many device memory allocations the carved blocks hold, which the driver's limit counts.</summary>
    internal int MemoryBlockCount
    {
        get
        {
            lock (_memoryGate) return _memoryBlocks.Values.Sum(b => b.Count);
        }
    }

    // A range for a buffer or image of these requirements in memory of these properties, from a
    // block with room or a new one.
    private MemorySlice AllocateMemory(in VkMemoryRequirements requirements, VkMemoryPropertyFlags properties, bool image)
    {
        var type = FindMemoryType(requirements.memoryTypeBits, properties);
        var hostVisible = (properties & VkMemoryPropertyFlags.HostVisible) != 0;
        var alignment = Math.Max(1UL, requirements.alignment);
        lock (_memoryGate)
        {
            if (!_memoryBlocks.TryGetValue((type, image), out var blocks)) _memoryBlocks[(type, image)] = blocks = [];

            if (requirements.size > MemoryBlockSize / 2)
            {
                var dedicated = NewBlock(type, requirements.size, hostVisible);
                dedicated.Dedicated = true;
                dedicated.Free.Clear();
                dedicated.Slices = 1;
                blocks.Add(dedicated);
                return new MemorySlice(dedicated.Memory, 0, requirements.size, dedicated.Mapped, dedicated);
            }

            foreach (var block in blocks)
                if (!block.Dedicated && TryCarve(block, requirements.size, alignment) is { } slice)
                {
                    // Where the device's own memory is the memory the CPU sees, as on lavapipe, one type
                    // serves both and a block made for a buffer of the device's own, left unmapped,
                    // holds the next of either kind that fits it. One the CPU writes is mapped there,
                    // which left out gave it a null pointer, and a voxel game's scene field pool, carved
                    // there after the field was made again three times in two seconds, took its
                    // first write through it and ended the game.
                    if (!hostVisible || block.Mapped != 0) return slice;
                    MapBlock(block);
                    return slice with { Mapped = block.Mapped + (nint)slice.Offset };
                }

            var fresh = NewBlock(type, MemoryBlockSize, hostVisible);
            blocks.Add(fresh);
            return TryCarve(fresh, requirements.size, alignment)!;
        }
    }

    // Takes a range from the first free one it fits in, aligned, and gives back what is left
    // before and after it.
    private static MemorySlice? TryCarve(MemoryBlock block, ulong size, ulong alignment)
    {
        for (int i = 0; i < block.Free.Count; i++)
        {
            var (offset, length) = block.Free[i];
            var start = (offset + alignment - 1) / alignment * alignment;
            if (start + size > offset + length) continue;

            block.Free.RemoveAt(i);
            var after = offset + length - (start + size);
            if (after > 0) block.Free.Insert(i, (start + size, after));
            if (start > offset) block.Free.Insert(i, (offset, start - offset));
            block.Slices++;
            return new MemorySlice(block.Memory, start, size, block.Mapped == 0 ? 0 : block.Mapped + (nint)start, block);
        }
        return null;
    }

    private MemoryBlock NewBlock(uint type, ulong size, bool hostVisible)
    {
        VkMemoryAllocateInfo info = new() { allocationSize = size, memoryTypeIndex = type };
        var memory = AllocateSaying(&info);
        DeviceObjects.Made(DeviceObjects.Kind.Memory);
        var block = new MemoryBlock { Memory = memory, Size = size };
        block.Free.Add((0, size));
        // Mapped once for good, since a block holds many buffers and Vulkan maps memory once.
        if (hostVisible) MapBlock(block);
        return block;
    }

    // Maps a block's whole memory for good.
    private void MapBlock(MemoryBlock block)
    {
        void* data;
        _deviceApi.vkMapMemory(block.Memory, 0, Vulkan.VK_WHOLE_SIZE, 0, &data).CheckResult();
        block.Mapped = (nint)data;
    }

    // Gives a range back to its block, merged with the free ranges beside it, and frees a block
    // left empty unless it is the last of its kind, which stays for the next request.
    private void FreeMemory(MemorySlice slice)
    {
        lock (_memoryGate)
        {
            var block = (MemoryBlock)slice.Block;
            if (block.Memory.Handle == 0) return;
            block.Slices--;
            var free = block.Free;
            var at = 0;
            while (at < free.Count && free[at].Offset < slice.Offset) at++;
            free.Insert(at, (slice.Offset, slice.Size));
            if (at + 1 < free.Count && free[at].Offset + free[at].Size == free[at + 1].Offset)
            {
                free[at] = (free[at].Offset, free[at].Size + free[at + 1].Size);
                free.RemoveAt(at + 1);
            }
            if (at > 0 && free[at - 1].Offset + free[at - 1].Size == free[at].Offset)
            {
                free[at - 1] = (free[at - 1].Offset, free[at - 1].Size + free[at].Size);
                free.RemoveAt(at);
            }

            if (block.Slices > 0) return;
            var blocks = _memoryBlocks.Values.First(b => b.Contains(block));
            if (!block.Dedicated && blocks.Count(b => !b.Dedicated) == 1) return;
            blocks.Remove(block);
            DeviceObjects.Gone(DeviceObjects.Kind.Memory);
            _deviceApi.vkFreeMemory(block.Memory);
            block.Memory = default;
        }
    }

    // Frees every block, with the device idle, before the device goes.
    private void DestroyMemoryBlocks()
    {
        lock (_memoryGate)
        {
            foreach (var block in _memoryBlocks.Values.SelectMany(b => b))
            {
                DeviceObjects.Gone(DeviceObjects.Kind.Memory);
                _deviceApi.vkFreeMemory(block.Memory);
                block.Memory = default;
            }
            _memoryBlocks.Clear();
        }
    }
}
