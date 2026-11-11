using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Engine.GraphicsDevice;
using static Engine.Illumination;

namespace Engine;

/// <summary>
/// The probes' compute stages on the device and the passes that make, trace, merge and gather them,
/// with the screen's probes (GlobalIlluminationStages.ScreenProbes.cs) and the reads a test or a
/// command takes of them (GlobalIlluminationStages.Readback.cs), a part the device keeps
/// (<see cref="GraphicsDevice.Part{T}"/>) and lets go with itself.
/// </summary>
internal sealed unsafe partial class GlobalIlluminationStages(GraphicsDevice device) : IDisposable
{
    /// <summary>The device's probe stages, made the first time they are asked for.</summary>
    internal static GlobalIlluminationStages Of(GraphicsDevice device) => device.Part(static device => new GlobalIlluminationStages(device));

    private const int TraceStage = 0, MergeStage = 1, AmbientStage = 2, ScreenStage = 3, FilterStage = 4, GlowStage = 5, SolidTraceStage = 6, RayTraceStage = 7,
        RayCrossStage = 8;
    private readonly ComputeStage[] _giStages = new ComputeStage[9];

    /// <summary>What <c>gi_trace.slang</c> is pushed.</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct IlluminationTrace
    {
        public uint Cascade, Probes, Texels, Last;
        public float Start, End, Spacing, Cascades;
        public uint Kept;
        public float Edge;
        public uint Parity;
        public float BelowEnd;
        public uint TexelsAbove, First, Flags, Half;
    }

    /// <summary>
    /// Whether cascade <paramref name="c"/> traces half its probes this frame, those whose
    /// coordinates' sum has the frame's parity, each keeping its rays' light of the frame before
    /// until its turn. A cascade past the first does so while it lies where it lay the frame before,
    /// the far cascade keeping its own parts, where the renderer asks it once a change of the lights
    /// or the field has settled, and a probe carried with its rays moving traces every frame all the
    /// same, its cycle of sixteen spots being one a frame (gi_trace.slang). The coarser cascades'
    /// light changes slowest, and they trace as many rays a frame as the first, so in halves a frame
    /// they cost about half.
    /// </summary>
    internal static bool Halved(int c, int cascades, int lastCounted, IReadOnlyList<bool>? lying, bool spread) =>
        spread && c >= 1 && !(lastCounted >= 0 && c == cascades - 1) && lying is not null && lying[c];

    /// <summary>The parts the far cascade's probes are traced in, one a frame (<see cref="RecordGlobalIllumination"/>).</summary>
    internal const int FarParts = 4;

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
            _giStages[GlowStage] = device.MakeComputeStage(glow, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.SampledImage,
                VkDescriptorType.UniformBuffer, VkDescriptorType.UniformBuffer, VkDescriptorType.UniformBuffer, VkDescriptorType.StorageBuffer,
                VkDescriptorType.StorageImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage, VkDescriptorType.SampledImage,
                VkDescriptorType.StorageImage, VkDescriptorType.StorageImage, VkDescriptorType.SampledImage], 16);
            _giStages[FilterStage] = device.MakeComputeStage(filter, [VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler,
                VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage,
                VkDescriptorType.SampledImage, VkDescriptorType.StorageImage], 16);
            _giStages[ScreenStage] = device.MakeComputeStage(screen, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler,
                VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer,
                VkDescriptorType.CombinedImageSampler, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage,
                VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler, VkDescriptorType.SampledImage, VkDescriptorType.StorageBuffer,
                VkDescriptorType.StorageBuffer, VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.SampledImage,
                VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.CombinedImageSampler], 32);
            VkDescriptorType[] traced = [VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer,
                VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler, VkDescriptorType.UniformBuffer,
                VkDescriptorType.StorageImage, VkDescriptorType.CombinedImageSampler, VkDescriptorType.CombinedImageSampler,
                VkDescriptorType.StorageImage, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage, VkDescriptorType.SampledImage,
                VkDescriptorType.SampledImage, VkDescriptorType.CombinedImageSampler, VkDescriptorType.SampledImage, VkDescriptorType.CombinedImageSampler,
                VkDescriptorType.StorageImage, VkDescriptorType.StorageImage];
            _giStages[TraceStage] = device.MakeComputeStage(trace, traced, (uint)sizeof(IlluminationTrace));
            _giStages[SolidTraceStage] = device.MakeComputeStage(spirv[6], traced, (uint)sizeof(IlluminationTrace));
            // The builds that trace the meshes through the device's rays, without and with the
            // crossing of what lets light through, the ray scene bound after the field's images,
            // where the device traces rays and the builds were given.
            for (int k = 7; k <= 8; k++)
                if (spirv[k].Length > 0 && device.CanQueryRays)
                    _giStages[k == 7 ? RayTraceStage : RayCrossStage] = device.MakeComputeStage(spirv[k], [.. traced, VkDescriptorType.AccelerationStructureKHR,
                        VkDescriptorType.StorageBuffer, VkDescriptorType.StorageBuffer], (uint)sizeof(IlluminationTrace));
            _giStages[MergeStage] = device.MakeComputeStage(merge, [VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.UniformBuffer, VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage,
                VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.CombinedImageSampler, VkDescriptorType.SampledImage,
                VkDescriptorType.SampledImage, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage, VkDescriptorType.SampledImage,
                VkDescriptorType.StorageImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage], 48);
            _giStages[AmbientStage] = device.MakeComputeStage(ambient, [VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.SampledImage, VkDescriptorType.StorageImage, VkDescriptorType.SampledImage, VkDescriptorType.StorageImage,
                VkDescriptorType.SampledImage, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage, VkDescriptorType.StorageImage,
                VkDescriptorType.SampledImage, VkDescriptorType.SampledImage], 32);
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
        ReadOnlySpan<byte> filter, ReadOnlySpan<byte> glow, ReadOnlySpan<byte> solidTrace = default, ReadOnlySpan<byte> rayTrace = default,
        ReadOnlySpan<byte> rayCross = default)
    {
        if (!device.IsInitialized || CanBounceLight) return;
        _giSpirv = [trace.ToArray(), merge.ToArray(), ambient.ToArray(), screen.ToArray(), filter.ToArray(), glow.ToArray(),
            solidTrace.IsEmpty ? trace.ToArray() : solidTrace.ToArray(), rayTrace.ToArray(), rayCross.ToArray()];
    }

    /// <summary>
    /// Makes the probes of <paramref name="texels"/>'s count of cascades, <paramref name="probes"/>
    /// along each side of each, each cascade's octahedron <paramref name="texels"/> texels a side,
    /// their faces cleared to no light, the last the far cascade's where <paramref name="farCascade"/>,
    /// and the carried probes' cycle's images made whole where <paramref name="cycle"/>.
    /// </summary>
    public GpuIllumination CreateGlobalIllumination(int probes, int[] texels, bool farCascade = false, bool cycle = false)
    {
        var p = (uint)probes;
        var radiance = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var merged = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var hitNormals = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var cyclePartialBefore = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var cyclePartial = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var cycleFull = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var cycleFullBefore = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var cycleSkyMarkBefore = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var cycleSkyMark = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var cycleEasedBefore = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var cycleEased = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var distances = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        var mergedSky = new (VkImage, VkDeviceMemory, VkImageView)[texels.Length];
        for (int c = 0; c < texels.Length; c++)
        {
            var n = (uint)texels[c];
            radiance[c] = device.ProbeImage(p * n, p * n, p);
            merged[c] = device.ProbeImage(p * n, p * n, p);
            hitNormals[c] = device.ProbeImage(p * n, p * n, p);
            var (cycleSide, cycleDepth) = cycle && c < CarryingCascades(texels.Length, farCascade) ? (p * n, p) : (1u, 1u);
            cyclePartialBefore[c] = device.ProbeImage(cycleSide, cycleSide, cycleDepth);
            cyclePartial[c] = device.ProbeImage(cycleSide, cycleSide, cycleDepth);
            cycleFull[c] = device.ProbeImage(cycleSide, cycleSide, cycleDepth);
            cycleFullBefore[c] = device.ProbeImage(cycleSide, cycleSide, cycleDepth);
            cycleSkyMarkBefore[c] = device.ProbeImage(cycleSide, cycleSide, cycleDepth);
            cycleSkyMark[c] = device.ProbeImage(cycleSide, cycleSide, cycleDepth);
            cycleEasedBefore[c] = device.ProbeImage(cycleSide, cycleSide, cycleDepth);
            cycleEased[c] = device.ProbeImage(cycleSide, cycleSide, cycleDepth);
            distances[c] = device.ProbeImage(p * n, p * n, p, VkFormat.R16G16Sfloat);
            mergedSky[c] = device.ProbeImage(p * n, p * n, p, VkFormat.R16Sfloat);
        }
        var (held, heldMemory, heldView) = device.ProbeImage(4 * p, p, p * (uint)texels.Length);
        var (partial, partialMemory, partialView) = device.ProbeImage(4 * p, p, p * (uint)texels.Length, VkFormat.R32G32B32A32Sfloat);
        var (cubes, cubesMemory, cubesView) = device.ProbeImage(6 * p, p, p * (uint)texels.Length);
        var (cubesAt, cubesAtMemory, cubesAtView) = device.ProbeImage(6 * p, p, p * (uint)texels.Length);
        var (reach, reachMemory, reachView) = device.ProbeImage(GpuIllumination.ReachTexels * p, GpuIllumination.ReachTexels * p, p * (uint)texels.Length, VkFormat.R16Sfloat);
        // Each ray's own reach where a cascade sends four rays a texel, the cascades of 8 a side
        // being the first ones, and one probe deep where none is.
        var rayReachDepth = Math.Max(p * (uint)GpuIllumination.FourRayCascades(texels), 1u);
        var rayReach = device.ProbeImage(GpuIllumination.ReachTexels * p, GpuIllumination.ReachTexels * p, rayReachDepth, VkFormat.R16Sfloat);
        var (skies, skiesMemory, skiesView) = device.ProbeImage(6 * p, p, p * (uint)texels.Length, VkFormat.R16Sfloat);
        var (ways, waysMemory, waysView) = device.ProbeImage(GpuIllumination.WaysTexels * p, GpuIllumination.WaysTexels * p, p * (uint)texels.Length);
        var lights = device.CreateBuffer(new BufferDesc(GpuIllumination.LightsBytes, BufferUsage.Uniform | BufferUsage.TransferDst));

        var cmd = device.BeginSingleTimeCommands();
        var whole = ColorLevels(0, 1);
        var all = radiance.Concat(merged).Concat(hitNormals).Concat(cyclePartialBefore).Concat(cyclePartial).Concat(cycleFull).Concat(cycleFullBefore).Concat(cycleSkyMarkBefore).Concat(cycleSkyMark).Concat(cycleEasedBefore).Concat(cycleEased).Concat(distances).Concat(mergedSky).Select(i => i.Item1).Append(cubes).Append(cubesAt).Append(reach).Append(skies)
            .Append(ways).Append(held).Append(partial).Append(rayReach.Item1).ToArray();
        device.PipelineBarrier(cmd, [.. all.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.Undefined, VkImageLayout.General,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite))]);
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        foreach (var image in all.Where(image => image != reach && image != rayReach.Item1 && image != held && image != skies)) device.Api.vkCmdClearColorImage(cmd, image, VkImageLayout.General, &none, 1, &whole);
        // Every face sees the whole sky until its rays have first been traced, so the environment's
        // reflection shows as it did before the probes know better.
        var open = new VkClearColorValue(1f, 0f, 0f, 0f);
        device.Api.vkCmdClearColorImage(cmd, skies, VkImageLayout.General, &open, 1, &whole);
        // No probe's own light known yet, and every probe taking the whole of the indirect light.
        var unknown = new VkClearColorValue(1f, 1f, 1f, -1f);
        device.Api.vkCmdClearColorImage(cmd, held, VkImageLayout.General, &unknown, 1, &whole);
        // Every probe sees every way until its rays have first been traced.
        var far = new VkClearColorValue(10000f, 0f, 0f, 0f);
        device.Api.vkCmdClearColorImage(cmd, reach, VkImageLayout.General, &far, 1, &whole);
        device.Api.vkCmdClearColorImage(cmd, rayReach.Item1, VkImageLayout.General, &far, 1, &whole);
        device.Api.vkCmdFillBuffer(cmd, ((VulkanBuffer)lights).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        device.PipelineBarrier(cmd, [
            .. all.Where(image => image != cubes && image != cubesAt && image != reach && image != skies && image != ways).Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.General,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite)),
            .. new[] { cubes, cubesAt, reach, skies, ways }.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead)),
        ], new VkMemoryBarrier2
        {
            srcStageMask = VkPipelineStageFlags2.Transfer,
            srcAccessMask = VkAccessFlags2.TransferWrite,
            dstStageMask = VkPipelineStageFlags2.AllCommands,
            dstAccessMask = VkAccessFlags2.UniformRead | VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
        });
        device.EndSingleTimeCommands(cmd);

        var owner = new VulkanImage(device, cubes, cubesMemory,
            new ImageDesc(new Extent2D(6 * p, p), ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.Storage));
        device.Name(owner, "Light probes' faces");
        var reachOwner = new VulkanImage(device, reach, reachMemory,
            new ImageDesc(new Extent2D(GpuIllumination.ReachTexels * p, GpuIllumination.ReachTexels * p), ImageFormat.R16_Float, ImageUsage.Sampled | ImageUsage.Storage));
        device.Name(reachOwner, "Light probes' reach");
        var skiesOwner = new VulkanImage(device, skies, skiesMemory,
            new ImageDesc(new Extent2D(6 * p, p), ImageFormat.R16_Float, ImageUsage.Sampled | ImageUsage.Storage));
        device.Name(skiesOwner, "Light probes' share of the sky");
        var waysOwner = new VulkanImage(device, ways, waysMemory,
            new ImageDesc(new Extent2D(GpuIllumination.WaysTexels * p, GpuIllumination.WaysTexels * p), ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.Storage));
        device.Name(waysOwner, "Light probes' light by way");
        var sampler = device.CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        return new GpuIllumination(probes, [.. texels], [.. radiance.Select(i => (i.Item1, i.Item3))], [.. merged.Select(i => (i.Item1, i.Item3))],
            cubes, new VulkanImageView(device, owner, cubesView), sampler, lights, () =>
            {
                sampler.Dispose();
                lights.Dispose();
                device.Api.vkDestroyImageView(cubesView);
                owner.Dispose();
                device.Api.vkDestroyImageView(reachView);
                reachOwner.Dispose();
                device.Api.vkDestroyImageView(skiesView);
                skiesOwner.Dispose();
                device.Api.vkDestroyImageView(waysView);
                waysOwner.Dispose();
                foreach (var (image, memory, view) in radiance.Concat(merged).Concat(hitNormals).Concat(cyclePartialBefore).Concat(cyclePartial).Concat(cycleFull).Concat(cycleFullBefore).Concat(cycleSkyMarkBefore).Concat(cycleSkyMark).Concat(cycleEasedBefore).Concat(cycleEased).Concat(distances).Concat(mergedSky).Append((held, heldMemory, heldView))
                    .Append((partial, partialMemory, partialView)).Append((cubesAt, cubesAtMemory, cubesAtView)).Append(rayReach))
                {
                    device.Api.vkDestroyImageView(view);
                    DeviceObjects.Gone(DeviceObjects.Kind.Image);
                    device.Api.vkDestroyImage(image);
                    DeviceObjects.Gone(DeviceObjects.Kind.Memory);
                    device.Api.vkFreeMemory(memory);
                }
            }, [.. distances.Select(i => (i.Item1, i.Item3))], reach, new VulkanImageView(device, reachOwner, reachView),
            partialView, heldView, [.. mergedSky.Select(i => (i.Item1, i.Item3))], skies, new VulkanImageView(device, skiesOwner, skiesView),
            ways, new VulkanImageView(device, waysOwner, waysView)) { Far = farCascade, CubesAt = cubesAt, CubesAtView = cubesAtView, RayReach = rayReach.Item3,
                HitNormals = [.. hitNormals.Select(i => (i.Item1, i.Item3))], CyclePartialBefore = [.. cyclePartialBefore.Select(i => (i.Item1, i.Item3))],
                CyclePartial = [.. cyclePartial.Select(i => (i.Item1, i.Item3))], CycleFull = [.. cycleFull.Select(i => (i.Item1, i.Item3))],
                CycleFullBefore = [.. cycleFullBefore.Select(i => (i.Item1, i.Item3))], CycleSkyMarkBefore = [.. cycleSkyMarkBefore.Select(i => (i.Item1, i.Item3))],
                CycleSkyMark = [.. cycleSkyMark.Select(i => (i.Item1, i.Item3))], CycleEasedBefore = [.. cycleEasedBefore.Select(i => (i.Item1, i.Item3))],
                CycleEased = [.. cycleEased.Select(i => (i.Item1, i.Item3))], CycleOn = cycle, CycleCascades = cycle ? CarryingCascades(texels.Length, farCascade) : 0, HeldImage = held };
    }

    // The cascades that can carry a probe's rays past their interval, every one but the last counted
    // and the far (gi_trace.slang's carried).
    private static int CarryingCascades(int cascades, bool farCascade) => Math.Max(0, cascades - (farCascade ? 2 : 1));

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
    /// <param name="sunMap">The window's shadow map as the frame before drew it, or a white texture where <paramref name="lights"/> holds none of its cascades.</param>
    /// <param name="sunSampler">Its sampler.</param>
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
    /// <param name="fieldWhole">Whether the field holds every mesh in reach (<see cref="SceneFieldPlan.Whole"/>), so a ray carried to the field's edge that meets nothing brings the sky.</param>
    /// <param name="farPart">Which of the <see cref="FarParts"/> parts of the far cascade's probes trace their rays this frame, or -1 for all of them.</param>
    /// <param name="rays">The window's meshes for the device's rays, which the probes' rays are traced through at the top quality in a frame where no surface lets light through, or null for the field alone.</param>
    /// <param name="shifts">How far each cascade's probes moved since the frame before, in probes, or null for one with no light before to blend with.</param>
    /// <param name="carriedShare">The share of a carried probe's own light this frame takes in its mean.</param>
    /// <param name="carriedSince">The frames since the lights or the field last changed, which begins a carried probe's cycle again at 0.</param>
    /// <param name="traceFlags">1 where a carried probe's rays move within their texels and are traced through <paramref name="rays"/>, 2 where every ray is.</param>
    /// <param name="spread">Whether the cascades past the first trace half their probes a frame while they lie still (<see cref="Halved"/>).</param>
    /// <param name="carriedPhase">Where a carried probe's cycle of sixteen frames stands, counted from the last change, so the first whole cycle is gathered in the sixteen frames after it.</param>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordGlobalIllumination(ICommandBuffer commandBuffer, GpuIllumination gi, GpuSceneField field, IImageView environment,
        ISampler environmentSampler, IImageView sunMap, ISampler sunSampler, ReadOnlySpan<byte> lights, IReadOnlyList<(float Start, float End)> intervals, float spacing,
        bool mergeOff = false, int alone = -1, bool viewed = false, long frame = 0, IReadOnlyList<bool>? lying = null, int lastCounted = -1, bool lightThrough = false,
        bool fieldWhole = false, int farPart = -1, GpuRayScene? rays = null, IReadOnlyList<(int X, int Y, int Z)?>? shifts = null, float carriedShare = 1, uint carriedSince = 0, uint carriedPhase = 0, uint traceFlags = 0, bool spread = false)
    {
        // Traced through the meshes where asked and the build for the frame was made, crossing what
        // lets light through where the frame holds any.
        var meshed = lightThrough ? RayCrossStage : RayTraceStage;
        var traced = rays is not null && GiStages[meshed].Pipeline.Handle != 0;
        var trace = traced ? meshed : lightThrough ? TraceStage : SolidTraceStage;
        var run = new ProbeRun(this, device, commandBuffer, [(trace, gi.Cascades), (MergeStage, gi.Cascades), (AmbientStage, gi.Cascades)]);
        var cmd = run.Commands;
        var readers = VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;
        device.MemoryBarrier(cmd, readers, VkAccessFlags2.UniformRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        var bytes = lights[..(Math.Min(lights.Length, GpuIllumination.LightsBytes) & ~3)];
        fixed (byte* data = bytes)
            device.Api.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)gi.Lights).Buffer, 0, (ulong)bytes.Length, data);
        // The lights are written, and the merges and faces of the frame before are read, before the
        // rays, a view's reads of them in the frame before's window pass too while one is shown.
        var before = VkPipelineStageFlags2.Transfer | VkPipelineStageFlags2.ComputeShader | (viewed ? VkPipelineStageFlags2.FragmentShader : 0);
        device.MemoryBarrier(cmd, before, VkAccessFlags2.TransferWrite | VkAccessFlags2.ShaderRead,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.UniformRead | VkAccessFlags2.ShaderWrite);
        // The shadow map the frame before's shadow pass wrote, before the rays read the sun through it.
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests, VkAccessFlags2.DepthStencilAttachmentWrite,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);

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
            run.Image(set, 12, VkDescriptorType.SampledImage, ((VulkanImageView)field.SecondView).View, null, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(set, 13, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)sunMap).View, sunSampler, VkImageLayout.ShaderReadOnlyOptimal);
            // The cascade above's share of the sky by direction, as its merge wrote it the frame
            // before, the last cascade's own standing in for the binding.
            run.Image(set, 14, VkDescriptorType.SampledImage, gi.MergedSky[Math.Min(c + 1, gi.Cascades - 1)].View, null, VkImageLayout.General);
            run.Image(set, 15, VkDescriptorType.CombinedImageSampler, gi.CubesAtView, gi.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(set, 16, VkDescriptorType.StorageImage, gi.HitNormals[c].View, null, VkImageLayout.General);
            run.Image(set, 17, VkDescriptorType.StorageImage, gi.RayReach, null, VkImageLayout.General);
            if (traced)
                device.WriteRayScene(set, 18, rays!);
            var push = new IlluminationTrace
            {
                Cascade = (uint)c, Probes = p, Texels = n, Last = c == gi.Cascades - 1 || c == lastCounted ? 1u : 0u,
                Start = intervals[c].Start, End = intervals[c].End, Spacing = spacing, Cascades = gi.Cascades,
                // The frame's parity, and 2 where the field holds every mesh in reach.
                Kept = lying is not null && lying[c] ? 1u : 0u, Parity = (uint)(frame & 1) | (fieldWhole ? 2u : 0u),
                Edge = intervals[lastCounted >= 0 ? lastCounted : gi.Cascades - 1].End,
                BelowEnd = c > 0 ? intervals[c - 1].End : 0,
                TexelsAbove = c + 1 < gi.Cascades && c != lastCounted ? (uint)gi.Texels[c + 1] : 0,
                Flags = gi.CycleOn ? traceFlags : traceFlags & 2u,
            };
            // The far cascade's probes trace one part of them a frame, layers of probes in turn,
            // each part's rays' light kept until its turn comes again, and the merge and the gather
            // take them whole each frame as they lie. Their rays are a large share of the far
            // cascade's work, and traced a quarter a frame the far light lies at most three frames
            // behind, a cave's far walls reading as dark as they do traced whole.
            var (first, count) = (0u, p * p * p);
            if (c == gi.Cascades - 1 && lastCounted >= 0 && farPart >= 0)
            {
                var part = (count + FarParts - 1) / FarParts;
                first = Math.Min(count, (uint)farPart * part);
                count = Math.Min(part, count - first);
            }
            push.First = first;
            if (Halved(c, gi.Cascades, lastCounted, lying, spread))
                push.Half = 1u | (uint)(frame & 1) << 1;
            run.Dispatch(trace, set, MemoryMarshal.AsBytes(new ReadOnlySpan<IlluminationTrace>(in push)),
                (count * n * n * (uint)gi.RaysAt(c) + 63) / 64);
            MarkGlobalIllumination(commandBuffer, $"gi_trace{c}");
        }
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);

        // Each cascade's carried probes' cycle as the frame before left it, which the merges read.
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead | VkAccessFlags2.TransferWrite);
        for (int c = 0; c < gi.CycleCascades; c++)
        {
            var (side, depth) = ((uint)(gi.Probes * gi.Texels[c]), (uint)gi.Probes);
            var copy = new VkImageCopy
            {
                srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                extent = new VkExtent3D(side, side, depth),
            };
            device.Api.vkCmdCopyImage(cmd, gi.CyclePartial[c].Image, VkImageLayout.General, gi.CyclePartialBefore[c].Image, VkImageLayout.General, 1, &copy);
            device.Api.vkCmdCopyImage(cmd, gi.CycleFull[c].Image, VkImageLayout.General, gi.CycleFullBefore[c].Image, VkImageLayout.General, 1, &copy);
            device.Api.vkCmdCopyImage(cmd, gi.CycleSkyMark[c].Image, VkImageLayout.General, gi.CycleSkyMarkBefore[c].Image, VkImageLayout.General, 1, &copy);
            device.Api.vkCmdCopyImage(cmd, gi.CycleEased[c].Image, VkImageLayout.General, gi.CycleEasedBefore[c].Image, VkImageLayout.General, 1, &copy);
        }
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite | VkAccessFlags2.TransferRead,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
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
            run.Image(set, 9, VkDescriptorType.CombinedImageSampler, ((VulkanImageView)field.AlbedoView).View, field.Sampler, VkImageLayout.ShaderReadOnlyOptimal);
            run.Image(set, 10, VkDescriptorType.SampledImage, gi.CyclePartialBefore[c].View, null, VkImageLayout.General);
            run.Image(set, 11, VkDescriptorType.SampledImage, gi.CycleFullBefore[c].View, null, VkImageLayout.General);
            run.Image(set, 12, VkDescriptorType.StorageImage, gi.CyclePartial[c].View, null, VkImageLayout.General);
            run.Image(set, 13, VkDescriptorType.StorageImage, gi.CycleFull[c].View, null, VkImageLayout.General);
            run.Image(set, 14, VkDescriptorType.SampledImage, gi.CycleSkyMarkBefore[c].View, null, VkImageLayout.General);
            run.Image(set, 15, VkDescriptorType.StorageImage, gi.CycleSkyMark[c].View, null, VkImageLayout.General);
            run.Image(set, 16, VkDescriptorType.SampledImage, gi.CycleEasedBefore[c].View, null, VkImageLayout.General);
            run.Image(set, 17, VkDescriptorType.StorageImage, gi.CycleEased[c].View, null, VkImageLayout.General);
            // How far the probes moved, three signed bytes, with 1 << 24 where there is light before.
            var moved = shifts is not null && c < shifts.Count && shifts[c] is { } shift && Math.Max(Math.Abs(shift.X), Math.Max(Math.Abs(shift.Y), Math.Abs(shift.Z))) < p
                ? (uint)(byte)(sbyte)shift.X | (uint)(byte)(sbyte)shift.Y << 8 | (uint)(byte)(sbyte)shift.Z << 16 | 1u << 24 : 0u;
            if (c < gi.CycleCascades && (traceFlags & 1u) != 0) moved |= 1u << 25;
            // The last cascade and the one before the far cascade's probes merge nothing from above,
            // since their rays went on to the field's edge and brought the sky where they met nothing.
            // Merged from the far cascade's probes, whose cells hold the ground as a slab, the one
            // before took their light over its own sky and their share of the sky in place of its own.
            var top = c == gi.Cascades - 1 || c == lastCounted;
            ReadOnlySpan<uint> push = [(uint)c, p, n, top ? 0u : (uint)gi.Texels[c + 1], BitConverter.SingleToUInt32Bits(spacing),
                BitConverter.SingleToUInt32Bits(mergeOff ? 1 : 0), BitConverter.SingleToUInt32Bits(alone + 1),
                BitConverter.SingleToUInt32Bits(top ? 0 : intervals[c + 1].End), (uint)gi.Cascades, moved, carriedSince, carriedPhase];
            run.Dispatch(MergeStage, set, MemoryMarshal.AsBytes(push), (p * p * p * n * n + 63) / 64);
            device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        }
        MarkGlobalIllumination(commandBuffer, "gi_merge");

        // The faces, the reach and the light by way go to the general layout for the gather, after
        // this frame's rays read them.
        device.PipelineBarrier(cmd, [.. new[] { gi.Cubes, gi.CubesAt, gi.Reach, gi.Skies, gi.Ways }.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal,
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
            run.Image(set, 8, VkDescriptorType.StorageImage, ((VulkanImageView)gi.WaysView).View, null, VkImageLayout.General);
            run.Image(set, 9, VkDescriptorType.StorageImage, gi.CubesAtView, null, VkImageLayout.General);
            run.Image(set, 10, VkDescriptorType.SampledImage, gi.HitNormals[c].View, null, VkImageLayout.General);
            run.Image(set, 11, VkDescriptorType.SampledImage, gi.RayReach, null, VkImageLayout.General);
            ReadOnlySpan<uint> push = [(uint)c, p, (uint)gi.Texels[c], (uint)(frame & 1) | (lying is not null && lying[c] ? 2u : 0u),
                BitConverter.SingleToUInt32Bits(intervals[c].End), BitConverter.SingleToUInt32Bits(carriedShare), 0, 0];
            run.Dispatch(AmbientStage, set, MemoryMarshal.AsBytes(push), (p * p * p * 6 + 63) / 64);
        }
        MarkGlobalIllumination(commandBuffer, "gi_gather");
        device.PipelineBarrier(cmd, [.. new[] { gi.Cubes, gi.CubesAt, gi.Reach, gi.Skies, gi.Ways }.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General,
            VkImageLayout.ShaderReadOnlyOptimal, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, readers, VkAccessFlags2.ShaderRead))]);
        // Each probe's own light and share, which the next frame's rays read.
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
        if (viewed)
            device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ShaderRead);
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
            dstSet = SetHandle(set), dstBinding = binding, descriptorCount = 1, descriptorType = VkDescriptorType.SampledImage, pImageInfo = &image,
        };
        device.Api.vkUpdateDescriptorSets(1, &write, 0, null);
    }

    // One frame's recording of the probes' work, the descriptor pool its sets come from, as many of
    // each stage as it is made for, freed once no frame in flight reads them.
    private sealed class ProbeRun : IDisposable
    {
        private readonly GlobalIlluminationStages _stages;
        private readonly GraphicsDevice _device;
        private readonly VkDescriptorPool _pool;

        public ProbeRun(GlobalIlluminationStages stages, GraphicsDevice device, ICommandBuffer commandBuffer, (int Stage, int Sets)[] sets)
        {
            if (commandBuffer is not VulkanCommandBuffer vkCmd) throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
            (_stages, _device) = (stages, device);
            Commands = vkCmd.Handle;
            _pool = device.MakeDescriptorPool(sets.Select(s => (stages.GiStages[s.Stage].Descriptors, s.Sets)));
        }

        public VkCommandBuffer Commands { get; }

        public VkDescriptorSet Set(int stage)
        {
            var layout = _stages.GiStages[stage].SetLayout;
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = _pool, descriptorSetCount = 1, pSetLayouts = &layout };
            VkDescriptorSet set;
            _device.Api.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
            return set;
        }

        public void Image(VkDescriptorSet set, uint binding, VkDescriptorType type, VkImageView view, ISampler? sampler, VkImageLayout layout)
        {
            var image = new VkDescriptorImageInfo { imageView = view, sampler = sampler is VulkanSampler s ? s.Sampler : default, imageLayout = layout };
            var write = new VkWriteDescriptorSet { dstSet = set, dstBinding = binding, descriptorCount = 1, descriptorType = type, pImageInfo = &image };
            _device.Api.vkUpdateDescriptorSets(1, &write, 0, null);
        }

        public void Buffer(VkDescriptorSet set, uint binding, VkDescriptorType type, IBuffer buffer)
        {
            var vk = (VulkanBuffer)buffer;
            var info = new VkDescriptorBufferInfo { buffer = vk.Buffer, offset = 0, range = vk.Description.Size };
            var write = new VkWriteDescriptorSet { dstSet = set, dstBinding = binding, descriptorCount = 1, descriptorType = type, pBufferInfo = &info };
            _device.Api.vkUpdateDescriptorSets(1, &write, 0, null);
        }

        public void Dispatch(int stage, VkDescriptorSet set, ReadOnlySpan<byte> push, uint groups)
        {
            var (pipeline, layout, _) = _stages.GiStages[stage];
            _device.Api.vkCmdBindPipeline(Commands, VkPipelineBindPoint.Compute, pipeline);
            _device.Api.vkCmdBindDescriptorSets(Commands, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
            fixed (byte* p = push)
                _device.Api.vkCmdPushConstants(Commands, layout, VkShaderStageFlags.Compute, 0, (uint)push.Length, p);
            _device.Api.vkCmdDispatch(Commands, Math.Max(1, groups), 1, 1);
        }

        public void Dispose() => _device.DestroyDescriptorPool(_pool);
    }

    /// <summary>Whether global illumination's passes are timed apart in the profile, as <c>gi.passes on</c> sets it.</summary>
    internal bool GlobalIlluminationPassesTimed { get; set; }

    // Marks the end of one of global illumination's passes for the profile, where they are timed
    // apart, the profile naming the time since the mark before by this one's label.
    internal void MarkGlobalIllumination(ICommandBuffer commandBuffer, string label)
    {
        if (GlobalIlluminationPassesTimed && device.TimestampsSupported)
            device.Timestamp(commandBuffer, label);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (pipeline, layout, setLayout) in _giStages.Where(s => s.Pipeline.Handle != 0))
        {
            DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
            device.Api.vkDestroyPipeline(pipeline);
            device.Api.vkDestroyPipelineLayout(layout);
            device.Api.vkDestroyDescriptorSetLayout(setLayout);
        }
        Array.Clear(_giStages);
        _giSpirv = null;
    }
}
