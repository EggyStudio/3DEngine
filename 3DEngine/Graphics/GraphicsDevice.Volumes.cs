using Vortice.Vulkan;


namespace Engine;

/// <summary>A 3D texture sampled through its view and sampler, as a tonemapping table is.</summary>
internal sealed record VolumeTexture(IImage Image, IImageView View, ISampler Sampler) : IDisposable
{
    /// <inheritdoc />
    public void Dispose()
    {
        Sampler.Dispose();
        View.Dispose();
        Image.Dispose();
    }
}

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>
    /// Creates a cube of <paramref name="size"/> texels a side in <paramref name="format"/> from
    /// <paramref name="texels"/>, x fastest, then y, then z, sampled with linear filtering and held
    /// at its edges, uploaded with the frame's other uploads.
    /// </summary>
    internal VolumeTexture CreateVolumeTexture(uint size, VkFormat format, ReadOnlySpan<byte> texels)
    {
        var info = new VkImageCreateInfo
        {
            imageType = VkImageType.Image3D,
            format = format,
            extent = new VkExtent3D(size, size, size),
            mipLevels = 1,
            arrayLayers = 1,
            samples = VkSampleCountFlags.Count1,
            tiling = VkImageTiling.Optimal,
            usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst,
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
        var owner = new VulkanImage(this, image, memory, new ImageDesc(new Extent2D(size, size), ImageFormat.Undefined, ImageUsage.Sampled | ImageUsage.TransferDst));

        var staging = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)texels.Length, BufferUsage.TransferSrc, CpuAccessMode.Write));
        texels.CopyTo(Map(staging));
        Unmap(staging);
        var cmd = UploadCommands();
        PipelineBarrier(cmd, ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite));
        var region = new VkBufferImageCopy
        {
            imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            imageExtent = new VkExtent3D(size, size, size),
        };
        _deviceApi.vkCmdCopyBufferToImage(cmd, staging.Buffer, image, VkImageLayout.TransferDstOptimal, 1, &region);
        PipelineBarrier(cmd, ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ShaderRead));
        _batchStaging.Add(staging);
        owner.Layout = VkImageLayout.ShaderReadOnlyOptimal;

        var viewInfo = new VkImageViewCreateInfo
        {
            image = image,
            viewType = VkImageViewType.Image3D,
            format = format,
            components = VkComponentMapping.Rgba,
            subresourceRange = ColorLevels(0, 1),
        };
        _deviceApi.vkCreateImageView(&viewInfo, null, out VkImageView view).CheckResult();
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        return new VolumeTexture(owner, new VulkanImageView(this, owner, view), sampler);
    }
}
