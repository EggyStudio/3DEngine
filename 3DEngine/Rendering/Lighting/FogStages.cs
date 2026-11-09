using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

/// <summary>
/// The fog's volume, a cell for each tile of the window by the fog's slices, the light each cell
/// scatters toward the eye and the light and share of light through from the eye to each slice's
/// far edge, each a 3D image of half floats, with the block of numbers both passes read.
/// </summary>
/// <remarks>
/// The cells' light is kept in the general layout, written and read by the two passes alone. The
/// summed light is read by the window's decode pass, so it is moved to the general layout for the
/// pass that writes it and back after, as the screen's probes' images are.
/// </remarks>
internal sealed class GpuFog : IDisposable
{
    private readonly Action _dispose;

    internal GpuFog(uint across, uint down, IImage scattered, IImageView scatteredView, IImage summed, IImageView summedView, ISampler sampler,
        IBuffer uniforms, Action dispose)
    {
        (Across, Down, Scattered, ScatteredView, Summed, SummedView, Sampler, Uniforms, _dispose) =
            (across, down, scattered, scatteredView, summed, summedView, sampler, uniforms, dispose);
    }

    /// <summary>The cells across, a tile of the window each.</summary>
    public uint Across { get; }

    /// <summary>The cells down.</summary>
    public uint Down { get; }

    internal IImage Scattered { get; }

    internal IImageView ScatteredView { get; }

    internal IImage Summed { get; }

    /// <summary>The light and the share of light through from the eye to each slice's far edge, which the decode pass reads.</summary>
    public IImageView SummedView { get; }

    /// <summary>The sampler <see cref="SummedView"/> is read through, between cells and clamped at the volume's edges.</summary>
    public ISampler Sampler { get; }

    /// <summary>The numbers both passes read, <c>FogUniforms</c> in <c>fog.slang</c>.</summary>
    public IBuffer Uniforms { get; }

    /// <summary>The size of <see cref="Uniforms"/>, a matrix, four of the sun's and nine vectors.</summary>
    public const int UniformBytes = 64 + 4 * 64 + 9 * 16;

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

/// <summary>
/// The fog's compute stages on the device and the passes that make and light its volume, a part the
/// device keeps (<see cref="GraphicsDevice.Part{T}"/>) and lets go with itself.
/// </summary>
internal sealed unsafe class FogStages(GraphicsDevice device) : IDisposable
{
    /// <summary>The device's fog stages, made the first time they are asked for.</summary>
    internal static FogStages Of(GraphicsDevice device) => device.Part(static device => new FogStages(device));

    private const int FogScatterStage = 0, FogIntegrateStage = 1;
    private readonly ComputeStage[] _fogStages = new ComputeStage[2];

    // The fog's kernels as InitializeFog was given them, made into pipelines the first frame fog is
    // drawn, so an app with none compiles none.
    private byte[][]? _fogSpirv;

    /// <summary>Whether the fog's shaders have been given.</summary>
    public bool CanDrawFog => _fogSpirv is not null;

    /// <summary>Keeps the compute stages of <c>fog_scatter.slang</c> and <c>fog_integrate.slang</c> for the first frame with fog.</summary>
    public void InitializeFog(ReadOnlySpan<byte> scatter, ReadOnlySpan<byte> integrate)
    {
        if (!device.IsInitialized || scatter.IsEmpty || integrate.IsEmpty) return;
        _fogSpirv = [scatter.ToArray(), integrate.ToArray()];
    }

    // Each stage reads its numbers from the block of uniforms, and the four bytes of push constants
    // each is made with, since a stage is made with a range of them, are never pushed.
    private ComputeStage[] Stages
    {
        get
        {
            if (_fogStages[FogIntegrateStage].Pipeline.Handle != 0 || _fogSpirv is not { } spirv) return _fogStages;
            _fogStages[FogScatterStage] = device.MakeComputeStage(spirv[0], [VkDescriptorType.StorageImage, VkDescriptorType.CombinedImageSampler,
                VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler,
                VkDescriptorType.UniformBuffer, VkDescriptorType.UniformBuffer], 4);
            _fogStages[FogIntegrateStage] = device.MakeComputeStage(spirv[1], [VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.UniformBuffer], 4);
            return _fogStages;
        }
    }

    /// <summary>
    /// Makes the fog's volume of <paramref name="across"/> by <paramref name="down"/> cells, its
    /// summed light cleared to none and all the scene's light through, so a decode pass that reads
    /// it before it is first drawn shows the scene as it is.
    /// </summary>
    public GpuFog CreateFog(uint across, uint down, uint slices)
    {
        var (scattered, scatteredMemory, scatteredView) = device.ProbeImage(across, down, slices);
        var (summed, summedMemory, summedView) = device.ProbeImage(across, down, slices);
        var uniforms = device.CreateBuffer(new BufferDesc(GpuFog.UniformBytes, BufferUsage.Uniform | BufferUsage.TransferDst));

        var cmd = device.BeginSingleTimeCommands();
        var whole = ColorLevels(0, 1);
        device.PipelineBarrier(cmd, [
            ImageBarrier(scattered, whole, VkImageLayout.Undefined, VkImageLayout.General,
                VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
            ImageBarrier(summed, whole, VkImageLayout.Undefined, VkImageLayout.General,
                VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
        ]);
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        device.Api.vkCmdClearColorImage(cmd, scattered, VkImageLayout.General, &none, 1, &whole);
        var clear = new VkClearColorValue(0f, 0f, 0f, 1f);
        device.Api.vkCmdClearColorImage(cmd, summed, VkImageLayout.General, &clear, 1, &whole);
        device.PipelineBarrier(cmd, [
            ImageBarrier(scattered, whole, VkImageLayout.General, VkImageLayout.General, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite),
            ImageBarrier(summed, whole, VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.Transfer,
                VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead),
        ]);
        device.EndSingleTimeCommands(cmd);

        var scatteredOwner = new VulkanImage(device, scattered, scatteredMemory,
            new ImageDesc(new Extent2D(across, down), ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.Storage));
        device.Name(scatteredOwner, "Fog's light by cell");
        var summedOwner = new VulkanImage(device, summed, summedMemory,
            new ImageDesc(new Extent2D(across, down), ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.Storage));
        device.Name(summedOwner, "Fog's light from the eye");
        var scatteredWrapped = new VulkanImageView(device, scatteredOwner, scatteredView);
        var summedWrapped = new VulkanImageView(device, summedOwner, summedView);
        var sampler = device.CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        return new GpuFog(across, down, scatteredOwner, scatteredWrapped, summedOwner, summedWrapped, sampler, uniforms, () =>
        {
            sampler.Dispose();
            uniforms.Dispose();
            scatteredWrapped.Dispose();
            scatteredOwner.Dispose();
            summedWrapped.Dispose();
            summedOwner.Dispose();
        });
    }

    /// <summary>
    /// Records the fog's two passes into <paramref name="commandBuffer"/> outside any render pass,
    /// its numbers written first, each cell lit from <paramref name="shadowMap"/> and the world's
    /// probes of <paramref name="gi"/> through <paramref name="field"/> where they are given, and
    /// the cells summed from the eye, left for the decode pass to read. The sets it took are let
    /// go when the run it answers is disposed, once no frame in flight reads them.
    /// </summary>
    public IDisposable RecordFog(ICommandBuffer commandBuffer, GpuFog fog, ReadOnlySpan<byte> uniforms, IImageView shadowMap, ISampler shadowSampler,
        GpuIllumination? gi, GpuSceneField field)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd) throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        var stages = Stages;
        var cmd = vkCmd.Handle;
        var pool = device.MakeDescriptorPool([(stages[FogScatterStage].Descriptors, 1), (stages[FogIntegrateStage].Descriptors, 1)]);
        var readers = VkPipelineStageFlags2.ComputeShader;
        device.MemoryBarrier(cmd, readers, VkAccessFlags2.UniformRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        var bytes = uniforms[..(Math.Min(uniforms.Length, GpuFog.UniformBytes) & ~3)];
        fixed (byte* data = bytes)
            device.Api.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)fog.Uniforms).Buffer, 0, (ulong)bytes.Length, data);
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, readers, VkAccessFlags2.UniformRead);

        VkDescriptorSet Set(int stage)
        {
            var layout = stages[stage].SetLayout;
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = pool, descriptorSetCount = 1, pSetLayouts = &layout };
            VkDescriptorSet set;
            device.Api.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
            return set;
        }
        void Image(VkDescriptorSet set, uint binding, VkDescriptorType type, IImageView view, ISampler? sampler, VkImageLayout layout)
        {
            var image = new VkDescriptorImageInfo
            {
                imageView = ((VulkanImageView)view).View, sampler = sampler is VulkanSampler s ? s.Sampler : default, imageLayout = layout,
            };
            var write = new VkWriteDescriptorSet { dstSet = set, dstBinding = binding, descriptorCount = 1, descriptorType = type, pImageInfo = &image };
            device.Api.vkUpdateDescriptorSets(1, &write, 0, null);
        }
        void Buffer(VkDescriptorSet set, uint binding, IBuffer buffer)
        {
            var vk = (VulkanBuffer)buffer;
            var info = new VkDescriptorBufferInfo { buffer = vk.Buffer, offset = 0, range = vk.Description.Size };
            var write = new VkWriteDescriptorSet { dstSet = set, dstBinding = binding, descriptorCount = 1, descriptorType = VkDescriptorType.UniformBuffer, pBufferInfo = &info };
            device.Api.vkUpdateDescriptorSets(1, &write, 0, null);
        }
        void Dispatch(int stage, VkDescriptorSet set, uint x, uint y, uint z)
        {
            var (pipeline, layout, _) = stages[stage];
            device.Api.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline);
            device.Api.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
            device.Api.vkCmdDispatch(cmd, Math.Max(1, x), Math.Max(1, y), Math.Max(1, z));
        }

        // Where light does not bounce, the summed light stands in for the probes' images, which the
        // numbers say are not read, as it is read only after this pass.
        var scatter = Set(FogScatterStage);
        Image(scatter, 0, VkDescriptorType.StorageImage, fog.ScatteredView, null, VkImageLayout.General);
        Image(scatter, 1, VkDescriptorType.CombinedImageSampler, shadowMap, shadowSampler, VkImageLayout.ShaderReadOnlyOptimal);
        Image(scatter, 2, VkDescriptorType.CombinedImageSampler, gi?.CubesView ?? fog.SummedView, gi?.Sampler ?? fog.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        Image(scatter, 3, VkDescriptorType.CombinedImageSampler, gi?.SkiesView ?? fog.SummedView, gi?.Sampler ?? fog.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        Image(scatter, 4, VkDescriptorType.CombinedImageSampler, gi?.ReachView ?? fog.SummedView, gi?.Sampler ?? fog.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        Buffer(scatter, 5, field.Info);
        Buffer(scatter, 6, fog.Uniforms);
        // The cells written again only once the frame before's sum has read them.
        device.MemoryBarrier(cmd, readers, VkAccessFlags2.ShaderRead, readers, VkAccessFlags2.ShaderWrite);
        Dispatch(FogScatterStage, scatter, (fog.Across + 3) / 4, (fog.Down + 3) / 4, (VolumetricFogRenderer.Slices + 3) / 4);

        var summed = ((VulkanImage)fog.Summed).Image;
        var whole = ColorLevels(0, 1);
        device.PipelineBarrier(cmd, [ImageBarrier(summed, whole, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.General,
            VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite)],
            new VkMemoryBarrier2
            {
                srcStageMask = VkPipelineStageFlags2.ComputeShader, srcAccessMask = VkAccessFlags2.ShaderWrite,
                dstStageMask = VkPipelineStageFlags2.ComputeShader, dstAccessMask = VkAccessFlags2.ShaderRead,
            });
        var integrate = Set(FogIntegrateStage);
        Image(integrate, 0, VkDescriptorType.SampledImage, fog.ScatteredView, null, VkImageLayout.General);
        Image(integrate, 1, VkDescriptorType.StorageImage, fog.SummedView, null, VkImageLayout.General);
        Buffer(integrate, 2, fog.Uniforms);
        Dispatch(FogIntegrateStage, integrate, (fog.Across + 7) / 8, (fog.Down + 7) / 8, 1);
        device.PipelineBarrier(cmd, [ImageBarrier(summed, whole, VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader,
            VkAccessFlags2.ShaderRead)]);
        return new FogRun(device, pool);
    }

    // The descriptor pool of a frame's fog, destroyed once no frame in flight reads its sets.
    private sealed class FogRun(GraphicsDevice device, VkDescriptorPool pool) : IDisposable
    {
        public void Dispose() => device.DestroyDescriptorPool(pool);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (pipeline, layout, setLayout) in _fogStages.Where(s => s.Pipeline.Handle != 0))
        {
            DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
            device.Api.vkDestroyPipeline(pipeline);
            device.Api.vkDestroyPipelineLayout(layout);
            device.Api.vkDestroyDescriptorSetLayout(setLayout);
        }
        Array.Clear(_fogStages);
        _fogSpirv = null;
    }
}
