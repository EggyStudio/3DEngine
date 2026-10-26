using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>
    /// Every pool the shared descriptor sets are allocated from, which go with the device.
    /// </summary>
    private readonly List<VkDescriptorPool> _descriptorPools = [];

    /// <summary>
    /// The shared sets' pools by the shape of the layouts they serve, the descriptors of each type
    /// a set takes, every pool counted from its shape times its sets. One that runs out is kept for
    /// the sets it holds, and one twice as large is made for the next, so a frame with more draws
    /// of their own sets than one pool holds still finds room.
    /// </summary>
    private readonly Dictionary<string, List<SetPool>> _setPools = [];

    // The sets a shape's first pool holds, and the most any of its pools do.
    private const int FirstPoolSets = 64, MostPoolSets = 4096;

    // A pool of the shared sets and how many more it holds, which a freed set gives back to.
    private sealed class SetPool(VkDescriptorPool handle, int sets)
    {
        public readonly VkDescriptorPool Handle = handle;
        public readonly int Sets = sets;
        public int Left = sets;
    }

    /// <summary>Default descriptor set layout (binding 0 = UBO vertex, binding 1 = combined image sampler fragment).</summary>
    private VkDescriptorSetLayout _cameraSetLayout;

    /// <summary>Wraps a Vulkan descriptor set allocated from the device's global pool.</summary>
    /// <seealso cref="IDescriptorSet"/>
    private sealed class VulkanDescriptorSet : IDescriptorSet
    {
        private readonly GraphicsDevice _device;
        private readonly SetPool _pool;

        /// <summary>The underlying Vulkan descriptor set handle.</summary>
        internal VkDescriptorSet Handle;

        /// <summary>Creates a new Vulkan descriptor set wrapper.</summary>
        /// <param name="device">The owning graphics device.</param>
        /// <param name="pool">The pool it was allocated from, which it goes back to.</param>
        /// <param name="handle">The allocated Vulkan descriptor set handle.</param>
        public VulkanDescriptorSet(GraphicsDevice device, SetPool pool, VkDescriptorSet handle)
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
            if (Handle.Handle != 0 && _device._descriptorPools.Contains(_pool.Handle))
            {
                var set = Handle;
                _device._deviceApi.vkFreeDescriptorSets(_pool.Handle, 1, &set);
                _pool.Left++;
                DeviceObjects.Gone(DeviceObjects.Kind.DescriptorSet);
            }
            if (Handle.Handle != 0) Interlocked.Decrement(ref _device._liveDescriptorSets);
            Handle = default;
        }
    }

    /// <summary>Creates the default descriptor set layout used for camera UBOs and texture samplers.</summary>
    private void CreateDescriptorResources()
    {
        Logger.Debug("Creating descriptor set layout (binding 0=UBO vertex, binding 1=CombinedImageSampler fragment)...");
        // A simple layout, binding 0 a uniform buffer and binding 1 a combined image sampler.
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
    }

    /// <summary>
    /// Makes a pool of the sets given, each a layout's descriptors by type, one entry a descriptor,
    /// and how many sets of it, its counts summed from them, or none where no set is given.
    /// </summary>
    /// <remarks>
    /// A driver may refuse a set past its pool's counts, as MoltenVK does, where Mesa's allocate it
    /// and the layer only warns, so no pool's counts are written by hand. A pool counted by hand is
    /// outgrown when a pass is given another binding, and the frames that use it then fail on macOS
    /// alone.
    /// </remarks>
    private VkDescriptorPool MakeDescriptorPool(IEnumerable<(VkDescriptorType[] Descriptors, int Sets)> layouts,
        VkDescriptorPoolCreateFlags flags = VkDescriptorPoolCreateFlags.None)
    {
        var counts = new SortedDictionary<VkDescriptorType, uint>();
        uint sets = 0;
        foreach (var (descriptors, n) in layouts)
        {
            foreach (var type in descriptors) counts[type] = counts.GetValueOrDefault(type) + (uint)n;
            sets += (uint)n;
        }
        if (sets == 0) return default;

        var sizes = stackalloc VkDescriptorPoolSize[Math.Max(1, counts.Count)];
        var i = 0;
        foreach (var (type, count) in counts) sizes[i++] = new VkDescriptorPoolSize(type, count);
        var poolInfo = new VkDescriptorPoolCreateInfo { flags = flags, maxSets = sets, poolSizeCount = (uint)counts.Count, pPoolSizes = sizes };
        _deviceApi.vkCreateDescriptorPool(&poolInfo, null, out var pool).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.DescriptorPool);
        return pool;
    }

    // Destroys a pool MakeDescriptorPool made, where it made one.
    private void DestroyDescriptorPool(VkDescriptorPool pool)
    {
        if (pool.Handle == 0) return;
        DeviceObjects.Gone(DeviceObjects.Kind.DescriptorPool);
        _deviceApi.vkDestroyDescriptorPool(pool);
    }

    // The camera set's descriptors and their shape, which its pools are counted from.
    private static readonly VkDescriptorType[] CameraDescriptors = [VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler];
    private static readonly string CameraShape = ShapeOf(CameraDescriptors);

    // The key of a layout's shape, its descriptors in order of type.
    private static string ShapeOf(VkDescriptorType[] descriptors) => string.Join(',', descriptors.Order());

    // A layout's descriptors by type, one entry a descriptor, as MakeDescriptorPool counts them.
    private static VkDescriptorType[] DescriptorsOf(ReadOnlySpan<DescriptorSetLayoutBinding> bindings)
    {
        var descriptors = new List<VkDescriptorType>();
        foreach (var binding in bindings)
            for (var d = 0u; d < binding.Count; d++) descriptors.Add(ToVkDescriptorType(binding.Type));
        return [.. descriptors];
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
        _setPools.Clear();
        if (_cameraSetLayout.Handle != 0)
        {
            _deviceApi.vkDestroyDescriptorSetLayout(_cameraSetLayout);
            _cameraSetLayout = default;
        }
    }

    /// <inheritdoc />
    public IDescriptorSet CreateDescriptorSet()
    {
        if (_cameraSetLayout.Handle == 0)
            CreateDescriptorResources();

        return Allocate(_cameraSetLayout, CameraDescriptors, CameraShape);
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
            Descriptors = DescriptorsOf(bindings);
            Shape = ShapeOf(Descriptors);
        }

        /// <summary>The bindings it was made with, which a pipeline's stages are counted from.</summary>
        internal DescriptorSetLayoutBinding[] Bindings { get; }

        /// <summary>A set's descriptors by type, which the pool its sets come from is counted from.</summary>
        internal VkDescriptorType[] Descriptors { get; }

        /// <summary>The key of the pools its sets come from, shared with every layout of its descriptors.</summary>
        internal string Shape { get; }

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
        if (_cameraSetLayout.Handle == 0)
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
        if (_cameraSetLayout.Handle == 0)
            CreateDescriptorResources();

        if (layout is not VulkanDescriptorSetLayout vkLayout)
            throw new ArgumentException("Descriptor set layout was not created by this device.", nameof(layout));

        return Allocate(vkLayout.Handle, vkLayout.Descriptors, vkLayout.Shape);
    }

    // Allocates a set of the layout from the newest pool of its shape with room for it, and from a
    // new one when none has.
    private VulkanDescriptorSet Allocate(VkDescriptorSetLayout layout, VkDescriptorType[] descriptors, string shape)
    {
        if (!_setPools.TryGetValue(shape, out var pools)) _setPools[shape] = pools = [];
        for (int attempt = 0; ; attempt++)
        {
            if (pools.FindLast(p => p.Left > 0) is not { } pool)
            {
                var sets = pools.Count == 0 ? FirstPoolSets : Math.Min(2 * pools[^1].Sets, MostPoolSets);
                Logger.Debug($"Making a descriptor pool of {sets} sets of {(shape.Length > 0 ? shape : "no descriptors")}...");
                pool = new SetPool(MakeDescriptorPool([(descriptors, sets)], VkDescriptorPoolCreateFlags.FreeDescriptorSet), sets);
                _descriptorPools.Add(pool.Handle);
                pools.Add(pool);
            }
            VkDescriptorSetAllocateInfo allocInfo = new()
            {
                descriptorPool = pool.Handle,
                descriptorSetCount = 1,
                pSetLayouts = &layout
            };

            VkDescriptorSet set;
            var result = _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set);
            if (result == VkResult.Success)
            {
                pool.Left--;
                return new VulkanDescriptorSet(this, pool, set);
            }
            // A pool whose sets were freed out of order may be too fragmented for one more set
            // though its counts hold it, and is passed over until a set of it is freed.
            if (attempt > 0 || result is not (VkResult.ErrorOutOfPoolMemory or VkResult.ErrorFragmentedPool)) result.CheckResult();
            pool.Left = 0;
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
        DescriptorType.AccelerationStructure => VkDescriptorType.AccelerationStructureKHR,
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    // A texture's descriptor as its binding is declared, the image and the sampler, the image
    // alone or the sampler alone, since Vulkan reads only what the type names.
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
