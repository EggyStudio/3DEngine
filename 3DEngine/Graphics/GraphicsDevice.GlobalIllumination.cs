using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// The light probes of the light that bounces on the GPU: for each cascade an image of what each
/// probe's rays brought back and one of that merged with the cascades above, a tile of octahedron
/// texels a probe, and for every cascade the six faces of the light each probe receives, which the
/// model pass reads, with a uniform buffer of the lights the rays' surfaces are lit by.
/// </summary>
/// <remarks>
/// The images of rays and merges stay in the general layout, written and read by the probes' work
/// alone. The faces wait to be sampled between frames, which the next frame's rays read as the
/// light that bounced, and go to the general layout while they are gathered again.
/// </remarks>
internal sealed class GpuIllumination : IDisposable
{
    private readonly Action _dispose;

    internal GpuIllumination(int probes, int[] texels, (VkImage Image, VkImageView View)[] radiance, (VkImage Image, VkImageView View)[] merged,
        VkImage cubes, IImageView cubesView, ISampler sampler, IBuffer lights, Action dispose)
    {
        Probes = probes;
        Texels = texels;
        Radiance = radiance;
        Merged = merged;
        Cubes = cubes;
        CubesView = cubesView;
        Sampler = sampler;
        Lights = lights;
        _dispose = dispose;
    }

    /// <summary>The probes along each side of a cascade.</summary>
    public int Probes { get; }

    /// <summary>Each cascade's octahedron's texels along each side, so its probes' rays.</summary>
    public IReadOnlyList<int> Texels { get; }

    /// <summary>How many cascades.</summary>
    public int Cascades => Texels.Count;

    /// <summary>The six faces of the light each probe receives, every cascade, as <c>gi_ambient.slang</c> writes them.</summary>
    public IImageView CubesView { get; }

    /// <summary>A sampler that blends between probes, clamped at the edges.</summary>
    public ISampler Sampler { get; }

    /// <summary>The lights, as <c>gi.slang</c>'s <c>GiLights</c>.</summary>
    public IBuffer Lights { get; }

    /// <summary>The bytes of <see cref="Lights"/>.</summary>
    public const int LightsBytes = 64 + 16 * 64;

    internal (VkImage Image, VkImageView View)[] Radiance { get; }
    internal (VkImage Image, VkImageView View)[] Merged { get; }
    internal VkImage Cubes { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

/// <summary>
/// The window's screen probes: the light arriving at each probe's surface and the surface's normal
/// and distance from the eye, an image of each, a texel a probe, and the buffer of the view they
/// were placed through.
/// </summary>
internal sealed class GpuScreenProbes : IDisposable
{
    private readonly Action _dispose;

    internal GpuScreenProbes(int across, int down, int tile, IImage irradiance, IImageView irradianceView, IImage geometry, IImageView geometryView,
        ISampler sampler, IBuffer view, Action dispose, IImage blended, IImageView blendedView)
    {
        Blended = blended;
        BlendedView = blendedView;
        Across = across;
        Down = down;
        Tile = tile;
        Irradiance = irradiance;
        IrradianceView = irradianceView;
        Geometry = geometry;
        GeometryView = geometryView;
        Sampler = sampler;
        View = view;
        _dispose = dispose;
    }

    /// <summary>The probes across the window.</summary>
    public int Across { get; }

    /// <summary>The probes down the window.</summary>
    public int Down { get; }

    /// <summary>The pixels along each side of the tile a probe stands in.</summary>
    public int Tile { get; }

    internal IImage Irradiance { get; }

    /// <summary>The light arriving at each probe's surface, a texel a probe, its alpha 1 where the probe holds it.</summary>
    public IImageView IrradianceView { get; }

    internal IImage Geometry { get; }

    /// <summary>Each probe's surface's normal, and in w its distance from the eye, or -1 for a probe on no surface.</summary>
    public IImageView GeometryView { get; }

    internal IImage Blended { get; }

    /// <summary>The light arriving at each probe's surface blended with its neighbors' on like surfaces, which the model pass reads.</summary>
    public IImageView BlendedView { get; }

    /// <summary>A sampler that reads a texel as it is.</summary>
    public ISampler Sampler { get; }

    /// <summary>The view the probes were placed through, as <c>gi_screen.slang</c>'s <c>ScreenView</c>.</summary>
    public IBuffer View { get; }

    /// <summary>The bytes of <see cref="View"/>.</summary>
    public const int ViewBytes = 2 * 64 + 4 * 16;

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

/// <summary>
/// The window's scene as a frame drew it, in linear light at half the window's size with its mips,
/// which a glossy surface's reflection reads the light of a surface it met on the screen from in
/// the frame after.
/// </summary>
internal sealed class GpuReflectionHistory : IDisposable
{
    private readonly Action _dispose;

    internal GpuReflectionHistory(Extent2D window, IImage image, IImageView view, ISampler sampler, VkImage depth, IImageView depthView,
        ISampler depthSampler, Action dispose)
    {
        Window = window;
        Image = image;
        View = view;
        Sampler = sampler;
        Depth = depth;
        DepthView = depthView;
        DepthSampler = depthSampler;
        _dispose = dispose;
    }

    internal VkImage Depth { get; }

    /// <summary>The window's depth at half its size the same frame, which tells a reflection whether the picture showed the surface it met or something in front of it.</summary>
    public IImageView DepthView { get; }

    /// <summary>A sampler that reads a depth as it is.</summary>
    public ISampler DepthSampler { get; }

    /// <summary>The size of the window it was made for, twice its own.</summary>
    public Extent2D Window { get; }

    internal IImage Image { get; }

    /// <summary>Every level of the picture, to sample a blurred level of for a rough surface.</summary>
    public IImageView View { get; }

    /// <summary>A sampler that blends between texels and between levels.</summary>
    public ISampler Sampler { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

internal sealed unsafe partial class GraphicsDevice
{
    private const int TraceStage = 0, MergeStage = 1, AmbientStage = 2, ScreenStage = 3, FilterStage = 4;
    private readonly ComputeStage[] _giStages = new ComputeStage[5];

    /// <summary>What <c>gi_trace.slang</c> is pushed.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct IlluminationTrace
    {
        public uint Cascade, Probes, Texels, Last;
        public float Start, End, Spacing, Cascades;
    }

    /// <summary>Whether the probes' shaders have been given, so light can bounce.</summary>
    public bool CanBounceLight => _giSpirv is not null;

    // The probes' kernels as InitializeGlobalIllumination was given them, made into pipelines the
    // first time a frame traces the probes, so an app whose light never bounces compiles none.
    private byte[][]? _giSpirv;

    private ComputeStage[] GiStages
    {
        get
        {
            if (_giStages[FilterStage].Pipeline.Handle != 0 || _giSpirv is not { } spirv) return _giStages;
            var (trace, merge, ambient, screen, filter) = (spirv[0], spirv[1], spirv[2], spirv[3], spirv[4]);
            _giStages[FilterStage] = MakeComputeStage(filter, [VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage], 16);
            _giStages[ScreenStage] = MakeComputeStage(screen, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler,
                VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer,
                VkDescriptorType.CombinedImageSampler, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage,
                VkDescriptorType.UniformBuffer], 16);
            _giStages[TraceStage] = MakeComputeStage(trace, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer,
                VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer,
                VkDescriptorType.StorageImage, VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler], (uint)sizeof(IlluminationTrace));
            _giStages[MergeStage] = MakeComputeStage(merge, [VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler], 32);
            _giStages[AmbientStage] = MakeComputeStage(ambient, [VkDescriptorType.SampledImage, VkDescriptorType.StorageImage], 16);
            return _giStages;
        }
    }

    /// <summary>
    /// Makes the probes' pipelines from <c>gi_trace.slang</c>, <c>gi_merge.slang</c>,
    /// <c>gi_ambient.slang</c>, <c>gi_screen.slang</c> and <c>gi_screen_filter.slang</c>, once.
    /// </summary>
    public void InitializeGlobalIllumination(ReadOnlySpan<byte> trace, ReadOnlySpan<byte> merge, ReadOnlySpan<byte> ambient, ReadOnlySpan<byte> screen,
        ReadOnlySpan<byte> filter)
    {
        if (!IsInitialized || CanBounceLight) return;
        _giSpirv = [trace.ToArray(), merge.ToArray(), ambient.ToArray(), screen.ToArray(), filter.ToArray()];
    }

    /// <summary>
    /// Makes the probes of <paramref name="texels"/>'s count of cascades, <paramref name="probes"/>
    /// along each side of each, each cascade's octahedron <paramref name="texels"/> texels a side,
    /// their faces cleared to no light.
    /// </summary>
    public GpuIllumination CreateGlobalIllumination(int probes, int[] texels)
    {
        var p = (uint)probes;
        var radiance = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var merged = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        for (int c = 0; c < texels.Length; c++)
        {
            var n = (uint)texels[c];
            radiance[c] = ProbeImage(p * n, p * n, p);
            merged[c] = ProbeImage(p * n, p * n, p);
        }
        var (cubes, cubesMemory, cubesView) = ProbeImage(6 * p, p, p * (uint)texels.Length);
        var lights = CreateBuffer(new BufferDesc(GpuIllumination.LightsBytes, BufferUsage.Uniform | BufferUsage.TransferDst));

        var cmd = BeginSingleTimeCommands();
        var whole = ColorLevels(0, 1);
        var all = radiance.Concat(merged).Select(i => i.Item1).Append(cubes).ToArray();
        PipelineBarrier(cmd, [.. all.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite))]);
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        foreach (var image in all) _deviceApi.vkCmdClearColorImage(cmd, image, VkImageLayout.General, &none, 1, &whole);
        _deviceApi.vkCmdFillBuffer(cmd, ((VulkanBuffer)lights).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        PipelineBarrier(cmd, [
            .. all.Where(image => image != cubes).Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.General,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite)),
            ImageBarrier(cubes, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead),
        ], new VkMemoryBarrier2
        {
            srcStageMask = VkPipelineStageFlags2.Transfer,
            srcAccessMask = VkAccessFlags2.TransferWrite,
            dstStageMask = VkPipelineStageFlags2.AllCommands,
            dstAccessMask = VkAccessFlags2.UniformRead,
        });
        EndSingleTimeCommands(cmd);

        var owner = new VulkanImage(this, cubes, cubesMemory,
            new ImageDesc(new Extent2D(6 * p, p), ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.Storage));
        Name(owner, "Light probes' faces");
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        return new GpuIllumination(probes, [.. texels], [.. radiance.Select(i => (i.Item1, i.Item3))], [.. merged.Select(i => (i.Item1, i.Item3))],
            cubes, new VulkanImageView(this, owner, cubesView), sampler, lights, () =>
            {
                sampler.Dispose();
                lights.Dispose();
                _deviceApi.vkDestroyImageView(cubesView);
                owner.Dispose();
                foreach (var (image, memory, view) in radiance.Concat(merged))
                {
                    _deviceApi.vkDestroyImageView(view);
                    DeviceObjects.Gone(DeviceObjects.Kind.Image);
                    _deviceApi.vkDestroyImage(image);
                    DeviceObjects.Gone(DeviceObjects.Kind.Memory);
                    _deviceApi.vkFreeMemory(memory);
                }
            });
    }

    // A 3D image of half-float colors the probes' work writes and reads, with its view.
    private (VkImage Image, VkDeviceMemory Memory, VkImageView View) ProbeImage(uint width, uint height, uint depth)
    {
        var info = new VkImageCreateInfo
        {
            imageType = VkImageType.Image3D,
            format = VkFormat.R16G16B16A16Sfloat,
            extent = new VkExtent3D(width, height, depth),
            mipLevels = 1,
            arrayLayers = 1,
            samples = VkSampleCountFlags.Count1,
            tiling = VkImageTiling.Optimal,
            usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.Storage | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst,
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
        var viewInfo = new VkImageViewCreateInfo
        {
            image = image,
            viewType = VkImageViewType.Image3D,
            format = VkFormat.R16G16B16A16Sfloat,
            components = VkComponentMapping.Rgba,
            subresourceRange = ColorLevels(0, 1),
        };
        _deviceApi.vkCreateImageView(&viewInfo, null, out VkImageView view).CheckResult();
        return (image, memory, view);
    }

    /// <summary>
    /// Records a frame of the probes' work after the field's: the lights written, each cascade's
    /// rays traced, the cascades merged from the last to the first, and each probe's faces
    /// gathered, left ready for the model pass to sample.
    /// </summary>
    /// <param name="commandBuffer">The frame's commands.</param>
    /// <param name="gi">The probes.</param>
    /// <param name="field">The scene's distance field, built this frame.</param>
    /// <param name="environment">The light from all around a ray of the last cascade meets where it meets nothing, or a black cube.</param>
    /// <param name="environmentSampler">Its sampler.</param>
    /// <param name="lights">The lights, <see cref="GpuIllumination.LightsBytes"/> of them.</param>
    /// <param name="intervals">Where each cascade's rays start and end.</param>
    /// <param name="spacing">The probes' spacing in cells of their field cascade.</param>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordGlobalIllumination(ICommandBuffer commandBuffer, GpuIllumination gi, GpuSceneField field, IImageView environment,
        ISampler environmentSampler, ReadOnlySpan<byte> lights, IReadOnlyList<(float Start, float End)> intervals, float spacing)
    {
        var run = new ProbeRun(this, commandBuffer, gi.Cascades);
        var cmd = run.Commands;
        var readers = VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;
        MemoryBarrier(cmd, readers, VkAccessFlags2.UniformRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        var bytes = lights[..(Math.Min(lights.Length, GpuIllumination.LightsBytes) & ~3)];
        fixed (byte* data = bytes)
            _deviceApi.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)gi.Lights).Buffer, 0, (ulong)bytes.Length, data);
        // The lights are written, and the merges and faces of the frame before are read, before the rays.
        MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer | VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.TransferWrite | VkAccessFlags2.ShaderRead,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.UniformRead | VkAccessFlags2.ShaderWrite);

        var p = (uint)gi.Probes;
        for (int c = 0; c < gi.Cascades; c++)
        {
            var n = (uint)gi.Texels[c];
            var set = run.Set(TraceStage);
            run.Image(set, 0, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.View).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Buffer(set, 1, VkDescriptorType.UniformBuffer, field.Info);
            run.Image(set, 2, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.AlbedoView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(set, 3, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.GlowView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Buffer(set, 4, VkDescriptorType.UniformBuffer, gi.Lights);
            run.Image(set, 5, VkDescriptorType.StorageImage, gi.Radiance[c].View, null, VkImageLayout.General);
            run.Image(set, 6, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)gi.CubesView).View, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(set, 7, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)environment).View, environmentSampler, VkImageLayout.ShaderReadOnlyOptimal);
            var push = new IlluminationTrace
            {
                Cascade = (uint)c, Probes = p, Texels = n, Last = c == gi.Cascades - 1 ? 1u : 0u,
                Start = intervals[c].Start, End = intervals[c].End, Spacing = spacing, Cascades = gi.Cascades,
            };
            run.Dispatch(TraceStage, set, MemoryMarshal.AsBytes(new ReadOnlySpan<IlluminationTrace>(in push)), (p * p * p * n * n + 63) / 64);
        }
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);

        for (int c = gi.Cascades - 1; c >= 0; c--)
        {
            var n = (uint)gi.Texels[c];
            var set = run.Set(MergeStage);
            run.Image(set, 0, VkDescriptorType.SampledImage, gi.Radiance[c].View, null, VkImageLayout.General);
            run.Image(set, 1, VkDescriptorType.SampledImage, c == gi.Cascades - 1 ? gi.Radiance[c].View : gi.Merged[c + 1].View, null, VkImageLayout.General);
            run.Image(set, 2, VkDescriptorType.StorageImage, gi.Merged[c].View, null, VkImageLayout.General);
            run.Buffer(set, 3, VkDescriptorType.UniformBuffer, field.Info);
            run.Image(set, 4, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.View).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            ReadOnlySpan<uint> push = [(uint)c, p, n, c == gi.Cascades - 1 ? 0u : (uint)gi.Texels[c + 1], BitConverter.SingleToUInt32Bits(spacing), 0, 0, 0];
            run.Dispatch(MergeStage, set, MemoryMarshal.AsBytes(push), (p * p * p * n * n + 63) / 64);
            MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        }

        // The faces go to the general layout for the gather, after this frame's rays read them.
        PipelineBarrier(cmd, ImageBarrier(gi.Cubes, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.General,
            readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite));
        for (int c = 0; c < gi.Cascades; c++)
        {
            var set = run.Set(AmbientStage);
            run.Image(set, 0, VkDescriptorType.SampledImage, gi.Merged[c].View, null, VkImageLayout.General);
            run.Image(set, 1, VkDescriptorType.StorageImage, ((VulkanImageView)gi.CubesView).View, null, VkImageLayout.General);
            ReadOnlySpan<uint> push = [(uint)c, p, (uint)gi.Texels[c], 0];
            run.Dispatch(AmbientStage, set, MemoryMarshal.AsBytes(push), (p * p * p * 6 + 63) / 64);
        }
        PipelineBarrier(cmd, ImageBarrier(gi.Cubes, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, readers, VkAccessFlags2.ShaderRead));
        return run;
    }

    /// <summary>
    /// Makes the picture a reflection reads the frame before from, for a window of
    /// <paramref name="width"/> by <paramref name="height"/> pixels, black until the first frame is
    /// kept in it.
    /// </summary>
    public GpuReflectionHistory CreateReflectionHistory(uint width, uint height)
    {
        var half = new Extent2D(Math.Max(1, width / 2), Math.Max(1, height / 2));
        var image = CreateImage(new ImageDesc(half, ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.TransferSrc | ImageUsage.TransferDst,
            ImageDesc.FullMipChain(half.Width, half.Height)));
        Name(image, "Reflections' frame before");
        var view = CreateImageView(image);
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, MipFilter: SamplerFilter.Linear));

        // The depth beside it, at the size of the window's depth, which is copied into it.
        var (depth, depthMemory) = TargetImage(VkFormat.D32Sfloat, half.Width, half.Height, VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst);
        var depthImage = new VulkanImage(this, depth, depthMemory, new ImageDesc(half, ImageFormat.D32_Float, ImageUsage.Sampled | ImageUsage.TransferDst));
        var depthView = new VulkanImageView(this, depthImage, TargetView(depth, VkFormat.D32Sfloat, VkImageAspectFlags.Depth));
        var depthSampler = CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        var vkImage = (VulkanImage)image;
        var levels = ColorLevels(0, LevelsOf(vkImage));
        var depthRange = new VkImageSubresourceRange(VkImageAspectFlags.Depth, 0, 1, 0, 1);
        var cmd = BeginSingleTimeCommands();
        PipelineBarrier(cmd,
        [
            ImageBarrier(vkImage.Image, levels, VkImageLayout.Undefined, VkImageLayout.General,
                VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
            ImageBarrier(depth, depthRange, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal,
                VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
        ]);
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        _deviceApi.vkCmdClearColorImage(cmd, vkImage.Image, VkImageLayout.General, &none, 1, &levels);
        var far = new VkClearDepthStencilValue(1f, 0);
        _deviceApi.vkCmdClearDepthStencilImage(cmd, depth, VkImageLayout.TransferDstOptimal, &far, 1, &depthRange);
        PipelineBarrier(cmd,
        [
            ImageBarrier(vkImage.Image, levels, VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead),
            ImageBarrier(depth, depthRange, VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead),
        ]);
        EndSingleTimeCommands(cmd);
        vkImage.Layout = VkImageLayout.ShaderReadOnlyOptimal;
        return new GpuReflectionHistory(new Extent2D(width, height), image, view, sampler, depth, depthView, depthSampler, () =>
        {
            depthSampler.Dispose();
            depthView.Dispose();
            depthImage.Dispose();
            sampler.Dispose();
            view.Dispose();
            image.Dispose();
        });
    }

    /// <summary>
    /// Records <paramref name="scene"/>, the window's scene the HDR frame drew this frame, copied
    /// into <paramref name="history"/> at half its size and filtered down its mips, and
    /// <paramref name="depth"/>, the window's depth at that size, copied beside it, after the model
    /// pass has read the frame before from them.
    /// </summary>
    public void RecordKeepFrame(ICommandBuffer commandBuffer, IImageView scene, IImageView depth, GpuReflectionHistory history)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd) throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        var cmd = vkCmd.Handle;
        var source = (VulkanImage)scene.Image;
        var target = (VulkanImage)history.Image;
        var sourceExtent = source.Description.Extent;
        var targetExtent = target.Description.Extent;
        var readers = VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;

        // The scene from the layout its pass left it in to be copied from, and every level of the
        // picture, read by the model pass a moment ago, to be written.
        PipelineBarrier(cmd,
        [
            ImageBarrier(source.Image, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferSrcOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput | readers, VkAccessFlags2.ColorAttachmentWrite | VkAccessFlags2.ShaderRead,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead),
            ImageBarrier(target.Image, ColorLevels(0, LevelsOf(target)), VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferDstOptimal,
                readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
        ]);
        VkImageBlit blit = new()
        {
            srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
        };
        blit.srcOffsets[1] = new VkOffset3D((int)sourceExtent.Width, (int)sourceExtent.Height, 1);
        blit.dstOffsets[1] = new VkOffset3D((int)targetExtent.Width, (int)targetExtent.Height, 1);
        _deviceApi.vkCmdBlitImage(cmd, source.Image, VkImageLayout.TransferSrcOptimal, target.Image, VkImageLayout.TransferDstOptimal, 1, &blit, VkFilter.Linear);
        PipelineBarrier(cmd, ImageBarrier(source.Image, ColorLevels(0, 1), VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead, readers, VkAccessFlags2.ShaderRead));
        RecordMipChain(cmd, target);

        // The depth, from the layout it is sampled in to be copied from, into the one beside the
        // picture, which the model pass read a moment ago.
        var depthImage = ((VulkanImage)depth.Image).Image;
        var depthRange = new VkImageSubresourceRange(VkImageAspectFlags.Depth, 0, 1, 0, 1);
        PipelineBarrier(cmd,
        [
            ImageBarrier(depthImage, depthRange, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferSrcOptimal,
                readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead),
            ImageBarrier(history.Depth, depthRange, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferDstOptimal,
                readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
        ]);
        var depthExtent = depth.Image.Description.Extent;
        VkImageCopy copy = new()
        {
            srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Depth, 0, 0, 1),
            dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Depth, 0, 0, 1),
            extent = new VkExtent3D(Math.Min(depthExtent.Width, targetExtent.Width), Math.Min(depthExtent.Height, targetExtent.Height), 1),
        };
        _deviceApi.vkCmdCopyImage(cmd, depthImage, VkImageLayout.TransferSrcOptimal, history.Depth, VkImageLayout.TransferDstOptimal, 1, &copy);
        PipelineBarrier(cmd,
        [
            ImageBarrier(depthImage, depthRange, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead, readers, VkAccessFlags2.ShaderRead),
            ImageBarrier(history.Depth, depthRange, VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, readers, VkAccessFlags2.ShaderRead),
        ]);
    }

    /// <summary>Makes the screen probes of a window <paramref name="width"/> by <paramref name="height"/> pixels, a probe every <paramref name="tile"/> pixels a side.</summary>
    public GpuScreenProbes CreateScreenProbes(uint width, uint height, int tile)
    {
        var across = (int)((width + tile - 1) / tile);
        var down = (int)((height + tile - 1) / tile);
        var desc = new ImageDesc(new Extent2D((uint)across, (uint)down), ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.Storage | ImageUsage.TransferDst);
        var irradiance = CreateImage(desc);
        var geometry = CreateImage(desc);
        var blended = CreateImage(desc);
        var irradianceView = CreateImageView(irradiance);
        var geometryView = CreateImageView(geometry);
        var blendedView = CreateImageView(blended);
        Name(irradiance, "Screen probes' light");
        Name(geometry, "Screen probes' surfaces");
        Name(blended, "Screen probes' light, blended");
        var view = CreateBuffer(new BufferDesc(GpuScreenProbes.ViewBytes, BufferUsage.Uniform | BufferUsage.TransferDst));
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        // Both images hold nothing, ready to be sampled, until the first frame places the probes.
        var cmd = BeginSingleTimeCommands();
        var whole = ColorLevels(0, 1);
        VkImage[] images = [((VulkanImage)irradiance).Image, ((VulkanImage)geometry).Image, ((VulkanImage)blended).Image];
        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite))]);
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        foreach (var image in images) _deviceApi.vkCmdClearColorImage(cmd, image, VkImageLayout.General, &none, 1, &whole);
        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead))]);
        EndSingleTimeCommands(cmd);
        foreach (var image in new[] { irradiance, geometry, blended }) ((VulkanImage)image).Layout = VkImageLayout.ShaderReadOnlyOptimal;

        return new GpuScreenProbes(across, down, tile, irradiance, irradianceView, geometry, geometryView, sampler, view, () =>
        {
            sampler.Dispose();
            view.Dispose();
            irradianceView.Dispose();
            geometryView.Dispose();
            blendedView.Dispose();
            irradiance.Dispose();
            geometry.Dispose();
            blended.Dispose();
        }, blended, blendedView);
    }

    /// <summary>
    /// Records the screen probes' work after the world's probes are merged: the view written, each
    /// probe placed on the window's depth and its rays traced and summed, left ready for the model
    /// pass to sample.
    /// </summary>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordScreenProbes(ICommandBuffer commandBuffer, GpuIllumination gi, GpuScreenProbes screen, GpuSceneField field,
        IImageView depth, ISampler depthSampler, ReadOnlySpan<byte> view)
    {
        var run = new ProbeRun(this, commandBuffer, 1);
        var cmd = run.Commands;
        var readers = VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;
        MemoryBarrier(cmd, readers, VkAccessFlags2.UniformRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        var bytes = view[..(Math.Min(view.Length, GpuScreenProbes.ViewBytes) & ~3)];
        fixed (byte* data = bytes)
            _deviceApi.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)screen.View).Buffer, 0, (ulong)bytes.Length, data);
        MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.UniformRead);
        VkImage[] images = [((VulkanImage)screen.Irradiance).Image, ((VulkanImage)screen.Geometry).Image, ((VulkanImage)screen.Blended).Image];
        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.General,
            readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite))]);

        var set = run.Set(ScreenStage);
        run.Image(set, 0, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)depth).View, depthSampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 1, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.View).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 2, VkDescriptorType.UniformBuffer, field.Info);
        run.Image(set, 3, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.AlbedoView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 4, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.GlowView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 5, VkDescriptorType.UniformBuffer, gi.Lights);
        run.Image(set, 6, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)gi.CubesView).View, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 7, VkDescriptorType.SampledImage, gi.Merged[0].View, null, VkImageLayout.General);
        run.Image(set, 8, VkDescriptorType.StorageImage, ((VulkanImageView)screen.IrradianceView).View, null, VkImageLayout.General);
        run.Image(set, 9, VkDescriptorType.StorageImage, ((VulkanImageView)screen.GeometryView).View, null, VkImageLayout.General);
        run.Buffer(set, 10, VkDescriptorType.UniformBuffer, screen.View);
        ReadOnlySpan<uint> push = [(uint)gi.Texels[0], 0, 0, 0];
        var (pipeline, layout, _) = GiStages[ScreenStage];
        _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline);
        _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
        fixed (uint* p = push)
            _deviceApi.vkCmdPushConstants(cmd, layout, VkShaderStageFlags.Compute, 0, 16, p);
        _deviceApi.vkCmdDispatch(cmd, (uint)(screen.Across + 7) / 8, (uint)(screen.Down + 7) / 8, 1);

        // Each probe's light blended with its neighbors' on like surfaces.
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        var blend = run.Set(FilterStage);
        run.Image(blend, 0, VkDescriptorType.SampledImage, ((VulkanImageView)screen.IrradianceView).View, null, VkImageLayout.General);
        run.Image(blend, 1, VkDescriptorType.SampledImage, ((VulkanImageView)screen.GeometryView).View, null, VkImageLayout.General);
        run.Image(blend, 2, VkDescriptorType.StorageImage, ((VulkanImageView)screen.BlendedView).View, null, VkImageLayout.General);
        var (filterPipeline, filterLayout, _) = GiStages[FilterStage];
        _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, filterPipeline);
        _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, filterLayout, 0, 1, &blend, 0, null);
        _deviceApi.vkCmdDispatch(cmd, (uint)(screen.Across + 7) / 8, (uint)(screen.Down + 7) / 8, 1);

        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, readers, VkAccessFlags2.ShaderRead))]);
        return run;
    }

    /// <summary>The faces of the light each probe receives, four floats a texel, read back for a test with the frames in flight finished.</summary>
    internal float[] ReadIlluminationCubes(GpuIllumination gi)
    {
        var (p, c) = ((uint)gi.Probes, (uint)gi.Cascades);
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc(6 * p * p * p * c * 8, BufferUsage.TransferDst, CpuAccessMode.Read));
        try
        {
            var cmd = BeginSingleTimeCommands();
            var whole = ColorLevels(0, 1);
            PipelineBarrier(cmd, ImageBarrier(gi.Cubes, whole, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferSrcOptimal,
                VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead));
            var region = new VkBufferImageCopy
            {
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageExtent = new VkExtent3D(6 * p, p, p * c),
            };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, gi.Cubes, VkImageLayout.TransferSrcOptimal, buffer.Buffer, 1, &region);
            PipelineBarrier(cmd, ImageBarrier(gi.Cubes, whole, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead));
            EndSingleTimeCommands(cmd);
            var halves = MemoryMarshal.Cast<byte, Half>(Map(buffer));
            var values = new float[halves.Length];
            for (int i = 0; i < halves.Length; i++) values[i] = (float)halves[i];
            return values;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    // One frame's recording of the probes' work: the descriptor pool its sets come from, freed once
    // no frame in flight reads them.
    private sealed class ProbeRun : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly VkDescriptorPool _pool;

        public ProbeRun(GraphicsDevice device, ICommandBuffer commandBuffer, int cascades)
        {
            if (commandBuffer is not VulkanCommandBuffer vkCmd) throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
            _device = device;
            Commands = vkCmd.Handle;
            var sets = (uint)(3 * cascades + 1);
            var sizes = stackalloc VkDescriptorPoolSize[4];
            sizes[0] = new VkDescriptorPoolSize { type = VkDescriptorType.CombinedImageSampler, descriptorCount = 6 * sets };
            sizes[1] = new VkDescriptorPoolSize { type = VkDescriptorType.UniformBuffer, descriptorCount = 3 * sets };
            sizes[2] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageImage, descriptorCount = 2 * sets };
            sizes[3] = new VkDescriptorPoolSize { type = VkDescriptorType.SampledImage, descriptorCount = 2 * sets };
            var poolInfo = new VkDescriptorPoolCreateInfo { maxSets = sets, poolSizeCount = 4, pPoolSizes = sizes };
            device._deviceApi.vkCreateDescriptorPool(&poolInfo, null, out _pool).CheckResult();
            DeviceObjects.Made(DeviceObjects.Kind.DescriptorPool);
        }

        public VkCommandBuffer Commands { get; }

        public VkDescriptorSet Set(int stage)
        {
            var layout = _device.GiStages[stage].SetLayout;
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

        public void Dispatch(int stage, VkDescriptorSet set, ReadOnlySpan<byte> push, uint groups)
        {
            var (pipeline, layout, _) = _device.GiStages[stage];
            _device._deviceApi.vkCmdBindPipeline(Commands, VkPipelineBindPoint.Compute, pipeline);
            _device._deviceApi.vkCmdBindDescriptorSets(Commands, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
            fixed (byte* p = push)
                _device._deviceApi.vkCmdPushConstants(Commands, layout, VkShaderStageFlags.Compute, 0, (uint)push.Length, p);
            _device._deviceApi.vkCmdDispatch(Commands, Math.Max(1, groups), 1, 1);
        }

        public void Dispose()
        {
            DeviceObjects.Gone(DeviceObjects.Kind.DescriptorPool);
            _device._deviceApi.vkDestroyDescriptorPool(_pool);
        }
    }

    // Runs before the device goes.
    private void DestroyGlobalIllumination()
    {
        foreach (var (pipeline, layout, setLayout) in _giStages.Where(s => s.Pipeline.Handle != 0))
        {
            DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
            _deviceApi.vkDestroyPipeline(pipeline);
            _deviceApi.vkDestroyPipelineLayout(layout);
            _deviceApi.vkDestroyDescriptorSetLayout(setLayout);
        }
        Array.Clear(_giStages);
        _giSpirv = null;
    }
}
