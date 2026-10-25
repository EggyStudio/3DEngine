using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// The light probes of global illumination on the GPU, for each cascade an image of what each
/// probe's rays brought back and one of that merged with the cascades above, a tile of octahedron
/// texels a probe, and for every cascade the six faces of the light each probe receives, which the
/// model pass reads, with a uniform buffer of the lights the rays' surfaces are lit by.
/// </summary>
/// <remarks>
/// The images of rays, merges and distances stay in the general layout, written and read by the
/// probes' work alone. The faces and the reach wait to be sampled between frames, which the next
/// frame's rays read as indirect light and how far each probe sees, and go to the general
/// layout while they are gathered again.
/// </remarks>
internal sealed class GpuIllumination : IDisposable
{
    private readonly Action _dispose;

    internal GpuIllumination(int probes, int[] texels, (VkImage Image, VkImageView View)[] radiance, (VkImage Image, VkImageView View)[] merged,
        VkImage cubes, IImageView cubesView, ISampler sampler, IBuffer lights, Action dispose,
        (VkImage Image, VkImageView View)[] distances, VkImage reach, IImageView reachView,
        VkImageView partial, VkImageView held, (VkImage Image, VkImageView View)[] mergedSky, VkImage skies, IImageView skiesView)
    {
        MergedSky = mergedSky;
        Skies = skies;
        SkiesView = skiesView;
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

    /// <summary>The rays each texel of cascade <paramref name="c"/>'s octahedron sends, as <c>gi.slang</c>'s <c>RaysAt</c> has them.</summary>
    public int RaysAt(int c) => Texels[c] <= 8 ? 4 : 1;

    /// <summary>The six faces of the light each probe receives, every cascade, as <c>gi_ambient.slang</c> writes them.</summary>
    public IImageView CubesView { get; }

    /// <summary>A sampler that blends between probes, clamped at the edges.</summary>
    public ISampler Sampler { get; }

    /// <summary>The lights, as <c>gi.slang</c>'s <c>GiLights</c>.</summary>
    public IBuffer Lights { get; }

    /// <summary>
    /// The bytes of <see cref="Lights"/>: the sun, the sky and the counts, sixteen lights, four
    /// cascades' corners of the frame before, and the glow lights (<see cref="GlowLights"/>).
    /// </summary>
    public const int LightsBytes = GlowLights.Offset + GlowLights.Size;

    /// <summary>
    /// How far each probe's rays reached along eight by eight directions, every cascade, before a
    /// surface stopped them, as <c>gi_ambient.slang</c> lays it out, which weighs how much of each
    /// probe a surface takes.
    /// </summary>
    public IImageView ReachView { get; }

    /// <summary>The bytes the reach and each cascade's distances take, each with the share its rays kept before their interval.</summary>
    public long ReachBytes => (long)(8 * Probes) * (8 * Probes) * Probes * Cascades * 2
        + Texels.Sum(n => (long)(Probes * n) * (Probes * n) * Probes * 4);

    /// <summary>
    /// The share of the sky each probe's faces see, every cascade, laid out as <see cref="CubesView"/>
    /// is, as <c>gi_ambient.slang</c> gathers it from the rays that met nothing, which the model pass
    /// weighs the environment's reflection by.
    /// </summary>
    public IImageView SkiesView { get; }

    /// <summary>The bytes the sky's shares take, the faces' and each cascade's merged rays'.</summary>
    public long SkyBytes => (long)(6 * Probes) * Probes * Probes * Cascades * 2
        + Texels.Sum(n => (long)(Probes * n) * (Probes * n) * Probes * 2);

    internal (VkImage Image, VkImageView View)[] Radiance { get; }
    internal (VkImage Image, VkImageView View)[] Merged { get; }
    internal (VkImage Image, VkImageView View)[] Distances { get; }

    // Each cascade's merged rays' share of the sky, a texel a ray, as gi_merge.slang writes it.
    internal (VkImage Image, VkImageView View)[] MergedSky { get; }

    internal VkImage Skies { get; }

    // What each workgroup of a probe's rays brought back straight from the sun, the lights and
    // emissive surfaces, and the luminance of all they brought back, four a probe, every cascade.
    internal VkImageView Partial { get; }

    // Each probe's own light and the luminance of all its rays brought, two frames side by side,
    // the share of the indirect light each probe's next rays take, and where the trace moved
    // each probe to, every cascade.
    internal VkImageView Held { get; }

    internal VkImage Cubes { get; }
    internal VkImage Reach { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

internal sealed unsafe partial class GraphicsDevice
{
    private const int TraceStage = 0, MergeStage = 1, AmbientStage = 2, ScreenStage = 3, FilterStage = 4, GlowStage = 5, SolidTraceStage = 6;
    private readonly ComputeStage[] _giStages = new ComputeStage[7];

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
            if (_giStages[GlowStage].Pipeline.Handle != 0 || _giSpirv is not { } spirv) return _giStages;
            var (trace, merge, ambient, screen, filter, glow) = (spirv[0], spirv[1], spirv[2], spirv[3], spirv[4], spirv[5]);
            _giStages[GlowStage] = MakeComputeStage(glow, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.SampledImage,
                VkDescriptorType.UniformBuffer, VkDescriptorType.UniformBuffer, VkDescriptorType.UniformBuffer, VkDescriptorType.StorageBuffer,
                VkDescriptorType.StorageImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage, VkDescriptorType.SampledImage,
                VkDescriptorType.StorageImage, VkDescriptorType.StorageImage, VkDescriptorType.SampledImage], 16);
            _giStages[FilterStage] = MakeComputeStage(filter, [VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler], 16);
            _giStages[ScreenStage] = MakeComputeStage(screen, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler,
                VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer,
                VkDescriptorType.CombinedImageSampler, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage,
                VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler, VkDescriptorType.SampledImage, VkDescriptorType.StorageBuffer,
                VkDescriptorType.StorageBuffer, VkDescriptorType.SampledImage], 16);
            VkDescriptorType[] traced = [VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer,
                VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer,
                VkDescriptorType.StorageImage, VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler,
                VkDescriptorType.StorageImage, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage, VkDescriptorType.SampledImage];
            _giStages[TraceStage] = MakeComputeStage(trace, traced, (uint)sizeof(IlluminationTrace));
            _giStages[SolidTraceStage] = MakeComputeStage(spirv[6], traced, (uint)sizeof(IlluminationTrace));
            _giStages[MergeStage] = MakeComputeStage(merge, [VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage,
                VkDescriptorType.SampledImage, VkDescriptorType.SampledImage], 48);
            _giStages[AmbientStage] = MakeComputeStage(ambient, [VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.SampledImage, VkDescriptorType.StorageImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.SampledImage, VkDescriptorType.StorageImage], 32);
            return _giStages;
        }
    }

    /// <summary>
    /// Makes the probes' pipelines from <c>gi_trace.slang</c>, <c>gi_merge.slang</c>,
    /// <c>gi_ambient.slang</c>, <c>gi_screen.slang</c>, <c>gi_screen_filter.slang</c> and
    /// <c>gi_screen_glow.slang</c>, once, with <paramref name="solidTrace"/>, <c>gi_trace.slang</c>
    /// built with <c>NO_LIGHT_THROUGH</c>, for a frame where no surface lets light through, or the
    /// trace itself where there is none.
    /// </summary>
    public void InitializeGlobalIllumination(ReadOnlySpan<byte> trace, ReadOnlySpan<byte> merge, ReadOnlySpan<byte> ambient, ReadOnlySpan<byte> screen,
        ReadOnlySpan<byte> filter, ReadOnlySpan<byte> glow, ReadOnlySpan<byte> solidTrace = default)
    {
        if (!IsInitialized || CanBounceLight) return;
        _giSpirv = [trace.ToArray(), merge.ToArray(), ambient.ToArray(), screen.ToArray(), filter.ToArray(), glow.ToArray(),
            solidTrace.IsEmpty ? trace.ToArray() : solidTrace.ToArray()];
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
        var mergedSky = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        for (int c = 0; c < texels.Length; c++)
        {
            var n = (uint)texels[c];
            radiance[c] = ProbeImage(p * n, p * n, p);
            merged[c] = ProbeImage(p * n, p * n, p);
            distances[c] = ProbeImage(p * n, p * n, p, VkFormat.R16G16Sfloat);
            mergedSky[c] = ProbeImage(p * n, p * n, p, VkFormat.R16Sfloat);
        }
        var (held, heldMemory, heldView) = ProbeImage(4 * p, p, p * (uint)texels.Length);
        var (partial, partialMemory, partialView) = ProbeImage(4 * p, p, p * (uint)texels.Length, VkFormat.R32G32B32A32Sfloat);
        var (cubes, cubesMemory, cubesView) = ProbeImage(6 * p, p, p * (uint)texels.Length);
        var (reach, reachMemory, reachView) = ProbeImage(8 * p, 8 * p, p * (uint)texels.Length, VkFormat.R16Sfloat);
        var (skies, skiesMemory, skiesView) = ProbeImage(6 * p, p, p * (uint)texels.Length, VkFormat.R16Sfloat);
        var lights = CreateBuffer(new BufferDesc(GpuIllumination.LightsBytes, BufferUsage.Uniform | BufferUsage.TransferDst));

        var cmd = BeginSingleTimeCommands();
        var whole = ColorLevels(0, 1);
        var all = radiance.Concat(merged).Concat(distances).Concat(mergedSky).Select(i => i.Item1).Append(cubes).Append(reach).Append(skies)
            .Append(held).Append(partial).ToArray();
        PipelineBarrier(cmd, [.. all.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite))]);
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        foreach (var image in all.Where(image => image != reach && image != held && image != skies)) _deviceApi.vkCmdClearColorImage(cmd, image, VkImageLayout.General, &none, 1, &whole);
        // Every face sees the whole sky until its rays have first been traced, so the environment's
        // reflection shows as it did before the probes know better.
        var open = new VkClearColorValue(1f, 0f, 0f, 0f);
        _deviceApi.vkCmdClearColorImage(cmd, skies, VkImageLayout.General, &open, 1, &whole);
        // No probe's own light known yet, and every probe taking the whole of the indirect light.
        var unknown = new VkClearColorValue(1f, 1f, 1f, -1f);
        _deviceApi.vkCmdClearColorImage(cmd, held, VkImageLayout.General, &unknown, 1, &whole);
        // Every probe sees every way until its rays have first been traced.
        var far = new VkClearColorValue(10000f, 0f, 0f, 0f);
        _deviceApi.vkCmdClearColorImage(cmd, reach, VkImageLayout.General, &far, 1, &whole);
        _deviceApi.vkCmdFillBuffer(cmd, ((VulkanBuffer)lights).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        PipelineBarrier(cmd, [
            .. all.Where(image => image != cubes && image != reach && image != skies).Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.General,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite)),
            .. new[] { cubes, reach, skies }.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
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
        var skiesOwner = new VulkanImage(this, skies, skiesMemory,
            new ImageDesc(new Extent2D(6 * p, p), ImageFormat.R16_Float, ImageUsage.Sampled | ImageUsage.Storage));
        Name(skiesOwner, "Light probes' share of the sky");
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
                _deviceApi.vkDestroyImageView(skiesView);
                skiesOwner.Dispose();
                foreach (var (image, memory, view) in radiance.Concat(merged).Concat(distances).Concat(mergedSky).Append((held, heldMemory, heldView))
                    .Append((partial, partialMemory, partialView)))
                {
                    _deviceApi.vkDestroyImageView(view);
                    DeviceObjects.Gone(DeviceObjects.Kind.Image);
                    _deviceApi.vkDestroyImage(image);
                    DeviceObjects.Gone(DeviceObjects.Kind.Memory);
                    _deviceApi.vkFreeMemory(memory);
                }
            }, [.. distances.Select(i => (i.Item1, i.Item3))], reach, new VulkanImageView(this, reachOwner, reachView),
            partialView, heldView, [.. mergedSky.Select(i => (i.Item1, i.Item3))], skies, new VulkanImageView(this, skiesOwner, skiesView));
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
    /// Records a frame of the probes' work after the field's, the lights written, each cascade's
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
    /// <param name="lying">Whether each cascade lies where it lay the frame before, so a probe's own light and its share of the indirect light of the frame before are its own, or none where the indirect light is taken whole however a probe's own light falls.</param>
    /// <param name="lastCounted">The cascade before the far cascade's probes, whose rays bring the light from all around where they meet nothing, as the last cascade's do, or -1 where the field has no far cascade.</param>
    /// <param name="lightThrough">Whether some surface lets light through this frame, which the rays cross, where a frame of none traces with the build that leaves the crossing out.</param>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordGlobalIllumination(ICommandBuffer commandBuffer, GpuIllumination gi, GpuSceneField field, IImageView environment,
        ISampler environmentSampler, ReadOnlySpan<byte> lights, IReadOnlyList<(float Start, float End)> intervals, float spacing,
        bool mergeOff = false, int alone = -1, bool viewed = false, long frame = 0, IReadOnlyList<bool>? lying = null, int lastCounted = -1, bool lightThrough = false)
    {
        var trace = lightThrough ? TraceStage : SolidTraceStage;
        var run = new ProbeRun(this, commandBuffer, [(trace, gi.Cascades), (MergeStage, gi.Cascades), (AmbientStage, gi.Cascades)]);
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
            var set = run.Set(trace);
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
            run.Image(set, 11, VkDescriptorType.SampledImage, ((VulkanImageView)field.FacesView).View, null, VkImageLayout.ShaderReadOnlyOptimal);
            var push = new IlluminationTrace
            {
                Cascade = (uint)c, Probes = p, Texels = n, Last = c == gi.Cascades - 1 || c == lastCounted ? 1u : 0u,
                Start = intervals[c].Start, End = intervals[c].End, Spacing = spacing, Cascades = gi.Cascades,
                Kept = lying is not null && lying[c] ? 1u : 0u, Parity = (uint)(frame & 1),
            };
            run.Dispatch(trace, set, MemoryMarshal.AsBytes(new ReadOnlySpan<IlluminationTrace>(in push)),
                (p * p * p * n * n * (uint)gi.RaysAt(c) + 63) / 64);
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
            run.Image(set, 6, VkDescriptorType.StorageImage, gi.MergedSky[c].View, null, VkImageLayout.General);
            // The last cascade reads no cascade above, its own image standing in for the binding.
            run.Image(set, 7, VkDescriptorType.SampledImage, gi.MergedSky[Math.Min(c + 1, gi.Cascades - 1)].View, null, VkImageLayout.General);
            // The reach the gather wrote the frame before, for the parallax read.
            run.Image(set, 8, VkDescriptorType.SampledImage, ((VulkanImageView)gi.ReachView).View, null, VkImageLayout.ShaderReadOnlyOptimal);
            // The last cascade and the one before the far cascade's probes merge nothing from above,
            // since their rays went on to the field's edge and brought the sky where they met nothing.
            // Merged from the far cascade's probes, whose cells hold the ground as a slab, the one
            // before took their light over its own sky and their share of the sky in place of its own.
            var top = c == gi.Cascades - 1 || c == lastCounted;
            ReadOnlySpan<uint> push = [(uint)c, p, n, top ? 0u : (uint)gi.Texels[c + 1], BitConverter.SingleToUInt32Bits(spacing),
                BitConverter.SingleToUInt32Bits(mergeOff ? 1 : 0), BitConverter.SingleToUInt32Bits(alone + 1),
                BitConverter.SingleToUInt32Bits(top ? 0 : intervals[c + 1].End), (uint)gi.Cascades, 0, 0, 0];
            run.Dispatch(MergeStage, set, MemoryMarshal.AsBytes(push), (p * p * p * n * n + 63) / 64);
            MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        }

        // The faces and the reach go to the general layout for the gather, after this frame's rays read them.
        PipelineBarrier(cmd, [.. new[] { gi.Cubes, gi.Reach, gi.Skies }.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal,
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
            run.Image(set, 6, VkDescriptorType.SampledImage, gi.MergedSky[c].View, null, VkImageLayout.General);
            run.Image(set, 7, VkDescriptorType.StorageImage, ((VulkanImageView)gi.SkiesView).View, null, VkImageLayout.General);
            ReadOnlySpan<uint> push = [(uint)c, p, (uint)gi.Texels[c], (uint)(frame & 1) | (lying is not null && lying[c] ? 2u : 0u),
                BitConverter.SingleToUInt32Bits(intervals[c].End), 0, 0, 0];
            run.Dispatch(AmbientStage, set, MemoryMarshal.AsBytes(push), (p * p * p * 6 + 63) / 64);
        }
        PipelineBarrier(cmd, [.. new[] { gi.Cubes, gi.Reach, gi.Skies }.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General,
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
        var glowSeen = CreateBuffer(new BufferDesc((ulong)(across * down) * GpuScreenProbes.GlowSeenBytes, BufferUsage.Storage | BufferUsage.TransferDst));
        var rayLog = CreateBuffer(new BufferDesc(GpuScreenProbes.RayLogBytes, BufferUsage.Storage, CpuAccessMode.Read));
        Name(rayLog, "Screen probe's rays, as gi.screenprobe asks for them");
        // The glow lights' light a texel of the view's depth each, half the view's size as
        // AmbientOcclusionRenderer draws it.
        var glowDesc = desc with { Extent = new Extent2D(Math.Max(1, width / 2), Math.Max(1, height / 2)) };
        IImage[] glowImages = [.. Enumerable.Range(0, 4).Select(_ => CreateImage(glowDesc)), CreateImage(glowDesc with { Format = ImageFormat.R8G8B8A8_UNorm }),
            CreateImage(glowDesc), CreateImage(glowDesc)];
        var glowViews = glowImages.Select(CreateImageView).ToArray();
        var glow = new ScreenGlow(glowImages[0], glowViews[0], glowImages[1], glowViews[1], glowImages[2], glowViews[2], glowImages[3], glowViews[3],
            glowImages[4], glowViews[4], glowImages[5], glowViews[5], glowImages[6], glowViews[6]);
        Name(glow.Light, "Screen's glow light");
        Name(glow.LastLight, "Screen's glow light, the frame before");
        Name(glow.State, "Screen's glow light's spread and frames");
        Name(glow.LastState, "Screen's glow light's spread and frames, the frame before");
        Name(glow.Normal, "Screen's glow light's normals");
        Name(glow.Far, "Screen's far glow light");
        Name(glow.LastFar, "Screen's far glow light, the frame before");
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        // Every image holds nothing, ready to be sampled, until the first frame places the probes.
        var cmd = BeginSingleTimeCommands();
        var whole = ColorLevels(0, 1);
        IImage[] made = [irradiance, geometry, blended, history, lastGeometry, .. glow.Images];
        VkImage[] images = [.. made.Select(image => ((VulkanImage)image).Image)];
        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite))]);
        // The glow light's texels on no surface, their distance from the eye -1.
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        var nowhere = new VkClearColorValue(0f, 0f, 0f, -1f);
        foreach (var image in made)
            _deviceApi.vkCmdClearColorImage(cmd, ((VulkanImage)image).Image, VkImageLayout.General,
                image == glow.Light || image == glow.LastLight ? &nowhere : &none, 1, &whole);
        _deviceApi.vkCmdFillBuffer(cmd, ((VulkanBuffer)glowSeen).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        PipelineBarrier(cmd, [.. images.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead))]);
        EndSingleTimeCommands(cmd);
        foreach (var image in made) ((VulkanImage)image).Layout = VkImageLayout.ShaderReadOnlyOptimal;

        return new GpuScreenProbes(across, down, tile, irradiance, irradianceView, geometry, geometryView, sampler, view, () =>
        {
            sampler.Dispose();
            view.Dispose();
            glowSeen.Dispose();
            rayLog.Dispose();
            IDisposable[] all = [irradianceView, geometryView, blendedView, historyView, lastGeometryView, .. glow.Views,
                irradiance, geometry, blended, history, lastGeometry, .. glow.Images];
            foreach (var one in all) one.Dispose();
        }, blended, blendedView, history, historyView, lastGeometry, lastGeometryView, glowSeen, glow, rayLog);
    }

    /// <summary>
    /// Records the screen probes' work after the world's probes are merged, the view written, each
    /// probe placed on the window's depth and its rays traced and summed, left ready for the model
    /// pass to sample.
    /// </summary>
    /// <remarks>
    /// With <paramref name="glowOnly"/> set the probes stand for the glow lights alone, as at
    /// <see cref="GlobalIllumination.Low"/>, which leaves the screen's probes out, each placed and
    /// marching to the lights with no ray traced, filtered or held, so the glow lights' light on the
    /// depth's texels is made from them as it is from the whole probes.
    /// </remarks>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordScreenProbes(ICommandBuffer commandBuffer, GpuIllumination gi, GpuScreenProbes screen, GpuSceneField field,
        IImageView depth, ISampler depthSampler, ReadOnlySpan<byte> view, bool glowLights, int loggedProbe = -1, bool glowOnly = false,
        bool loggedNotANumber = false, bool lightThrough = false)
    {
        var run = new ProbeRun(this, commandBuffer, [(ScreenStage, 1), (FilterStage, glowOnly ? 0 : 1), (GlowStage, glowLights ? 1 : 0)]);
        var cmd = run.Commands;
        var readers = VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;
        MemoryBarrier(cmd, readers, VkAccessFlags2.UniformRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        var bytes = view[..(Math.Min(view.Length, GpuScreenProbes.ViewBytes) & ~3)];
        fixed (byte* data = bytes)
            _deviceApi.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)screen.View).Buffer, 0, (ulong)bytes.Length, data);
        MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.UniformRead);
        // The glow lights' buffer written again only once the frame before's passes have read it.
        MemoryBarrier(cmd, readers, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite);
        var glow = screen.Glow;
        VkImage[] images = [.. new[] { screen.Irradiance, screen.Geometry, screen.Blended, screen.History, screen.LastGeometry }.Concat(glow.Images)
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
        run.Buffer(set, 13, VkDescriptorType.StorageBuffer, screen.GlowSeen);
        run.Buffer(set, 14, VkDescriptorType.StorageBuffer, screen.RayLog);
        run.Image(set, 15, VkDescriptorType.SampledImage, ((VulkanImageView)field.FacesView).View, null, VkImageLayout.ShaderReadOnlyOptimal);
        ReadOnlySpan<uint> push = [(uint)gi.Texels[0], gi.Cascades > 1 ? (uint)gi.Texels[1] : 0, (uint)(loggedProbe + 1), (glowOnly ? 1u : 0) | (loggedNotANumber ? 2u : 0) | (lightThrough ? 4u : 0)];
        var (pipeline, layout, _) = GiStages[ScreenStage];
        _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, pipeline);
        _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
        fixed (uint* p = push)
            _deviceApi.vkCmdPushConstants(cmd, layout, VkShaderStageFlags.Compute, 0, 16, p);
        _deviceApi.vkCmdDispatch(cmd, (uint)(screen.Across + 7) / 8, (uint)(screen.Down + 7) / 8, 1);

        // Each probe's light blended with its neighbors' on like surfaces, where they traced any.
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        if (!glowOnly)
        {
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
        }

        // The glow lights' light on each texel of the view's depth, from the probes around it and
        // the frame before's, beside the blend, which writes none of what it reads. A frame with no
        // glow lights skips it, the model pass reading none of it then. Its threads leaving at
        // once, it cost each frame of an app of a cube 0.09 s on lavapipe at 320 by 180, an eighth
        // of it.
        var glowExtent = glow.Light.Description.Extent;
        if (glowLights)
        {
            var lit = run.Set(GlowStage);
            run.Image(lit, 0, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)depth).View, depthSampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(lit, 1, VkDescriptorType.SampledImage, ((VulkanImageView)screen.GeometryView).View, null, VkImageLayout.General);
            run.Buffer(lit, 2, VkDescriptorType.UniformBuffer, screen.View);
            run.Buffer(lit, 3, VkDescriptorType.UniformBuffer, gi.Lights);
            run.Buffer(lit, 4, VkDescriptorType.UniformBuffer, field.Info);
            run.Buffer(lit, 5, VkDescriptorType.StorageBuffer, screen.GlowSeen);
            run.Image(lit, 6, VkDescriptorType.StorageImage, ((VulkanImageView)glow.LightView).View, null, VkImageLayout.General);
            run.Image(lit, 7, VkDescriptorType.SampledImage, ((VulkanImageView)glow.LastLightView).View, null, VkImageLayout.General);
            run.Image(lit, 8, VkDescriptorType.StorageImage, ((VulkanImageView)glow.StateView).View, null, VkImageLayout.General);
            run.Image(lit, 9, VkDescriptorType.SampledImage, ((VulkanImageView)glow.LastStateView).View, null, VkImageLayout.General);
            run.Image(lit, 10, VkDescriptorType.StorageImage, ((VulkanImageView)glow.FarView).View, null, VkImageLayout.General);
            run.Image(lit, 11, VkDescriptorType.StorageImage, ((VulkanImageView)glow.NormalView).View, null, VkImageLayout.General);
            run.Image(lit, 12, VkDescriptorType.SampledImage, ((VulkanImageView)glow.LastFarView).View, null, VkImageLayout.General);
            var (glowPipeline, glowLayout, _) = GiStages[GlowStage];
            _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, glowPipeline);
            _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, glowLayout, 0, 1, &lit, 0, null);
            _deviceApi.vkCmdDispatch(cmd, (glowExtent.Width + 7) / 8, (glowExtent.Height + 7) / 8, 1);
        }

        // This frame's blended light and surfaces, and its glow light, kept for the next frame's blends.
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead | VkAccessFlags2.TransferWrite);
        var region = new VkImageCopy
        {
            srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            extent = new VkExtent3D((uint)screen.Across, (uint)screen.Down, 1),
        };
        if (!glowOnly)
        {
            _deviceApi.vkCmdCopyImage(cmd, ((VulkanImage)screen.Blended).Image, VkImageLayout.General, ((VulkanImage)screen.History).Image, VkImageLayout.General, 1, &region);
            _deviceApi.vkCmdCopyImage(cmd, ((VulkanImage)screen.Geometry).Image, VkImageLayout.General, ((VulkanImage)screen.LastGeometry).Image, VkImageLayout.General, 1, &region);
        }
        region.extent = new VkExtent3D(glowExtent.Width, glowExtent.Height, 1);
        _deviceApi.vkCmdCopyImage(cmd, ((VulkanImage)glow.Light).Image, VkImageLayout.General, ((VulkanImage)glow.LastLight).Image, VkImageLayout.General, 1, &region);
        _deviceApi.vkCmdCopyImage(cmd, ((VulkanImage)glow.State).Image, VkImageLayout.General, ((VulkanImage)glow.LastState).Image, VkImageLayout.General, 1, &region);
        _deviceApi.vkCmdCopyImage(cmd, ((VulkanImage)glow.Far).Image, VkImageLayout.General, ((VulkanImage)glow.LastFar).Image, VkImageLayout.General, 1, &region);

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

    // One frame's recording of the probes' work, the descriptor pool its sets come from, as many of
    // each stage as it is made for, freed once no frame in flight reads them.
    private sealed class ProbeRun : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly VkDescriptorPool _pool;

        public ProbeRun(GraphicsDevice device, ICommandBuffer commandBuffer, (int Stage, int Sets)[] sets)
        {
            if (commandBuffer is not VulkanCommandBuffer vkCmd) throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
            _device = device;
            Commands = vkCmd.Handle;
            _pool = device.MakeDescriptorPool(sets.Select(s => (device.GiStages[s.Stage].Descriptors, s.Sets)));
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

        public void Dispose() => _device.DestroyDescriptorPool(_pool);
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
