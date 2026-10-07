using System.Numerics;
using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// A cube of half-float linear light a compute shader writes: a reflection probe's capture or an
/// environment map, prefiltered by roughness down its mips, with its irradiance as nine spherical
/// harmonic coefficients in a storage buffer, or the sky's cube of one level.
/// </summary>
/// <remarks>
/// The irradiance is in a buffer the GPU writes and the model pass reads, where a lighting buffer
/// written on the CPU would need it read back first.
/// </remarks>
internal sealed class FilteredCube : IDisposable
{
    private readonly Action _dispose;

    internal FilteredCube(IImageView view, ISampler sampler, IImage image, uint size, uint mipLevels, IBuffer? irradiance,
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
    /// The light's irradiance over pi, nine float4 whose xyz are the coefficients of bands 0 to 2
    /// in the order the model pass evaluates them (Y00, Y1-1, Y10, Y11, Y2-2, Y2-1, Y20, Y21, Y22),
    /// each multiplied by its band's share of a cosine lobe, or <c>null</c> for a cube that lights
    /// nothing, as the sky's.
    /// </summary>
    public IBuffer? Irradiance { get; }

    // Each mip as six layers, for the filter to write.
    internal VkImageView[] Levels { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

/// <summary>
/// Filtering a cube of light on the GPU, a reflection probe's six faces or an environment's
/// equirectangular image, recorded into a frame before any pass samples it, so a capture or a new
/// environment costs that frame's work and nothing is read back.
/// </summary>
/// <remarks>
/// <para>
/// The light is gathered into the first level of a source cube, from a probe's faces, half-float
/// targets drawn through views of a right angle (<c>probe_gather.slang</c>), or from an
/// environment's image (<c>env_gather.slang</c>), each level after it the average of four of the
/// one above (<c>probe_mips.slang</c>). The target cube is that source prefiltered by roughness,
/// level by level (<c>probe_prefilter.slang</c>), and its irradiance the source projected onto the
/// nine harmonics at a level <see cref="IrradianceSize"/> texels wide (<c>probe_irradiance.slang</c>).
/// A cube's texels differ in solid angle by a factor of about five at most, so its mips weigh each
/// direction by what it covers, where an equirectangular image's rows near a pole hold one
/// direction many times over.
/// </para>
/// <para>
/// A source cube of each width is made once and reused, since a probe's faces are drawn a face a
/// frame and its filter runs once all six are, and an environment is set at a level's start. It
/// stays in the general layout, written as a storage image and read as a cube, and a target moves
/// to be sampled once its levels are written.
/// </para>
/// </remarks>
internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>The width of a face of the cube a probe's faces are gathered into.</summary>
    internal const int ProbeSourceSize = 64;

    /// <summary>The width of the source cube's level the irradiance is summed over, fine enough for bands that hold no finer detail.</summary>
    internal const int IrradianceSize = 16;

    private const int GatherStage = 0, MipStage = 1, PrefilterStage = 2, IrradianceStage = 3, EnvironmentStage = 4;
    private readonly ComputeStage[] _filterStages = new ComputeStage[5];
    private readonly Dictionary<uint, (VkImage Image, VkDeviceMemory Memory, VkImageView Cube, VkImageView[] Levels)> _filterSources = [];
    private ISampler? _filterSampler;
    private ISampler? _equirectangularSampler;

    // One compute stage the filter records: its pipeline, layout and the set layout of its bindings.
    private readonly record struct ComputeStage(VkPipeline Pipeline, VkPipelineLayout Layout, VkDescriptorSetLayout SetLayout);

    /// <summary>Whether the filter's shaders have been given, so probes can be captured and environments filtered.</summary>
    public bool CanFilterProbes => _filterStages[EnvironmentStage].Pipeline.Handle != 0;

    /// <summary>
    /// Makes the filter's pipelines from the compute stages of <c>probe_gather.slang</c>,
    /// <c>probe_mips.slang</c>, <c>probe_prefilter.slang</c>, <c>probe_irradiance.slang</c> and
    /// <c>env_gather.slang</c>, once.
    /// </summary>
    public void InitializeProbeFilter(ReadOnlySpan<byte> gather, ReadOnlySpan<byte> mips, ReadOnlySpan<byte> prefilter, ReadOnlySpan<byte> irradiance,
        ReadOnlySpan<byte> environment)
    {
        if (!IsInitialized || CanFilterProbes) return;
        _filterStages[GatherStage] = MakeComputeStage(gather, [.. Enumerable.Repeat(VkDescriptorType.CombinedImageSampler, 6), VkDescriptorType.UniformBuffer, VkDescriptorType.StorageImage], 4);
        _filterStages[MipStage] = MakeComputeStage(mips, [VkDescriptorType.StorageImage, VkDescriptorType.StorageImage], 4);
        _filterStages[PrefilterStage] = MakeComputeStage(prefilter, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageImage], 16);
        _filterStages[IrradianceStage] = MakeComputeStage(irradiance, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageBuffer], 8);
        _filterStages[EnvironmentStage] = MakeComputeStage(environment, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageImage], 8);
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
            DeviceObjects.Made(DeviceObjects.Kind.Pipeline);
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
        DeviceObjects.Made(DeviceObjects.Kind.Image);
        _deviceApi.vkGetImageMemoryRequirements(image, out VkMemoryRequirements requirements);
        var allocation = new VkMemoryAllocateInfo
        {
            allocationSize = requirements.size,
            memoryTypeIndex = FindMemoryType(requirements.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal),
        };
        _deviceApi.vkAllocateMemory(&allocation, null, out VkDeviceMemory memory).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.Memory);
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

    /// <summary>
    /// Makes a cube of faces <paramref name="size"/> texels wide with <paramref name="mipLevels"/>
    /// mips, down to one texel where none is given, and its irradiance buffer where
    /// <paramref name="irradiance"/> is set, for <see cref="RecordProbeFilter"/> or
    /// <see cref="RecordEnvironmentFilter"/> to fill.
    /// </summary>
    public FilteredCube CreateFilteredCube(uint size, uint? mipLevels = null, bool irradiance = true)
    {
        size = Math.Max(1, size);
        var levels = Math.Clamp(mipLevels ?? uint.MaxValue, 1, (uint)Math.Log2(size) + 1);
        var (image, memory, cube, levelViews) = StorageCube(size, levels);
        var owner = new VulkanImage(this, image, memory,
            new ImageDesc(new Extent2D(size, size), ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.Storage, levels));
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        var coefficients = irradiance ? CreateBuffer(new BufferDesc(9 * 16, BufferUsage.Storage | BufferUsage.TransferSrc)) : null;
        return new FilteredCube(new VulkanImageView(this, owner, cube), sampler, owner, size, levels, coefficients, levelViews, () =>
        {
            coefficients?.Dispose();
            sampler.Dispose();
            foreach (var level in levelViews) _deviceApi.vkDestroyImageView(level);
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
        Vector3 eye, FilteredCube target)
    {
        var run = new FilterRun(this, commandBuffer);
        var source = Source(ProbeSourceSize);

        // The faces' view-projections, the way each looks, the point in the middle of its far plane
        // less the eye, and the eye, as probe_gather.slang's Views lays them out.
        var views = CreateBuffer(new BufferDesc(6 * 64 + 6 * 16 + 16, BufferUsage.Uniform, CpuAccessMode.Write));
        run.Holds(views);
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

        // After the faces' passes, which leave them ready to sample.
        run.Begin(source, [target], VkPipelineStageFlags2.ColorAttachmentOutput, VkAccessFlags2.ColorAttachmentWrite);
        var gather = run.Set(GatherStage);
        for (uint f = 0; f < 6; f++)
            run.Image(gather, f, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)faces[(int)f]).View, _filterSampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(gather, 6, VkDescriptorType.UniformBuffer, views);
        run.Image(gather, 7, VkDescriptorType.StorageImage, source.Levels[0], null, VkImageLayout.General);
        run.Dispatch(GatherStage, gather, FilterRun.Push(ProbeSourceSize), ProbeSourceSize, 6);

        run.Filter(source, ProbeSourceSize, target);
        run.End([target]);
        return run;
    }

    /// <summary>
    /// Records into the frame the filtering of an environment's equirectangular image, uploaded with
    /// its mips and <paramref name="imageHeight"/> texels tall, into <paramref name="target"/>'s cube
    /// and irradiance, and its resampling into <paramref name="sky"/>, which the frame's passes
    /// sample after.
    /// </summary>
    /// <returns>What the recording holds, its descriptor sets, to free once no frame in flight reads them.</returns>
    /// <exception cref="InvalidOperationException">The filter's shaders have not been given.</exception>
    public IDisposable RecordEnvironmentFilter(ICommandBuffer commandBuffer, IImageView image, uint imageHeight, FilteredCube target, FilteredCube sky)
    {
        var run = new FilterRun(this, commandBuffer);
        // A source twice the target's width, so its mirror mip reads four texels of the image's own.
        var sourceSize = Math.Max(IrradianceSize, 2 * target.Size);
        var source = Source(sourceSize);
        _equirectangularSampler ??= CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.Repeat, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        // After the upload that filled the image and its mips, submitted ahead of the frame.
        run.Begin(source, [target, sky], VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        foreach (var (cube, size) in new[] { (source.Levels[0], sourceSize), (sky.Levels[0], sky.Size) })
        {
            var set = run.Set(EnvironmentStage);
            run.Image(set, 0, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)image).View, _equirectangularSampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(set, 1, VkDescriptorType.StorageImage, cube, null, VkImageLayout.General);
            // The image's mip whose rows are as far apart as the four samples a texel takes, half a
            // texel, which at a face's middle is a radian over the face's width.
            var level = MathF.Max(0, MathF.Log2(imageHeight / (MathF.PI * size)));
            run.Dispatch(EnvironmentStage, set, FilterRun.Push(size, BitConverter.SingleToUInt32Bits(level)), size, 6);
        }

        run.Filter(source, sourceSize, target);
        run.End([target, sky]);
        return run;
    }

    // The source cube of a width, made the first time it is asked for.
    private (VkImage Image, VkDeviceMemory Memory, VkImageView Cube, VkImageView[] Levels) Source(uint size)
    {
        if (!CanFilterProbes) throw new InvalidOperationException("The probe filter's shaders have not been given.");
        if (!_filterSources.TryGetValue(size, out var source))
            _filterSources[size] = source = StorageCube(size, (uint)Math.Log2(size) + 1);
        _filterSampler ??= CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        return source;
    }

    // One recording of the filter: its descriptor pool, which every dispatch's set comes from, and
    // what else it holds until no frame in flight reads it.
    private sealed class FilterRun : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly VkCommandBuffer _cmd;
        private readonly VkDescriptorPool _pool;
        private readonly List<IDisposable> _held = [];
        private static readonly VkImageSubresourceRange Whole = new(VkImageAspectFlags.Color, 0, Vulkan.VK_REMAINING_MIP_LEVELS, 0, 6);

        public FilterRun(GraphicsDevice device, ICommandBuffer commandBuffer)
        {
            if (commandBuffer is not VulkanCommandBuffer vkCmd) throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
            _device = device;
            _cmd = vkCmd.Handle;

            // Enough for a gather or two, a source's mips and a target's levels, and the irradiance.
            var sizes = stackalloc VkDescriptorPoolSize[4];
            sizes[0] = new VkDescriptorPoolSize { type = VkDescriptorType.CombinedImageSampler, descriptorCount = 32 };
            sizes[1] = new VkDescriptorPoolSize { type = VkDescriptorType.UniformBuffer, descriptorCount = 2 };
            sizes[2] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageImage, descriptorCount = 64 };
            sizes[3] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageBuffer, descriptorCount = 2 };
            var poolInfo = new VkDescriptorPoolCreateInfo { maxSets = 48, poolSizeCount = 4, pPoolSizes = sizes };
            device._deviceApi.vkCreateDescriptorPool(&poolInfo, null, out _pool).CheckResult();
            DeviceObjects.Made(DeviceObjects.Kind.DescriptorPool);
        }

        public void Holds(IDisposable held) => _held.Add(held);

        public static byte[] Push(params ReadOnlySpan<uint> words) => System.Runtime.InteropServices.MemoryMarshal.AsBytes(words).ToArray();

        public VkDescriptorSet Set(int stage)
        {
            var layout = _device._filterStages[stage].SetLayout;
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = _pool, descriptorSetCount = 1, pSetLayouts = &layout };
            VkDescriptorSet set;
            _device._deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
            return set;
        }

        public void Image(VkDescriptorSet set, uint binding, VkDescriptorType type, VkImageView view, ISampler? sampler, VkImageLayout layout)
        {
            var image = new VkDescriptorImageInfo { imageView = view, sampler = sampler is VulkanSampler s ? s.Sampler : default, imageLayout = layout };
            var write = new VkWriteDescriptorSet { dstSet = set, dstBinding = binding, descriptorCount = 1, descriptorType = type, pImageInfo = &image };
            _device._deviceApi.vkUpdateDescriptorSets(1, &write, 0, null);
        }

        public void Buffer(VkDescriptorSet set, uint binding, VkDescriptorType type, IBuffer buffer)
        {
            var vk = (VulkanBuffer)buffer;
            var info = new VkDescriptorBufferInfo { buffer = vk.Buffer, offset = 0, range = vk.Description.Size };
            var write = new VkWriteDescriptorSet { dstSet = set, dstBinding = binding, descriptorCount = 1, descriptorType = type, pBufferInfo = &info };
            _device._deviceApi.vkUpdateDescriptorSets(1, &write, 0, null);
        }

        // A dispatch over layers of size texels square, in groups of 8 by 8.
        public void Dispatch(int stage, VkDescriptorSet set, ReadOnlySpan<byte> push, uint size, uint layers)
        {
            var (pipeline, layout, _) = _device._filterStages[stage];
            _device._deviceApi.vkCmdBindPipeline(_cmd, VkPipelineBindPoint.Compute, pipeline);
            _device._deviceApi.vkCmdBindDescriptorSets(_cmd, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
            fixed (byte* p = push)
                _device._deviceApi.vkCmdPushConstants(_cmd, layout, VkShaderStageFlags.Compute, 0, (uint)push.Length, p);
            var groups = Math.Max(1, (size + 7) / 8);
            _device._deviceApi.vkCmdDispatch(_cmd, groups, groups, layers);
        }

        private void Between() => _device.MemoryBarrier(_cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);

        // The source and targets to the general layout, what they held of an earlier filter let go,
        // after the last frame's sampling of anything here and after what wrote what the gather reads.
        public void Begin((VkImage Image, VkDeviceMemory Memory, VkImageView Cube, VkImageView[] Levels) source, FilteredCube[] targets,
            VkPipelineStageFlags2 inputStage, VkAccessFlags2 inputAccess)
        {
            var barriers = new List<VkImageMemoryBarrier2>
            {
                ImageBarrier(source.Image, Whole, VkImageLayout.Undefined, VkImageLayout.General,
                    VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
                    VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite),
            };
            foreach (var target in targets)
                barriers.Add(ImageBarrier(((VulkanImage)target.Image).Image, Whole, VkImageLayout.Undefined, VkImageLayout.General,
                    VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite));
            _device.PipelineBarrier(_cmd, [.. barriers], new VkMemoryBarrier2
            {
                srcStageMask = inputStage,
                srcAccessMask = inputAccess,
                dstStageMask = VkPipelineStageFlags2.ComputeShader,
                dstAccessMask = VkAccessFlags2.ShaderRead,
            });
        }

        // The source's mips from its first level, then the target's levels and its irradiance.
        public void Filter((VkImage Image, VkDeviceMemory Memory, VkImageView Cube, VkImageView[] Levels) source, uint sourceSize, FilteredCube target)
        {
            for (int m = 1; m < source.Levels.Length; m++)
            {
                Between();
                var set = Set(MipStage);
                Image(set, 0, VkDescriptorType.StorageImage, source.Levels[m - 1], null, VkImageLayout.General);
                Image(set, 1, VkDescriptorType.StorageImage, source.Levels[m], null, VkImageLayout.General);
                var size = Math.Max(1, sourceSize >> m);
                Dispatch(MipStage, set, Push(size), size, 6);
            }
            Between();

            // A mirror's texel reads the source at the level whose texels are its own size.
            var sourceTexel = 4 * MathF.PI / (6f * sourceSize * sourceSize);
            var mirrorLevel = MathF.Log2((float)sourceSize / target.Size);
            for (uint m = 0; m < target.MipLevels; m++)
            {
                var set = Set(PrefilterStage);
                Image(set, 0, VkDescriptorType.CombinedImageSampler, source.Cube, _device._filterSampler, VkImageLayout.General);
                Image(set, 1, VkDescriptorType.StorageImage, target.Levels[m], null, VkImageLayout.General);
                var size = Math.Max(1, target.Size >> (int)m);
                var roughness = target.MipLevels > 1 ? (float)m / (target.MipLevels - 1) : 0;
                Dispatch(PrefilterStage, set, Push(size, BitConverter.SingleToUInt32Bits(roughness), BitConverter.SingleToUInt32Bits(sourceTexel),
                    BitConverter.SingleToUInt32Bits(mirrorLevel)), size, 6);
            }

            if (target.Irradiance is not { } irradiance) return;
            var sums = Set(IrradianceStage);
            Image(sums, 0, VkDescriptorType.CombinedImageSampler, source.Cube, _device._filterSampler, VkImageLayout.General);
            Buffer(sums, 1, VkDescriptorType.StorageBuffer, irradiance);
            // One group of 64 threads sums every texel.
            Dispatch(IrradianceStage, sums, Push(IrradianceSize, BitConverter.SingleToUInt32Bits(MathF.Log2((float)sourceSize / IrradianceSize))), 1, 1);
        }

        // The targets to be sampled, and their irradiance read, by the frame's passes.
        public void End(FilteredCube[] targets)
        {
            var barriers = targets.Select(target => ImageBarrier(((VulkanImage)target.Image).Image, Whole, VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
                VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.Transfer,
                VkAccessFlags2.ShaderRead | VkAccessFlags2.TransferRead)).ToArray();
            _device.PipelineBarrier(_cmd, barriers, new VkMemoryBarrier2
            {
                srcStageMask = VkPipelineStageFlags2.ComputeShader,
                srcAccessMask = VkAccessFlags2.ShaderWrite,
                dstStageMask = VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.ComputeShader
                    | VkPipelineStageFlags2.Transfer,
                dstAccessMask = VkAccessFlags2.ShaderRead | VkAccessFlags2.TransferRead,
            });
        }

        public void Dispose()
        {
            DeviceObjects.Gone(DeviceObjects.Kind.DescriptorPool);
            _device._deviceApi.vkDestroyDescriptorPool(_pool);
            foreach (var held in _held) held.Dispose();
        }
    }

    /// <summary>
    /// A mip of a filtered cube read back, its six faces' RGBA half floats in Vulkan's order, face
    /// by face, each row by row, after every frame in flight has finished, for a test to look at.
    /// </summary>
    internal Half[] ReadCubeFaces(FilteredCube cube, uint level = 0)
    {
        var image = ((VulkanImage)cube.Image).Image;
        var size = Math.Max(1, cube.Size >> (int)level);
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc(size * size * 6 * 8, BufferUsage.TransferDst, CpuAccessMode.Read));
        try
        {
            var range = new VkImageSubresourceRange(VkImageAspectFlags.Color, level, 1, 0, 6);
            var cmd = BeginSingleTimeCommands();
            PipelineBarrier(cmd, ImageBarrier(image, range, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferSrcOptimal,
                VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead));
            var region = new VkBufferImageCopy
            {
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, level, 0, 6),
                imageExtent = new VkExtent3D(size, size, 1),
            };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, image, VkImageLayout.TransferSrcOptimal, buffer.Buffer, 1, &region);
            PipelineBarrier(cmd, ImageBarrier(image, range, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead));
            EndSingleTimeCommands(cmd);
            return System.Runtime.InteropServices.MemoryMarshal.Cast<byte, Half>(Map(buffer)).ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>A filtered cube's nine irradiance coefficients read back, after every frame in flight has finished, for a test to look at.</summary>
    internal Vector3[] ReadCubeIrradiance(FilteredCube cube)
    {
        var irradiance = (VulkanBuffer)(cube.Irradiance ?? throw new ArgumentException("The cube has no irradiance.", nameof(cube)));
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc(9 * 16, BufferUsage.TransferDst, CpuAccessMode.Read));
        try
        {
            var cmd = BeginSingleTimeCommands();
            var copy = new VkBufferCopy { size = 9 * 16 };
            _deviceApi.vkCmdCopyBuffer(cmd, irradiance.Buffer, buffer.Buffer, 1, &copy);
            EndSingleTimeCommands(cmd);
            var floats = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, float>(Map(buffer));
            var coefficients = new Vector3[9];
            for (int i = 0; i < 9; i++) coefficients[i] = new Vector3(floats[i * 4], floats[i * 4 + 1], floats[i * 4 + 2]);
            return coefficients;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    // Lets the filter's pipelines and its source cubes go with the device, before it goes.
    private void DisposeProbeFilter()
    {
        foreach (var (pipeline, layout, setLayout) in _filterStages.Where(s => s.Pipeline.Handle != 0))
        {
            DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
            _deviceApi.vkDestroyPipeline(pipeline);
            _deviceApi.vkDestroyPipelineLayout(layout);
            _deviceApi.vkDestroyDescriptorSetLayout(setLayout);
        }
        foreach (var source in _filterSources.Values)
        {
            foreach (var level in source.Levels) _deviceApi.vkDestroyImageView(level);
            _deviceApi.vkDestroyImageView(source.Cube);
            DeviceObjects.Gone(DeviceObjects.Kind.Image);
            _deviceApi.vkDestroyImage(source.Image);
            DeviceObjects.Gone(DeviceObjects.Kind.Memory);
            _deviceApi.vkFreeMemory(source.Memory);
        }
        _filterSources.Clear();
        _filterSampler?.Dispose();
        _equirectangularSampler?.Dispose();
        _filterSampler = _equirectangularSampler = null;
        Array.Clear(_filterStages);
    }
}
