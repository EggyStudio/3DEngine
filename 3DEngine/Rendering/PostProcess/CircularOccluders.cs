using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Vulkan;
using static Engine.GraphicsDevice;

namespace Engine;

/// <summary>
/// Ambient occlusion by circular occluders, Isheden's CORTAO (<c>cortao/</c>, taken at its commit
/// 420b168), worked out from the window's depth at half its size: a prepass packing each pixel's
/// distance along the view and its normal into a chain of levels (<c>ao_cortao_prepass.slang</c>),
/// a main pass reading points of that chain around each pixel as circles and tracing sixteen rays
/// over its hemisphere against them through a table (<c>ao_cortao_main.slang</c>), and its own
/// filter pass (<c>ao_cortao_filter.slang</c>), each a compute stage recorded into the frame ahead
/// of the occlusion's history and blurs (<see cref="AmbientOcclusionRenderer"/>), which read the
/// result in place of the horizons.
/// </summary>
/// <remarks>
/// <para>
/// The engine draws no buffer of normals, so each pass takes a pixel's normal from the depth
/// around it (<c>ao_cortao.h</c>), where CORTAO expects a normal buffer. The table is CORTAO's own
/// for sixteen rays, 24 by 24 ways by 16 sizes (<c>cortao/occluders_24x24x16x16.bin</c>), read as
/// a buffer, and the bent normal is written beside the occlusion and read by nothing yet.
/// </para>
/// <para>
/// Its parameters are worked out as CORTAO's <c>CORTAOParameters.h</c> works them, from the
/// camera's projection with Vulkan's turn of y taken out of it, which CORTAO takes as an
/// unflipped projection read through a flipped y, and the radius, its share of the picture's
/// height, the slope compensation and the backfaces' threshold and boost taken from
/// <see cref="AmbientOcclusionSettings"/>.
/// </para>
/// </remarks>
internal sealed unsafe class CircularOccluders(GraphicsDevice device) : IDisposable
{
    /// <summary>The device's occluders, made the first time it is asked for.</summary>
    internal static CircularOccluders Of(GraphicsDevice device) => device.Part(static device => new CircularOccluders(device));

    /// <summary>The bytes of CORTAO's table for sixteen rays, 24 * 24 * 16 entries of two bytes each.</summary>
    internal const int TableBytes = 24 * 24 * 16 * 2;

    /// <summary>The most levels of the packed image CORTAO reads.</summary>
    internal const int MostLevels = 8;

    /// <summary>The name the main pass is built with to read eight points a pixel, where its plain build reads sixteen.</summary>
    internal const string EightOccluders = "EIGHT_OCCLUDERS";

    [StructLayout(LayoutKind.Sequential)]
    internal struct PrepassPush
    {
        public uint PackedResolution;
        public float DepthScale, DepthOffset;
        public uint RandomSeed, MipCount, Padding0, Padding1, Padding2;
        public Vector4 Unproject;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MainPush
    {
        public uint PackedResolution, RandomSeed;
        public float NormalBias, Radius;
        public Vector4 Unproject, Projection;
        public float MaxApparentCircleRadiusCoefficient, MipBias, InverseMaxSlopeCompensation, BackfaceThreshold;
        public float BackfaceBoost;
        public uint Padding0, Padding1, Padding2;
        public Vector4 DepthToLinear;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FilterPush
    {
        public uint PackedResolution;
        public float DepthSensitivity, NormalSensitivity;
        public uint Padding0;
    }

    private ComputeStage _prepass, _main8, _main16, _filter;
    private IBuffer? _table;
    private ISampler? _nearest;
    private Sized? _sized;
    private readonly List<(long Frame, Sized Sized)> _retired = [];
    private long _frame;
    private bool _ready;

    /// <summary>Whether every stage, the table and the sampler have been made, so the occlusion can be worked out.</summary>
    public bool Ready => _ready;

    /// <summary>Whether a size of the window's images is held, in use or let go and not yet destroyed.</summary>
    public bool Holds => _sized is not null || _retired.Count > 0;

    /// <summary>
    /// Makes the stages from the compute stages of <c>ao_cortao_prepass.slang</c>, of
    /// <c>ao_cortao_main.slang</c> built at 8 and at 16 samples and of <c>ao_cortao_filter.slang</c>,
    /// and uploads <paramref name="table"/>, CORTAO's table for sixteen rays, once, all or none,
    /// what was made before a failure destroyed before it is thrown on.
    /// </summary>
    public void Initialize(ReadOnlySpan<byte> prepass, ReadOnlySpan<byte> main8, ReadOnlySpan<byte> main16, ReadOnlySpan<byte> filter,
        ReadOnlySpan<byte> table)
    {
        if (!device.IsInitialized || Ready) return;
        if (table.Length != TableBytes)
            throw new ArgumentException($"CORTAO's table for sixteen rays holds {TableBytes} bytes, where {table.Length} were given.", nameof(table));
        try
        {
            _prepass = device.MakeComputeStage(prepass, [VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageImage],
                (uint)sizeof(PrepassPush), [1, MostLevels]);
            VkDescriptorType[] main = [VkDescriptorType.SampledImage, VkDescriptorType.Sampler, VkDescriptorType.StorageBuffer,
                VkDescriptorType.CombinedImageSampler, VkDescriptorType.StorageImage];
            _main8 = device.MakeComputeStage(main8, main, (uint)sizeof(MainPush));
            _main16 = device.MakeComputeStage(main16, main, (uint)sizeof(MainPush));
            _filter = device.MakeComputeStage(filter, [VkDescriptorType.SampledImage, VkDescriptorType.SampledImage, VkDescriptorType.Sampler,
                VkDescriptorType.StorageImage], (uint)sizeof(FilterPush));
            _table = device.CreateBuffer(new BufferDesc((ulong)TableBytes, BufferUsage.Storage, CpuAccessMode.Write));
            device.Map(_table).CopyFrom(table);
            device.Unmap(_table);
            _nearest = device.CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
                SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
            _ready = true;
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>The occlusion in alpha and the bent normal in view space as 0.5 + 0.5 of it, as the main pass wrote them, for the window.</summary>
    public IImage? Raw => _sized?.Raw.Owner;

    /// <summary>The occlusion CORTAO's filter pass wrote, in every channel, for the window.</summary>
    public IImage? Filtered => _sized?.Filtered.Owner;

    /// <summary>
    /// The first level of the packed image the main pass read for the window, each texel's distance
    /// along the view in its high bits and its normal in view space in its low ten, rows from the
    /// top, copied once every frame in flight has finished with it, or null where none is held.
    /// </summary>
    internal uint[]? ReadPacked()
    {
        if (_sized is not { } sized || !sized.Written) return null;
        device.FlushUploads();
        device.Api.vkDeviceWaitIdle().CheckResult();
        var (w, h) = (sized.Extent.Width, sized.Extent.Height);
        var buffer = (VulkanBuffer)device.CreateBuffer(new BufferDesc((ulong)(w * h * 4), BufferUsage.TransferDst, CpuAccessMode.Read));
        try
        {
            var cmd = device.BeginSingleTimeCommands();
            device.PipelineBarrier(cmd, ImageBarrier(sized.Packed.Image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.TransferSrcOptimal,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead));
            var copy = new VkBufferImageCopy { imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1), imageExtent = new VkExtent3D(w, h, 1) };
            device.Api.vkCmdCopyImageToBuffer(cmd, sized.Packed.Image, VkImageLayout.TransferSrcOptimal, buffer.Buffer, 1, &copy);
            device.PipelineBarrier(cmd, ImageBarrier(sized.Packed.Image, ColorLevels(0, 1), VkImageLayout.TransferSrcOptimal, VkImageLayout.General,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite));
            device.EndSingleTimeCommands(cmd);
            return MemoryMarshal.Cast<byte, uint>(device.Read(buffer)).ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Records the passes into <paramref name="commandBuffer"/> for the window's depth of
    /// <paramref name="extent"/>, through <paramref name="turned"/>, the camera's view and
    /// projection with its eye at nothing, as <paramref name="settings"/> say, its radius, samples,
    /// filter and circles, and gives the view the history pass reads the occlusion from, or null
    /// where the camera's projection is not one CORTAO takes.
    /// </summary>
    public IImageView? Record(ICommandBuffer commandBuffer, IImageView depth, ISampler depthSampler, Extent2D extent, Matrix4x4 turned,
        AmbientOcclusionSettings settings, uint seed)
    {
        var (samples, filter) = (settings.Samples, settings.OwnFilter);
        Retire();
        if (!Ready || Decompose(turned) is not { } projection) return null;
        var sized = Ensure(extent, depth, depthSampler);
        var cmd = ((VulkanCommandBuffer)commandBuffer).Handle;

        var w = extent.Width;
        var h = extent.Height;
        var (prepass, main, filtered) = Parameters(projection, extent, settings, seed, sized.Levels);
        var first = !sized.Written;
        sized.Written = true;
        // The depth drawn, and the frame before's reads of the results by the occlusion's passes,
        // before the passes write.
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.LateFragmentTests | VkPipelineStageFlags2.EarlyFragmentTests,
            VkAccessFlags2.DepthStencilAttachmentWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        var from = first ? VkImageLayout.Undefined : VkImageLayout.ShaderReadOnlyOptimal;
        device.PipelineBarrier(cmd, [
            ImageBarrier(sized.Packed.Image, ColorLevels(0, sized.Levels), first ? VkImageLayout.Undefined : VkImageLayout.General, VkImageLayout.General,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite),
            ImageBarrier(sized.Raw.Image, ColorLevels(0, 1), from, VkImageLayout.General,
                VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
            ImageBarrier(sized.Filtered.Image, ColorLevels(0, 1), from, VkImageLayout.General,
                VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
        ]);
        // The texels the main pass leaves, those of the sky, and those the filter leaves, its blocks
        // of sky, cleared each frame to open, the bent normal toward the eye.
        var open = new VkClearColorValue(0.5f, 0.5f, 1f, 1f);
        var whole = ColorLevels(0, 1);
        device.Api.vkCmdClearColorImage(cmd, sized.Raw.Image, VkImageLayout.General, &open, 1, &whole);
        open = new VkClearColorValue(1f, 1f, 1f, 1f);
        device.Api.vkCmdClearColorImage(cmd, sized.Filtered.Image, VkImageLayout.General, &open, 1, &whole);
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead);

        Dispatch(cmd, _prepass, sized.PrepassSet, prepass, (w + 7) / 8, (h + 7) / 8);
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        var stage = samples <= 8 ? _main8 : _main16;
        Dispatch(cmd, stage, samples <= 8 ? sized.Main8Set : sized.Main16Set, main, (w + 15) / 16, (h + 15) / 16);
        device.MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        if (filter)
            Dispatch(cmd, _filter, sized.FilterSet, filtered, (w / 2 + 8) / 8, (h / 2 + 8) / 8);

        // Both results to be sampled by the occlusion's passes and read back by a command.
        device.PipelineBarrier(cmd, [
            ImageBarrier(sized.Raw.Image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite | VkAccessFlags2.ShaderRead,
                VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead),
            ImageBarrier(sized.Filtered.Image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
                VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ShaderRead),
        ]);
        return filter ? sized.Filtered.View : sized.Raw.View;
    }

    private void Dispatch<T>(VkCommandBuffer cmd, ComputeStage stage, VkDescriptorSet set, T push, uint groupsX, uint groupsY) where T : unmanaged
    {
        device.Api.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Compute, stage.Pipeline);
        device.Api.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Compute, stage.Layout, 0, 1, &set, 0, null);
        device.Api.vkCmdPushConstants(cmd, stage.Layout, VkShaderStageFlags.Compute, 0, (uint)sizeof(T), &push);
        device.Api.vkCmdDispatch(cmd, Math.Max(1, groupsX), Math.Max(1, groupsY), 1);
    }

    /// <summary>
    /// The projection of <paramref name="turned"/>, a camera's view and projection with its eye at
    /// nothing, a turn times a projection CORTAO takes, a perspective with any offset of its middle
    /// (CORTAO's README, "projectionMatrix"), or null where it is no such product. The turn's last
    /// column of the product is the way back along the view, and the projection's scales and
    /// offsets follow from the turn's columns being of length one and square to each other.
    /// </summary>
    internal static Matrix4x4? Decompose(Matrix4x4 turned)
    {
        var col0 = new Vector3(turned.M11, turned.M21, turned.M31);
        var col1 = new Vector3(turned.M12, turned.M22, turned.M32);
        var col2 = new Vector3(turned.M13, turned.M23, turned.M33);
        var back = -new Vector3(turned.M14, turned.M24, turned.M34);
        if (MathF.Abs(back.Length() - 1) > 1e-3f || MathF.Abs(turned.M44) > 1e-5f) return null;
        var b = Vector3.Dot(col0, back);
        var across = col0 - b * back;
        var a = across.Length();
        var d = Vector3.Dot(col1, back);
        var up = Vector3.Cross(back, across / Math.Max(a, 1e-12f));
        var c = Vector3.Dot(col1 - d * back, up);
        if (a < 1e-6f || MathF.Abs(c) < 1e-6f) return null;
        return new Matrix4x4(a, 0, 0, 0, 0, c, 0, 0, b, d, Vector3.Dot(col2, back), -1, 0, 0, turned.M43, 0);
    }

    // The three passes' parameters for a projection, as CORTAOParameters.h works them out from its
    // configuration, the radius, the share of the picture's height, the slope compensation and the
    // backfaces' threshold and boost the settings', with Vulkan's turn of y taken out of the
    // projection and flipY set.
    internal static (PrepassPush Prepass, MainPush Main, FilterPush Filter) Parameters(Matrix4x4 projection, Extent2D extent, AmbientOcclusionSettings settings,
        uint seed, uint levels)
    {
        var radius = settings.Radius;
        // The projection as CORTAO's column-major array holds it, y up.
        var p = projection;
        if (p.M22 < 0) (p.M12, p.M22, p.M32, p.M42) = (-p.M12, -p.M22, -p.M32, -p.M42);
        Matrix4x4.Invert(p, out var inverse);
        var packed = extent.Width | extent.Height << 16;
        // A depth to 1 / its distance along the view, depth * scale + offset.
        var (scale, offset) = (1 / projection.M43, projection.M33 / projection.M43);
        var (projX, projY, projOffsetX, projOffsetY) = (p.M11, p.M22, p.M31, p.M32);
        var (invX, invY, invOffsetX, invOffsetY) = (inverse.M11, inverse.M22, inverse.M41, inverse.M42);
        var unproject = new Vector4(2 * invX, -2 * invY, -invX + invOffsetX, invY + invOffsetY);
        var apparent = invY * settings.ScreenShare * 2;
        var prepass = new PrepassPush
        {
            PackedResolution = packed, DepthScale = scale, DepthOffset = offset, RandomSeed = seed, MipCount = levels, Unproject = unproject,
        };
        var main = new MainPush
        {
            PackedResolution = packed,
            RandomSeed = seed,
            NormalBias = invX / extent.Width * MathF.Sqrt(2),
            Radius = radius,
            Unproject = unproject,
            Projection = new Vector4(0.5f * projX, -0.5f * projY, -0.5f + 0.5f * projOffsetX, -0.5f - 0.5f * projOffsetY),
            MaxApparentCircleRadiusCoefficient = apparent / MathF.Sqrt(apparent * apparent + 1),
            MipBias = MathF.Log2(projY * extent.Height * 0.5f) - 4,
            InverseMaxSlopeCompensation = 1 / settings.SlopeCompensation,
            BackfaceThreshold = settings.BackfaceThreshold,
            BackfaceBoost = settings.BackfaceBoost,
            DepthToLinear = new Vector4(scale, offset, 0, 0),
        };
        return (prepass, main, new FilterPush { PackedResolution = packed, DepthSensitivity = 512, NormalSensitivity = 128 });
    }

    // What is made for one size of the depth: the packed image and a view of each level, the two
    // results, and the sets each stage reads through.
    private sealed class Sized(Extent2D extent, IImageView depth, uint levels, Storage packed, VkImageView[] levelViews, Storage raw, Storage filtered,
        VkDescriptorPool pool) : IDisposable
    {
        public Extent2D Extent { get; } = extent;
        public IImageView Depth { get; } = depth;
        public uint Levels { get; } = levels;
        public Storage Packed { get; } = packed;
        public VkImageView[] LevelViews { get; } = levelViews;
        public Storage Raw { get; } = raw;
        public Storage Filtered { get; } = filtered;
        public VkDescriptorPool Pool { get; } = pool;
        public VkDescriptorSet PrepassSet, Main8Set, Main16Set, FilterSet;
        // Whether a frame has written the images, which leaves them in the layouts the next frame's barriers take them from.
        public bool Written { get; set; }
        public Action? Disposing { get; init; }

        public void Dispose() => Disposing?.Invoke();
    }

    // An image a compute stage writes and a pass samples, its owner, its view of every level and the raw handle.
    private sealed record Storage(VkImage Image, VulkanImage Owner, IImageView View);

    private Sized Ensure(Extent2D extent, IImageView depth, ISampler depthSampler)
    {
        if (_sized is { } made && made.Extent == extent && ReferenceEquals(made.Depth, depth)) return made;
        if (_sized is not null) _retired.Add((_frame, _sized));

        var levels = Math.Min((uint)MostLevels, ImageDesc.FullMipChain(extent.Width, extent.Height));
        var packed = Image(extent, levels, VkFormat.R32Uint, ImageFormat.R16_Float, "CORTAO's packed depth and normals");
        var levelViews = new VkImageView[levels];
        for (uint l = 0; l < levels; l++) levelViews[l] = View(packed.Image, VkFormat.R32Uint, l, 1);
        var raw = Image(extent, 1, VkFormat.R8G8B8A8Unorm, ImageFormat.R8G8B8A8_UNorm, "CORTAO's occlusion and bent normals");
        var filtered = Image(extent, 1, VkFormat.R8G8B8A8Unorm, ImageFormat.R8G8B8A8_UNorm, "CORTAO's filtered occlusion");
        var pool = device.MakeDescriptorPool([
            ([VkDescriptorType.CombinedImageSampler, .. Enumerable.Repeat(VkDescriptorType.StorageImage, MostLevels)], 1),
            (_main8.Descriptors, 2), (_filter.Descriptors, 1)]);
        var sized = new Sized(extent, depth, levels, packed, levelViews, raw, filtered, pool)
        {
            Disposing = () =>
            {
                device.DestroyDescriptorPool(pool);
                foreach (var view in levelViews) device.Api.vkDestroyImageView(view);
                foreach (var storage in new[] { packed, raw, filtered })
                {
                    storage.View.Dispose();
                    storage.Owner.Dispose();
                }
            },
        };
        sized.PrepassSet = Allocate(pool, _prepass);
        sized.Main8Set = Allocate(pool, _main8);
        sized.Main16Set = Allocate(pool, _main16);
        sized.FilterSet = Allocate(pool, _filter);

        var depthView = ((VulkanImageView)depth).View;
        var depthVk = ((VulkanSampler)depthSampler).Sampler;
        var nearest = ((VulkanSampler)_nearest!).Sampler;
        var table = (VulkanBuffer)_table!;
        var depthInfo = new VkDescriptorImageInfo { imageView = depthView, sampler = depthVk, imageLayout = VkImageLayout.ShaderReadOnlyOptimal };
        var levelInfos = stackalloc VkDescriptorImageInfo[MostLevels];
        for (int l = 0; l < MostLevels; l++)
            levelInfos[l] = new VkDescriptorImageInfo { imageView = levelViews[l < levels ? l : 0], imageLayout = VkImageLayout.General };
        var packedInfo = new VkDescriptorImageInfo { imageView = ((VulkanImageView)packed.View).View, imageLayout = VkImageLayout.General };
        var nearestInfo = new VkDescriptorImageInfo { sampler = nearest };
        var tableInfo = new VkDescriptorBufferInfo { buffer = table.Buffer, offset = 0, range = (ulong)TableBytes };
        var rawStore = new VkDescriptorImageInfo { imageView = ((VulkanImageView)raw.View).View, imageLayout = VkImageLayout.General };
        var filteredStore = new VkDescriptorImageInfo { imageView = ((VulkanImageView)filtered.View).View, imageLayout = VkImageLayout.General };

        var writes = stackalloc VkWriteDescriptorSet[16];
        int n = 0;
        writes[n++] = Write(sized.PrepassSet, 0, VkDescriptorType.CombinedImageSampler, &depthInfo);
        writes[n++] = new VkWriteDescriptorSet { dstSet = sized.PrepassSet, dstBinding = 1, descriptorCount = MostLevels, descriptorType = VkDescriptorType.StorageImage, pImageInfo = levelInfos };
        foreach (var set in new[] { sized.Main8Set, sized.Main16Set })
        {
            writes[n++] = Write(set, 0, VkDescriptorType.SampledImage, &packedInfo);
            writes[n++] = Write(set, 1, VkDescriptorType.Sampler, &nearestInfo);
            writes[n++] = new VkWriteDescriptorSet { dstSet = set, dstBinding = 2, descriptorCount = 1, descriptorType = VkDescriptorType.StorageBuffer, pBufferInfo = &tableInfo };
            writes[n++] = Write(set, 3, VkDescriptorType.CombinedImageSampler, &depthInfo);
            writes[n++] = Write(set, 4, VkDescriptorType.StorageImage, &rawStore);
        }
        // The filter reads the raw result in the general layout it is written in.
        writes[n++] = Write(sized.FilterSet, 0, VkDescriptorType.SampledImage, &packedInfo);
        writes[n++] = Write(sized.FilterSet, 1, VkDescriptorType.SampledImage, &rawStore);
        writes[n++] = Write(sized.FilterSet, 2, VkDescriptorType.Sampler, &nearestInfo);
        writes[n++] = Write(sized.FilterSet, 3, VkDescriptorType.StorageImage, &filteredStore);
        device.Api.vkUpdateDescriptorSets((uint)n, writes, 0, null);
        return _sized = sized;
    }

    private static VkWriteDescriptorSet Write(VkDescriptorSet set, uint binding, VkDescriptorType type, VkDescriptorImageInfo* info) =>
        new() { dstSet = set, dstBinding = binding, descriptorCount = 1, descriptorType = type, pImageInfo = info };

    private VkDescriptorSet Allocate(VkDescriptorPool pool, ComputeStage stage)
    {
        var layout = stage.SetLayout;
        var info = new VkDescriptorSetAllocateInfo { descriptorPool = pool, descriptorSetCount = 1, pSetLayouts = &layout };
        VkDescriptorSet set;
        device.Api.vkAllocateDescriptorSets(&info, &set).CheckResult();
        return set;
    }

    // A 2D image of levels a compute stage writes, a pass samples and a command reads back, named
    // as the device names its images.
    private Storage Image(Extent2D extent, uint levels, VkFormat format, ImageFormat described, string name)
    {
        var info = new VkImageCreateInfo
        {
            imageType = VkImageType.Image2D,
            format = format,
            extent = new VkExtent3D(extent.Width, extent.Height, 1),
            mipLevels = levels,
            arrayLayers = 1,
            samples = VkSampleCountFlags.Count1,
            tiling = VkImageTiling.Optimal,
            usage = VkImageUsageFlags.Storage | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc | VkImageUsageFlags.TransferDst,
            sharingMode = VkSharingMode.Exclusive,
            initialLayout = VkImageLayout.Undefined,
        };
        device.Api.vkCreateImage(&info, null, out VkImage image).CheckResult();
        DeviceObjects.Made(DeviceObjects.Kind.Image);
        device.Api.vkGetImageMemoryRequirements(image, out VkMemoryRequirements requirements);
        var allocation = new VkMemoryAllocateInfo
        {
            allocationSize = requirements.size,
            memoryTypeIndex = device.FindMemoryType(requirements.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal),
        };
        var memory = device.AllocateSaying(&allocation);
        DeviceObjects.Made(DeviceObjects.Kind.Memory);
        device.Api.vkBindImageMemory(image, memory, 0).CheckResult();
        var owner = new VulkanImage(device, image, memory, new ImageDesc(extent, described, ImageUsage.Sampled | ImageUsage.Storage, levels));
        device.Name(owner, name);
        return new Storage(image, owner, new VulkanImageView(device, owner, View(image, format, 0, levels)));
    }

    private VkImageView View(VkImage image, VkFormat format, uint firstLevel, uint levels)
    {
        var viewInfo = new VkImageViewCreateInfo
        {
            image = image,
            viewType = VkImageViewType.Image2D,
            format = format,
            components = VkComponentMapping.Rgba,
            subresourceRange = ColorLevels(firstLevel, levels),
        };
        device.Api.vkCreateImageView(&viewInfo, null, out VkImageView view).CheckResult();
        return view;
    }

    // Destroys the images of a size let go once the frames in flight that might read them have finished.
    private void Retire()
    {
        _frame++;
        for (int i = _retired.Count - 1; i >= 0; i--)
        {
            if (_frame - _retired[i].Frame < GpuTextures.RetireFrames) continue;
            _retired[i].Sized.Dispose();
            _retired.RemoveAt(i);
        }
    }

    /// <summary>
    /// Lets the window's images go, on a frame the occlusion is worked out some other way, giving
    /// each size let go, the one in use and those not yet destroyed, for the caller to destroy once
    /// the frames in flight that might read it have finished.
    /// </summary>
    public IReadOnlyList<IDisposable> Release()
    {
        var gone = new List<IDisposable>(_retired.Count + 1);
        foreach (var (_, sized) in _retired) gone.Add(sized);
        _retired.Clear();
        if (_sized is not null) gone.Add(_sized);
        _sized = null;
        return gone;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var (_, sized) in _retired) sized.Dispose();
        _retired.Clear();
        _sized?.Dispose();
        _sized = null;
        foreach (var stage in new[] { _prepass, _main8, _main16, _filter })
        {
            if (stage.Pipeline.Handle == 0) continue;
            DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
            device.Api.vkDestroyPipeline(stage.Pipeline);
            device.Api.vkDestroyPipelineLayout(stage.Layout);
            device.Api.vkDestroyDescriptorSetLayout(stage.SetLayout);
        }
        (_prepass, _main8, _main16, _filter) = (default, default, default, default);
        _table?.Dispose();
        _nearest?.Dispose();
        (_table, _nearest, _ready) = (null, null, false);
    }
}
