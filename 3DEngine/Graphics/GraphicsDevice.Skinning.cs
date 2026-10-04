using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// One skinned mesh's buffers on the GPU: its vertices at rest, the four joints and weights of each,
/// a ring of buffers for the joints' matrices, and the vertex buffer the posed vertices are written
/// into, which the model pass draws.
/// </summary>
public sealed class GpuSkin : IDisposable
{
    private readonly Action _dispose;

    internal GpuSkin(IBuffer output, int vertexCount, IBuffer[] rows, VkDescriptorSet[] sets, Action dispose)
    {
        Output = output;
        VertexCount = vertexCount;
        Rows = rows;
        Sets = sets;
        _dispose = dispose;
    }

    /// <summary>The vertex buffer the posed vertices are written into.</summary>
    public IBuffer Output { get; }

    /// <summary>How many vertices the mesh has.</summary>
    public int VertexCount { get; }

    internal IBuffer[] Rows { get; }
    internal VkDescriptorSet[] Sets { get; }
    internal int Slot { get; set; } = -1;

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

/// <summary>
/// Skinning on the GPU: a compute shader moves each vertex of a mesh from its rest by the joints that
/// hold it, writing into the mesh's own vertex buffer, recorded into the frame before anything draws.
/// </summary>
/// <remarks>
/// The joints' matrices go through a ring of buffers the CPU writes, one more than there are frames
/// a buffer can be read in, so the one written never belongs to a frame the GPU is still running.
/// The vertex buffer is one, since the frames run on one queue. A barrier before each dispatch waits
/// for earlier frames to finish reading it, and one after makes the new vertices visible to the draws.
/// </remarks>
public sealed unsafe partial class GraphicsDevice
{
    private const int SkinBindings = 5;
    private VkPipeline _skinPipeline;
    private VkPipelineLayout _skinLayout;
    private VkDescriptorSetLayout _skinSetLayout;

    /// <summary>Whether the skinning shader has been given, so skins can be made.</summary>
    public bool CanSkin => _skinPipeline.Handle != 0;

    /// <summary>Makes the skinning pipeline from <c>skin.slang</c>'s compute stage, once.</summary>
    public void InitializeSkinning(ReadOnlySpan<byte> spirv)
    {
        if (!IsInitialized || CanSkin) return;

        var bindings = stackalloc VkDescriptorSetLayoutBinding[SkinBindings];
        for (uint b = 0; b < SkinBindings; b++)
            bindings[b] = new VkDescriptorSetLayoutBinding { binding = b, descriptorType = VkDescriptorType.StorageBuffer, descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
        var setInfo = new VkDescriptorSetLayoutCreateInfo { bindingCount = SkinBindings, pBindings = bindings };
        _deviceApi.vkCreateDescriptorSetLayout(&setInfo, null, out var setLayout).CheckResult();
        _skinSetLayout = setLayout;

        var layoutInfo = new VkPipelineLayoutCreateInfo { setLayoutCount = 1, pSetLayouts = &setLayout };
        _deviceApi.vkCreatePipelineLayout(&layoutInfo, null, out _skinLayout).CheckResult();

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
                layout = _skinLayout,
            };
            _deviceApi.vkCreateComputePipelines(default, 1, &info, null, &pipeline).CheckResult();
        }
        _skinPipeline = pipeline;
        _deviceApi.vkDestroyShaderModule(module);
    }

    /// <summary>
    /// Makes the GPU side of a skinned mesh from its vertices at rest, as <see cref="ModelVertex"/>
    /// values, and four joints and weights a vertex.
    /// </summary>
    /// <exception cref="InvalidOperationException">The skinning shader has not been given.</exception>
    public GpuSkin CreateSkin(ReadOnlySpan<ModelVertex> rest, ReadOnlySpan<ushort> joints, ReadOnlySpan<float> weights, int jointCount, int ring)
    {
        if (!CanSkin) throw new InvalidOperationException("The skinning shader has not been given.");
        var count = rest.Length;
        IBuffer Filled(ReadOnlySpan<byte> data, BufferUsage usage)
        {
            var buffer = CreateBuffer(new BufferDesc((ulong)Math.Max(16, data.Length), usage, CpuAccessMode.Write));
            data.CopyTo(Map(buffer));
            return buffer;
        }

        // Four joints and four weights a vertex, padded with nothing held where the arrays fall short.
        var paddedJoints = new ushort[count * 4];
        var paddedWeights = new float[count * 4];
        joints[..Math.Min(joints.Length, paddedJoints.Length)].CopyTo(paddedJoints);
        weights[..Math.Min(weights.Length, paddedWeights.Length)].CopyTo(paddedWeights);

        var restBuffer = Filled(MemoryMarshal.AsBytes(rest), BufferUsage.Storage);
        var jointBuffer = Filled(MemoryMarshal.AsBytes(paddedJoints.AsSpan()), BufferUsage.Storage);
        var weightBuffer = Filled(MemoryMarshal.AsBytes(paddedWeights.AsSpan()), BufferUsage.Storage);
        // The posed vertices start at rest, so a mesh drawn before its first pose looks as it rests.
        var output = Filled(MemoryMarshal.AsBytes(rest), BufferUsage.Storage | BufferUsage.Vertex);
        var rows = new IBuffer[ring];
        for (int i = 0; i < ring; i++) rows[i] = CreateBuffer(new BufferDesc((ulong)(Math.Max(1, jointCount) * 64), BufferUsage.Storage, CpuAccessMode.Write));

        var size = new VkDescriptorPoolSize { type = VkDescriptorType.StorageBuffer, descriptorCount = (uint)(ring * SkinBindings) };
        var poolInfo = new VkDescriptorPoolCreateInfo { maxSets = (uint)ring, poolSizeCount = 1, pPoolSizes = &size };
        _deviceApi.vkCreateDescriptorPool(&poolInfo, null, out var pool).CheckResult();

        var sets = new VkDescriptorSet[ring];
        var layout = _skinSetLayout;
        // Above the loop, since stack space taken inside it is given back only when the method returns.
        var infos = stackalloc VkDescriptorBufferInfo[SkinBindings];
        var writes = stackalloc VkWriteDescriptorSet[SkinBindings];
        for (int i = 0; i < ring; i++)
        {
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = pool, descriptorSetCount = 1, pSetLayouts = &layout };
            VkDescriptorSet set;
            _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
            sets[i] = set;

            IBuffer[] buffers = [rows[i], restBuffer, jointBuffer, weightBuffer, output];
            for (int b = 0; b < SkinBindings; b++)
            {
                var vk = (VulkanBuffer)buffers[b];
                infos[b] = new VkDescriptorBufferInfo { buffer = vk.Buffer, offset = 0, range = vk.Description.Size };
                writes[b] = new VkWriteDescriptorSet { dstSet = set, dstBinding = (uint)b, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &infos[b] };
            }
            _deviceApi.vkUpdateDescriptorSets(SkinBindings, writes, 0, null);
        }

        return new GpuSkin(output, count, rows, sets, () =>
        {
            _deviceApi.vkDestroyDescriptorPool(pool);
            foreach (var row in rows) row.Dispose();
            restBuffer.Dispose();
            jointBuffer.Dispose();
            weightBuffer.Dispose();
            output.Dispose();
        });
    }

    /// <summary>
    /// Records into <paramref name="commands"/>, outside any render pass, the dispatch that poses
    /// <paramref name="skin"/> by <paramref name="joints"/>, each a joint's matrix from rest to its
    /// pose in the model's space, as System.Numerics multiplies a row vector.
    /// </summary>
    public void RecordSkin(ICommandBuffer commands, GpuSkin skin, ReadOnlySpan<Matrix4x4> joints)
    {
        if (commands is not VulkanCommandBuffer vkCommands || !CanSkin) return;
        var cmd = vkCommands.Handle;

        skin.Slot = (skin.Slot + 1) % skin.Rows.Length;
        var rows = Map(skin.Rows[skin.Slot]);
        var bytes = MemoryMarshal.AsBytes(joints);
        bytes[..Math.Min(bytes.Length, rows.Length)].CopyTo(rows);

        // Earlier frames have finished drawing the vertices about to be replaced.
        MemoryBarrier(cmd, VkPipelineStageFlags2.VertexInput | VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.None,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.None);

        var set = skin.Sets[skin.Slot];
        _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, _skinPipeline);
        _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, _skinLayout, 0, 1, &set, 0, null);
        _deviceApi.vkCmdDispatch(cmd, (uint)(skin.VertexCount + 63) / 64, 1, 1);

        // The draws after it read what it wrote.
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.VertexInput, VkAccessFlags2.VertexAttributeRead);
    }

    // Runs before the device goes.
    private void DestroySkinning()
    {
        if (_skinPipeline.Handle != 0) _deviceApi.vkDestroyPipeline(_skinPipeline);
        if (_skinLayout.Handle != 0) _deviceApi.vkDestroyPipelineLayout(_skinLayout);
        if (_skinSetLayout.Handle != 0) _deviceApi.vkDestroyDescriptorSetLayout(_skinSetLayout);
        _skinPipeline = default;
        _skinLayout = default;
        _skinSetLayout = default;
    }
}
