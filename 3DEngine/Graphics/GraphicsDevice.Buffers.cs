using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>Vulkan implementation of <see cref="IBuffer"/> wrapping a <c>VkBuffer</c> and the range of device memory it is bound to.</summary>
    /// <seealso cref="IBuffer"/>
    private sealed class VulkanBuffer : IBuffer
    {
        private readonly GraphicsDevice _device;

        /// <summary>The underlying Vulkan buffer handle.</summary>
        internal VkBuffer Buffer;

        /// <summary>The range of device memory backing this buffer.</summary>
        internal MemorySlice? Memory;

        /// <inheritdoc />
        public BufferDesc Description { get; }

        /// <summary>Whether this buffer is host-visible and can be mapped for CPU access.</summary>
        internal bool IsHostVisible;

        /// <summary>The buffer's first byte as the CPU sees it, mapped for good with its block, or <see cref="nint.Zero"/> for a buffer the CPU cannot see.</summary>
        internal nint MappedPtr;

        /// <summary>Where it was made and what it is for, its line in the device's census.</summary>
        private readonly BufferMaker _made;

        /// <summary>Creates a new Vulkan buffer wrapper.</summary>
        /// <param name="device">The owning graphics device.</param>
        /// <param name="buffer">The Vulkan buffer handle.</param>
        /// <param name="memory">The range of device memory it is bound to.</param>
        /// <param name="desc">The buffer creation descriptor.</param>
        /// <param name="hostVisible">Whether the buffer is host-visible.</param>
        /// <param name="made">Where it was made and what it is for.</param>
        public VulkanBuffer(GraphicsDevice device, VkBuffer buffer, MemorySlice memory, BufferDesc desc, bool hostVisible, BufferMaker made)
        {
            _device = device;
            Buffer = buffer;
            Memory = memory;
            Description = desc;
            IsHostVisible = hostVisible;
            MappedPtr = hostVisible ? memory.Mapped : nint.Zero;
            _made = made;
            Interlocked.Increment(ref device._liveBuffers);
            device.Census(made, 1, (long)desc.Size);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Buffer.Handle != 0)
            {
                DeviceObjects.Gone(DeviceObjects.Kind.Buffer);
                _device._deviceApi.vkDestroyBuffer(Buffer);
                Buffer = default;
                Interlocked.Decrement(ref _device._liveBuffers);
                _device.Census(_made, -1, -(long)Description.Size);
            }

            if (Memory is { } memory)
            {
                _device.FreeMemory(memory);
                Memory = null;
            }
        }
    }

    IBuffer IGraphicsDevice.CreateBuffer(BufferDesc desc, string maker, int line) => CreateBuffer(desc, maker, line);
    Span<byte> IGraphicsDevice.Map(IBuffer buffer) => Map(buffer);
    void IGraphicsDevice.Unmap(IBuffer buffer) => Unmap(buffer);

    /// <summary>Creates a GPU buffer backed by Vulkan device memory with the specified descriptor.</summary>
    /// <param name="desc">Buffer creation descriptor (size, usage, CPU access).</param>
    /// <param name="maker">The file that asks for it, which the compiler fills in, for the census.</param>
    /// <param name="line">The line of <paramref name="maker"/> that asks for it.</param>
    /// <returns>A new <see cref="IBuffer"/> handle.</returns>
    /// <exception cref="InvalidOperationException">The device has not been initialized.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="desc"/> specifies a zero-byte size.</exception>
    public IBuffer CreateBuffer(BufferDesc desc, [CallerFilePath] string maker = "", [CallerLineNumber] int line = 0)
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
        if (desc.Usage.HasFlag(BufferUsage.Storage)) usage |= VkBufferUsageFlags.StorageBuffer;
        if (desc.Usage.HasFlag(BufferUsage.Indirect)) usage |= VkBufferUsageFlags.IndirectBuffer;

        VkBufferCreateInfo bufferInfo = new()
        {
            size = desc.Size,
            usage = usage,
            sharingMode = VkSharingMode.Exclusive
        };

        _deviceApi.vkCreateBuffer(&bufferInfo, null, out VkBuffer buffer).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.Buffer);

        _deviceApi.vkGetBufferMemoryRequirements(buffer, out VkMemoryRequirements requirements);

        // A buffer only the CPU reads takes memory the CPU caches where the device has it, since the
        // first memory the CPU sees is often write combined, which the CPU reads a byte at a time:
        // on an RTX 4070 a texture's 4 MB of pixels took 34 ms to read from it.
        const VkMemoryPropertyFlags shared = VkMemoryPropertyFlags.HostVisible | VkMemoryPropertyFlags.HostCoherent;
        var properties = desc.CpuAccess switch
        {
            CpuAccessMode.None => VkMemoryPropertyFlags.DeviceLocal,
            CpuAccessMode.Read when HasMemoryType(requirements.memoryTypeBits, shared | VkMemoryPropertyFlags.HostCached) => shared | VkMemoryPropertyFlags.HostCached,
            _ => shared,
        };

        var memory = AllocateMemory(requirements, properties, image: false);
        _deviceApi.vkBindBufferMemory(buffer, memory.Memory, memory.Offset).CheckResult();

        return new VulkanBuffer(this, buffer, memory, desc, desc.CpuAccess != CpuAccessMode.None, new BufferMaker(maker, line, desc.Usage));
    }

    /// <summary>
    /// Creates a buffer in the device's own memory holding <paramref name="data"/>, copied in from a
    /// staging buffer by the uploads' next submit, for what the CPU writes once and the GPU reads
    /// every frame, as a mesh's vertices and indices.
    /// </summary>
    /// <remarks>
    /// A buffer the CPU writes lies in memory the CPU sees, which on a GPU of its own is the
    /// computer's, so every pass that reads it reads it across the bus. Intel's Sponza, 3.75 million
    /// triangles over 2 million vertices, took each pass that drew all of it about 10 ms of the GPU
    /// on an RTX 4070 Laptop GPU, whatever the shadow map's size, its shadows 35 ms in five such
    /// passes.
    /// </remarks>
    /// <param name="data">What the buffer holds.</param>
    /// <param name="usage">How the GPU reads it, to which the copy's own usage is added.</param>
    /// <param name="maker">The file that asks for it, which the compiler fills in, for the census.</param>
    /// <param name="line">The line of <paramref name="maker"/> that asks for it.</param>
    internal IBuffer CreateFilledBuffer(ReadOnlySpan<byte> data, BufferUsage usage, [CallerFilePath] string maker = "", [CallerLineNumber] int line = 0)
    {
        var buffer = CreateBuffer(new BufferDesc((ulong)Math.Max(16, data.Length), usage | BufferUsage.TransferDst, CpuAccessMode.None), maker, line);
        FillBuffer(buffer, 0, data, maker, line);
        return buffer;
    }

    /// <summary>
    /// Writes <paramref name="data"/> into a buffer in the device's own memory at
    /// <paramref name="offset"/>, made with <see cref="BufferUsage.TransferDst"/>, through a staging
    /// buffer copied from by the uploads' next submit, which every later submit reads after.
    /// </summary>
    /// <param name="destination">The buffer written into.</param>
    /// <param name="offset">Where in it, in bytes.</param>
    /// <param name="data">What is written.</param>
    /// <param name="maker">The file that asks for it, which the compiler fills in, for the census.</param>
    /// <param name="line">The line of <paramref name="maker"/> that asks for it.</param>
    internal void FillBuffer(IBuffer destination, ulong offset, ReadOnlySpan<byte> data, [CallerFilePath] string maker = "", [CallerLineNumber] int line = 0)
    {
        if (data.IsEmpty) return;
        var staging = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)Math.Max(16, data.Length), BufferUsage.TransferSrc, CpuAccessMode.Write), maker, line);
        data.CopyTo(Map(staging));
        Unmap(staging);
        var cmd = UploadCommands();
        var region = new VkBufferCopy { dstOffset = offset, size = (ulong)data.Length };
        _deviceApi.vkCmdCopyBuffer(cmd, staging.Buffer, ((VulkanBuffer)destination).Buffer, 1, &region);
        // Every submit after the uploads' reads what they wrote, the draws' vertices and indices
        // and a shader's reads of a buffer alike.
        MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite,
            VkPipelineStageFlags2.VertexAttributeInput | VkPipelineStageFlags2.IndexInput | VkPipelineStageFlags2.AllCommands,
            VkAccessFlags2.VertexAttributeRead | VkAccessFlags2.IndexRead | VkAccessFlags2.MemoryRead);
        _batchStaging.Add(staging);
    }

    // The device's memory types, each with what it is and the heap it lies in, written to the log
    // once the device is chosen, so a buffer the GPU reads every frame from the computer's memory
    // is seen for where it lies (CreateFilledBuffer).
    private void LogMemoryTypes()
    {
        _instanceApi.vkGetPhysicalDeviceMemoryProperties(_physicalDevice, out var memory);
        var types = new List<string>();
        for (int i = 0; i < memory.memoryTypeCount; i++)
        {
            var type = memory.memoryTypes[i];
            var heap = memory.memoryHeaps[(int)type.heapIndex];
            var flags = type.propertyFlags;
            var kinds = new[] { (VkMemoryPropertyFlags.DeviceLocal, "device local"), (VkMemoryPropertyFlags.HostVisible, "host visible"),
                    (VkMemoryPropertyFlags.HostCoherent, "coherent"), (VkMemoryPropertyFlags.HostCached, "cached") }
                .Where(kind => (flags & kind.Item1) != 0).Select(kind => kind.Item2);
            var where = (heap.flags & VkMemoryHeapFlags.DeviceLocal) != 0 ? "on the device" : "in the computer's memory";
            types.Add($"{i} {(flags == 0 ? "plain" : string.Join(" ", kinds))}, heap {type.heapIndex} of {heap.size / (1024.0 * 1024 * 1024):0.0} GB {where}");
        }
        Logger.Info($"Memory types: {string.Join("; ", types)}");
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

        // Its block is mapped for good, so the buffer's own range of it is returned.
        return new Span<byte>((void*)vkBuffer.MappedPtr, checked((int)vkBuffer.Description.Size));
    }

    /// <summary>Ends a write through <see cref="Map"/>, which on this device changes nothing, since the memory stays mapped and is coherent.</summary>
    /// <param name="buffer">The buffer to unmap.</param>
    public void Unmap(IBuffer buffer)
    {
        if (buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("Buffer was not created by this device.", nameof(buffer));

        // A block stays mapped while any buffer in it lives, and its memory is coherent, so there
        // is nothing to undo or flush.
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

        // A host-visible destination is mapped and copied into directly.
        if (dst.IsHostVisible)
        {
            var span = Map(dst);
            data.CopyTo(span.Slice((int)destinationOffset));
            Unmap(dst);
            return;
        }

        // A device-local buffer is filled from a transient staging buffer by a one-time command buffer.
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

        FlushUploads();
        _deviceApi.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, fence).CheckResult();
        _deviceApi.vkWaitForFences(1, &fence, true, ulong.MaxValue).CheckResult();

        _deviceApi.vkDestroyFence(fence);
        _deviceApi.vkFreeCommandBuffers(_commandPool, 1, &cmd);
    }

    // Whether the device has memory of a type the filter allows with these properties.
    private bool HasMemoryType(uint typeFilter, VkMemoryPropertyFlags properties)
    {
        _instanceApi.vkGetPhysicalDeviceMemoryProperties(_physicalDevice, out var memProperties);
        for (int i = 0; i < memProperties.memoryTypeCount; i++)
            if ((typeFilter & (1u << i)) != 0 && (memProperties.memoryTypes[i].propertyFlags & properties) == properties)
                return true;
        return false;
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
