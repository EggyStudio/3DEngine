using Vortice.Vulkan;

namespace Engine;

/// <summary>A range of device memory a buffer or image is bound to, carved from a larger block.</summary>
/// <param name="Memory">The block's memory, which the resource is bound to at <paramref name="Offset"/>.</param>
/// <param name="Offset">Where the range starts in the block, in bytes.</param>
/// <param name="Size">How many bytes the range holds.</param>
/// <param name="Mapped">The range's first byte as the CPU sees it, or zero for memory the CPU cannot see.</param>
/// <param name="Block">The block the range belongs to, which takes it back.</param>
internal sealed record MemorySlice(VkDeviceMemory Memory, ulong Offset, ulong Size, nint Mapped, object Block);

public sealed unsafe partial class GraphicsDevice
{
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
                    return slice;

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
        _deviceApi.vkAllocateMemory(&info, null, out VkDeviceMemory memory).CheckResult();
        var block = new MemoryBlock { Memory = memory, Size = size };
        block.Free.Add((0, size));
        // Mapped once for good, since a block holds many buffers and Vulkan maps memory once.
        if (hostVisible)
        {
            void* data;
            _deviceApi.vkMapMemory(memory, 0, Vulkan.VK_WHOLE_SIZE, 0, &data).CheckResult();
            block.Mapped = (nint)data;
        }
        return block;
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
                _deviceApi.vkFreeMemory(block.Memory);
                block.Memory = default;
            }
            _memoryBlocks.Clear();
        }
    }
}
