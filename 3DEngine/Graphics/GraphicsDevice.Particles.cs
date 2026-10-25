using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// The GPU side of one particle emitter, a storage buffer of its particles, which a compute
/// dispatch steps each frame and the particle pass draws from, and the set the dispatch reads it
/// through.
/// </summary>
/// <remarks>
/// The buffer starts with a header of eight float4 values, four the dispatch writes from its push
/// constants, what the draw reads of the emitter, the fourth with the newest particle's slot, then
/// three rows of the matrix the draw places the particles by and one unread, which the recording
/// writes before the dispatch. It then holds a
/// particle in two float4 values each, its position and age, and its velocity and life, a particle
/// with no life being dead.
/// After the particles are <see cref="SortKeys"/> float4 values, the keys an emitter laid over by
/// alpha is sorted by, far to near.
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

    /// <summary>The keys after the particles, the least power of two as many as they are, which the bitonic sort needs.</summary>
    public int SortKeys => (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Capacity);

    internal VkDescriptorSet Set { get; }

    /// <summary>The bytes the header takes before the first particle.</summary>
    public const int HeaderBytes = 128;

    /// <summary>The float4 value of the header the rows of the matrix the draw places the particles by start at.</summary>
    public const int PlacedBy = 4;

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
    /// <summary>The size at birth and at death, how bright an unlit one is, and flags of how it is drawn, which particles.slang unpacks.</summary>
    public System.Numerics.Vector4 Look;
    /// <summary>
    /// The first slot new particles are written into, how many, and how many slots there are, the
    /// last below 2^21, its bits above that what a particle does where it meets the scene's depth,
    /// two bits, and the share of its speed a bounce keeps in 255ths, eight more, since the push
    /// block has no room left.
    /// </summary>
    public uint First, Count, Capacity;
    /// <summary>How strongly the air slows a particle, read by the shader from the bits of the last word.</summary>
    public float Drag;
}

/// <summary>
/// The view the frame's particles meet the scene's depth through, as <c>particle_step.slang</c>
/// reads it in its second set beside that depth.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ParticleView
{
    /// <summary>World to clip space for the view the depth was drawn through.</summary>
    public System.Numerics.Matrix4x4 ViewProjection;
    /// <summary>Clip space back to world space, which finds the surface a depth texel holds.</summary>
    public System.Numerics.Matrix4x4 InverseViewProjection;
    /// <summary>1 in x when a depth is bound to collide with, 0 when nothing collides this frame.</summary>
    public System.Numerics.Vector4 Depth;
}

internal sealed unsafe partial class GraphicsDevice
{
    // The particles' view of the scene's depth, a set and a buffer for each frame in flight, so a
    // frame writes its own while the frames before it are still read.
    private VkDescriptorSetLayout _particleViewLayout;
    private VkDescriptorPool _particleViewPool;
    private readonly VkDescriptorSet[] _particleViewSets = new VkDescriptorSet[MaxFramesInFlight];
    private readonly IBuffer?[] _particleViewBuffers = new IBuffer?[MaxFramesInFlight];

    // The type of each binding of an emitter's set and of the view's, which their set layouts and
    // the pools of their sets are made from.
    private static readonly VkDescriptorType[] ParticleDescriptors = [VkDescriptorType.StorageBuffer];
    private static readonly VkDescriptorType[] ParticleViewDescriptors =
        [VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer];

    private VkPipeline _particlePipeline;
    private VkPipelineLayout _particleLayout;
    private VkDescriptorSetLayout _particleSetLayout;
    private VkPipeline _sortPipeline;
    private VkPipelineLayout _sortLayout;

    /// <summary>What a step of the particle sort is handed, as <c>particle_sort.slang</c> reads it.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct SortStep
    {
        public uint J, K, Count, Mode;
        public System.Numerics.Vector4 EyeAndCapacity;
        public uint Newest, Span;
    }

    /// <summary>Whether the particle shader has been given, so emitters can be made.</summary>
    public bool CanStepParticles => _particlePipeline.Handle != 0;

    /// <summary>Makes the particle pipeline from <c>particle_step.slang</c>'s compute stage, once.</summary>
    public void InitializeParticles(ReadOnlySpan<byte> spirv)
    {
        if (!IsInitialized || CanStepParticles) return;

        var binding = new VkDescriptorSetLayoutBinding { binding = 0, descriptorType = ParticleDescriptors[0], descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
        var setInfo = new VkDescriptorSetLayoutCreateInfo { bindingCount = 1, pBindings = &binding };
        _deviceApi.vkCreateDescriptorSetLayout(&setInfo, null, out var setLayout).CheckResult();
        _particleSetLayout = setLayout;

        // The second set, the scene's depth and the view it was drawn through, and the scene's
        // distance field and where its cascades lie.
        var viewBindings = stackalloc VkDescriptorSetLayoutBinding[ParticleViewDescriptors.Length];
        for (int b = 0; b < ParticleViewDescriptors.Length; b++)
            viewBindings[b] = new VkDescriptorSetLayoutBinding { binding = (uint)b, descriptorType = ParticleViewDescriptors[b], descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
        var viewInfo = new VkDescriptorSetLayoutCreateInfo { bindingCount = (uint)ParticleViewDescriptors.Length, pBindings = viewBindings };
        _deviceApi.vkCreateDescriptorSetLayout(&viewInfo, null, out var viewLayout).CheckResult();
        _particleViewLayout = viewLayout;

        _particleViewPool = MakeDescriptorPool([(ParticleViewDescriptors, MaxFramesInFlight)]);
        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = _particleViewPool, descriptorSetCount = 1, pSetLayouts = &viewLayout };
            VkDescriptorSet viewSet;
            _deviceApi.vkAllocateDescriptorSets(&allocInfo, &viewSet).CheckResult();
            _particleViewSets[i] = viewSet;
            var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)sizeof(ParticleView), BufferUsage.Uniform, CpuAccessMode.Write));
            Map(buffer).Clear();
            Unmap(buffer);
            _particleViewBuffers[i] = buffer;
            var bufferInfo = new VkDescriptorBufferInfo { buffer = buffer.Buffer, offset = 0, range = (ulong)sizeof(ParticleView) };
            var write = new VkWriteDescriptorSet { dstSet = viewSet, dstBinding = 1, descriptorCount = 1, descriptorType = VkDescriptorType.UniformBuffer, pBufferInfo = &bufferInfo };
            _deviceApi.vkUpdateDescriptorSets(1, &write, 0, null);
        }

        (_particlePipeline, _particleLayout) = ParticleCompute(spirv, (uint)sizeof(ParticleStep), _particleViewLayout);
    }

    // A field of nothing for the particles' set where the frame has no field bound.
    private GpuSceneField? _noField;

    /// <summary>
    /// Sets the scene's depth the particles stepped after this in the frame meet, and the view it
    /// was drawn through, or with <paramref name="view"/>'s <see cref="ParticleView.Depth"/> 0 none,
    /// <paramref name="depth"/> then any image the shader can sample, and the scene's distance
    /// field they meet first where it holds them, or none where <paramref name="field"/> is null.
    /// Called once a frame before the first step.
    /// </summary>
    public void SetParticleView(IImageView depth, ISampler sampler, in ParticleView view, GpuSceneField? field = null)
    {
        if (!CanStepParticles || _particleViewBuffers[_currentFrame] is not VulkanBuffer buffer) return;
        var bytes = Map(buffer);
        fixed (ParticleView* given = &view)
            new ReadOnlySpan<byte>(given, sizeof(ParticleView)).CopyTo(bytes);
        Unmap(buffer);

        var imageInfo = new VkDescriptorImageInfo
        {
            sampler = ((VulkanSampler)sampler).Sampler,
            imageView = ((VulkanImageView)depth).View,
            imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
        };
        field ??= _noField ??= CreateSceneField(1, 1);
        var fieldImage = new VkDescriptorImageInfo
        {
            sampler = ((VulkanSampler)field.Sampler).Sampler,
            imageView = ((VulkanImageView)field.View).View,
            imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
        };
        var fieldInfo = new VkDescriptorBufferInfo { buffer = ((VulkanBuffer)field.Info).Buffer, offset = 0, range = GpuSceneField.InfoBytes };
        var writes = stackalloc VkWriteDescriptorSet[3];
        writes[0] = new VkWriteDescriptorSet { dstSet = _particleViewSets[_currentFrame], dstBinding = 0, descriptorCount = 1, descriptorType = VkDescriptorType.CombinedImageSampler, pImageInfo = &imageInfo };
        writes[1] = new VkWriteDescriptorSet { dstSet = _particleViewSets[_currentFrame], dstBinding = 2, descriptorCount = 1, descriptorType = VkDescriptorType.CombinedImageSampler, pImageInfo = &fieldImage };
        writes[2] = new VkWriteDescriptorSet { dstSet = _particleViewSets[_currentFrame], dstBinding = 3, descriptorCount = 1, descriptorType = VkDescriptorType.UniformBuffer, pBufferInfo = &fieldInfo };
        _deviceApi.vkUpdateDescriptorSets(3, writes, 0, null);
    }

    /// <summary>Makes the pipeline that sorts an emitter's particles from <c>particle_sort.slang</c>'s compute stage, once.</summary>
    public void InitializeParticleSort(ReadOnlySpan<byte> spirv)
    {
        if (!CanStepParticles || _sortPipeline.Handle != 0) return;
        (_sortPipeline, _sortLayout) = ParticleCompute(spirv, (uint)sizeof(SortStep));
    }

    // A compute pipeline over an emitter's buffer, with push constants of a size, and a second set
    // where one is given.
    private (VkPipeline Pipeline, VkPipelineLayout Layout) ParticleCompute(ReadOnlySpan<byte> spirv, uint pushSize, VkDescriptorSetLayout second = default)
    {
        var setLayouts = stackalloc VkDescriptorSetLayout[2];
        setLayouts[0] = _particleSetLayout;
        setLayouts[1] = second;
        var push = new VkPushConstantRange { stageFlags = VkShaderStageFlags.Compute, offset = 0, size = pushSize };
        var layoutInfo = new VkPipelineLayoutCreateInfo { setLayoutCount = second.Handle != 0 ? 2u : 1u, pSetLayouts = setLayouts, pushConstantRangeCount = 1, pPushConstantRanges = &push };
        _deviceApi.vkCreatePipelineLayout(&layoutInfo, null, out var pipelineLayout).CheckResult();

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
                layout = pipelineLayout,
            };
            _deviceApi.vkCreateComputePipelines(default, 1, &info, null, &pipeline).CheckResult();
            DeviceObjects.Made(DeviceObjects.Kind.Pipeline);
        }
        _deviceApi.vkDestroyShaderModule(module);
        return (pipeline, pipelineLayout);
    }

    /// <summary>Makes the buffer of an emitter of <paramref name="capacity"/> particles, every one dead.</summary>
    /// <exception cref="InvalidOperationException">The particle shader has not been given.</exception>
    public GpuParticles CreateParticles(int capacity)
    {
        if (!CanStepParticles) throw new InvalidOperationException("The particle shader has not been given.");
        capacity = Math.Max(1, capacity);
        var keys = (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)capacity);
        var bytes = (ulong)(GpuParticles.HeaderBytes + capacity * GpuParticles.ParticleBytes + keys * 16);
        var buffer = CreateBuffer(new BufferDesc(bytes, BufferUsage.Storage | BufferUsage.TransferDst, CpuAccessMode.Write));
        Map(buffer).Clear();

        var pool = MakeDescriptorPool([(ParticleDescriptors, 1)]);
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
            DestroyDescriptorPool(pool);
            buffer.Dispose();
        });
    }

    /// <summary>
    /// Makes the buffer of the particles a program placed at <paramref name="points"/>, one a point,
    /// standing still there at the start of a life no dispatch ages, which
    /// <see cref="RecordParticleHeader"/> gives the rest of what the draw reads each frame.
    /// </summary>
    public GpuParticles CreatePlacedParticles(ReadOnlySpan<System.Numerics.Vector3> points)
    {
        var particles = CreateParticles(points.Length);
        var slots = MemoryMarshal.Cast<byte, System.Numerics.Vector4>(Map(particles.Buffer)[GpuParticles.HeaderBytes..]);
        for (int i = 0; i < points.Length; i++)
        {
            slots[i * 2] = new System.Numerics.Vector4(points[i], 0);
            slots[i * 2 + 1] = new System.Numerics.Vector4(0, 0, 0, 1);
        }
        return particles;
    }

    /// <summary>
    /// Records into <paramref name="commands"/>, outside any render pass, the dispatch that steps
    /// <paramref name="particles"/> and starts the new ones <paramref name="step"/> asks for, the
    /// draw placing them by <paramref name="placedBy"/>, the identity for particles in the world.
    /// </summary>
    /// <remarks>
    /// The dispatch covers the <paramref name="span"/> slots back from the newest particle, the new
    /// ones among them, which the caller knows hold every particle that may be alive.
    /// </remarks>
    public void RecordParticles(ICommandBuffer commands, GpuParticles particles, in ParticleStep step, System.Numerics.Matrix4x4 placedBy, int span)
    {
        if (commands is not VulkanCommandBuffer vkCommands || !CanStepParticles) return;
        var cmd = vkCommands.Handle;

        // Earlier frames have finished drawing the particles about to be moved, and the matrix the
        // draw places them by is written for this frame's.
        Span<System.Numerics.Vector4> rows = stackalloc System.Numerics.Vector4[4];
        Rows(placedBy, rows);
        WriteHeader(cmd, particles, rows, GpuParticles.PlacedBy);

        var sets = stackalloc VkDescriptorSet[2];
        sets[0] = particles.Set;
        sets[1] = _particleViewSets[_currentFrame];
        _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, _particlePipeline);
        _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, _particleLayout, 0, 2, sets, 0, null);
        fixed (ParticleStep* pushed = &step)
            _deviceApi.vkCmdPushConstants(cmd, _particleLayout, VkShaderStageFlags.Compute, 0, (uint)sizeof(ParticleStep), pushed);
        _deviceApi.vkCmdDispatch(cmd, (uint)(Math.Clamp(span, 1, particles.Capacity) + 63) / 64, 1, 1);

        // The draws after it read what it wrote.
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
            VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ShaderStorageRead);
    }

    /// <summary>
    /// Records into <paramref name="commands"/>, outside any render pass, the header of
    /// <paramref name="particles"/> that no dispatch steps, the particles a program placed, as the
    /// step writes an emitter's own, its first four values and then the rows of the matrix the draw
    /// places them by.
    /// </summary>
    public void RecordParticleHeader(ICommandBuffer commands, GpuParticles particles, ReadOnlySpan<System.Numerics.Vector4> look,
        System.Numerics.Matrix4x4 placedBy)
    {
        if (commands is not VulkanCommandBuffer vkCommands) return;
        Span<System.Numerics.Vector4> header = stackalloc System.Numerics.Vector4[GpuParticles.HeaderBytes / 16];
        look[..GpuParticles.PlacedBy].CopyTo(header);
        Rows(placedBy, header[GpuParticles.PlacedBy..]);
        WriteHeader(vkCommands.Handle, particles, header, 0);
    }

    // The three rows of a matrix that take a point to where it is placed, as System.Numerics lays a
    // matrix out for a row vector, each with the translation last, and a fourth unread.
    private static void Rows(System.Numerics.Matrix4x4 m, Span<System.Numerics.Vector4> rows)
    {
        rows[0] = new System.Numerics.Vector4(m.M11, m.M21, m.M31, m.M41);
        rows[1] = new System.Numerics.Vector4(m.M12, m.M22, m.M32, m.M42);
        rows[2] = new System.Numerics.Vector4(m.M13, m.M23, m.M33, m.M43);
        rows[3] = System.Numerics.Vector4.Zero;
    }

    // Writes values of the header from the first given on, once the frames before have finished
    // stepping, sorting and drawing the particles, and before this frame's do.
    private void WriteHeader(VkCommandBuffer cmd, GpuParticles particles, ReadOnlySpan<System.Numerics.Vector4> values, int first)
    {
        var users = VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;
        MemoryBarrier(cmd, users, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer | VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.None);
        fixed (System.Numerics.Vector4* data = values)
            _deviceApi.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)particles.Buffer).Buffer, (ulong)(first * 16), (ulong)(values.Length * 16), data);
        MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, users,
            VkAccessFlags2.ShaderStorageRead | VkAccessFlags2.ShaderStorageWrite);
    }

    /// <summary>
    /// Records, after the step of <paramref name="particles"/>, the dispatches that sort the
    /// <paramref name="span"/> slots back from the <paramref name="newest"/> from the farthest from
    /// <paramref name="eye"/> to the nearest, which the draw then reads them in.
    /// </summary>
    /// <remarks>
    /// The keys are sorted in blocks of 512 in shared memory, so an emitter of up to 512 particles
    /// takes two dispatches, the keys and the sort, and a larger one a dispatch for each step across
    /// blocks and one for the steps within them after each, as <c>particle_sort.slang</c> says.
    /// </remarks>
    public void RecordParticleSort(ICommandBuffer commands, GpuParticles particles, System.Numerics.Vector3 eye, int newest, int span)
    {
        if (commands is not VulkanCommandBuffer vkCommands || _sortPipeline.Handle == 0) return;
        var cmd = vkCommands.Handle;
        var set = particles.Set;
        span = Math.Clamp(span, 1, particles.Capacity);
        var count = System.Numerics.BitOperations.RoundUpToPowerOf2((uint)span);
        _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, _sortPipeline);
        _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, _sortLayout, 0, 1, &set, 0, null);

        // As particle_sort.slang has them, a workgroup's threads and the keys it sorts in shared memory.
        const uint Threads = 256, Block = 512;
        void Dispatch(uint j, uint k, uint mode, uint groups)
        {
            // Each reads what the dispatch before it wrote, the step's particles or the keys.
            MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderStorageRead | VkAccessFlags2.ShaderStorageWrite);
            var step = new SortStep
            {
                J = j, K = k, Count = count, Mode = mode, EyeAndCapacity = new System.Numerics.Vector4(eye, particles.Capacity),
                Newest = (uint)newest, Span = (uint)span,
            };
            _deviceApi.vkCmdPushConstants(cmd, _sortLayout, VkShaderStageFlags.Compute, 0, (uint)sizeof(SortStep), &step);
            _deviceApi.vkCmdDispatch(cmd, groups, 1, 1);
        }
        var perKey = (count + Threads - 1) / Threads;
        var blocks = Math.Max(1, count / Block);
        Dispatch(0, 0, 0, perKey);
        Dispatch(0, 0, 2, blocks);
        for (uint k = Block * 2; k <= count; k <<= 1)
        {
            for (uint j = k >> 1; j >= Block; j >>= 1)
                Dispatch(j, k, 1, perKey);
            Dispatch(0, k, 3, blocks);
        }

        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
            VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ShaderStorageRead);
    }

    // Runs before the device goes.
    private void DestroyParticles()
    {
        if (_sortPipeline.Handle != 0)
        {
            DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
            _deviceApi.vkDestroyPipeline(_sortPipeline);
        }
        if (_sortLayout.Handle != 0) _deviceApi.vkDestroyPipelineLayout(_sortLayout);
        _sortPipeline = default;
        _sortLayout = default;
        if (_particlePipeline.Handle != 0)
        {
            DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
            _deviceApi.vkDestroyPipeline(_particlePipeline);
        }
        if (_particleLayout.Handle != 0) _deviceApi.vkDestroyPipelineLayout(_particleLayout);
        if (_particleSetLayout.Handle != 0) _deviceApi.vkDestroyDescriptorSetLayout(_particleSetLayout);
        DestroyDescriptorPool(_particleViewPool);
        if (_particleViewLayout.Handle != 0) _deviceApi.vkDestroyDescriptorSetLayout(_particleViewLayout);
        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _particleViewBuffers[i]?.Dispose();
            _particleViewBuffers[i] = null;
        }
        _noField?.Dispose();
        _noField = null;
        _particlePipeline = default;
        _particleLayout = default;
        _particleSetLayout = default;
        _particleViewPool = default;
        _particleViewLayout = default;
    }
}
