using Vortice.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Global descriptor pool from which all descriptor sets are allocated.</summary>
    private VkDescriptorPool _descriptorPool;

    /// <summary>Default descriptor set layout (binding 0 = UBO vertex, binding 1 = combined image sampler fragment).</summary>
    private VkDescriptorSetLayout _cameraSetLayout;

    /// <summary>Wraps a Vulkan descriptor set allocated from the device's global pool.</summary>
    /// <seealso cref="IDescriptorSet"/>
    private sealed class VulkanDescriptorSet : IDescriptorSet
    {
        private readonly GraphicsDevice _device;

        /// <summary>The underlying Vulkan descriptor set handle.</summary>
        internal VkDescriptorSet Handle;

        /// <summary>Creates a new Vulkan descriptor set wrapper.</summary>
        /// <param name="device">The owning graphics device.</param>
        /// <param name="handle">The allocated Vulkan descriptor set handle.</param>
        public VulkanDescriptorSet(GraphicsDevice device, VkDescriptorSet handle)
        {
            _device = device;
            Handle = handle;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Handle.Handle != 0 && _device._descriptorPool.Handle != 0)
            {
                var set = Handle;
                _device._deviceApi.vkFreeDescriptorSets(_device._descriptorPool, 1, &set);
            }
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

        Logger.Debug("Creating descriptor pool (4096 UBOs + 4096 samplers, maxSets=4096)...");
        VkDescriptorPoolSize* poolSizes = stackalloc VkDescriptorPoolSize[2];
        poolSizes[0] = new VkDescriptorPoolSize(VkDescriptorType.UniformBuffer, 4096);
        poolSizes[1] = new VkDescriptorPoolSize(VkDescriptorType.CombinedImageSampler, 4096);

        VkDescriptorPoolCreateInfo poolInfo = new()
        {
            flags = VkDescriptorPoolCreateFlags.FreeDescriptorSet,
            maxSets = 4096,
            poolSizeCount = 2,
            pPoolSizes = poolSizes
        };

        _deviceApi.vkCreateDescriptorPool(&poolInfo, null, out _descriptorPool).CheckResult();
        Logger.Debug("Descriptor pool created successfully.");
    }

    /// <summary>Destroys the descriptor pool and descriptor set layout.</summary>
    private void DestroyDescriptorResources()
    {
        Logger.Debug("Destroying descriptor resources (pool + layout)...");
        if (_descriptorPool.Handle != 0)
        {
            _deviceApi.vkDestroyDescriptorPool(_descriptorPool);
            _descriptorPool = default;
        }
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

        VkDescriptorSet set;
        VkDescriptorSetLayout* layouts = stackalloc VkDescriptorSetLayout[1];
        layouts[0] = _cameraSetLayout;

        VkDescriptorSetAllocateInfo allocInfo = new()
        {
            descriptorPool = _descriptorPool,
            descriptorSetCount = 1,
            pSetLayouts = layouts
        };

        _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
        return new VulkanDescriptorSet(this, set);
    }

    /// <summary>Wraps a Vulkan descriptor set layout for custom pipeline layouts.</summary>
    private sealed class VulkanDescriptorSetLayout : IDescriptorSetLayout
    {
        private readonly GraphicsDevice _device;
        internal VkDescriptorSetLayout Handle;

        public VulkanDescriptorSetLayout(GraphicsDevice device, VkDescriptorSetLayout handle)
        {
            _device = device;
            Handle = handle;
        }

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
                descriptorType = bindings[i].Type switch
                {
                    DescriptorType.UniformBuffer => VkDescriptorType.UniformBuffer,
                    DescriptorType.CombinedImageSampler => VkDescriptorType.CombinedImageSampler,
                    _ => throw new ArgumentOutOfRangeException()
                },
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
        return new VulkanDescriptorSetLayout(this, layout);
    }

    /// <inheritdoc />
    public IDescriptorSet CreateDescriptorSet(IDescriptorSetLayout layout)
    {
        if (_descriptorPool.Handle == 0)
            CreateDescriptorResources();

        if (layout is not VulkanDescriptorSetLayout vkLayout)
            throw new ArgumentException("Descriptor set layout was not created by this device.", nameof(layout));

        VkDescriptorSet set;
        VkDescriptorSetLayout* layouts = stackalloc VkDescriptorSetLayout[1];
        layouts[0] = vkLayout.Handle;

        VkDescriptorSetAllocateInfo allocInfo = new()
        {
            descriptorPool = _descriptorPool,
            descriptorSetCount = 1,
            pSetLayouts = layouts
        };

        _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
        return new VulkanDescriptorSet(this, set);
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
                descriptorType = VkDescriptorType.UniformBuffer,
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

            imageInfos[0] = new VkDescriptorImageInfo
            {
                imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
                imageView = vkView.View,
                sampler = vkSampler.Sampler
            };

            writes[writeCount++] = new VkWriteDescriptorSet
            {
                dstSet = vkSet.Handle,
                dstBinding = sb.Binding,
                descriptorCount = 1,
                descriptorType = VkDescriptorType.CombinedImageSampler,
                pImageInfo = &imageInfos[0]
            };
        }

        if (writeCount == 0)
            return;

        _deviceApi.vkUpdateDescriptorSets((uint)writeCount, writes, 0, null);
    }

    void IGraphicsDevice.UpdateDescriptorSet(IDescriptorSet descriptorSet, in UniformBufferBinding? uniformBinding, in CombinedImageSamplerBinding? samplerBinding)
        => UpdateDescriptorSet(descriptorSet, uniformBinding, samplerBinding);
}
