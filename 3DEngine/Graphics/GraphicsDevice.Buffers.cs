using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Vulkan implementation of <see cref="IBuffer"/> wrapping a <c>VkBuffer</c> and its device memory.</summary>
    /// <seealso cref="IBuffer"/>
    private sealed class VulkanBuffer : IBuffer
    {
        private readonly GraphicsDevice _device;

        /// <summary>The underlying Vulkan buffer handle.</summary>
        internal VkBuffer Buffer;

        /// <summary>The device memory backing this buffer.</summary>
        internal VkDeviceMemory Memory;

        /// <inheritdoc />
        public BufferDesc Description { get; }

        /// <summary>Whether this buffer is host-visible and can be mapped for CPU access.</summary>
        internal bool IsHostVisible;

        /// <summary>Pointer to the mapped memory region, or <see cref="nint.Zero"/> if unmapped.</summary>
        internal nint MappedPtr;

        /// <summary>Creates a new Vulkan buffer wrapper.</summary>
        /// <param name="device">The owning graphics device.</param>
        /// <param name="buffer">The Vulkan buffer handle.</param>
        /// <param name="memory">The backing device memory.</param>
        /// <param name="desc">The buffer creation descriptor.</param>
        /// <param name="hostVisible">Whether the buffer is host-visible.</param>
        public VulkanBuffer(GraphicsDevice device, VkBuffer buffer, VkDeviceMemory memory, BufferDesc desc, bool hostVisible)
        {
            _device = device;
            Buffer = buffer;
            Memory = memory;
            Description = desc;
            IsHostVisible = hostVisible;
            MappedPtr = nint.Zero;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Buffer.Handle != 0)
            {
                _device._deviceApi.vkDestroyBuffer(Buffer);
                Buffer = default;
            }

            if (Memory.Handle != 0)
            {
                _device._deviceApi.vkFreeMemory(Memory);
                Memory = default;
            }
        }
    }

    IBuffer IGraphicsDevice.CreateBuffer(BufferDesc desc) => CreateBuffer(desc);
    Span<byte> IGraphicsDevice.Map(IBuffer buffer) => Map(buffer);
    void IGraphicsDevice.Unmap(IBuffer buffer) => Unmap(buffer);

    /// <summary>Creates a GPU buffer backed by Vulkan device memory with the specified descriptor.</summary>
    /// <param name="desc">Buffer creation descriptor (size, usage, CPU access).</param>
    /// <returns>A new <see cref="IBuffer"/> handle.</returns>
    /// <exception cref="InvalidOperationException">The device has not been initialized.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="desc"/> specifies a zero-byte size.</exception>
    public IBuffer CreateBuffer(BufferDesc desc)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Graphics device not initialized");
        if (desc.Size == 0)
            throw new ArgumentOutOfRangeException(nameof(desc), "Buffer size must be greater than zero.");

        VkBufferUsageFlags usage = 0;
        if (desc.Usage.HasFlag(BufferUsage.Vertex)) usage |= VkBufferUsageFlags.VertexBuffer;
        if (desc.Usage.HasFlag(BufferUsage.Index)) usage |= VkBufferUsageFlags.IndexBuffer;
        if (desc.Usage.HasFlag(BufferUsage.Uniform)) usage |= VkBufferUsageFlags.UniformBuffer;
        if (desc.Usage.HasFlag(BufferUsage.TransferSrc)) usage |= VkBufferUsageFlags.TransferSrc;
        if (desc.Usage.HasFlag(BufferUsage.TransferDst)) usage |= VkBufferUsageFlags.TransferDst;

        VkBufferCreateInfo bufferInfo = new()
        {
            size = desc.Size,
            usage = usage,
            sharingMode = VkSharingMode.Exclusive
        };

        _deviceApi.vkCreateBuffer(&bufferInfo, null, out VkBuffer buffer).CheckResult();

        _deviceApi.vkGetBufferMemoryRequirements(buffer, out VkMemoryRequirements requirements);

        var properties = desc.CpuAccess == CpuAccessMode.None
            ? VkMemoryPropertyFlags.DeviceLocal
            : VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent;

        uint memoryTypeIndex = FindMemoryType(requirements.memoryTypeBits, properties);

        VkMemoryAllocateInfo allocInfo = new()
        {
            allocationSize = requirements.size,
            memoryTypeIndex = memoryTypeIndex
        };

        _deviceApi.vkAllocateMemory(&allocInfo, null, out VkDeviceMemory memory).CheckResult();
        _deviceApi.vkBindBufferMemory(buffer, memory, 0).CheckResult();

        return new VulkanBuffer(this, buffer, memory, desc, desc.CpuAccess != CpuAccessMode.None);
    }

    /// <summary>Maps a host-visible buffer's memory for CPU access and returns a writable byte span.</summary>
    /// <param name="buffer">The buffer to map (must have been created with <see cref="CpuAccessMode"/> other than <see cref="CpuAccessMode.None"/>).</param>
    /// <returns>A <see cref="Span{T}"/> of bytes over the mapped memory region.</returns>
    public Span<byte> Map(IBuffer buffer)
    {
        if (buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("Buffer was not created by this device.", nameof(buffer));

        if (!vkBuffer.IsHostVisible)
            throw new InvalidOperationException("Buffer is not host-visible and cannot be mapped.");

        if (vkBuffer.MappedPtr != nint.Zero)
        {
            // Already mapped; just return the same span.
            return new Span<byte>((void*)vkBuffer.MappedPtr, checked((int)vkBuffer.Description.Size));
        }

        void* data;
        _deviceApi.vkMapMemory(vkBuffer.Memory, 0, vkBuffer.Description.Size, 0, &data).CheckResult();
        vkBuffer.MappedPtr = (nint)data;
        return new Span<byte>(data, checked((int)vkBuffer.Description.Size));
    }

    /// <summary>Unmaps a previously mapped buffer, invalidating any <see cref="Span{T}"/> returned by <see cref="Map"/>.</summary>
    /// <param name="buffer">The buffer to unmap.</param>
    public void Unmap(IBuffer buffer)
    {
        if (buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("Buffer was not created by this device.", nameof(buffer));

        if (!vkBuffer.IsHostVisible || vkBuffer.MappedPtr == nint.Zero)
            return;

        _deviceApi.vkUnmapMemory(vkBuffer.Memory);
        vkBuffer.MappedPtr = nint.Zero;
    }

    /// <summary>Copies raw byte data into a GPU buffer, using staging if the buffer is device-local.</summary>
    /// <param name="destination">The target buffer.</param>
    /// <param name="data">Raw byte data to upload.</param>
    /// <param name="destinationOffset">Byte offset into the destination buffer.</param>
    public void UploadBuffer(IBuffer destination, ReadOnlySpan<byte> data, ulong destinationOffset = 0)
    {
        if (destination is not VulkanBuffer dst)
            throw new ArgumentException("Buffer was not created by this device.", nameof(destination));
        if (data.Length == 0) return;

        // If the destination is host-visible, just map and copy directly.
        if (dst.IsHostVisible)
        {
            var span = Map(dst);
            data.CopyTo(span.Slice((int)destinationOffset));
            Unmap(dst);
            return;
        }

        // Device-local: create a transient staging buffer and copy via one-time command buffer.
        var stagingDesc = new BufferDesc((ulong)data.Length, BufferUsage.TransferSrc, CpuAccessMode.Write);
        var staging = (VulkanBuffer)CreateBuffer(stagingDesc);
        try
        {
            var stagingSpan = Map(staging);
            data.CopyTo(stagingSpan);
            Unmap(staging);

            VkBufferCopy copyRegion = new()
            {
                srcOffset = 0,
                dstOffset = destinationOffset,
                size = (ulong)data.Length
            };

            var cmd = BeginSingleTimeCommands();
            _deviceApi.vkCmdCopyBuffer(cmd, staging.Buffer, dst.Buffer, 1, &copyRegion);
            EndSingleTimeCommands(cmd);
        }
        finally
        {
            staging.Dispose();
        }
    }


    /// <summary>Uploads a span of unmanaged structs to a GPU buffer.</summary>
    /// <typeparam name="T">Unmanaged element type.</typeparam>
    /// <param name="destination">The target buffer.</param>
    /// <param name="data">Data to upload.</param>
    /// <param name="destinationOffsetBytes">Byte offset into the destination buffer.</param>
    public void Upload<T>(IBuffer destination, ReadOnlySpan<T> data, ulong destinationOffsetBytes = 0)
        where T : unmanaged
    {
        if (data.IsEmpty) return;

        ulong elementSize = (ulong)Unsafe.SizeOf<T>();
        ulong totalSize = elementSize * (ulong)data.Length;

        // reinterpret the span as bytes
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(data);
        if ((ulong)bytes.Length < totalSize)
            throw new ArgumentException("Span length is smaller than expected total size.", nameof(data));

        UploadBuffer(destination, bytes, destinationOffsetBytes);
    }

    /// <summary>Uploads a single unmanaged struct to a GPU buffer.</summary>
    /// <typeparam name="T">Unmanaged element type.</typeparam>
    /// <param name="destination">The target buffer.</param>
    /// <param name="value">The value to upload.</param>
    /// <param name="destinationOffsetBytes">Byte offset into the destination buffer.</param>
    public void Upload<T>(IBuffer destination, T value, ulong destinationOffsetBytes = 0)
        where T : unmanaged
    {
        Span<T> temp = stackalloc T[1];
        temp[0] = value;
        Upload(destination, temp, destinationOffsetBytes);
    }

    /// <summary>Uploads an array of unmanaged structs to a GPU buffer.</summary>
    /// <typeparam name="T">Unmanaged element type.</typeparam>
    /// <param name="destination">The target buffer.</param>
    /// <param name="data">Data to upload.</param>
    /// <param name="destinationOffsetBytes">Byte offset into the destination buffer.</param>
    public void Upload<T>(IBuffer destination, T[] data, ulong destinationOffsetBytes = 0)
        where T : unmanaged
    {
        if (data.Length == 0) return;
        Upload(destination, data.AsSpan(), destinationOffsetBytes);
    }


    /// <summary>Issues a pipeline barrier on a buffer for synchronizing GPU access.</summary>
    internal void BufferMemoryBarrier(IBuffer buffer,
        VkAccessFlags srcAccess,
        VkAccessFlags dstAccess,
        VkPipelineStageFlags srcStage,
        VkPipelineStageFlags dstStage)
    {
        if (buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("Buffer was not created by this device.", nameof(buffer));

        var cmd = BeginSingleTimeCommands();

        VkBufferMemoryBarrier barrier = new()
        {
            srcAccessMask = srcAccess,
            dstAccessMask = dstAccess,
            srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            buffer = vkBuffer.Buffer,
            offset = 0,
            size = vkBuffer.Description.Size
        };

        _deviceApi.vkCmdPipelineBarrier(cmd, srcStage, dstStage, 0, 0, null, 1, &barrier, 0, null);

        EndSingleTimeCommands(cmd);
    }

    /// <summary>Allocates and begins a single-use command buffer for one-shot GPU operations.</summary>
    private VkCommandBuffer BeginSingleTimeCommands()
    {
        VkCommandBufferAllocateInfo allocInfo = new()
        {
            commandPool = _commandPool,
            level = VkCommandBufferLevel.Primary,
            commandBufferCount = 1
        };

        VkCommandBuffer cmd;
        _deviceApi.vkAllocateCommandBuffers(&allocInfo, &cmd).CheckResult();

        VkCommandBufferBeginInfo beginInfo = new()
        {
            flags = VkCommandBufferUsageFlags.OneTimeSubmit
        };

        _deviceApi.vkBeginCommandBuffer(cmd, &beginInfo).CheckResult();
        return cmd;
    }

    /// <summary>Ends, submits, and waits for a single-use command buffer, then frees it.</summary>
    private void EndSingleTimeCommands(VkCommandBuffer cmd)
    {
        _deviceApi.vkEndCommandBuffer(cmd).CheckResult();

        VkSubmitInfo submitInfo = new()
        {
            commandBufferCount = 1,
            pCommandBuffers = &cmd
        };

        VkFence fence;
        VkFenceCreateInfo fenceInfo = new();
        _deviceApi.vkCreateFence(&fenceInfo, null, out fence).CheckResult();

        _deviceApi.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, fence).CheckResult();
        _deviceApi.vkWaitForFences(1, &fence, true, ulong.MaxValue).CheckResult();

        _deviceApi.vkDestroyFence(fence);
        _deviceApi.vkFreeCommandBuffers(_commandPool, 1, &cmd);
    }

    /// <summary>Finds a Vulkan memory type index matching the required type filter and property flags.</summary>
    /// <exception cref="InvalidOperationException">Thrown when no suitable memory type exists.</exception>
    private uint FindMemoryType(uint typeFilter, VkMemoryPropertyFlags properties)
    {
        VkPhysicalDeviceMemoryProperties memProperties;
        _instanceApi.vkGetPhysicalDeviceMemoryProperties(_physicalDevice, out memProperties);

        for (int i = 0; i < memProperties.memoryTypeCount; i++)
        {
            if ((typeFilter & (1u << i)) != 0 &&
                (memProperties.memoryTypes[i].propertyFlags & properties) == properties)
            {
                return (uint)i;
            }
        }

        throw new InvalidOperationException("Failed to find suitable memory type.");
    }
}
