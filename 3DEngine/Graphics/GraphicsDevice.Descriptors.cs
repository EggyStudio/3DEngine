using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>
    /// The pools descriptor sets are allocated from, the newest last. One that runs out is kept for
    /// the sets it holds, and another as large is made for the next, so a frame with more draws
    /// of their own sets than one pool holds still finds room.
    /// </summary>
    private readonly List<VkDescriptorPool> _descriptorPools = [];

    // The pool new sets come from, or none before the first is made.
    private VkDescriptorPool _descriptorPool => _descriptorPools.Count > 0 ? _descriptorPools[^1] : default;

    /// <summary>Default descriptor set layout (binding 0 = UBO vertex, binding 1 = combined image sampler fragment).</summary>
    private VkDescriptorSetLayout _cameraSetLayout;

    /// <summary>Wraps a Vulkan descriptor set allocated from the device's global pool.</summary>
    /// <seealso cref="IDescriptorSet"/>
    private sealed class VulkanDescriptorSet : IDescriptorSet
    {
        private readonly GraphicsDevice _device;
        private readonly VkDescriptorPool _pool;

        /// <summary>The underlying Vulkan descriptor set handle.</summary>
        internal VkDescriptorSet Handle;

        /// <summary>Creates a new Vulkan descriptor set wrapper.</summary>
        /// <param name="device">The owning graphics device.</param>
        /// <param name="pool">The pool it was allocated from, which it goes back to.</param>
        /// <param name="handle">The allocated Vulkan descriptor set handle.</param>
        public VulkanDescriptorSet(GraphicsDevice device, VkDescriptorPool pool, VkDescriptorSet handle)
        {
            _device = device;
            _pool = pool;
            Handle = handle;
            Interlocked.Increment(ref device._liveDescriptorSets);
            DeviceObjects.Made(DeviceObjects.Kind.DescriptorSet);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            // A set outliving the device's pools went with them.
            if (Handle.Handle != 0 && _device._descriptorPools.Contains(_pool))
            {
                var set = Handle;
                _device._deviceApi.vkFreeDescriptorSets(_pool, 1, &set);
                DeviceObjects.Gone(DeviceObjects.Kind.DescriptorSet);
            }
            if (Handle.Handle != 0) Interlocked.Decrement(ref _device._liveDescriptorSets);
            Handle = default;
        }
    }

    /// <summary>Creates the default descriptor set layout and descriptor pool used for camera UBOs and texture samplers.</summary>
    private void CreateDescriptorResources()
    {
        Logger.Debug("Creating descriptor set layout (binding 0=UBO vertex, binding 1=CombinedImageSampler fragment)...");
        // Simple layout: binding 0 = uniform buffer, binding 1 = combined image sampler.
        VkDescriptorSetLayoutBinding* bindings = stackalloc VkDescriptorSetLayoutBinding[2];
        bindings[0] = new VkDescriptorSetLayoutBinding
        {
            binding = 0,
            descriptorType = VkDescriptorType.UniformBuffer,
            descriptorCount = 1,
            stageFlags = VkShaderStageFlags.Vertex | VkShaderStageFlags.Fragment
        };
        bindings[1] = new VkDescriptorSetLayoutBinding
        {
            binding = 1,
            descriptorType = VkDescriptorType.CombinedImageSampler,
            descriptorCount = 1,
            stageFlags = VkShaderStageFlags.Fragment
        };

        VkDescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            bindingCount = 2,
            pBindings = bindings
        };

        _deviceApi.vkCreateDescriptorSetLayout(&layoutInfo, null, out _cameraSetLayout).CheckResult();
        Logger.Debug("Descriptor set layout created.");

        AddDescriptorPool();
    }

    // Makes another pool of the same size, the one new sets come from.
    private void AddDescriptorPool()
    {
        Logger.Debug($"Creating descriptor pool {_descriptorPools.Count + 1} (4096 UBOs, 4096 dynamic UBOs, 16384 samplers, 1024 storage buffers, 16384 images and 4096 samplers apart, maxSets=4096)...");
        VkDescriptorPoolSize* poolSizes = stackalloc VkDescriptorPoolSize[6];
        poolSizes[0] = new VkDescriptorPoolSize(VkDescriptorType.UniformBuffer, 4096);
        // A model pass set holds five maps, so samplers run out first.
        poolSizes[1] = new VkDescriptorPoolSize(VkDescriptorType.CombinedImageSampler, 16384);
        poolSizes[2] = new VkDescriptorPoolSize(VkDescriptorType.UniformBufferDynamic, 4096);
        // For the storage buffers a drawing shader reads, which few draws have.
        poolSizes[3] = new VkDescriptorPoolSize(VkDescriptorType.StorageBuffer, 1024);
        // For the model pass's lights' sets, which read their images through two samplers bound
        // apart, and a program's shaders that declare a texture and its sampler apart.
        poolSizes[4] = new VkDescriptorPoolSize(VkDescriptorType.SampledImage, 16384);
        poolSizes[5] = new VkDescriptorPoolSize(VkDescriptorType.Sampler, 4096);

        VkDescriptorPoolCreateInfo poolInfo = new()
        {
            flags = VkDescriptorPoolCreateFlags.FreeDescriptorSet,
            maxSets = 4096,
            poolSizeCount = 6,
            pPoolSizes = poolSizes
        };

        _deviceApi.vkCreateDescriptorPool(&poolInfo, null, out var pool).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.DescriptorPool);
        _descriptorPools.Add(pool);
    }

    /// <summary>Destroys the descriptor pool and descriptor set layout.</summary>
    private void DestroyDescriptorResources()
    {
        Logger.Debug("Destroying descriptor resources (pool + layout)...");
        foreach (var pool in _descriptorPools)
        {
            DeviceObjects.Gone(DeviceObjects.Kind.DescriptorPool);
            _deviceApi.vkDestroyDescriptorPool(pool);
        }
        // The sets still in them went with them.
        DeviceObjects.Gone(DeviceObjects.Kind.DescriptorSet, Volatile.Read(ref _liveDescriptorSets));
        _descriptorPools.Clear();
        if (_cameraSetLayout.Handle != 0)
        {
            _deviceApi.vkDestroyDescriptorSetLayout(_cameraSetLayout);
            _cameraSetLayout = default;
        }
    }

    /// <inheritdoc />
    public IDescriptorSet CreateDescriptorSet()
    {
        if (_descriptorPool.Handle == 0)
            CreateDescriptorResources();

        VkDescriptorSetLayout* layouts = stackalloc VkDescriptorSetLayout[1];
        layouts[0] = _cameraSetLayout;

        return Allocate(layouts[0]);
    }

    /// <summary>Wraps a Vulkan descriptor set layout for custom pipeline layouts.</summary>
    private sealed class VulkanDescriptorSetLayout : IDescriptorSetLayout
    {
        private readonly GraphicsDevice _device;
        internal VkDescriptorSetLayout Handle;

        public VulkanDescriptorSetLayout(GraphicsDevice device, VkDescriptorSetLayout handle, DescriptorSetLayoutBinding[] bindings)
        {
            _device = device;
            Handle = handle;
            Bindings = bindings;
        }

        /// <summary>The bindings it was made with, which a pipeline's stages are counted from.</summary>
        internal DescriptorSetLayoutBinding[] Bindings { get; }

        public void Dispose()
        {
            if (Handle.Handle != 0)
            {
                _device._deviceApi.vkDestroyDescriptorSetLayout(Handle);
                Handle = default;
            }
        }
    }

    /// <inheritdoc />
    public IDescriptorSetLayout CreateDescriptorSetLayout(DescriptorSetLayoutBinding[] bindings)
    {
        if (_descriptorPool.Handle == 0)
            CreateDescriptorResources();

        VkDescriptorSetLayoutBinding* vkBindings = stackalloc VkDescriptorSetLayoutBinding[bindings.Length];
        for (int i = 0; i < bindings.Length; i++)
        {
            vkBindings[i] = new VkDescriptorSetLayoutBinding
            {
                binding = bindings[i].Binding,
                descriptorType = ToVkDescriptorType(bindings[i].Type),
                descriptorCount = bindings[i].Count,
                stageFlags = ToVkShaderStageFlags(bindings[i].Stages)
            };
        }

        VkDescriptorSetLayoutCreateInfo layoutInfo = new()
        {
            bindingCount = (uint)bindings.Length,
            pBindings = vkBindings
        };

        _deviceApi.vkCreateDescriptorSetLayout(&layoutInfo, null, out VkDescriptorSetLayout layout).CheckResult();
        return new VulkanDescriptorSetLayout(this, layout, [.. bindings]);
    }

    /// <inheritdoc />
    public IDescriptorSet CreateDescriptorSet(IDescriptorSetLayout layout)
    {
        if (_descriptorPool.Handle == 0)
            CreateDescriptorResources();

        if (layout is not VulkanDescriptorSetLayout vkLayout)
            throw new ArgumentException("Descriptor set layout was not created by this device.", nameof(layout));

        VkDescriptorSetLayout* layouts = stackalloc VkDescriptorSetLayout[1];
        layouts[0] = vkLayout.Handle;

        return Allocate(layouts[0]);
    }

    // Allocates a set of the layout from the newest pool, and from a new one when that is full.
    private VulkanDescriptorSet Allocate(VkDescriptorSetLayout layout)
    {
        for (int attempt = 0; ; attempt++)
        {
            var pool = _descriptorPool;
            VkDescriptorSetAllocateInfo allocInfo = new()
            {
                descriptorPool = pool,
                descriptorSetCount = 1,
                pSetLayouts = &layout
            };

            VkDescriptorSet set;
            var result = _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set);
            if (result == VkResult.Success) return new VulkanDescriptorSet(this, pool, set);
            if (attempt > 0 || result is not (VkResult.ErrorOutOfPoolMemory or VkResult.ErrorFragmentedPool)) result.CheckResult();
            AddDescriptorPool();
        }
    }

    IDescriptorSetLayout IGraphicsDevice.CreateDescriptorSetLayout(DescriptorSetLayoutBinding[] bindings) => CreateDescriptorSetLayout(bindings);
    IDescriptorSet IGraphicsDevice.CreateDescriptorSet() => CreateDescriptorSet();
    IDescriptorSet IGraphicsDevice.CreateDescriptorSet(IDescriptorSetLayout layout) => CreateDescriptorSet(layout);

    /// <summary>Updates a descriptor set with optional uniform buffer and combined image sampler bindings by writing to Vulkan descriptors.</summary>
    /// <param name="descriptorSet">The descriptor set to update.</param>
    /// <param name="uniformBinding">Optional uniform buffer binding descriptor.</param>
    /// <param name="samplerBinding">Optional combined image sampler binding descriptor.</param>
    internal void UpdateDescriptorSet(
        IDescriptorSet descriptorSet,
        in UniformBufferBinding? uniformBinding,
        in CombinedImageSamplerBinding? samplerBinding)
    {
        if (descriptorSet is not VulkanDescriptorSet vkSet)
            throw new ArgumentException("Descriptor set was not created by this device.", nameof(descriptorSet));

        VkWriteDescriptorSet* writes = stackalloc VkWriteDescriptorSet[2];
        VkDescriptorBufferInfo* bufferInfos = stackalloc VkDescriptorBufferInfo[1];
        VkDescriptorImageInfo* imageInfos = stackalloc VkDescriptorImageInfo[1];
        int writeCount = 0;

        if (uniformBinding.HasValue)
        {
            var ub = uniformBinding.Value;
            if (ub.Buffer is not GraphicsDevice.VulkanBuffer vkBuffer)
                throw new ArgumentException("Uniform buffer was not created by this device.", nameof(uniformBinding));

            bufferInfos[0] = new VkDescriptorBufferInfo
            {
                buffer = vkBuffer.Buffer,
                offset = ub.Offset,
                range = ub.Size
            };

            writes[writeCount++] = new VkWriteDescriptorSet
            {
                dstSet = vkSet.Handle,
                dstBinding = ub.Binding,
                descriptorCount = 1,
                descriptorType = ub.Dynamic ? VkDescriptorType.UniformBufferDynamic : VkDescriptorType.UniformBuffer,
                pBufferInfo = &bufferInfos[0]
            };
        }

        if (samplerBinding.HasValue)
        {
            var sb = samplerBinding.Value;
            if (sb.ImageView is not GraphicsDevice.VulkanImageView vkView)
                throw new ArgumentException("Image view was not created by this device.", nameof(samplerBinding));
            if (sb.Sampler is not GraphicsDevice.VulkanSampler vkSampler)
                throw new ArgumentException("Sampler was not created by this device.", nameof(samplerBinding));

            imageInfos[0] = ImageInfo(sb.Type, vkView.View, vkSampler.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            writes[writeCount++] = new VkWriteDescriptorSet
            {
                dstSet = vkSet.Handle,
                dstBinding = sb.Binding,
                descriptorCount = 1,
                descriptorType = ToVkDescriptorType(sb.Type),
                pImageInfo = &imageInfos[0]
            };
        }

        if (writeCount == 0)
            return;

        _deviceApi.vkUpdateDescriptorSets((uint)writeCount, writes, 0, null);
    }

    void IGraphicsDevice.UpdateDescriptorSet(IDescriptorSet descriptorSet, in UniformBufferBinding? uniformBinding, in CombinedImageSamplerBinding? samplerBinding)
        => UpdateDescriptorSet(descriptorSet, uniformBinding, samplerBinding);

    private static VkDescriptorType ToVkDescriptorType(DescriptorType type) => type switch
    {
        DescriptorType.UniformBuffer => VkDescriptorType.UniformBuffer,
        DescriptorType.CombinedImageSampler => VkDescriptorType.CombinedImageSampler,
        DescriptorType.UniformBufferDynamic => VkDescriptorType.UniformBufferDynamic,
        DescriptorType.StorageBuffer => VkDescriptorType.StorageBuffer,
        DescriptorType.StorageImage => VkDescriptorType.StorageImage,
        DescriptorType.SampledImage => VkDescriptorType.SampledImage,
        DescriptorType.Sampler => VkDescriptorType.Sampler,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    // A texture's descriptor as its binding is declared: the image and the sampler, the image
    // alone, or the sampler alone, since Vulkan reads only what the type names.
    private static VkDescriptorImageInfo ImageInfo(DescriptorType type, VkImageView view, VkSampler sampler, VkImageLayout layout) => type switch
    {
        DescriptorType.SampledImage => new VkDescriptorImageInfo { imageView = view, imageLayout = layout },
        DescriptorType.Sampler => new VkDescriptorImageInfo { sampler = sampler },
        _ => new VkDescriptorImageInfo { imageView = view, sampler = sampler, imageLayout = layout },
    };

    /// <inheritdoc />
    public void UpdateDescriptorSet(IDescriptorSet descriptorSet, in StorageBufferBinding storageBinding)
    {
        if (descriptorSet is not VulkanDescriptorSet vkSet)
            throw new ArgumentException("Descriptor set was not created by this device.", nameof(descriptorSet));
        if (storageBinding.Buffer is not VulkanBuffer vkBuffer)
            throw new ArgumentException("Storage buffer was not created by this device.", nameof(storageBinding));

        var info = new VkDescriptorBufferInfo { buffer = vkBuffer.Buffer, offset = 0, range = vkBuffer.Description.Size };
        var write = new VkWriteDescriptorSet
        {
            dstSet = vkSet.Handle,
            dstBinding = storageBinding.Binding,
            descriptorCount = 1,
            descriptorType = VkDescriptorType.StorageBuffer,
            pBufferInfo = &info,
        };
        _deviceApi.vkUpdateDescriptorSets(1, &write, 0, null);
    }
}
