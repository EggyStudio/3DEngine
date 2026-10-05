using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// The GPU side of one particle emitter: a storage buffer of its particles, which a compute
/// dispatch steps each frame and the particle pass draws from, and the set the dispatch reads it
/// through.
/// </summary>
/// <remarks>
/// The buffer starts with a header of four float4 values the dispatch writes from its push
/// constants, what the draw reads of the emitter, then holds a particle in two float4 values
/// each, its position and age, and its velocity and life, a particle with no life being dead.
/// </remarks>
internal sealed class GpuParticles : IDisposable
{
    private readonly Action _dispose;

    internal GpuParticles(IBuffer buffer, int capacity, VkDescriptorSet set, Action dispose)
    {
        Buffer = buffer;
        Capacity = capacity;
        Set = set;
        _dispose = dispose;
    }

    /// <summary>The header, then the particles.</summary>
    public IBuffer Buffer { get; }

    /// <summary>How many particles it holds at most.</summary>
    public int Capacity { get; }

    internal VkDescriptorSet Set { get; }

    /// <summary>The bytes the header takes before the first particle.</summary>
    public const int HeaderBytes = 64;

    /// <summary>The bytes of one particle.</summary>
    public const int ParticleBytes = 32;

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

/// <summary>What a particle dispatch is handed, as <c>particle_step.slang</c> reads its push constants.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ParticleStep
{
    /// <summary>Where new particles start, in xyz, and the seconds the step covers, in w.</summary>
    public System.Numerics.Vector4 OriginAndSeconds;
    /// <summary>The velocity a new particle starts with, in xyz, and the cone it is spread over in radians, in w.</summary>
    public System.Numerics.Vector4 VelocityAndSpread;
    /// <summary>The pull on every particle, in xyz, and how long one lives in seconds, in w.</summary>
    public System.Numerics.Vector4 GravityAndLife;
    /// <summary>How much a life varies, how much a speed varies, a seed for this step, and the radius new particles start within.</summary>
    public System.Numerics.Vector4 Variation;
    /// <summary>The color at birth, linear, with its alpha.</summary>
    public System.Numerics.Vector4 StartColor;
    /// <summary>The color at death, linear, with its alpha.</summary>
    public System.Numerics.Vector4 EndColor;
    /// <summary>The size at birth and at death, how bright an unlit one is, and 1 when it is lit plus 2 when it is textured.</summary>
    public System.Numerics.Vector4 Look;
    /// <summary>The first slot new particles are written into, how many, and how many slots there are.</summary>
    public uint First, Count, Capacity;
    /// <summary>How strongly the air slows a particle, read by the shader from the bits of the last word.</summary>
    public float Drag;
}

internal sealed unsafe partial class GraphicsDevice
{
    private VkPipeline _particlePipeline;
    private VkPipelineLayout _particleLayout;
    private VkDescriptorSetLayout _particleSetLayout;

    /// <summary>Whether the particle shader has been given, so emitters can be made.</summary>
    public bool CanStepParticles => _particlePipeline.Handle != 0;

    /// <summary>Makes the particle pipeline from <c>particle_step.slang</c>'s compute stage, once.</summary>
    public void InitializeParticles(ReadOnlySpan<byte> spirv)
    {
        if (!IsInitialized || CanStepParticles) return;

        var binding = new VkDescriptorSetLayoutBinding { binding = 0, descriptorType = VkDescriptorType.StorageBuffer, descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
        var setInfo = new VkDescriptorSetLayoutCreateInfo { bindingCount = 1, pBindings = &binding };
        _deviceApi.vkCreateDescriptorSetLayout(&setInfo, null, out var setLayout).CheckResult();
        _particleSetLayout = setLayout;

        var push = new VkPushConstantRange { stageFlags = VkShaderStageFlags.Compute, offset = 0, size = (uint)sizeof(ParticleStep) };
        var layoutInfo = new VkPipelineLayoutCreateInfo { setLayoutCount = 1, pSetLayouts = &setLayout, pushConstantRangeCount = 1, pPushConstantRanges = &push };
        _deviceApi.vkCreatePipelineLayout(&layoutInfo, null, out _particleLayout).CheckResult();

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
                layout = _particleLayout,
            };
            _deviceApi.vkCreateComputePipelines(default, 1, &info, null, &pipeline).CheckResult();
        }
        _particlePipeline = pipeline;
        _deviceApi.vkDestroyShaderModule(module);
    }

    /// <summary>Makes the buffer of an emitter of <paramref name="capacity"/> particles, every one dead.</summary>
    /// <exception cref="InvalidOperationException">The particle shader has not been given.</exception>
    public GpuParticles CreateParticles(int capacity)
    {
        if (!CanStepParticles) throw new InvalidOperationException("The particle shader has not been given.");
        capacity = Math.Max(1, capacity);
        var bytes = (ulong)(GpuParticles.HeaderBytes + capacity * GpuParticles.ParticleBytes);
        var buffer = CreateBuffer(new BufferDesc(bytes, BufferUsage.Storage, CpuAccessMode.Write));
        Map(buffer).Clear();

        var size = new VkDescriptorPoolSize { type = VkDescriptorType.StorageBuffer, descriptorCount = 1 };
        var poolInfo = new VkDescriptorPoolCreateInfo { maxSets = 1, poolSizeCount = 1, pPoolSizes = &size };
        _deviceApi.vkCreateDescriptorPool(&poolInfo, null, out var pool).CheckResult();
        var layout = _particleSetLayout;
        var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = pool, descriptorSetCount = 1, pSetLayouts = &layout };
        VkDescriptorSet set;
        _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
        var vk = (VulkanBuffer)buffer;
        var info = new VkDescriptorBufferInfo { buffer = vk.Buffer, offset = 0, range = vk.Description.Size };
        var write = new VkWriteDescriptorSet { dstSet = set, dstBinding = 0, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &info };
        _deviceApi.vkUpdateDescriptorSets(1, &write, 0, null);

        return new GpuParticles(buffer, capacity, set, () =>
        {
            _deviceApi.vkDestroyDescriptorPool(pool);
            buffer.Dispose();
        });
    }

    /// <summary>
    /// Records into <paramref name="commands"/>, outside any render pass, the dispatch that steps
    /// <paramref name="particles"/> and starts the new ones <paramref name="step"/> asks for.
    /// </summary>
    public void RecordParticles(ICommandBuffer commands, GpuParticles particles, in ParticleStep step)
    {
        if (commands is not VulkanCommandBuffer vkCommands || !CanStepParticles) return;
        var cmd = vkCommands.Handle;

        // Earlier frames have finished drawing the particles about to be moved.
        MemoryBarrier(cmd, VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader,
            VkAccessFlags2.None, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.None);

        var set = particles.Set;
        _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, _particlePipeline);
        _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, _particleLayout, 0, 1, &set, 0, null);
        fixed (ParticleStep* pushed = &step)
            _deviceApi.vkCmdPushConstants(cmd, _particleLayout, VkShaderStageFlags.Compute, 0, (uint)sizeof(ParticleStep), pushed);
        _deviceApi.vkCmdDispatch(cmd, (uint)(particles.Capacity + 63) / 64, 1, 1);

        // The draws after it read what it wrote.
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
            VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ShaderStorageRead);
    }

    // Runs before the device goes.
    private void DestroyParticles()
    {
        if (_particlePipeline.Handle != 0) _deviceApi.vkDestroyPipeline(_particlePipeline);
        if (_particleLayout.Handle != 0) _deviceApi.vkDestroyPipelineLayout(_particleLayout);
        if (_particleSetLayout.Handle != 0) _deviceApi.vkDestroyDescriptorSetLayout(_particleSetLayout);
        _particlePipeline = default;
        _particleLayout = default;
        _particleSetLayout = default;
    }
}
