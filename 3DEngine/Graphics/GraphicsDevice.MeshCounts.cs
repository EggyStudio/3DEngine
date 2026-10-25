using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// The indirect draws of meshes whose vertex count a shader writes, a record of a draw's arguments
/// each, and the batch that fills their counts before the frame that draws them.
/// </summary>
/// <remarks>
/// <para>
/// A draw's instance count and first instance are known as its pass records it, its vertex count
/// only on the GPU, and no copy or dispatch may run inside a pass. So each draw takes a record of
/// a ring kept a frame in flight, the CPU writing what it knows, and once the frame is recorded a
/// batch submitted ahead of it copies each mesh's count into a list, an entry a mesh, and one
/// dispatch a chunk of records clamps each to its mesh's capacity into the record, a barrier after
/// making them the draws' arguments. The program's dispatches that wrote the counts were submitted
/// before the batch, which reads after them.
/// </para>
/// <para>
/// A chunk holds <see cref="CountChunk"/> records, so a frame drawing a few hundred such meshes in
/// a few passes fills them in one dispatch, the copies one a mesh.
/// </para>
/// </remarks>
internal sealed unsafe partial class GraphicsDevice
{
    private const int CountChunk = 4096;
    private const int CountRecordBytes = 32;
    private static readonly VkDescriptorType[] CountDescriptors = [VkDescriptorType.StorageBuffer, VkDescriptorType.StorageBuffer];
    private VkPipeline _countPipeline;
    private VkPipelineLayout _countLayout;
    private VkDescriptorSetLayout _countSetLayout;
    private readonly Lock _countGate = new();
    private CountSlot[]? _countSlots;
    // A timestamp before the batch and one after, a pair a frame in flight, in a pool of their own,
    // since the batch is submitted ahead of the frame, which resets the frame's marks.
    private VkQueryPool _countQueries;

    // A frame in flight's records and the list of the counts they draw by, each a buffer and the
    // byte its count begins at, many meshes keeping theirs in one buffer.
    private sealed class CountSlot
    {
        public readonly List<IBuffer> Chunks = [];
        public int Records;
        public readonly List<(IBuffer Buffer, uint Offset)> Meshes = [];
        public readonly Dictionary<(IBuffer Buffer, uint Offset), int> Entries = new(new CountKeys());
        public IBuffer? Counts;
        public VkDescriptorPool Pool;
        public int PoolSets;
        public bool Timed;
    }

    /// <summary>How long the GPU took over the last batch that filled the counted draws' counts, as its slot last held it, in milliseconds.</summary>
    public double MeshCountsGpuMs { get; private set; }

    /// <summary>How long recording the last such batch took the CPU, in milliseconds.</summary>
    public double MeshCountsCpuMs { get; private set; }

    /// <summary>Whether the counts' shader has been given, so a mesh whose count a shader writes can be drawn.</summary>
    public bool CanDrawCounted => _countPipeline.Handle != 0;

    /// <summary>How many records of indirect draws the frame being recorded holds.</summary>
    internal int CountedRecords
    {
        get { lock (_countGate) return _countSlots?[_currentFrame].Records ?? 0; }
    }

    /// <summary>Makes the pipeline that fills the counted draws' vertex counts from <c>mesh_counts.slang</c>'s compute stage, once.</summary>
    public void InitializeMeshCounts(ReadOnlySpan<byte> spirv)
    {
        if (!IsInitialized || CanDrawCounted) return;

        var bindings = stackalloc VkDescriptorSetLayoutBinding[2];
        for (uint b = 0; b < 2; b++)
            bindings[b] = new VkDescriptorSetLayoutBinding { binding = b, descriptorType = CountDescriptors[b], descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
        var setInfo = new VkDescriptorSetLayoutCreateInfo { bindingCount = 2, pBindings = bindings };
        _deviceApi.vkCreateDescriptorSetLayout(&setInfo, null, out var setLayout).CheckResult();
        _countSetLayout = setLayout;

        // How many records the chunk holds.
        var records = new VkPushConstantRange { stageFlags = VkShaderStageFlags.Compute, offset = 0, size = 4 };
        var layoutInfo = new VkPipelineLayoutCreateInfo { setLayoutCount = 1, pSetLayouts = &setLayout, pushConstantRangeCount = 1, pPushConstantRanges = &records };
        _deviceApi.vkCreatePipelineLayout(&layoutInfo, null, out _countLayout).CheckResult();

        VkShaderModule module;
        fixed (byte* code = spirv)
        {
            var moduleInfo = new VkShaderModuleCreateInfo { codeSize = (nuint)spirv.Length, pCode = (uint*)code };
            _deviceApi.vkCreateShaderModule(&moduleInfo, null, out module).CheckResult();
        }
        var main = "main"u8;
        VkPipeline pipeline;
        fixed (byte* name = main)
        {
            var info = new VkComputePipelineCreateInfo
            {
                stage = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.Compute, module = module, pName = name },
                layout = _countLayout,
            };
            _deviceApi.vkCreateComputePipelines(default, 1, &info, null, &pipeline).CheckResult();
            DeviceObjects.Made(DeviceObjects.Kind.Pipeline);
        }
        _countPipeline = pipeline;
        _deviceApi.vkDestroyShaderModule(module);
    }

    // A count's buffer by reference and its byte, which the slots key a mesh's count by.
    private sealed class CountKeys : IEqualityComparer<(IBuffer Buffer, uint Offset)>
    {
        public bool Equals((IBuffer Buffer, uint Offset) a, (IBuffer Buffer, uint Offset) b) => ReferenceEquals(a.Buffer, b.Buffer) && a.Offset == b.Offset;

        public int GetHashCode((IBuffer Buffer, uint Offset) key) => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(key.Buffer), key.Offset);
    }

    /// <inheritdoc />
    public void DrawCounted(ICommandBuffer commandBuffer, IBuffer count, uint capacity, uint instanceCount, uint firstInstance, uint firstVertex = 0, uint countOffset = 0)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        if (!CanDrawCounted || capacity == 0 || instanceCount == 0) return;

        IBuffer chunk;
        int record;
        lock (_countGate)
        {
            _countSlots ??= [.. Enumerable.Range(0, MaxFramesInFlight).Select(_ => new CountSlot())];
            var slot = _countSlots[_currentFrame];
            if (!slot.Entries.TryGetValue((count, countOffset), out var entry))
            {
                entry = slot.Meshes.Count;
                slot.Meshes.Add((count, countOffset));
                slot.Entries[(count, countOffset)] = entry;
            }
            if (slot.Records == slot.Chunks.Count * CountChunk)
                slot.Chunks.Add(CreateBuffer(new BufferDesc(CountChunk * CountRecordBytes, BufferUsage.Storage | BufferUsage.Indirect, CpuAccessMode.Write)));
            (chunk, record) = (slot.Chunks[slot.Records / CountChunk], slot.Records % CountChunk);
            slot.Records++;
            var fields = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(Map(chunk).Slice(record * CountRecordBytes, CountRecordBytes));
            fields[0] = 0;
            fields[1] = instanceCount;
            fields[2] = firstVertex;
            fields[3] = firstInstance;
            fields[4] = (uint)entry;
            fields[5] = capacity;
        }
        _deviceApi.vkCmdDrawIndirect(vkCmd.Handle, ((VulkanBuffer)chunk).Buffer, (ulong)(record * CountRecordBytes), 1, CountRecordBytes);
    }

    // Records the batch that fills the frame's counted draws' vertex counts, into the uploads the
    // frame's submission sends ahead of it, and leaves the frame in flight's records for the next
    // frame of its slot. EndFrame calls it once the frame is recorded.
    private void RecordMeshCounts()
    {
        var mark = System.Diagnostics.Stopwatch.GetTimestamp();
        lock (_countGate)
        {
            if (_countSlots?[_currentFrame] is not { Records: > 0 } slot)
            {
                (MeshCountsGpuMs, MeshCountsCpuMs) = (0, 0);
                return;
            }
            var cmd = UploadCommands();
            var queries = (uint)(_currentFrame * 2);
            if (TimestampsSupported)
            {
                // The slot's last batch has finished, as the frame's start waited for it.
                if (_countQueries.Handle == 0)
                {
                    var info = new VkQueryPoolCreateInfo { queryType = VkQueryType.Timestamp, queryCount = (uint)(2 * MaxFramesInFlight) };
                    _deviceApi.vkCreateQueryPool(&info, null, out _countQueries).CheckResult();
                }
                if (slot.Timed)
                {
                    var ticks = stackalloc ulong[2];
                    if (_deviceApi.vkGetQueryPoolResults(_countQueries, queries, 2, 2 * sizeof(ulong), ticks, sizeof(ulong), VkQueryResultFlags.Bit64) == VkResult.Success)
                        MeshCountsGpuMs = ((ticks[1] - ticks[0]) & _timestampMask) * _nanosecondsPerTick / 1e6;
                }
                _deviceApi.vkCmdResetQueryPool(cmd, _countQueries, queries, 2);
                _deviceApi.vkCmdWriteTimestamp(cmd, VkPipelineStageFlags.TopOfPipe, _countQueries, queries);
                slot.Timed = true;
            }

            // The list of the meshes' counts, made larger where the frame drew more meshes. The
            // slot's last frame has finished, as the frame's start waited for it.
            var bytes = (ulong)Math.Max(64, slot.Meshes.Count * 4);
            if (slot.Counts is null || slot.Counts.Description.Size < bytes)
            {
                slot.Counts?.Dispose();
                slot.Counts = CreateBuffer(new BufferDesc(bytes * 2, BufferUsage.Storage | BufferUsage.TransferDst));
            }
            var chunks = (slot.Records + CountChunk - 1) / CountChunk;
            if (slot.Pool.Handle == 0 || slot.PoolSets < chunks)
            {
                if (slot.Pool.Handle != 0) DestroyDescriptorPool(slot.Pool);
                slot.PoolSets = Math.Max(chunks, slot.PoolSets * 2);
                slot.Pool = MakeDescriptorPool([(CountDescriptors, slot.PoolSets)]);
            }
            else _deviceApi.vkResetDescriptorPool(slot.Pool, 0);

            // Each mesh's count, which the program's dispatches wrote before this batch, into the list.
            MemoryBarrier(cmd, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.MemoryWrite, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead);
            var counts = ((VulkanBuffer)slot.Counts).Buffer;
            for (var i = 0; i < slot.Meshes.Count; i++)
            {
                var region = new VkBufferCopy { srcOffset = slot.Meshes[i].Offset, dstOffset = (ulong)(i * 4), size = 4 };
                _deviceApi.vkCmdCopyBuffer(cmd, ((VulkanBuffer)slot.Meshes[i].Buffer).Buffer, counts, 1, &region);
            }
            MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);

            _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, _countPipeline);
            var setLayout = _countSetLayout;
            var infos = stackalloc VkDescriptorBufferInfo[2];
            var writes = stackalloc VkWriteDescriptorSet[2];
            for (var c = 0; c < chunks; c++)
            {
                var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = slot.Pool, descriptorSetCount = 1, pSetLayouts = &setLayout };
                VkDescriptorSet set;
                _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
                infos[0] = new VkDescriptorBufferInfo { buffer = counts, offset = 0, range = slot.Counts.Description.Size };
                infos[1] = new VkDescriptorBufferInfo { buffer = ((VulkanBuffer)slot.Chunks[c]).Buffer, offset = 0, range = CountChunk * CountRecordBytes };
                for (uint b = 0; b < 2; b++)
                    writes[b] = new VkWriteDescriptorSet { dstSet = set, dstBinding = b, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &infos[b] };
                _deviceApi.vkUpdateDescriptorSets(2, writes, 0, null);
                _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, _countLayout, 0, 1, &set, 0, null);
                var records = (uint)Math.Min(CountChunk, slot.Records - c * CountChunk);
                _deviceApi.vkCmdPushConstants(cmd, _countLayout, VkShaderStageFlags.Compute, 0, 4, &records);
                _deviceApi.vkCmdDispatch(cmd, (records + 63) / 64, 1, 1);
            }
            // The frame's draws read their arguments and the meshes' vertices after this.
            MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
                VkPipelineStageFlags2.DrawIndirect | VkPipelineStageFlags2.VertexInput, VkAccessFlags2.IndirectCommandRead | VkAccessFlags2.VertexAttributeRead);
            if (TimestampsSupported) _deviceApi.vkCmdWriteTimestamp(cmd, VkPipelineStageFlags.BottomOfPipe, _countQueries, queries + 1);

            slot.Records = 0;
            slot.Meshes.Clear();
            slot.Entries.Clear();
        }
        MeshCountsCpuMs = System.Diagnostics.Stopwatch.GetElapsedTime(mark).TotalMilliseconds;
    }

    private VkPipeline _cornersPipeline;
    private VkPipelineLayout _cornersLayout;
    private VkDescriptorSetLayout _cornersSetLayout;
    private static readonly VkDescriptorType[] CornersDescriptors = [VkDescriptorType.StorageBuffer, VkDescriptorType.StorageBuffer, VkDescriptorType.StorageBuffer];

    /// <summary>Makes the pipeline that writes a counted mesh's triangles into the scene field's pool from <c>field_counted.slang</c>, once.</summary>
    public void InitializeCountedCorners(ReadOnlySpan<byte> spirv)
    {
        if (!IsInitialized || _cornersPipeline.Handle != 0) return;
        var bindings = stackalloc VkDescriptorSetLayoutBinding[3];
        for (uint b = 0; b < 3; b++)
            bindings[b] = new VkDescriptorSetLayoutBinding { binding = b, descriptorType = VkDescriptorType.StorageBuffer, descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
        var setInfo = new VkDescriptorSetLayoutCreateInfo { bindingCount = 3, pBindings = bindings };
        _deviceApi.vkCreateDescriptorSetLayout(&setInfo, null, out var setLayout).CheckResult();
        _cornersSetLayout = setLayout;
        var push = new VkPushConstantRange { stageFlags = VkShaderStageFlags.Compute, offset = 0, size = 16 };
        var layoutInfo = new VkPipelineLayoutCreateInfo { setLayoutCount = 1, pSetLayouts = &setLayout, pushConstantRangeCount = 1, pPushConstantRanges = &push };
        _deviceApi.vkCreatePipelineLayout(&layoutInfo, null, out _cornersLayout).CheckResult();

        VkShaderModule module;
        fixed (byte* code = spirv)
        {
            var moduleInfo = new VkShaderModuleCreateInfo { codeSize = (nuint)spirv.Length, pCode = (uint*)code };
            _deviceApi.vkCreateShaderModule(&moduleInfo, null, out module).CheckResult();
        }
        var main = "main"u8;
        VkPipeline pipeline;
        fixed (byte* name = main)
        {
            var info = new VkComputePipelineCreateInfo
            {
                stage = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.Compute, module = module, pName = name },
                layout = _cornersLayout,
            };
            _deviceApi.vkCreateComputePipelines(default, 1, &info, null, &pipeline).CheckResult();
            DeviceObjects.Made(DeviceObjects.Kind.Pipeline);
        }
        _cornersPipeline = pipeline;
        _deviceApi.vkDestroyShaderModule(module);
    }

    /// <summary>
    /// Records the writing of meshes over shader buffers into the scene field's pool of corners, a
    /// dispatch a mesh, each from its first corner as many vertices as its count read back said, read
    /// from its first vertex in its buffers, after what wrote their buffers and before the builds
    /// that read the pool.
    /// </summary>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordCountedCorners(ICommandBuffer commandBuffer, IBuffer corners,
        IReadOnlyList<(IBuffer Positions, IBuffer? Colors, int FirstCorner, int Vertices, int FirstVertex)> meshes)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        var pool = MakeDescriptorPool([(CornersDescriptors, Math.Max(1, meshes.Count))]);
        var hold = new DescriptorPoolHold(this, pool);
        if (meshes.Count == 0 || _cornersPipeline.Handle == 0) return hold;
        var cmd = vkCmd.Handle;
        MemoryBarrier(cmd, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.MemoryWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
        _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, _cornersPipeline);
        var setLayout = _cornersSetLayout;
        var infos = stackalloc VkDescriptorBufferInfo[3];
        var writes = stackalloc VkWriteDescriptorSet[3];
        var values = stackalloc uint[4];
        foreach (var (positions, colors, first, vertices, source) in meshes)
        {
            if (vertices <= 0) continue;
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = pool, descriptorSetCount = 1, pSetLayouts = &setLayout };
            VkDescriptorSet set;
            _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
            IBuffer[] bound = [positions, colors ?? positions, corners];
            for (var b = 0; b < 3; b++)
            {
                var buffer = (VulkanBuffer)bound[b];
                infos[b] = new VkDescriptorBufferInfo { buffer = buffer.Buffer, offset = 0, range = buffer.Description.Size };
                writes[b] = new VkWriteDescriptorSet { dstSet = set, dstBinding = (uint)b, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &infos[b] };
            }
            _deviceApi.vkUpdateDescriptorSets(3, writes, 0, null);
            _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, _cornersLayout, 0, 1, &set, 0, null);
            (values[0], values[1], values[2], values[3]) = ((uint)first, (uint)vertices, colors is null ? 0u : 1u, (uint)source);
            _deviceApi.vkCmdPushConstants(cmd, _cornersLayout, VkShaderStageFlags.Compute, 0, 16, values);
            _deviceApi.vkCmdDispatch(cmd, ((uint)vertices + 63) / 64, 1, 1);
        }
        // The builds after read the corners.
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        return hold;
    }

    // A descriptor pool a recording's sets came from, destroyed once no frame in flight reads them.
    private sealed class DescriptorPoolHold(GraphicsDevice device, VkDescriptorPool pool) : IDisposable
    {
        private bool _gone;

        public void Dispose()
        {
            if (_gone) return;
            _gone = true;
            device.DestroyDescriptorPool(pool);
        }
    }

    // Frees the records, the lists and the pipelines, with the queue idle, before the device goes.
    private void DestroyMeshCounts()
    {
        lock (_countGate)
        {
            foreach (var slot in _countSlots ?? [])
            {
                foreach (var chunk in slot.Chunks) chunk.Dispose();
                slot.Counts?.Dispose();
                if (slot.Pool.Handle != 0) DestroyDescriptorPool(slot.Pool);
            }
            _countSlots = null;
            if (_countQueries.Handle != 0) _deviceApi.vkDestroyQueryPool(_countQueries);
            _countQueries = default;
            if (_countPipeline.Handle != 0)
            {
                _deviceApi.vkDestroyPipeline(_countPipeline);
                DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
            }
            if (_countLayout.Handle != 0) _deviceApi.vkDestroyPipelineLayout(_countLayout);
            if (_countSetLayout.Handle != 0) _deviceApi.vkDestroyDescriptorSetLayout(_countSetLayout);
            (_countPipeline, _countLayout, _countSetLayout) = (default, default, default);
            if (_cornersPipeline.Handle != 0)
            {
                _deviceApi.vkDestroyPipeline(_cornersPipeline);
                DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
            }
            if (_cornersLayout.Handle != 0) _deviceApi.vkDestroyPipelineLayout(_cornersLayout);
            if (_cornersSetLayout.Handle != 0) _deviceApi.vkDestroyDescriptorSetLayout(_cornersSetLayout);
            (_cornersPipeline, _cornersLayout, _cornersSetLayout) = (default, default, default);
        }
    }
}
