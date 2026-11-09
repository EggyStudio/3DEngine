using Vortice.Vulkan;

namespace Engine;

/// <summary>A buffer in memory of its own with the address the GPU reads it by, as an acceleration structure, what it is built from and its scratch space need.</summary>
internal sealed unsafe class AddressedBuffer : IDisposable
{
    private readonly GraphicsDevice _device;

    internal AddressedBuffer(GraphicsDevice device, VkBuffer buffer, VkDeviceMemory memory, ulong address, ulong size, void* mapped)
    {
        _device = device;
        Buffer = buffer;
        Memory = memory;
        Address = address;
        Size = size;
        Mapped = mapped;
    }

    internal VkBuffer Buffer { get; }
    internal VkDeviceMemory Memory { get; }

    /// <summary>Where the GPU reads it.</summary>
    internal ulong Address { get; }

    /// <summary>Its size in bytes.</summary>
    internal ulong Size { get; }

    /// <summary>Its bytes as the CPU writes them, or null for one only the GPU reaches.</summary>
    internal void* Mapped { get; }

    /// <inheritdoc />
    public void Dispose() => _device.DestroyAddressedBuffer(this);
}

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>
    /// Makes a buffer of <paramref name="size"/> bytes in memory of its own with its address, the
    /// CPU's to write where <paramref name="host"/>, on the device where <paramref name="lasting"/>
    /// says the CPU writes it once and the GPU reads it many times, and the CPU sees the device's
    /// own memory whole.
    /// </summary>
    internal AddressedBuffer CreateAddressedBuffer(ulong size, VkBufferUsageFlags usage, bool host, bool lasting = false)
    {
        VkBufferCreateInfo info = new()
        {
            size = Math.Max(16, size),
            usage = usage | VkBufferUsageFlags.ShaderDeviceAddress,
            sharingMode = VkSharingMode.Exclusive,
        };
        _deviceApi.vkCreateBuffer(&info, null, out VkBuffer buffer).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.Buffer);
        _deviceApi.vkGetBufferMemoryRequirements(buffer, out VkMemoryRequirements requirements);
        var flags = new VkMemoryAllocateFlagsInfo { flags = VkMemoryAllocateFlags.DeviceAddress };
        VkMemoryAllocateInfo allocation = new()
        {
            pNext = &flags,
            allocationSize = requirements.size,
            memoryTypeIndex = FindMemoryType(requirements.memoryTypeBits, host
                ? VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent | (lasting && _seesDeviceWhole ? VkMemoryPropertyFlags.DeviceLocal : 0)
                : VkMemoryPropertyFlags.DeviceLocal),
        };
        VkDeviceMemory memory;
        try
        {
            memory = AllocateSaying(&allocation);
        }
        catch (DeviceMemoryException)
        {
            _deviceApi.vkDestroyBuffer(buffer, null);
            DeviceObjects.Gone(DeviceObjects.Kind.Buffer);
            throw;
        }
        DeviceObjects.Made(DeviceObjects.Kind.Memory);
        _deviceApi.vkBindBufferMemory(buffer, memory, 0).CheckResult();
        void* mapped = null;
        if (host) _deviceApi.vkMapMemory(memory, 0, Vulkan.VK_WHOLE_SIZE, 0, &mapped).CheckResult();
        var addressInfo = new VkBufferDeviceAddressInfo { buffer = buffer };
        return new AddressedBuffer(this, buffer, memory, _deviceApi.vkGetBufferDeviceAddress(&addressInfo), info.size, mapped);
    }

    internal void DestroyAddressedBuffer(AddressedBuffer buffer)
    {
        if (buffer.Mapped != null) _deviceApi.vkUnmapMemory(buffer.Memory);
        _deviceApi.vkDestroyBuffer(buffer.Buffer);
        _deviceApi.vkFreeMemory(buffer.Memory);
        DeviceObjects.Gone(DeviceObjects.Kind.Buffer);
        DeviceObjects.Gone(DeviceObjects.Kind.Memory);
    }
}
