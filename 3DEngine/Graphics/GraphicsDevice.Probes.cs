using System.Numerics;
using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// A reflection probe's capture on the GPU: a cube of half-float linear light prefiltered by
/// roughness down its mips, as an environment map's is, and its irradiance as nine spherical
/// harmonic coefficients in a storage buffer, which the model pass reads where a lighting buffer
/// written on the CPU would need them read back first.
/// </summary>
internal sealed class ProbeMap : IDisposable
{
    private readonly Action _dispose;

    internal ProbeMap(IImageView view, ISampler sampler, IImage image, uint size, uint mipLevels, IBuffer irradiance,
        VkImageView[] levels, Action dispose)
    {
        View = view;
        Sampler = sampler;
        Image = image;
        Size = size;
        MipLevels = mipLevels;
        Irradiance = irradiance;
        Levels = levels;
        _dispose = dispose;
    }

    /// <summary>The cube view of every face and mip.</summary>
    public IImageView View { get; }

    /// <summary>A trilinear sampler clamped to each face's edge.</summary>
    public ISampler Sampler { get; }

    /// <summary>The cube's image, six layers.</summary>
    public IImage Image { get; }

    /// <summary>The width of a face at the first mip.</summary>
    public uint Size { get; }

    /// <summary>How many mips, from a mirror at the first to fully rough at the last.</summary>
    public uint MipLevels { get; }

    /// <summary>
    /// The capture's irradiance over pi, nine float4 whose xyz are the coefficients in the order
    /// <see cref="EnvironmentMap.Irradiance"/> holds an environment's, each multiplied by its band's
    /// share of a cosine lobe.
    /// </summary>
    public IBuffer Irradiance { get; }

    // Each mip as six layers, for the filter to write.
    internal VkImageView[] Levels { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

/// <summary>
/// Filtering a reflection probe's capture on the GPU, recorded into the frame that draws its last
/// face, so a capture costs that frame's work and nothing is read back.
/// </summary>
/// <remarks>
/// <para>
/// The six faces, half-float targets drawn through views of a right angle, are gathered into the
/// first level of a cube of <see cref="ProbeSourceSize"/> texels (<c>probe_gather.slang</c>), each
/// level after it the average of four of the one above (<c>probe_mips.slang</c>). The probe's own
/// cube is that gathered one prefiltered by roughness, level by level (<c>probe_prefilter.slang</c>),
/// and its irradiance the gathered cube projected onto the nine harmonics at a level
/// <see cref="ProbeIrradianceSize"/> texels wide (<c>probe_irradiance.slang</c>).
/// </para>
/// <para>
/// The gathered cube is one, reused by every capture, since a probe's faces are drawn a face a
/// frame and its filter runs once all six are, so two never run in one frame. It stays in the
/// general layout, written as a storage image and read as a cube, and a probe's cube moves to
/// be sampled once its levels are written.
/// </para>
/// </remarks>
internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>The width of a face of the cube a probe's faces are gathered into.</summary>
    internal const int ProbeSourceSize = 64;

    /// <summary>The width of the gathered cube's level the irradiance is summed over, fine enough for bands that hold no finer detail.</summary>
    internal const int ProbeIrradianceSize = 16;

    private readonly ComputeStage[] _probeStages = new ComputeStage[4];
    private (VkImage Image, VkDeviceMemory Memory, VkImageView Cube, VkImageView[] Levels)? _probeSource;
    private ISampler? _probeFaceSampler;
    private ISampler? _probeSourceSampler;

    // One compute stage the filter records: its pipeline, layout and the set layout of its bindings.
    private readonly record struct ComputeStage(VkPipeline Pipeline, VkPipelineLayout Layout, VkDescriptorSetLayout SetLayout);

    /// <summary>Whether the filter's shaders have been given, so probes can be captured.</summary>
    public bool CanFilterProbes => _probeStages[3].Pipeline.Handle != 0;

    /// <summary>
    /// Makes the probe filter's pipelines from the compute stages of <c>probe_gather.slang</c>,
    /// <c>probe_mips.slang</c>, <c>probe_prefilter.slang</c> and <c>probe_irradiance.slang</c>, once.
    /// </summary>
    public void InitializeProbeFilter(ReadOnlySpan<byte> gather, ReadOnlySpan<byte> mips, ReadOnlySpan<byte> prefilter, ReadOnlySpan<byte> irradiance)
    {
        if (!IsInitialized || CanFilterProbes) return;
        _probeStages[0] = MakeComputeStage(gather, [.. Enumerable.Repeat(VkDescriptorType.CombinedImageSampler, 6), VkDescriptorType.UniformBuffer, VkDescriptorType.StorageImage], 4);
        _probeStages[1] = MakeComputeStage(mips, [VkDescriptorType.StorageImage, VkDescriptorType.StorageImage], 4);
        _probeStages[2] = MakeComputeStage(prefilter, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageImage], 16);
        _probeStages[3] = MakeComputeStage(irradiance, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageBuffer], 8);
    }

    private ComputeStage MakeComputeStage(ReadOnlySpan<byte> spirv, VkDescriptorType[] types, uint pushSize)
    {
        var bindings = stackalloc VkDescriptorSetLayoutBinding[types.Length];
        for (int b = 0; b < types.Length; b++)
            bindings[b] = new VkDescriptorSetLayoutBinding { binding = (uint)b, descriptorType = types[b], descriptorCount = 1, stageFlags = VkShaderStageFlags.Compute };
        var setInfo = new VkDescriptorSetLayoutCreateInfo { bindingCount = (uint)types.Length, pBindings = bindings };
        _deviceApi.vkCreateDescriptorSetLayout(&setInfo, null, out var setLayout).CheckResult();

        var push = new VkPushConstantRange { stageFlags = VkShaderStageFlags.Compute, offset = 0, size = pushSize };
        var layoutInfo = new VkPipelineLayoutCreateInfo { setLayoutCount = 1, pSetLayouts = &setLayout, pushConstantRangeCount = 1, pPushConstantRanges = &push };
        _deviceApi.vkCreatePipelineLayout(&layoutInfo, null, out var layout).CheckResult();

        VkShaderModule module;
        fixed (byte* code = spirv)
        {
            var moduleInfo = new VkShaderModuleCreateInfo { codeSize = (nuint)spirv.Length, pCode = (uint*)code };
            _deviceApi.vkCreateShaderModule(&moduleInfo, null, out module).CheckResult();
        }
        VkPipeline pipeline;
        fixed (byte* name = "main"u8)
        {
            var info = new VkComputePipelineCreateInfo
            {
                stage = new VkPipelineShaderStageCreateInfo { stage = VkShaderStageFlags.Compute, module = module, pName = name },
                layout = layout,
            };
            _deviceApi.vkCreateComputePipelines(default, 1, &info, null, &pipeline).CheckResult();
        }
        _deviceApi.vkDestroyShaderModule(module);
        return new ComputeStage(pipeline, layout, setLayout);
    }

    // A cube image of half floats a compute shader writes and a pass samples, with a view of each
    // level as six layers for the writing.
    private (VkImage Image, VkDeviceMemory Memory, VkImageView Cube, VkImageView[] Levels) StorageCube(uint size, uint mipLevels)
    {
        var info = new VkImageCreateInfo
        {
            flags = VkImageCreateFlags.CubeCompatible,
            imageType = VkImageType.Image2D,
            format = VkFormat.R16G16B16A16Sfloat,
            extent = new VkExtent3D(size, size, 1),
            mipLevels = mipLevels,
            arrayLayers = 6,
            samples = VkSampleCountFlags.Count1,
            tiling = VkImageTiling.Optimal,
            usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.Storage | VkImageUsageFlags.TransferSrc,
            sharingMode = VkSharingMode.Exclusive,
            initialLayout = VkImageLayout.Undefined,
        };
        _deviceApi.vkCreateImage(&info, null, out VkImage image).CheckResult();
        _deviceApi.vkGetImageMemoryRequirements(image, out VkMemoryRequirements requirements);
        var allocation = new VkMemoryAllocateInfo
        {
            allocationSize = requirements.size,
            memoryTypeIndex = FindMemoryType(requirements.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal),
        };
        _deviceApi.vkAllocateMemory(&allocation, null, out VkDeviceMemory memory).CheckResult();
        _deviceApi.vkBindImageMemory(image, memory, 0).CheckResult();

        VkImageView View(VkImageViewType type, uint firstLevel, uint levels)
        {
            var viewInfo = new VkImageViewCreateInfo
            {
                image = image,
                viewType = type,
                format = VkFormat.R16G16B16A16Sfloat,
                components = VkComponentMapping.Rgba,
                subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, firstLevel, levels, 0, 6),
            };
            _deviceApi.vkCreateImageView(&viewInfo, null, out VkImageView view).CheckResult();
            return view;
        }
        var cube = View(VkImageViewType.ImageCube, 0, mipLevels);
        var levelViews = new VkImageView[mipLevels];
        for (uint m = 0; m < mipLevels; m++) levelViews[m] = View(VkImageViewType.Image2DArray, m, 1);
        return (image, memory, cube, levelViews);
    }

    /// <summary>Makes a probe's cube of faces <paramref name="size"/> texels wide, its mips down to one texel, and its irradiance buffer, for <see cref="RecordProbeFilter"/> to fill.</summary>
    public ProbeMap CreateProbeMap(uint size = 32)
    {
        size = Math.Max(1, size);
        var mipLevels = (uint)Math.Log2(size) + 1;
        var (image, memory, cube, levels) = StorageCube(size, mipLevels);
        var owner = new VulkanImage(this, image, memory,
            new ImageDesc(new Extent2D(size, size), ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.Storage, mipLevels));
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        var irradiance = CreateBuffer(new BufferDesc(9 * 16, BufferUsage.Storage));
        return new ProbeMap(new VulkanImageView(this, owner, cube), sampler, owner, size, mipLevels, irradiance, levels, () =>
        {
            irradiance.Dispose();
            sampler.Dispose();
            foreach (var level in levels) _deviceApi.vkDestroyImageView(level);
            _deviceApi.vkDestroyImageView(cube);
            owner.Dispose();
        });
    }

    /// <summary>
    /// Records into the frame the filtering of a probe's six faces, drawn through
    /// <paramref name="viewProjections"/> from <paramref name="eye"/> and left ready to sample,
    /// into <paramref name="target"/>'s cube and irradiance, which the frames after sample.
    /// </summary>
    /// <returns>What the recording holds, its descriptor sets and the faces' views, to free once no frame in flight reads them.</returns>
    /// <exception cref="InvalidOperationException">The filter's shaders have not been given.</exception>
    public IDisposable RecordProbeFilter(ICommandBuffer commandBuffer, IReadOnlyList<IImageView> faces, ReadOnlySpan<Matrix4x4> viewProjections,
        Vector3 eye, ProbeMap target)
    {
        if (!CanFilterProbes) throw new InvalidOperationException("The probe filter's shaders have not been given.");
        if (commandBuffer is not VulkanCommandBuffer vkCmd) throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        var cmd = vkCmd.Handle;
        var sourceLevels = (uint)Math.Log2(ProbeSourceSize) + 1;
        var source = _probeSource ??= StorageCube(ProbeSourceSize, sourceLevels);
        _probeFaceSampler ??= CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        _probeSourceSampler ??= _probeFaceSampler;

        // The faces' view-projections, the way each looks, the point in the middle of its far plane
        // less the eye, and the eye, as probe_gather.slang's Views lays them out.
        var views = CreateBuffer(new BufferDesc(6 * 64 + 6 * 16 + 16, BufferUsage.Uniform, CpuAccessMode.Write));
        var mapped = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(Map(views));
        for (int f = 0; f < 6; f++)
        {
            var m = viewProjections[f];
            ReadOnlySpan<float> values = [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];
            values.CopyTo(mapped[(f * 16)..]);
            Matrix4x4.Invert(m, out var inverse);
            var far = Vector4.Transform(new Vector4(0, 0, 1, 1), inverse);
            var forward = Vector3.Normalize(new Vector3(far.X, far.Y, far.Z) / far.W - eye);
            mapped[96 + f * 4] = forward.X;
            mapped[96 + f * 4 + 1] = forward.Y;
            mapped[96 + f * 4 + 2] = forward.Z;
        }
        (mapped[120], mapped[121], mapped[122]) = (eye.X, eye.Y, eye.Z);
        Unmap(views);

        // A set for each dispatch: the gather, each level made, each level prefiltered and the irradiance.
        var mipSets = (int)sourceLevels - 1;
        var setCount = 1 + mipSets + (int)target.MipLevels + 1;
        var sizes = stackalloc VkDescriptorPoolSize[4];
        sizes[0] = new VkDescriptorPoolSize { type = VkDescriptorType.CombinedImageSampler, descriptorCount = (uint)(6 + target.MipLevels + 1) };
        sizes[1] = new VkDescriptorPoolSize { type = VkDescriptorType.UniformBuffer, descriptorCount = 1 };
        sizes[2] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageImage, descriptorCount = (uint)(1 + 2 * mipSets + target.MipLevels) };
        sizes[3] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageBuffer, descriptorCount = 1 };
        var poolInfo = new VkDescriptorPoolCreateInfo { maxSets = (uint)setCount, poolSizeCount = 4, pPoolSizes = sizes };
        _deviceApi.vkCreateDescriptorPool(&poolInfo, null, out VkDescriptorPool pool).CheckResult();

        VkDescriptorSet Set(int stage)
        {
            var layout = _probeStages[stage].SetLayout;
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = pool, descriptorSetCount = 1, pSetLayouts = &layout };
            VkDescriptorSet set;
            _deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
            return set;
        }
        void Image(VkDescriptorSet set, uint binding, VkDescriptorType type, VkImageView view, ISampler? sampler, VkImageLayout layout)
        {
            var image = new VkDescriptorImageInfo { imageView = view, sampler = sampler is VulkanSampler s ? s.Sampler : default, imageLayout = layout };
            var write = new VkWriteDescriptorSet { dstSet = set, dstBinding = binding, descriptorCount = 1, descriptorType = type, pImageInfo = &image };
            _deviceApi.vkUpdateDescriptorSets(1, &write, 0, null);
        }
        void Buffer(VkDescriptorSet set, uint binding, VkDescriptorType type, IBuffer buffer)
        {
            var vk = (VulkanBuffer)buffer;
            var info = new VkDescriptorBufferInfo { buffer = vk.Buffer, offset = 0, range = vk.Description.Size };
            var write = new VkWriteDescriptorSet { dstSet = set, dstBinding = binding, descriptorCount = 1, descriptorType = type, pBufferInfo = &info };
            _deviceApi.vkUpdateDescriptorSets(1, &write, 0, null);
        }
        void Dispatch(int stage, VkDescriptorSet set, ReadOnlySpan<byte> push, uint groupsX, uint groupsY, uint groupsZ)
        {
            var (pipeline, layout, _) = _probeStages[stage];
            _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline);
            _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
            fixed (byte* p = push)
                _deviceApi.vkCmdPushConstants(cmd, layout, VkShaderStageFlags.Compute, 0, (uint)push.Length, p);
            _deviceApi.vkCmdDispatch(cmd, Math.Max(1, groupsX), Math.Max(1, groupsY), groupsZ);
        }
        void Between() => MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
        static byte[] Push(params ReadOnlySpan<uint> words) => System.Runtime.InteropServices.MemoryMarshal.AsBytes(words).ToArray();

        // Both cubes to the general layout, what they held of an earlier capture let go, after the
        // faces' passes and the last frame's sampling of anything here.
        var whole = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, Vulkan.VK_REMAINING_MIP_LEVELS, 0, 6);
        PipelineBarrier(cmd, [
            ImageBarrier(source.Image, whole, VkImageLayout.Undefined, VkImageLayout.General,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite),
            ImageBarrier(((VulkanImage)target.Image).Image, whole, VkImageLayout.Undefined, VkImageLayout.General,
                VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite),
        ], new VkMemoryBarrier2
        {
            srcStageMask = VkPipelineStageFlags2.ColorAttachmentOutput,
            srcAccessMask = VkAccessFlags2.ColorAttachmentWrite,
            dstStageMask = VkPipelineStageFlags2.ComputeShader,
            dstAccessMask = VkAccessFlags2.ShaderRead,
        });

        var gather = Set(0);
        for (uint f = 0; f < 6; f++)
            Image(gather, f, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)faces[(int)f]).View, _probeFaceSampler, VkImageLayout.ShaderReadOnlyOptimal);
        Buffer(gather, 6, VkDescriptorType.UniformBuffer, views);
        Image(gather, 7, VkDescriptorType.StorageImage, source.Levels[0], null, VkImageLayout.General);
        Dispatch(0, gather, Push(ProbeSourceSize), (ProbeSourceSize + 7) / 8, (ProbeSourceSize + 7) / 8, 6);

        for (int m = 1; m < sourceLevels; m++)
        {
            Between();
            var set = Set(1);
            Image(set, 0, VkDescriptorType.StorageImage, source.Levels[m - 1], null, VkImageLayout.General);
            Image(set, 1, VkDescriptorType.StorageImage, source.Levels[m], null, VkImageLayout.General);
            var size = (uint)Math.Max(1, ProbeSourceSize >> m);
            Dispatch(1, set, Push(size), (size + 7) / 8, (size + 7) / 8, 6);
        }
        Between();

        // A mirror's texel reads the gathered cube at the level whose texels are its own size.
        var sourceTexel = 4 * MathF.PI / (6f * ProbeSourceSize * ProbeSourceSize);
        var mirrorLevel = MathF.Log2((float)ProbeSourceSize / target.Size);
        for (uint m = 0; m < target.MipLevels; m++)
        {
            var set = Set(2);
            Image(set, 0, VkDescriptorType.CombinedImageSampler, source.Cube, _probeSourceSampler, VkImageLayout.General);
            Image(set, 1, VkDescriptorType.StorageImage, target.Levels[m], null, VkImageLayout.General);
            var size = Math.Max(1, target.Size >> (int)m);
            var roughness = target.MipLevels > 1 ? (float)m / (target.MipLevels - 1) : 0;
            Dispatch(2, set, Push(size, BitConverter.SingleToUInt32Bits(roughness), BitConverter.SingleToUInt32Bits(sourceTexel),
                BitConverter.SingleToUInt32Bits(mirrorLevel)), (size + 7) / 8, (size + 7) / 8, 6);
        }

        var sums = Set(3);
        Image(sums, 0, VkDescriptorType.CombinedImageSampler, source.Cube, _probeSourceSampler, VkImageLayout.General);
        Buffer(sums, 1, VkDescriptorType.StorageBuffer, target.Irradiance);
        Dispatch(3, sums, Push(ProbeIrradianceSize, BitConverter.SingleToUInt32Bits(MathF.Log2((float)ProbeSourceSize / ProbeIrradianceSize))), 1, 1, 1);

        // The probe's cube to be sampled, and its irradiance read, by the frames' passes.
        PipelineBarrier(cmd, [
            ImageBarrier(((VulkanImage)target.Image).Image, whole, VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
                VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.Transfer,
                VkAccessFlags2.ShaderRead | VkAccessFlags2.TransferRead),
        ], new VkMemoryBarrier2
        {
            srcStageMask = VkPipelineStageFlags2.ComputeShader,
            srcAccessMask = VkAccessFlags2.ShaderWrite,
            dstStageMask = VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.ComputeShader,
            dstAccessMask = VkAccessFlags2.ShaderRead,
        });

        return new Recorded(() =>
        {
            _deviceApi.vkDestroyDescriptorPool(pool);
            views.Dispose();
        });
    }

    private sealed class Recorded(Action dispose) : IDisposable
    {
        public void Dispose() => dispose();
    }

    /// <summary>
    /// A mip of a probe's cube read back, its six faces' RGBA half floats in Vulkan's order, face
    /// by face, each row by row, after every frame in flight has finished, for a test to look at.
    /// </summary>
    internal Half[] ReadProbeFaces(ProbeMap map, uint level = 0)
    {
        var image = ((VulkanImage)map.Image).Image;
        var size = Math.Max(1, map.Size >> (int)level);
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc(size * size * 6 * 8, BufferUsage.TransferDst, CpuAccessMode.Read));
        try
        {
            var first = new VkImageSubresourceRange(VkImageAspectFlags.Color, level, 1, 0, 6);
            var cmd = BeginSingleTimeCommands();
            PipelineBarrier(cmd, ImageBarrier(image, first, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferSrcOptimal,
                VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead));
            var region = new VkBufferImageCopy
            {
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, level, 0, 6),
                imageExtent = new VkExtent3D(size, size, 1),
            };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, image, VkImageLayout.TransferSrcOptimal, buffer.Buffer, 1, &region);
            PipelineBarrier(cmd, ImageBarrier(image, first, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead));
            EndSingleTimeCommands(cmd);
            return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Half>(Map(buffer)).ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    // Lets the filter's pipelines and its gathered cube go with the device, before it goes.
    private void DisposeProbeFilter()
    {
        foreach (var (pipeline, layout, setLayout) in _probeStages.Where(s => s.Pipeline.Handle != 0))
        {
            _deviceApi.vkDestroyPipeline(pipeline);
            _deviceApi.vkDestroyPipelineLayout(layout);
            _deviceApi.vkDestroyDescriptorSetLayout(setLayout);
        }
        if (_probeSource is { } source)
        {
            foreach (var level in source.Levels) _deviceApi.vkDestroyImageView(level);
            _deviceApi.vkDestroyImageView(source.Cube);
            _deviceApi.vkDestroyImage(source.Image);
            _deviceApi.vkFreeMemory(source.Memory);
        }
        _probeFaceSampler?.Dispose();
        _probeSource = null;
        _probeFaceSampler = _probeSourceSampler = null;
        Array.Clear(_probeStages);
    }
}
