using Vortice.Vulkan;

namespace Engine;

/// <summary>A cube map of half-float linear color with a mip chain, sampled through a cube view.</summary>
/// <remarks>Its mips hold whatever the caller made of them, as an environment prefiltered by roughness does.</remarks>
internal sealed class CubeMap : IDisposable
{
    private readonly Action _dispose;

    internal CubeMap(IImageView view, ISampler sampler, uint size, uint mipLevels, Action dispose)
    {
        View = view;
        Sampler = sampler;
        Size = size;
        MipLevels = mipLevels;
        _dispose = dispose;
    }

    /// <summary>The cube view of every face and mip.</summary>
    public IImageView View { get; }

    /// <summary>A trilinear sampler clamped to each face's edge.</summary>
    public ISampler Sampler { get; }

    /// <summary>The width and height of each face at the first mip.</summary>
    public uint Size { get; }

    /// <summary>How many mips each face has.</summary>
    public uint MipLevels { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>
    /// Creates a cube map of faces <paramref name="size"/> texels wide with <paramref name="mipLevels"/>
    /// mips, from <paramref name="texels"/>: RGBA half floats, mip by mip from the largest, and within
    /// a mip face by face in Vulkan's order (+X, -X, +Y, -Y, +Z, -Z), each face row by row.
    /// </summary>
    /// <exception cref="ArgumentException">The texels are fewer than the faces and mips take.</exception>
    public CubeMap CreateCubeMap(uint size, uint mipLevels, ReadOnlySpan<Half> texels)
    {
        if (!IsInitialized) throw new InvalidOperationException("Graphics device not initialized");
        size = Math.Max(1, size);
        mipLevels = Math.Clamp(mipLevels, 1, (uint)Math.Log2(size) + 1);

        ulong bytes = 0;
        for (uint m = 0; m < mipLevels; m++)
        {
            ulong s = Math.Max(1, size >> (int)m);
            bytes += s * s * 6 * 8;
        }
        if ((ulong)texels.Length * 2 < bytes)
            throw new ArgumentException($"A cube of {size} with {mipLevels} mips takes {bytes / 2} halves, not {texels.Length}.", nameof(texels));

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
            usage = VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferDst,
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

        var staging = (VulkanBuffer)CreateBuffer(new BufferDesc(bytes, BufferUsage.TransferSrc, CpuAccessMode.Write));
        try
        {
            System.Runtime.InteropServices.MemoryMarshal.AsBytes(texels)[..(int)bytes].CopyTo(Map(staging));
            Unmap(staging);

            var everything = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, mipLevels, 0, 6);
            var cmd = BeginSingleTimeCommands();
            PipelineBarrier(cmd, ImageBarrier(image, everything, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal,
                VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite));

            var regions = new VkBufferImageCopy[mipLevels];
            ulong offset = 0;
            for (uint m = 0; m < mipLevels; m++)
            {
                uint s = Math.Max(1, size >> (int)m);
                regions[m] = new VkBufferImageCopy
                {
                    bufferOffset = offset,
                    imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, m, 0, 6),
                    imageExtent = new VkExtent3D(s, s, 1),
                };
                offset += (ulong)s * s * 6 * 8;
            }
            fixed (VkBufferImageCopy* r = regions)
                _deviceApi.vkCmdCopyBufferToImage(cmd, staging.Buffer, image, VkImageLayout.TransferDstOptimal, mipLevels, r);

            PipelineBarrier(cmd, ImageBarrier(image, everything, VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ShaderRead));
            EndSingleTimeCommands(cmd);
        }
        finally
        {
            staging.Dispose();
        }

        var viewInfo = new VkImageViewCreateInfo
        {
            image = image,
            viewType = VkImageViewType.ImageCube,
            format = VkFormat.R16G16B16A16Sfloat,
            components = VkComponentMapping.Rgba,
            subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, mipLevels, 0, 6),
        };
        _deviceApi.vkCreateImageView(&viewInfo, null, out VkImageView view).CheckResult();

        var cube = new VulkanImage(this, image, memory,
            new ImageDesc(new Extent2D(size, size), ImageFormat.Undefined, ImageUsage.Sampled | ImageUsage.TransferDst, mipLevels));
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Linear, SamplerFilter.Linear,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));
        return new CubeMap(new VulkanImageView(this, cube, view), sampler, size, mipLevels, () =>
        {
            sampler.Dispose();
            _deviceApi.vkDestroyImageView(view);
            cube.Dispose();
        });
    }
}
