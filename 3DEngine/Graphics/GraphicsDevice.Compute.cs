using Vortice.Vulkan;

namespace Engine;

/// <summary>A compute shader's pipeline, with the descriptor layout of its uniforms, storage buffers, images and textures.</summary>
internal sealed class ComputePipeline : IDisposable
{
    private readonly Action _dispose;

    internal ComputePipeline(VkPipeline pipeline, VkPipelineLayout layout, VkDescriptorSetLayout setLayout, int uniformSize,
        IReadOnlyList<int> bufferBindings, Action dispose, IReadOnlyList<int>? imageBindings = null,
        IReadOnlyList<(int Binding, DescriptorType Type)>? textureBindings = null)
    {
        ImageBindings = imageBindings ?? [];
        TextureBindings = textureBindings ?? [];
        Pipeline = pipeline;
        Layout = layout;
        SetLayout = setLayout;
        UniformSize = uniformSize;
        BufferBindings = bufferBindings;
        _dispose = dispose;
    }

    internal VkPipeline Pipeline { get; }
    internal VkPipelineLayout Layout { get; }
    internal VkDescriptorSetLayout SetLayout { get; }

    /// <summary>The size of the uniform buffer at binding 0, or 0 when the shader declares no uniforms.</summary>
    public int UniformSize { get; }

    /// <summary>The bindings of the storage buffers the shader uses.</summary>
    public IReadOnlyList<int> BufferBindings { get; }

    /// <summary>The bindings of the images the shader writes.</summary>
    public IReadOnlyList<int> ImageBindings { get; }

    /// <summary>The bindings of the textures the shader samples, and of the samplers it declares apart, each with how it is declared.</summary>
    public IReadOnlyList<(int Binding, DescriptorType Type)> TextureBindings { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

internal sealed unsafe partial class GraphicsDevice
{
    // Dispatches submitted and not yet known to have finished, with what each holds until then.
    private readonly List<(VkFence Fence, VkCommandBuffer Commands, VkDescriptorPool Pool, IBuffer? Uniforms)> _computeInFlight = [];
    private readonly HashSet<ComputePipeline> _computePipelines = [];
    private readonly object _computeGate = new();
    private VkCommandPool _computePool;

    /// <summary>Creates the pipeline of a compute shader from its SPIR-V.</summary>
    /// <param name="spirv">The compute stage.</param>
    /// <param name="uniformSize">The size of its uniform buffer at binding 0, or 0 when it has none.</param>
    /// <param name="bufferBindings">The bindings of its storage buffers.</param>
    /// <param name="imageBindings">The bindings of the images it writes.</param>
    /// <param name="textureBindings">The bindings of the textures it samples.</param>
    /// <exception cref="InvalidOperationException">The device has not been initialized.</exception>
    public ComputePipeline CreateComputePipeline(ReadOnlySpan<byte> spirv, int uniformSize, IReadOnlyList<int> bufferBindings,
        IReadOnlyList<int>? imageBindings = null, IReadOnlyList<(int Binding, DescriptorType Type)>? textureBindings = null)
    {
        if (!IsInitialized) throw new InvalidOperationException("Graphics device not initialized");
        imageBindings ??= [];
        textureBindings ??= [];

        CheckComputeLimits(textureBindings.Select(t => t.Type), "a compute shader");
        var count = bufferBindings.Count + imageBindings.Count + textureBindings.Count + (uniformSize > 0 ? 1 : 0);
        var bindings = stackalloc VkDescriptorSetLayoutBinding[Math.Max(1, count)];
        int b = 0;
        if (uniformSize > 0)
            bindings[b++] = new VkDescriptorSetLayoutBinding { binding = 0, descriptorType = VkDescriptorType.UniformBuffer, descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
        foreach (var binding in bufferBindings)
            bindings[b++] = new VkDescriptorSetLayoutBinding { binding = (uint)binding, descriptorType = VkDescriptorType.StorageBuffer, descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
        foreach (var binding in imageBindings)
            bindings[b++] = new VkDescriptorSetLayoutBinding { binding = (uint)binding, descriptorType = VkDescriptorType.StorageImage, descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
        foreach (var (binding, type) in textureBindings)
            bindings[b++] = new VkDescriptorSetLayoutBinding { binding = (uint)binding, descriptorType = ToVkDescriptorType(type), descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
        var setInfo = new VkDescriptorSetLayoutCreateInfo { bindingCount = (uint)count, pBindings = bindings };
        _deviceApi.vkCreateDescriptorSetLayout(&setInfo, null, out VkDescriptorSetLayout createdSetLayout).CheckResult();
        var setLayout = createdSetLayout;

        var layoutInfo = new VkPipelineLayoutCreateInfo { setLayoutCount = 1, pSetLayouts = &createdSetLayout };
        _deviceApi.vkCreatePipelineLayout(&layoutInfo, null, out VkPipelineLayout layout).CheckResult();

        VkShaderModule module;
        fixed (byte* code = spirv)
        {
            var moduleInfo = new VkShaderModuleCreateInfo { codeSize = (nuint)spirv.Length, pCode = (uint*)code };
            _deviceApi.vkCreateShaderModule(&moduleInfo, null, out module).CheckResult();
        }

        // slangc names every entry point main in the SPIR-V it writes.
        var main = "main"u8;
        VkPipeline createdPipeline;
        fixed (byte* name = main)
        {
            var info = new VkComputePipelineCreateInfo
            {
                stage = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.Compute, module = module, pName = name },
                layout = layout,
            };
            _deviceApi.vkCreateComputePipelines(default, 1, &info, null, &createdPipeline).CheckResult();
        }
        var pipeline = createdPipeline;
        _deviceApi.vkDestroyShaderModule(module);

        ComputePipeline? created = null;
        created = new ComputePipeline(pipeline, layout, setLayout, uniformSize, [.. bufferBindings], imageBindings: [.. imageBindings], textureBindings: [.. textureBindings], dispose: () =>
        {
            lock (_computeGate)
            {
                if (!_computePipelines.Remove(created!)) return;
                // A dispatch still running may use it.
                WaitForComputeLocked();
            }
            _deviceApi.vkDestroyPipeline(pipeline);
            _deviceApi.vkDestroyPipelineLayout(layout);
            _deviceApi.vkDestroyDescriptorSetLayout(setLayout);
        });
        lock (_computeGate) _computePipelines.Add(created);
        return created;
    }

    /// <summary>Creates a storage buffer of <paramref name="size"/> bytes, zeroed, that the CPU maps to fill and read.</summary>
    public IBuffer CreateStorageBuffer(int size)
    {
        var buffer = CreateBuffer(new BufferDesc((ulong)Math.Max(4, size), BufferUsage.Storage, CpuAccessMode.ReadWrite));
        Map(buffer).Clear();
        return buffer;
    }

    /// <summary>
    /// Runs <paramref name="pipeline"/> over <paramref name="groupsX"/> by <paramref name="groupsY"/>
    /// by <paramref name="groupsZ"/> groups of threads, with <paramref name="uniforms"/> at binding
    /// 0, each storage buffer, image and texture at its binding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Submitted at once to the queue the frames go to, without waiting, so it runs before the
    /// frame being recorded and after the ones before it. Barriers on both sides order it against
    /// them on the GPU: what earlier work wrote is seen, and what it writes is seen by later work and
    /// by the CPU once <see cref="WaitForCompute"/> returns. An image it writes is moved to the
    /// general layout for it, from the one textures are sampled in, and back after, and a
    /// mipmapped one has its other levels made again from the first, which the shader writes.
    /// </para>
    /// <para>
    /// Callable from the systems of a stage that run in parallel, which a lock keeps to one
    /// submission at a time. Frames are submitted in <see cref="Stage.Last"/>, when no system runs.
    /// </para>
    /// </remarks>
    public void Dispatch(ComputePipeline pipeline, ReadOnlySpan<byte> uniforms, IReadOnlyList<(int Binding, IBuffer Buffer)> buffers,
        uint groupsX, uint groupsY, uint groupsZ, IReadOnlyList<(int Binding, IImage Image, IImageView View)>? images = null,
        IReadOnlyList<(int Binding, IImageView View, ISampler Sampler)>? textures = null)
    {
        images ??= [];
        textures ??= [];
        lock (_computeGate)
        {
            Retire(wait: false);
            if (_computePool.Handle == 0)
            {
                var poolInfo = new VkCommandPoolCreateInfo { flags = VkCommandPoolCreateFlags.Transient, queueFamilyIndex = _graphicsQueueFamily };
                _deviceApi.vkCreateCommandPool(&poolInfo, null, out _computePool).CheckResult();
            }

            // A pool of one set for each dispatch, freed with it, since dispatches come and go
            // at the program's pace rather than the frame's.
            var sizes = stackalloc VkDescriptorPoolSize[6];
            sizes[0] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageBuffer, descriptorCount = (uint)Math.Max(1, buffers.Count) };
            sizes[1] = new VkDescriptorPoolSize { type = VkDescriptorType.UniformBuffer, descriptorCount = 1 };
            sizes[2] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageImage, descriptorCount = (uint)Math.Max(1, images.Count) };
            sizes[3] = new VkDescriptorPoolSize { type = VkDescriptorType.CombinedImageSampler, descriptorCount = (uint)Math.Max(1, textures.Count) };
            sizes[4] = new VkDescriptorPoolSize { type = VkDescriptorType.SampledImage, descriptorCount = (uint)Math.Max(1, textures.Count) };
            sizes[5] = new VkDescriptorPoolSize { type = VkDescriptorType.Sampler, descriptorCount = (uint)Math.Max(1, textures.Count) };
            var descriptorPoolInfo = new VkDescriptorPoolCreateInfo { maxSets = 1, poolSizeCount = 6, pPoolSizes = sizes };
            _deviceApi.vkCreateDescriptorPool(&descriptorPoolInfo, null, out VkDescriptorPool pool).CheckResult();
            var setLayout = pipeline.SetLayout;
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = pool, descriptorSetCount = 1, pSetLayouts = &setLayout };
            VkDescriptorSet set;
            _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();

            IBuffer? uniformBuffer = null;
            var writes = stackalloc VkWriteDescriptorSet[buffers.Count + images.Count + textures.Count + 1];
            var infos = stackalloc VkDescriptorBufferInfo[buffers.Count + 1];
            var imageInfos = stackalloc VkDescriptorImageInfo[images.Count + textures.Count + 1];
            int w = 0, wi = 0;
            if (pipeline.UniformSize > 0)
            {
                uniformBuffer = CreateBuffer(new BufferDesc((ulong)pipeline.UniformSize, BufferUsage.Uniform, CpuAccessMode.Write));
                var mapped = Map(uniformBuffer);
                mapped.Clear();
                uniforms[..Math.Min(uniforms.Length, mapped.Length)].CopyTo(mapped);
                infos[w] = new VkDescriptorBufferInfo { buffer = ((VulkanBuffer)uniformBuffer).Buffer, offset = 0, range = (ulong)pipeline.UniformSize };
                writes[w] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 0, descriptorCount = 1, descriptorType = VkDescriptorType.UniformBuffer, pBufferInfo = &infos[w] };
                w++;
            }
            foreach (var (binding, buffer) in buffers)
            {
                var vk = (VulkanBuffer)buffer;
                infos[w] = new VkDescriptorBufferInfo { buffer = vk.Buffer, offset = 0, range = vk.Description.Size };
                writes[w] = new VkWriteDescriptorSet { dstSet = set, dstBinding = (uint)binding, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &infos[w] };
                w++;
            }
            foreach (var (binding, _, view) in images)
            {
                imageInfos[wi] = new VkDescriptorImageInfo { imageView = ((VulkanImageView)view).View, imageLayout = VkImageLayout.General };
                writes[w++] = new VkWriteDescriptorSet { dstSet = set, dstBinding = (uint)binding, descriptorCount = 1, descriptorType = VkDescriptorType.StorageImage, pImageInfo = &imageInfos[wi++] };
            }
            foreach (var (binding, view, sampler) in textures)
            {
                var type = DescriptorType.CombinedImageSampler;
                foreach (var declared in pipeline.TextureBindings)
                    if (declared.Binding == binding) type = declared.Type;
                imageInfos[wi] = ImageInfo(type, ((VulkanImageView)view).View, ((VulkanSampler)sampler).Sampler, VkImageLayout.ShaderReadOnlyOptimal);
                writes[w++] = new VkWriteDescriptorSet { dstSet = set, dstBinding = (uint)binding, descriptorCount = 1, descriptorType = ToVkDescriptorType(type), pImageInfo = &imageInfos[wi++] };
            }
            _deviceApi.vkUpdateDescriptorSets((uint)w, writes, 0, null);

            var commandInfo = new VkCommandBufferAllocateInfo { commandPool = _computePool, level = VkCommandBufferLevel.Primary, commandBufferCount = 1 };
            VkCommandBuffer cmd;
            _deviceApi.vkAllocateCommandBuffers(&commandInfo, &cmd).CheckResult();
            var begin = new VkCommandBufferBeginInfo { flags = VkCommandBufferUsageFlags.OneTimeSubmit };
            _deviceApi.vkBeginCommandBuffer(cmd, &begin).CheckResult();

            // Earlier work on the queue, a frame's or another dispatch's, has finished writing
            // what this one reads.
            var before = new VkMemoryBarrier2
            {
                srcStageMask = VkPipelineStageFlags2.AllCommands,
                srcAccessMask = VkAccessFlags2.MemoryWrite,
                dstStageMask = VkPipelineStageFlags2.ComputeShader,
                dstAccessMask = VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
            };
            var toGeneral = new VkImageMemoryBarrier2[images.Count];
            for (int i = 0; i < images.Count; i++)
                toGeneral[i] = ImageLayoutBarrier(images[i].Image, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.General,
                    VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead,
                    VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
            PipelineBarrier(cmd, toGeneral, before);

            _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline.Pipeline);
            _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, pipeline.Layout, 0, 1, &set, 0, null);
            _deviceApi.vkCmdDispatch(cmd, Math.Max(1, groupsX), Math.Max(1, groupsY), Math.Max(1, groupsZ));

            // Later work, and the CPU once the fence is waited on, see what it wrote.
            var later = VkPipelineStageFlags2.AllCommands | VkPipelineStageFlags2.Host | VkPipelineStageFlags2.Transfer;
            var after = new VkMemoryBarrier2
            {
                srcStageMask = VkPipelineStageFlags2.ComputeShader,
                srcAccessMask = VkAccessFlags2.ShaderWrite,
                dstStageMask = later,
                dstAccessMask = VkAccessFlags2.MemoryRead | VkAccessFlags2.MemoryWrite | VkAccessFlags2.HostRead,
            };
            // A mipmapped image goes to the transfer layout instead, since its other levels are
            // made again from the first one the shader wrote.
            var toSampled = new VkImageMemoryBarrier2[images.Count];
            for (int i = 0; i < images.Count; i++)
                toSampled[i] = images[i].Image.Description.MipLevels > 1
                    ? ImageLayoutBarrier(images[i].Image, VkImageLayout.General, VkImageLayout.TransferDstOptimal,
                        VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
                        later, VkAccessFlags2.TransferRead | VkAccessFlags2.TransferWrite)
                    : ImageLayoutBarrier(images[i].Image, VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
                        VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
                        later, VkAccessFlags2.ShaderRead | VkAccessFlags2.MemoryRead);
            PipelineBarrier(cmd, toSampled, after);
            foreach (var (_, image, _) in images)
                if (image.Description.MipLevels > 1)
                    RecordMipChain(cmd, (VulkanImage)image);
            _deviceApi.vkEndCommandBuffer(cmd).CheckResult();

            var fenceInfo = new VkFenceCreateInfo();
            _deviceApi.vkCreateFence(&fenceInfo, null, out VkFence fence).CheckResult();
            var submit = new VkSubmitInfo { commandBufferCount = 1, pCommandBuffers = &cmd };
            FlushUploads();
            _deviceApi.vkQueueSubmit(_graphicsQueue, 1, &submit, fence).CheckResult();
            _computeInFlight.Add((fence, cmd, pool, uniformBuffer));
        }
    }

    // A barrier moving every level of a color image from one layout to another.
    private static VkImageMemoryBarrier2 ImageLayoutBarrier(IImage image, VkImageLayout from, VkImageLayout to,
        VkPipelineStageFlags2 srcStage, VkAccessFlags2 before, VkPipelineStageFlags2 dstStage, VkAccessFlags2 after) =>
        ImageBarrier(((VulkanImage)image).Image, ColorLevels(0, Math.Max(1, image.Description.MipLevels)), from, to, srcStage, before, dstStage, after);

    /// <summary>Waits for every dispatch submitted so far to finish, so the CPU can read or overwrite the buffers they used.</summary>
    public void WaitForCompute()
    {
        lock (_computeGate) WaitForComputeLocked();
    }

    private void WaitForComputeLocked() => Retire(wait: true);

    // Frees what finished dispatches held, waiting for the rest first when asked.
    private void Retire(bool wait)
    {
        for (int i = 0; i < _computeInFlight.Count; i++)
        {
            var (fence, commands, pool, uniforms) = _computeInFlight[i];
            if (wait) _deviceApi.vkWaitForFences(1, &fence, true, ulong.MaxValue).CheckResult();
            else if (_deviceApi.vkGetFenceStatus(fence) != VkResult.Success) continue;

            _deviceApi.vkDestroyFence(fence);
            _deviceApi.vkFreeCommandBuffers(_computePool, 1, &commands);
            _deviceApi.vkDestroyDescriptorPool(pool);
            uniforms?.Dispose();
            _computeInFlight.RemoveAt(i--);
        }
    }

    // Frees whatever a program left loaded, before the device goes and with the queue idle.
    private void DestroyCompute()
    {
        lock (_computeGate)
        {
            Retire(wait: true);
            foreach (var pipeline in _computePipelines.ToArray()) pipeline.Dispose();
            if (_computePool.Handle != 0) _deviceApi.vkDestroyCommandPool(_computePool);
            _computePool = default;
        }
    }
}
