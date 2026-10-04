using Vortice.Vulkan;

namespace Engine;

/// <summary>A compute shader's pipeline, with the descriptor layout of its uniforms and storage buffers.</summary>
public sealed class ComputePipeline : IDisposable
{
    private readonly Action _dispose;

    internal ComputePipeline(VkPipeline pipeline, VkPipelineLayout layout, VkDescriptorSetLayout setLayout, int uniformSize,
        IReadOnlyList<int> bufferBindings, Action dispose)
    {
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

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

public sealed unsafe partial class GraphicsDevice
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
    /// <exception cref="InvalidOperationException">The device has not been initialized.</exception>
    public ComputePipeline CreateComputePipeline(ReadOnlySpan<byte> spirv, int uniformSize, IReadOnlyList<int> bufferBindings)
    {
        if (!IsInitialized) throw new InvalidOperationException("Graphics device not initialized");

        var count = bufferBindings.Count + (uniformSize > 0 ? 1 : 0);
        var bindings = stackalloc VkDescriptorSetLayoutBinding[Math.Max(1, count)];
        int b = 0;
        if (uniformSize > 0)
            bindings[b++] = new VkDescriptorSetLayoutBinding { binding = 0, descriptorType = VkDescriptorType.UniformBuffer, descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
        foreach (var binding in bufferBindings)
            bindings[b++] = new VkDescriptorSetLayoutBinding { binding = (uint)binding, descriptorType = VkDescriptorType.StorageBuffer, descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
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
        created = new ComputePipeline(pipeline, layout, setLayout, uniformSize, [.. bufferBindings], () =>
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
    /// 0 and each storage buffer at its binding.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Submitted at once to the queue the frames go to, without waiting, so it runs before the
    /// frame being recorded and after the ones before it. Barriers on both sides order it against
    /// them on the GPU: what earlier work wrote is seen, and what it writes is seen by later work and
    /// by the CPU once <see cref="WaitForCompute"/> returns.
    /// </para>
    /// <para>
    /// Callable from the systems of a stage that run in parallel, which a lock keeps to one
    /// submission at a time. Frames are submitted in <see cref="Stage.Last"/>, when no system runs.
    /// </para>
    /// </remarks>
    public void Dispatch(ComputePipeline pipeline, ReadOnlySpan<byte> uniforms, IReadOnlyList<(int Binding, IBuffer Buffer)> buffers,
        uint groupsX, uint groupsY, uint groupsZ)
    {
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
            var sizes = stackalloc VkDescriptorPoolSize[2];
            sizes[0] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageBuffer, descriptorCount = (uint)Math.Max(1, buffers.Count) };
            sizes[1] = new VkDescriptorPoolSize { type = VkDescriptorType.UniformBuffer, descriptorCount = 1 };
            var descriptorPoolInfo = new VkDescriptorPoolCreateInfo { maxSets = 1, poolSizeCount = 2, pPoolSizes = sizes };
            _deviceApi.vkCreateDescriptorPool(&descriptorPoolInfo, null, out VkDescriptorPool pool).CheckResult();
            var setLayout = pipeline.SetLayout;
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = pool, descriptorSetCount = 1, pSetLayouts = &setLayout };
            VkDescriptorSet set;
            _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();

            IBuffer? uniformBuffer = null;
            var writes = stackalloc VkWriteDescriptorSet[buffers.Count + 1];
            var infos = stackalloc VkDescriptorBufferInfo[buffers.Count + 1];
            int w = 0;
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
            _deviceApi.vkUpdateDescriptorSets((uint)w, writes, 0, null);

            var commandInfo = new VkCommandBufferAllocateInfo { commandPool = _computePool, level = VkCommandBufferLevel.Primary, commandBufferCount = 1 };
            VkCommandBuffer cmd;
            _deviceApi.vkAllocateCommandBuffers(&commandInfo, &cmd).CheckResult();
            var begin = new VkCommandBufferBeginInfo { flags = VkCommandBufferUsageFlags.OneTimeSubmit };
            _deviceApi.vkBeginCommandBuffer(cmd, &begin).CheckResult();

            // Earlier work on the queue, a frame's or another dispatch's, has finished writing
            // what this one reads.
            var before = new VkMemoryBarrier
            {
                srcAccessMask = VkAccessFlags.MemoryWrite,
                dstAccessMask = VkAccessFlags.ShaderRead | VkAccessFlags.ShaderWrite,
            };
            _deviceApi.vkCmdPipelineBarrier(cmd, VkPipelineStageFlags.AllCommands, VkPipelineStageFlags.ComputeShader, 0, 1, &before, 0, null, 0, null);

            _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline.Pipeline);
            _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, pipeline.Layout, 0, 1, &set, 0, null);
            _deviceApi.vkCmdDispatch(cmd, Math.Max(1, groupsX), Math.Max(1, groupsY), Math.Max(1, groupsZ));

            // Later work, and the CPU once the fence is waited on, see what it wrote.
            var after = new VkMemoryBarrier
            {
                srcAccessMask = VkAccessFlags.ShaderWrite,
                dstAccessMask = VkAccessFlags.MemoryRead | VkAccessFlags.MemoryWrite | VkAccessFlags.HostRead,
            };
            _deviceApi.vkCmdPipelineBarrier(cmd, VkPipelineStageFlags.ComputeShader, VkPipelineStageFlags.AllCommands | VkPipelineStageFlags.Host,
                0, 1, &after, 0, null, 0, null);
            _deviceApi.vkEndCommandBuffer(cmd).CheckResult();

            var fenceInfo = new VkFenceCreateInfo();
            _deviceApi.vkCreateFence(&fenceInfo, null, out VkFence fence).CheckResult();
            var submit = new VkSubmitInfo { commandBufferCount = 1, pCommandBuffers = &cmd };
            _deviceApi.vkQueueSubmit(_graphicsQueue, 1, &submit, fence).CheckResult();
            _computeInFlight.Add((fence, cmd, pool, uniformBuffer));
        }
    }

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

    // Runs before the device goes, with the queue idle: whatever a program left loaded is freed.
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
