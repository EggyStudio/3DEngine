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
/// The images of rays, merges and distances stay in the general layout, written and read by the
/// probes' work alone. The faces and the reach wait to be sampled between frames, which the next
/// frame's rays read as the light that bounced and how far each probe sees, and go to the general
/// layout while they are gathered again.
/// </remarks>
internal sealed class GpuIllumination : IDisposable
{
    private readonly Action _dispose;

    internal GpuIllumination(int probes, int[] texels, (VkImage Image, VkImageView View)[] radiance, (VkImage Image, VkImageView View)[] merged,
        VkImage cubes, IImageView cubesView, ISampler sampler, IBuffer lights, Action dispose,
        (VkImage Image, VkImageView View)[] distances, VkImage reach, IImageView reachView,
        VkImageView partial, VkImageView held)
    {
        Partial = partial;
        Held = held;
        Distances = distances;
        Reach = reach;
        ReachView = reachView;
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

    /// <summary>
    /// How far each probe's rays reached along eight by eight directions, every cascade, before a
    /// surface stopped them, as <c>gi_ambient.slang</c> lays it out, which weighs how much of each
    /// probe a surface takes.
    /// </summary>
    public IImageView ReachView { get; }

    /// <summary>The bytes the reach and each cascade's distances take.</summary>
    public long ReachBytes => (long)(8 * Probes) * (8 * Probes) * Probes * Cascades * 2
        + Texels.Sum(n => (long)(Probes * n) * (Probes * n) * Probes * 2);

    internal (VkImage Image, VkImageView View)[] Radiance { get; }
    internal (VkImage Image, VkImageView View)[] Merged { get; }
    internal (VkImage Image, VkImageView View)[] Distances { get; }

    // What each workgroup of a probe's rays brought back straight from the sun, the lights and what
    // gives off light, and the luminance of all they brought back, four a probe, every cascade.
    internal VkImageView Partial { get; }

    // Each probe's own light and the luminance of all its rays brought, two frames side by side,
    // the share of the light that bounced each probe's next rays take, and where the trace moved
    // each probe to, every cascade.
    internal VkImageView Held { get; }

    internal VkImage Cubes { get; }
    internal VkImage Reach { get; }

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
        ISampler sampler, IBuffer view, Action dispose, IImage blended, IImageView blendedView, IImage history, IImageView historyView,
        IImage lastGeometry, IImageView lastGeometryView)
    {
        History = history;
        HistoryView = historyView;
        LastGeometry = lastGeometry;
        LastGeometryView = lastGeometryView;
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

    /// <summary>The light arriving at each probe's surface blended with its neighbors' on like surfaces, which the model pass reads, its alpha one more than the share of it the frame before's light gave.</summary>
    public IImageView BlendedView { get; }

    internal IImage History { get; }

    /// <summary>The blended light of the frame before, copied from <see cref="BlendedView"/>, which the next frame's blend takes in.</summary>
    public IImageView HistoryView { get; }

    internal IImage LastGeometry { get; }

    /// <summary>The probes' surfaces of the frame before, copied from <see cref="GeometryView"/>, which tell where its light may be taken.</summary>
    public IImageView LastGeometryView { get; }

    /// <summary>A sampler that reads a texel as it is.</summary>
    public ISampler Sampler { get; }

    /// <summary>The view the probes were placed through, as <c>gi_screen.slang</c>'s <c>ScreenView</c>.</summary>
    public IBuffer View { get; }

    /// <summary>The bytes of <see cref="View"/>: this frame's camera and its inverse, four rows of the probes' layout, and the frame before's camera and eye.</summary>
    public const int ViewBytes = 3 * 64 + 5 * 16;

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
        public uint Kept, Unused1, Parity, Unused2;
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
            _giStages[FilterStage] = MakeComputeStage(filter, [VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler], 16);
            _giStages[ScreenStage] = MakeComputeStage(screen, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler,
                VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer,
                VkDescriptorType.CombinedImageSampler, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage,
                VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler, VkDescriptorType.SampledImage], 16);
            _giStages[TraceStage] = MakeComputeStage(trace, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer,
                VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer,
                VkDescriptorType.StorageImage, VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler,
                VkDescriptorType.StorageImage, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage], (uint)sizeof(IlluminationTrace));
            _giStages[MergeStage] = MakeComputeStage(merge, [VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageImage], 32);
            _giStages[AmbientStage] = MakeComputeStage(ambient, [VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.SampledImage, VkDescriptorType.StorageImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage], 16);
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
        var distances = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        for (int c = 0; c < texels.Length; c++)
        {
            var n = (uint)texels[c];
            radiance[c] = ProbeImage(p * n, p * n, p);
            merged[c] = ProbeImage(p * n, p * n, p);
            distances[c] = ProbeImage(p * n, p * n, p, VkFormat.R16Sfloat);
        }
        var (held, heldMemory, heldView) = ProbeImage(4 * p, p, p * (uint)texels.Length);
        var (partial, partialMemory, partialView) = ProbeImage(4 * p, p, p * (uint)texels.Length, VkFormat.R32G32B32A32Sfloat);
        var (cubes, cubesMemory, cubesView) = ProbeImage(6 * p, p, p * (uint)texels.Length);
        var (reach, reachMemory, reachView) = ProbeImage(8 * p, 8 * p, p * (uint)texels.Length, VkFormat.R16Sfloat);
        var lights = CreateBuffer(new BufferDesc(GpuIllumination.LightsBytes, BufferUsage.Uniform | BufferUsage.TransferDst));

        var cmd = BeginSingleTimeCommands();
        var whole = ColorLevels(0, 1);
        var all = radiance.Concat(merged).Concat(distances).Select(i => i.Item1).Append(cubes).Append(reach).Append(held).Append(partial).ToArray();
        PipelineBarrier(cmd, [.. all.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite))]);
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        foreach (var image in all.Where(image => image != reach && image != held)) _deviceApi.vkCmdClearColorImage(cmd, image, VkImageLayout.General, &none, 1, &whole);
        // No probe's own light known yet, and every probe taking the whole of the light that bounced.
        var unknown = new VkClearColorValue(1f, 1f, 1f, -1f);
        _deviceApi.vkCmdClearColorImage(cmd, held, VkImageLayout.General, &unknown, 1, &whole);
        // Every probe sees every way until its rays have first been traced.
        var far = new VkClearColorValue(10000f, 0f, 0f, 0f);
        _deviceApi.vkCmdClearColorImage(cmd, reach, VkImageLayout.General, &far, 1, &whole);
        _deviceApi.vkCmdFillBuffer(cmd, ((VulkanBuffer)lights).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        PipelineBarrier(cmd, [
            .. all.Where(image => image != cubes && image != reach).Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.General,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite)),
            .. new[] { cubes, reach }.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead)),
        ], new VkMemoryBarrier2
        {
            srcStageMask = VkPipelineStageFlags2.Transfer,
            srcAccessMask = VkAccessFlags2.TransferWrite,
            dstStageMask = VkPipelineStageFlags2.AllCommands,
            dstAccessMask = VkAccessFlags2.UniformRead | VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
        });
        EndSingleTimeCommands(cmd);

        var owner = new VulkanImage(this, cubes, cubesMemory,
            new ImageDesc(new Extent2D(6 * p, p), ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.Storage));
        Name(owner, "Light probes' faces");
        var reachOwner = new VulkanImage(this, reach, reachMemory,
            new ImageDesc(new Extent2D(8 * p, 8 * p), ImageFormat.R16_Float, ImageUsage.Sampled | ImageUsage.Storage));
        Name(reachOwner, "Light probes' reach");
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        return new GpuIllumination(probes, [.. texels], [.. radiance.Select(i => (i.Item1, i.Item3))], [.. merged.Select(i => (i.Item1, i.Item3))],
            cubes, new VulkanImageView(this, owner, cubesView), sampler, lights, () =>
            {
                sampler.Dispose();
                lights.Dispose();
                _deviceApi.vkDestroyImageView(cubesView);
                owner.Dispose();
                _deviceApi.vkDestroyImageView(reachView);
                reachOwner.Dispose();
                foreach (var (image, memory, view) in radiance.Concat(merged).Concat(distances).Append((held, heldMemory, heldView))
                    .Append((partial, partialMemory, partialView)))
                {
                    _deviceApi.vkDestroyImageView(view);
                    DeviceObjects.Gone(DeviceObjects.Kind.Image);
                    _deviceApi.vkDestroyImage(image);
                    DeviceObjects.Gone(DeviceObjects.Kind.Memory);
                    _deviceApi.vkFreeMemory(memory);
                }
            }, [.. distances.Select(i => (i.Item1, i.Item3))], reach, new VulkanImageView(this, reachOwner, reachView),
            partialView, heldView);
    }

    // A 3D image of half-float colors the probes' work writes and reads, with its view.
    private (VkImage Image, VkDeviceMemory Memory, VkImageView View) ProbeImage(uint width, uint height, uint depth,
        VkFormat format = VkFormat.R16G16B16A16Sfloat)
    {
        var info = new VkImageCreateInfo
        {
            imageType = VkImageType.Image3D,
            format = format,
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
            format = format,
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
    /// <param name="mergeOff">Whether each cascade keeps its own rays' light, taking nothing from the cascade above.</param>
    /// <param name="alone">The one cascade whose rays' light alone reaches the first, or -1 for every cascade.</param>
    /// <param name="viewed">Whether a view of the rays' light or the merges reads them in the window's pass (<see cref="BindProbeVolume"/>).</param>
    /// <param name="frame">The frame's number, whose parity says which of the two frames of each probe's own light is this one's.</param>
    /// <param name="lying">
    /// Whether each cascade lies where it lay the frame before, so a probe's own light and its share
    /// of the light that bounced the frame before are its own, or none where the light that bounced
    /// is taken whole however a probe's own light falls.
    /// </param>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordGlobalIllumination(ICommandBuffer commandBuffer, GpuIllumination gi, GpuSceneField field, IImageView environment,
        ISampler environmentSampler, ReadOnlySpan<byte> lights, IReadOnlyList<(float Start, float End)> intervals, float spacing,
        bool mergeOff = false, int alone = -1, bool viewed = false, long frame = 0, IReadOnlyList<bool>? lying = null)
    {
        var run = new ProbeRun(this, commandBuffer, gi.Cascades);
        var cmd = run.Commands;
        var readers = VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;
        MemoryBarrier(cmd, readers, VkAccessFlags2.UniformRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        var bytes = lights[..(Math.Min(lights.Length, GpuIllumination.LightsBytes) & ~3)];
        fixed (byte* data = bytes)
            _deviceApi.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)gi.Lights).Buffer, 0, (ulong)bytes.Length, data);
        // The lights are written, and the merges and faces of the frame before are read, before the
        // rays, a view's reads of them in the frame before's window pass too while one is shown.
        var before = VkPipelineStageFlags2.Transfer | VkPipelineStageFlags2.ComputeShader | (viewed ? VkPipelineStageFlags2.FragmentShader : 0);
        MemoryBarrier(cmd, before, VkAccessFlags2.TransferWrite | VkAccessFlags2.ShaderRead,
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
            run.Image(set, 8, VkDescriptorType.StorageImage, gi.Distances[c].View, null, VkImageLayout.General);
            run.Image(set, 9, VkDescriptorType.StorageImage, gi.Held, null, VkImageLayout.General);
            run.Image(set, 10, VkDescriptorType.StorageImage, gi.Partial, null, VkImageLayout.General);
            var push = new IlluminationTrace
            {
                Cascade = (uint)c, Probes = p, Texels = n, Last = c == gi.Cascades - 1 ? 1u : 0u,
                Start = intervals[c].Start, End = intervals[c].End, Spacing = spacing, Cascades = gi.Cascades,
                Kept = lying is not null && lying[c] ? 1u : 0u, Parity = (uint)(frame & 1),
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
            run.Image(set, 5, VkDescriptorType.StorageImage, gi.Held, null, VkImageLayout.General);
            ReadOnlySpan<uint> push = [(uint)c, p, n, c == gi.Cascades - 1 ? 0u : (uint)gi.Texels[c + 1], BitConverter.SingleToUInt32Bits(spacing),
                BitConverter.SingleToUInt32Bits(mergeOff ? 1 : 0), BitConverter.SingleToUInt32Bits(alone + 1), 0];
            run.Dispatch(MergeStage, set, MemoryMarshal.AsBytes(push), (p * p * p * n * n + 63) / 64);
            MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        }

        // The faces and the reach go to the general layout for the gather, after this frame's rays read them.
        PipelineBarrier(cmd, [.. new[] { gi.Cubes, gi.Reach }.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal,
            VkImageLayout.General, readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite))]);
        for (int c = 0; c < gi.Cascades; c++)
        {
            var set = run.Set(AmbientStage);
            run.Image(set, 0, VkDescriptorType.SampledImage, gi.Merged[c].View, null, VkImageLayout.General);
            run.Image(set, 1, VkDescriptorType.StorageImage, ((VulkanImageView)gi.CubesView).View, null, VkImageLayout.General);
            run.Image(set, 2, VkDescriptorType.SampledImage, gi.Distances[c].View, null, VkImageLayout.General);
            run.Image(set, 3, VkDescriptorType.StorageImage, ((VulkanImageView)gi.ReachView).View, null, VkImageLayout.General);
            run.Image(set, 4, VkDescriptorType.SampledImage, gi.Partial, null, VkImageLayout.General);
            run.Image(set, 5, VkDescriptorType.StorageImage, gi.Held, null, VkImageLayout.General);
            ReadOnlySpan<uint> push = [(uint)c, p, (uint)gi.Texels[c], (uint)(frame & 1) | (lying is not null && lying[c] ? 2u : 0u)];
            run.Dispatch(AmbientStage, set, MemoryMarshal.AsBytes(push), (p * p * p * 6 + 63) / 64);
        }
        PipelineBarrier(cmd, [.. new[] { gi.Cubes, gi.Reach }.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General,
            VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, readers, VkAccessFlags2.ShaderRead))]);
        // Each probe's own light and share, which the next frame's rays read.
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
        if (viewed)
            MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ShaderRead);
        return run;
    }

    /// <summary>
    /// Binds the rays' light of <paramref name="cascade"/>, or its merge, at
    /// <paramref name="binding"/> of <paramref name="set"/> as a sampled image in the general layout
    /// it stays in, for a view of it drawn after a recording that was told it is viewed.
    /// </summary>
    public void BindProbeVolume(IDescriptorSet set, uint binding, GpuIllumination gi, int cascade, bool merged)
    {
        var image = new VkDescriptorImageInfo { imageView = (merged ? gi.Merged : gi.Radiance)[cascade].View, imageLayout = VkImageLayout.General };
        var write = new VkWriteDescriptorSet
        {
            dstSet = ((VulkanDescriptorSet)set).Handle, dstBinding = binding, descriptorCount = 1, descriptorType = VkDescriptorType.SampledImage, pImageInfo = &image,
        };
        _deviceApi.vkUpdateDescriptorSets(1, &write, 0, null);
    }

    /// <summary>Makes the screen probes of a window <paramref name="width"/> by <paramref name="height"/> pixels, a probe every <paramref name="tile"/> pixels a side.</summary>
    public GpuScreenProbes CreateScreenProbes(uint width, uint height, int tile)
    {
        var across = (int)((width + tile - 1) / tile);
        var down = (int)((height + tile - 1) / tile);
        var desc = new ImageDesc(new Extent2D((uint)across, (uint)down), ImageFormat.R16G16B16A16_Float,
            ImageUsage.Sampled | ImageUsage.Storage | ImageUsage.TransferDst | ImageUsage.TransferSrc);
        var irradiance = CreateImage(desc);
        var geometry = CreateImage(desc);
        var blended = CreateImage(desc);
        var history = CreateImage(desc);
        var lastGeometry = CreateImage(desc);
        var irradianceView = CreateImageView(irradiance);
        var geometryView = CreateImageView(geometry);
        var blendedView = CreateImageView(blended);
        var historyView = CreateImageView(history);
        var lastGeometryView = CreateImageView(lastGeometry);
        Name(irradiance, "Screen probes' light");
        Name(geometry, "Screen probes' surfaces");
        Name(blended, "Screen probes' light, blended");
        Name(history, "Screen probes' light, the frame before");
        Name(lastGeometry, "Screen probes' surfaces, the frame before");
        var view = CreateBuffer(new BufferDesc(GpuScreenProbes.ViewBytes, BufferUsage.Uniform | BufferUsage.TransferDst));
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        // Every image holds nothing, ready to be sampled, until the first frame places the probes.
        var cmd = BeginSingleTimeCommands();
        var whole = ColorLevels(0, 1);
        VkImage[] images = [.. new[] { irradiance, geometry, blended, history, lastGeometry }.Select(image => ((VulkanImage)image).Image)];
        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite))]);
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        foreach (var image in images) _deviceApi.vkCmdClearColorImage(cmd, image, VkImageLayout.General, &none, 1, &whole);
        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead))]);
        EndSingleTimeCommands(cmd);
        foreach (var image in new[] { irradiance, geometry, blended, history, lastGeometry }) ((VulkanImage)image).Layout = VkImageLayout.ShaderReadOnlyOptimal;

        return new GpuScreenProbes(across, down, tile, irradiance, irradianceView, geometry, geometryView, sampler, view, () =>
        {
            sampler.Dispose();
            view.Dispose();
            foreach (var made in new IDisposable[] { irradianceView, geometryView, blendedView, historyView, lastGeometryView,
                         irradiance, geometry, blended, history, lastGeometry })
                made.Dispose();
        }, blended, blendedView, history, historyView, lastGeometry, lastGeometryView);
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
        VkImage[] images = [.. new[] { screen.Irradiance, screen.Geometry, screen.Blended, screen.History, screen.LastGeometry }
            .Select(image => ((VulkanImage)image).Image)];
        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.General,
            readers | VkPipelineStageFlags2.Transfer, VkAccessFlags2.ShaderRead | VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader,
            VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead))]);

        var set = run.Set(ScreenStage);
        run.Image(set, 0, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)depth).View, depthSampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 1, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.View).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 2, VkDescriptorType.UniformBuffer, field.Info);
        run.Image(set, 3, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.AlbedoView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 4, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.GlowView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 5, VkDescriptorType.UniformBuffer, gi.Lights);
        run.Image(set, 6, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)gi.CubesView).View, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Image(set, 7, VkDescriptorType.SampledImage, gi.Merged[0].View, null, VkImageLayout.General);
        run.Image(set, 12, VkDescriptorType.SampledImage, gi.Merged[Math.Min(1, gi.Cascades - 1)].View, null, VkImageLayout.General);
        run.Image(set, 8, VkDescriptorType.StorageImage, ((VulkanImageView)screen.IrradianceView).View, null, VkImageLayout.General);
        run.Image(set, 9, VkDescriptorType.StorageImage, ((VulkanImageView)screen.GeometryView).View, null, VkImageLayout.General);
        run.Image(set, 11, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)gi.ReachView).View, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
        run.Buffer(set, 10, VkDescriptorType.UniformBuffer, screen.View);
        ReadOnlySpan<uint> push = [(uint)gi.Texels[0], gi.Cascades > 1 ? (uint)gi.Texels[1] : 0, 0, 0];
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
        run.Image(blend, 3, VkDescriptorType.SampledImage, ((VulkanImageView)screen.HistoryView).View, null, VkImageLayout.General);
        run.Image(blend, 4, VkDescriptorType.SampledImage, ((VulkanImageView)screen.LastGeometryView).View, null, VkImageLayout.General);
        run.Buffer(blend, 5, VkDescriptorType.UniformBuffer, screen.View);
        run.Image(blend, 6, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)depth).View, depthSampler, VkImageLayout.ShaderReadOnlyOptimal);
        var (filterPipeline, filterLayout, _) = GiStages[FilterStage];
        _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, filterPipeline);
        _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, filterLayout, 0, 1, &blend, 0, null);
        _deviceApi.vkCmdDispatch(cmd, (uint)(screen.Across + 7) / 8, (uint)(screen.Down + 7) / 8, 1);

        // This frame's blended light and surfaces kept for the next frame's blend.
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead | VkAccessFlags2.TransferWrite);
        var region = new VkImageCopy
        {
            srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            extent = new VkExtent3D((uint)screen.Across, (uint)screen.Down, 1),
        };
        _deviceApi.vkCmdCopyImage(cmd, ((VulkanImage)screen.Blended).Image, VkImageLayout.General, ((VulkanImage)screen.History).Image, VkImageLayout.General, 1, &region);
        _deviceApi.vkCmdCopyImage(cmd, ((VulkanImage)screen.Geometry).Image, VkImageLayout.General, ((VulkanImage)screen.LastGeometry).Image, VkImageLayout.General, 1, &region);

        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.Transfer, VkAccessFlags2.ShaderWrite | VkAccessFlags2.TransferWrite | VkAccessFlags2.TransferRead,
            readers, VkAccessFlags2.ShaderRead))]);
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

    /// <summary>
    /// A cascade's rays' light or its merge, four floats a texel, its probes' octahedrons side by side
    /// and a layer of probes a slice, read back with the frames in flight finished.
    /// </summary>
    internal float[] ReadProbeVolume(GpuIllumination gi, int cascade, bool merged)
    {
        var side = (uint)(gi.Probes * gi.Texels[cascade]);
        var depth = (uint)gi.Probes;
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc(side * side * depth * 8, BufferUsage.TransferDst, CpuAccessMode.Read));
        try
        {
            // The volumes stay in the general layout, which a copy reads.
            var cmd = BeginSingleTimeCommands();
            MemoryBarrier(cmd, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead);
            var region = new VkBufferImageCopy
            {
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageExtent = new VkExtent3D(side, side, depth),
            };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, (merged ? gi.Merged : gi.Radiance)[cascade].Image, VkImageLayout.General, buffer.Buffer, 1, &region);
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
            var sizes = stackalloc VkDescriptorPoolSize[5];
            sizes[0] = new VkDescriptorPoolSize { type = VkDescriptorType.CombinedImageSampler, descriptorCount = 6 * sets };
            sizes[1] = new VkDescriptorPoolSize { type = VkDescriptorType.UniformBuffer, descriptorCount = 3 * sets };
            sizes[2] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageImage, descriptorCount = 4 * sets };
            sizes[3] = new VkDescriptorPoolSize { type = VkDescriptorType.SampledImage, descriptorCount = 2 * sets };
            sizes[4] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageBuffer, descriptorCount = sets };
            var poolInfo = new VkDescriptorPoolCreateInfo { maxSets = sets, poolSizeCount = 5, pPoolSizes = sizes };
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
