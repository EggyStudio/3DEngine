using System.Numerics;
using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// The scene's distance field on the GPU: two images of half floats, each its cascades one after
/// another along z, the field the still meshes make, which a build writes, and the field the
/// passes read, that one with the shapes of the meshes that moved stamped into it, the color and
/// the light given off of each cell's nearest still surface in two images more, and a uniform
/// buffer saying where each cascade lies, which a frame writes before any pass reads it.
/// </summary>
/// <remarks>
/// The field the passes read waits to be sampled between the frames' work on it, and goes to the
/// general layout while a build or a stamp writes it. The still field stays in the general layout,
/// read and written only by the field's own work. A build fills a buffer of a word a cell of one
/// cascade with the least of the codes its triangles give, then turns them into distances.
/// </remarks>
internal sealed class GpuSceneField : IDisposable
{
    private readonly Action _dispose;

    internal GpuSceneField(int cascades, int resolution, IImageView view, ISampler sampler, IBuffer info, IBuffer cells,
        VkImage still, VkImageView stillView, VkImage field, VkImageView fieldView, Action dispose,
        IBuffer albedo, IBuffer glow, VkImage albedoImage, IImageView albedoView, VkImage glowImage, IImageView glowView)
    {
        Albedo = albedo;
        Glow = glow;
        AlbedoImage = albedoImage;
        AlbedoView = albedoView;
        GlowImage = glowImage;
        GlowView = glowView;
        Cascades = cascades;
        Resolution = resolution;
        View = view;
        Sampler = sampler;
        Info = info;
        Cells = cells;
        Still = still;
        StillView = stillView;
        Field = field;
        FieldView = fieldView;
        _dispose = dispose;
    }

    /// <summary>How many cascades the images hold.</summary>
    public int Cascades { get; }

    /// <summary>The cells along each side of a cascade.</summary>
    public int Resolution { get; }

    /// <summary>The field the passes read, every cascade, as a 3D texture.</summary>
    public IImageView View { get; }

    /// <summary>A sampler that blends between the eight cells around a point, clamped at the edges.</summary>
    public ISampler Sampler { get; }

    /// <summary>Where each cascade lies, as <c>scenefield.slang</c>'s <c>SceneFieldInfo</c>.</summary>
    public IBuffer Info { get; }

    /// <summary>The bytes of <see cref="Info"/>.</summary>
    public const int InfoBytes = 16 + 8 * 16;

    /// <summary>
    /// The color of each cell's nearest still surface, linear, its alpha 1 where a surface painted
    /// the cell and 0 where none did, laid out as <see cref="View"/> is.
    /// </summary>
    public IImageView AlbedoView { get; }

    /// <summary>The light each cell's nearest still surface gives off, linear, laid out as <see cref="View"/> is.</summary>
    public IImageView GlowView { get; }

    internal IBuffer Albedo { get; }
    internal IBuffer Glow { get; }
    internal VkImage AlbedoImage { get; }
    internal VkImage GlowImage { get; }

    internal IBuffer Cells { get; }
    internal VkImage Still { get; }
    internal VkImageView StillView { get; }
    internal VkImage Field { get; }
    internal VkImageView FieldView { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

internal sealed unsafe partial class GraphicsDevice
{
    private const int SplatStage = 0, ResolveStage = 1, StampStage = 2;
    private readonly ComputeStage[] _fieldStages = new ComputeStage[3];

    /// <summary>What <c>field_splat.slang</c> is pushed.</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct SplatPush
    {
        public Vector4 OriginAndCell;
        public uint Size, Instances, Triangles, Band;
        public uint GroupsX, Paint, Unused1, Unused2;
    }

    /// <summary>Whether the field's shaders have been given, so a field can be built.</summary>
    public bool CanBuildSceneField => _fieldSpirv is not null;

    // The field's kernels as InitializeSceneField was given them, made into pipelines the first
    // time a frame builds the field, so an app that never builds one compiles none of them.
    private byte[][]? _fieldSpirv;

    private ComputeStage[] FieldStages
    {
        get
        {
            if (_fieldStages[StampStage].Pipeline.Handle != 0 || _fieldSpirv is not { } spirv) return _fieldStages;
            _fieldStages[SplatStage] = MakeComputeStage(spirv[0], [VkDescriptorType.StorageBuffer, VkDescriptorType.StorageBuffer,
                VkDescriptorType.StorageBuffer, VkDescriptorType.StorageBuffer, VkDescriptorType.StorageBuffer], (uint)sizeof(SplatPush));
            _fieldStages[ResolveStage] = MakeComputeStage(spirv[1], [VkDescriptorType.StorageBuffer, VkDescriptorType.StorageImage,
                VkDescriptorType.StorageImage, VkDescriptorType.StorageBuffer, VkDescriptorType.StorageBuffer, VkDescriptorType.StorageImage,
                VkDescriptorType.StorageImage], 16);
            _fieldStages[StampStage] = MakeComputeStage(spirv[2], [VkDescriptorType.StorageBuffer, VkDescriptorType.StorageBuffer,
                VkDescriptorType.StorageImage, VkDescriptorType.StorageImage, VkDescriptorType.UniformBuffer], 16);
            return _fieldStages;
        }
    }

    /// <summary>
    /// The distance a cell of a cleared field holds, which reads as nothing near, in world units,
    /// past any band a field is built with.
    /// </summary>
    public const float FarFieldDistance = 10000;

    /// <summary>Makes the field's pipelines from <c>field_splat.slang</c>, <c>field_resolve.slang</c> and <c>field_stamp.slang</c>, once.</summary>
    public void InitializeSceneField(ReadOnlySpan<byte> splat, ReadOnlySpan<byte> resolve, ReadOnlySpan<byte> stamp)
    {
        if (!IsInitialized || CanBuildSceneField) return;
        _fieldSpirv = [splat.ToArray(), resolve.ToArray(), stamp.ToArray()];
    }

    /// <summary>
    /// Makes a field of <paramref name="cascades"/> cascades of <paramref name="resolution"/>
    /// cells a side, every cell far from anything and the info saying the field is off until a
    /// frame writes it.
    /// </summary>
    public GpuSceneField CreateSceneField(int cascades, int resolution)
    {
        var size = (uint)Math.Max(1, resolution);
        var depth = size * (uint)Math.Max(1, cascades);
        var (still, stillMemory, stillView) = FieldImage(size, depth);
        var (field, fieldMemory, fieldView) = FieldImage(size, depth);
        var (albedoImage, albedoMemory, albedoView) = FieldImage(size, depth, VkFormat.R8G8B8A8Unorm);
        var (glowImage, glowMemory, glowView) = FieldImage(size, depth, VkFormat.R16G16B16A16Sfloat);
        var info = CreateBuffer(new BufferDesc(GpuSceneField.InfoBytes, BufferUsage.Uniform | BufferUsage.TransferDst));
        var cells = CreateBuffer(new BufferDesc((ulong)size * size * size * 4, BufferUsage.Storage | BufferUsage.TransferDst));
        var albedo = CreateBuffer(new BufferDesc((ulong)size * size * size * 4, BufferUsage.Storage | BufferUsage.TransferDst));
        var glow = CreateBuffer(new BufferDesc((ulong)size * size * size * 8, BufferUsage.Storage | BufferUsage.TransferDst));

        // The distances cleared to far and the colors to none, the still image left in the general
        // layout and the ones the passes read ready to be sampled, and the info zeroed, which
        // reads as no cascade.
        var cmd = BeginSingleTimeCommands();
        var whole = ColorLevels(0, 1);
        VkImage[] read = [field, albedoImage, glowImage];
        PipelineBarrier(cmd, [
            ImageBarrier(still, whole, VkImageLayout.Undefined, VkImageLayout.General, VkPipelineStageFlags2.None, VkAccessFlags2.None,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite),
            .. read.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.Undefined, VkImageLayout.General, VkPipelineStageFlags2.None, VkAccessFlags2.None,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite)),
        ]);
        var far = new VkClearColorValue(FarFieldDistance, FarFieldDistance, FarFieldDistance, FarFieldDistance);
        var none = new VkClearColorValue(0f, 0f, 0f, 0f);
        _deviceApi.vkCmdClearColorImage(cmd, still, VkImageLayout.General, &far, 1, &whole);
        _deviceApi.vkCmdClearColorImage(cmd, field, VkImageLayout.General, &far, 1, &whole);
        _deviceApi.vkCmdClearColorImage(cmd, albedoImage, VkImageLayout.General, &none, 1, &whole);
        _deviceApi.vkCmdClearColorImage(cmd, glowImage, VkImageLayout.General, &none, 1, &whole);
        _deviceApi.vkCmdFillBuffer(cmd, ((VulkanBuffer)info).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        PipelineBarrier(cmd, [
            ImageBarrier(still, whole, VkImageLayout.General, VkImageLayout.General, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite),
            .. read.Select(image => ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead)),
        ], new VkMemoryBarrier2
        {
            srcStageMask = VkPipelineStageFlags2.Transfer,
            srcAccessMask = VkAccessFlags2.TransferWrite,
            dstStageMask = VkPipelineStageFlags2.AllCommands,
            dstAccessMask = VkAccessFlags2.UniformRead,
        });
        EndSingleTimeCommands(cmd);

        var owner = new VulkanImage(this, field, fieldMemory,
            new ImageDesc(new Extent2D(size, depth), ImageFormat.Undefined, ImageUsage.Sampled | ImageUsage.Storage));
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        Name(owner, "Scene distance field");
        var albedoOwner = new VulkanImage(this, albedoImage, albedoMemory,
            new ImageDesc(new Extent2D(size, depth), ImageFormat.R8G8B8A8_UNorm, ImageUsage.Sampled | ImageUsage.Storage));
        var glowOwner = new VulkanImage(this, glowImage, glowMemory,
            new ImageDesc(new Extent2D(size, depth), ImageFormat.R16G16B16A16_Float, ImageUsage.Sampled | ImageUsage.Storage));
        Name(albedoOwner, "Scene distance field colors");
        Name(glowOwner, "Scene distance field light given off");
        return new GpuSceneField(cascades, (int)size, new VulkanImageView(this, owner, fieldView), sampler, info, cells,
            still, stillView, field, fieldView, () =>
            {
                sampler.Dispose();
                cells.Dispose();
                albedo.Dispose();
                glow.Dispose();
                info.Dispose();
                _deviceApi.vkDestroyImageView(fieldView);
                owner.Dispose();
                _deviceApi.vkDestroyImageView(albedoView);
                albedoOwner.Dispose();
                _deviceApi.vkDestroyImageView(glowView);
                glowOwner.Dispose();
                _deviceApi.vkDestroyImageView(stillView);
                DeviceObjects.Gone(DeviceObjects.Kind.Image);
                _deviceApi.vkDestroyImage(still);
                DeviceObjects.Gone(DeviceObjects.Kind.Memory);
                _deviceApi.vkFreeMemory(stillMemory);
            }, albedo, glow, albedoImage, new VulkanImageView(this, albedoOwner, albedoView), glowImage, new VulkanImageView(this, glowOwner, glowView));
    }

    // A 3D image a compute shader writes and a pass samples, of half floats unless another format
    // is given, with its view.
    private (VkImage Image, VkDeviceMemory Memory, VkImageView View) FieldImage(uint size, uint depth, VkFormat format = VkFormat.R16Sfloat)
    {
        var info = new VkImageCreateInfo
        {
            imageType = VkImageType.Image3D,
            format = format,
            extent = new VkExtent3D(size, size, depth),
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
    /// Records the writing of <paramref name="info"/> into the field's uniform buffer, after the
    /// passes of the frames before have read it and before this frame's do.
    /// </summary>
    public void RecordSceneFieldInfo(ICommandBuffer commandBuffer, GpuSceneField field, ReadOnlySpan<byte> info)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd) return;
        var cmd = vkCmd.Handle;
        var readers = VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader;
        MemoryBarrier(cmd, readers, VkAccessFlags2.UniformRead, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        // The update's size is whole words, which the info is.
        var bytes = info[..(Math.Min(info.Length, GpuSceneField.InfoBytes) & ~3)];
        fixed (byte* data = bytes)
            _deviceApi.vkCmdUpdateBuffer(cmd, ((VulkanBuffer)field.Info).Buffer, 0, (ulong)bytes.Length, data);
        MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, readers, VkAccessFlags2.UniformRead);
    }

    /// <summary>
    /// Records the field the passes read going to the general layout for the frame's builds and
    /// stamps to write, after the frames before have sampled it.
    /// </summary>
    public void RecordSceneFieldOpen(ICommandBuffer commandBuffer, GpuSceneField field)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd) return;
        PipelineBarrier(vkCmd.Handle, [.. new[] { field.Field, field.AlbedoImage, field.GlowImage }.Select(image => ImageBarrier(image, ColorLevels(0, 1),
            VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.General,
            VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite))]);
    }

    /// <summary>Records the field the passes read going back to be sampled, once the frame's builds and stamps have written it.</summary>
    public void RecordSceneFieldClose(ICommandBuffer commandBuffer, GpuSceneField field)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd) return;
        PipelineBarrier(vkCmd.Handle, [.. new[] { field.Field, field.AlbedoImage, field.GlowImage }.Select(image => ImageBarrier(image, ColorLevels(0, 1),
            VkImageLayout.General, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
            VkPipelineStageFlags2.VertexShader | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead))]);
    }

    /// <summary>
    /// Records, between <see cref="RecordSceneFieldOpen"/> and <see cref="RecordSceneFieldClose"/>,
    /// the building of cascade <paramref name="cascade"/> from the triangles of
    /// <paramref name="instanceCount"/> instances, <paramref name="triangleCount"/> triangles in all,
    /// its corner at <paramref name="origin"/> and its cells <paramref name="cell"/> wide, exact
    /// within <paramref name="band"/> cells of a surface.
    /// </summary>
    /// <param name="commandBuffer">The frame's commands.</param>
    /// <param name="field">The field.</param>
    /// <param name="corners">Every triangle the instances use, three float4 corners each in its mesh's own space.</param>
    /// <param name="instances">The instances, as <c>field_splat.slang</c>'s <c>FieldInstance</c>.</param>
    /// <param name="instanceCount">How many instances.</param>
    /// <param name="triangleCount">How many triangles they have in all.</param>
    /// <param name="cascade">The cascade built.</param>
    /// <param name="origin">The cascade's corner.</param>
    /// <param name="cell">The width of its cells.</param>
    /// <param name="band">How many cells from a surface the distances are exact.</param>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordSceneFieldBuild(ICommandBuffer commandBuffer, GpuSceneField field, IBuffer corners, IBuffer instances,
        int instanceCount, int triangleCount, int cascade, Vector3 origin, float cell, int band)
    {
        var run = new FieldRun(this, commandBuffer);
        var cmd = run.Commands;
        var cells = ((VulkanBuffer)field.Cells).Buffer;
        var size = (uint)field.Resolution;

        // The words of the build before are read by its resolve before this fills them, and the
        // fill is seen by the splat.
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite);
        // The code of a cell no triangle comes within the band of, the band's distance in front, in
        // 1024ths of a cell above the bits that say whether its triangle is double-sided and whether
        // the cell is in front.
        _deviceApi.vkCmdFillBuffer(cmd, cells, 0, Vulkan.VK_WHOLE_SIZE, ((uint)band * 1024 << 2) | 1);
        _deviceApi.vkCmdFillBuffer(cmd, ((VulkanBuffer)field.Albedo).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        _deviceApi.vkCmdFillBuffer(cmd, ((VulkanBuffer)field.Glow).Buffer, 0, Vulkan.VK_WHOLE_SIZE, 0);
        MemoryBarrier(cmd, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);

        if (instanceCount > 0 && triangleCount > 0)
        {
            var splat = run.Set(SplatStage);
            run.Buffer(splat, 0, VkDescriptorType.StorageBuffer, corners);
            run.Buffer(splat, 1, VkDescriptorType.StorageBuffer, instances);
            run.Buffer(splat, 2, VkDescriptorType.StorageBuffer, field.Cells);
            run.Buffer(splat, 3, VkDescriptorType.StorageBuffer, field.Albedo);
            run.Buffer(splat, 4, VkDescriptorType.StorageBuffer, field.Glow);
            var groupsX = (uint)Math.Min(triangleCount, 65535);
            var push = new SplatPush
            {
                OriginAndCell = new Vector4(origin, cell),
                Size = size,
                Instances = (uint)instanceCount,
                Triangles = (uint)triangleCount,
                Band = (uint)band,
                GroupsX = groupsX,
            };
            var groupsY = ((uint)triangleCount + groupsX - 1) / groupsX;
            run.Dispatch(SplatStage, splat, MemoryMarshal.AsBytes(new ReadOnlySpan<SplatPush>(in push)), groupsX, groupsY, 1);
            MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
            // The same triangles again, each painting the cells whose least word is its own.
            push.Paint = 1;
            run.Dispatch(SplatStage, splat, MemoryMarshal.AsBytes(new ReadOnlySpan<SplatPush>(in push)), groupsX, groupsY, 1);
            MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
                VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead);
        }

        var resolve = run.Set(ResolveStage);
        run.Buffer(resolve, 0, VkDescriptorType.StorageBuffer, field.Cells);
        run.Image(resolve, 1, field.StillView);
        run.Image(resolve, 2, field.FieldView);
        run.Buffer(resolve, 3, VkDescriptorType.StorageBuffer, field.Albedo);
        run.Buffer(resolve, 4, VkDescriptorType.StorageBuffer, field.Glow);
        run.Image(resolve, 5, ((VulkanImageView)field.AlbedoView).View);
        run.Image(resolve, 6, ((VulkanImageView)field.GlowView).View);
        var cascadePush = new Vector4(cascade, size, cell, band);
        var groups = (size + 3) / 4;
        run.Dispatch(ResolveStage, resolve, MemoryMarshal.AsBytes(new ReadOnlySpan<Vector4>(in cascadePush)), groups, groups, groups);
        MemoryBarrier(cmd, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
        return run;
    }

    /// <summary>
    /// Records, between <see cref="RecordSceneFieldOpen"/> and <see cref="RecordSceneFieldClose"/>
    /// and after the frame's info is written, the stamping of <paramref name="shapeCount"/> shapes
    /// into <paramref name="brickCount"/> bricks of the field the passes read, each brick's cells
    /// the least of the still field's and the shapes'.
    /// </summary>
    /// <param name="commandBuffer">The frame's commands.</param>
    /// <param name="field">The field.</param>
    /// <param name="bricks">The bricks, a uint4 each of the cascade and the brick's place in it.</param>
    /// <param name="brickCount">How many bricks.</param>
    /// <param name="shapes">The shapes, as <c>field_stamp.slang</c>'s <c>FieldShape</c>, at least one even where none is stamped.</param>
    /// <param name="shapeCount">How many shapes.</param>
    /// <returns>What the recording holds, to free once no frame in flight reads it.</returns>
    public IDisposable RecordSceneFieldStamp(ICommandBuffer commandBuffer, GpuSceneField field, IBuffer bricks, int brickCount, IBuffer shapes, int shapeCount)
    {
        var run = new FieldRun(this, commandBuffer);
        var set = run.Set(StampStage);
        run.Buffer(set, 0, VkDescriptorType.StorageBuffer, bricks);
        run.Buffer(set, 1, VkDescriptorType.StorageBuffer, shapes);
        run.Image(set, 2, field.StillView);
        run.Image(set, 3, field.FieldView);
        run.Buffer(set, 4, VkDescriptorType.UniformBuffer, field.Info);
        ReadOnlySpan<uint> counts = [(uint)brickCount, (uint)shapeCount, 0, 0];
        run.Dispatch(StampStage, set, MemoryMarshal.AsBytes(counts), (uint)brickCount, 1, 1);
        MemoryBarrier(run.Commands, VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderWrite,
            VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite);
        return run;
    }

    /// <summary>
    /// The field the passes read as distances, cascade after cascade, each a z of rows of cells,
    /// read back once the frames in flight have finished, for a test of what was built.
    /// </summary>
    internal float[] ReadSceneField(GpuSceneField field)
    {
        var halves = MemoryMarshal.Cast<byte, Half>(ReadFieldImage(field, field.Field, 2));
        var distances = new float[halves.Length];
        for (int i = 0; i < halves.Length; i++) distances[i] = (float)halves[i];
        return distances;
    }

    /// <summary>
    /// The colors of the cells' nearest surfaces, four floats a cell from 0 to 1, and the light
    /// they give off, four a cell, laid out as <see cref="ReadSceneField"/> lays the distances.
    /// </summary>
    internal (float[] Albedo, float[] Glow) ReadSceneFieldColors(GpuSceneField field)
    {
        var bytes = ReadFieldImage(field, field.AlbedoImage, 4);
        var albedo = new float[bytes.Length];
        for (int i = 0; i < bytes.Length; i++) albedo[i] = bytes[i] / 255f;
        var halves = MemoryMarshal.Cast<byte, Half>(ReadFieldImage(field, field.GlowImage, 8));
        var glow = new float[halves.Length];
        for (int i = 0; i < halves.Length; i++) glow[i] = (float)halves[i];
        return (albedo, glow);
    }

    // One of the images the passes read, copied out with the frames in flight finished.
    private byte[] ReadFieldImage(GpuSceneField field, VkImage image, int bytesPerCell)
    {
        var size = (uint)field.Resolution;
        var depth = size * (uint)field.Cascades;
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)size * size * depth * (ulong)bytesPerCell, BufferUsage.TransferDst, CpuAccessMode.Read));
        try
        {
            var cmd = BeginSingleTimeCommands();
            var whole = ColorLevels(0, 1);
            PipelineBarrier(cmd, ImageBarrier(image, whole, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferSrcOptimal,
                VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead | VkAccessFlags2.ShaderWrite, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead));
            var region = new VkBufferImageCopy
            {
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageExtent = new VkExtent3D(size, size, depth),
            };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, image, VkImageLayout.TransferSrcOptimal, buffer.Buffer, 1, &region);
            PipelineBarrier(cmd, ImageBarrier(image, whole, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead, VkPipelineStageFlags2.AllCommands, VkAccessFlags2.ShaderRead));
            EndSingleTimeCommands(cmd);
            return Map(buffer).ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    // One recording of the field's work: the descriptor pool its sets come from, freed once no
    // frame in flight reads them.
    private sealed class FieldRun : IDisposable
    {
        private readonly GraphicsDevice _device;
        private readonly VkDescriptorPool _pool;

        public FieldRun(GraphicsDevice device, ICommandBuffer commandBuffer)
        {
            if (commandBuffer is not VulkanCommandBuffer vkCmd) throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
            _device = device;
            Commands = vkCmd.Handle;
            var sizes = stackalloc VkDescriptorPoolSize[3];
            sizes[0] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageBuffer, descriptorCount = 10 };
            sizes[1] = new VkDescriptorPoolSize { type = VkDescriptorType.StorageImage, descriptorCount = 6 };
            sizes[2] = new VkDescriptorPoolSize { type = VkDescriptorType.UniformBuffer, descriptorCount = 1 };
            var poolInfo = new VkDescriptorPoolCreateInfo { maxSets = 2, poolSizeCount = 3, pPoolSizes = sizes };
            device._deviceApi.vkCreateDescriptorPool(&poolInfo, null, out _pool).CheckResult();
            DeviceObjects.Made(DeviceObjects.Kind.DescriptorPool);
        }

        public VkCommandBuffer Commands { get; }

        public VkDescriptorSet Set(int stage)
        {
            var layout = _device.FieldStages[stage].SetLayout;
            var allocInfo = new VkDescriptorSetAllocateInfo { descriptorPool = _pool, descriptorSetCount = 1, pSetLayouts = &layout };
            VkDescriptorSet set;
            _device._deviceApi.vkAllocateDescriptorSets(&allocInfo, &set).CheckResult();
            return set;
        }

        public void Image(VkDescriptorSet set, uint binding, VkImageView view)
        {
            var image = new VkDescriptorImageInfo { imageView = view, imageLayout = VkImageLayout.General };
            var write = new VkWriteDescriptorSet { dstSet = set, dstBinding = binding, descriptorCount = 1, descriptorType = VkDescriptorType.StorageImage, pImageInfo = &image };
            _device._deviceApi.vkUpdateDescriptorSets(1, &write, 0, null);
        }

        public void Buffer(VkDescriptorSet set, uint binding, VkDescriptorType type, IBuffer buffer)
        {
            var vk = (VulkanBuffer)buffer;
            var info = new VkDescriptorBufferInfo { buffer = vk.Buffer, offset = 0, range = vk.Description.Size };
            var write = new VkWriteDescriptorSet { dstSet = set, dstBinding = binding, descriptorCount = 1, descriptorType = type, pBufferInfo = &info };
            _device._deviceApi.vkUpdateDescriptorSets(1, &write, 0, null);
        }

        public void Dispatch(int stage, VkDescriptorSet set, ReadOnlySpan<byte> push, uint x, uint y, uint z)
        {
            var (pipeline, layout, _) = _device.FieldStages[stage];
            _device._deviceApi.vkCmdBindPipeline(Commands, VkPipelineBindPoint.Compute, pipeline);
            _device._deviceApi.vkCmdBindDescriptorSets(Commands, VkPipelineBindPoint.Compute, layout, 0, 1, &set, 0, null);
            fixed (byte* p = push)
                _device._deviceApi.vkCmdPushConstants(Commands, layout, VkShaderStageFlags.Compute, 0, (uint)push.Length, p);
            _device._deviceApi.vkCmdDispatch(Commands, Math.Max(1, x), Math.Max(1, y), Math.Max(1, z));
        }

        public void Dispose()
        {
            DeviceObjects.Gone(DeviceObjects.Kind.DescriptorPool);
            _device._deviceApi.vkDestroyDescriptorPool(_pool);
        }
    }

    // Runs before the device goes.
    private void DestroySceneField()
    {
        foreach (var (pipeline, layout, setLayout) in _fieldStages.Where(s => s.Pipeline.Handle != 0))
        {
            DeviceObjects.Gone(DeviceObjects.Kind.Pipeline);
            _deviceApi.vkDestroyPipeline(pipeline);
            _deviceApi.vkDestroyPipelineLayout(layout);
            _deviceApi.vkDestroyDescriptorSetLayout(setLayout);
        }
        Array.Clear(_fieldStages);
        _fieldSpirv = null;
    }
}
