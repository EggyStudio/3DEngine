using System.Runtime.InteropServices;
using Vortice.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Wraps a Vulkan image with its backing device memory and current layout.</summary>
    /// <seealso cref="IImage"/>
    private sealed class VulkanImage : IImage
    {
        private readonly GraphicsDevice _device;

        /// <summary>The underlying Vulkan image handle.</summary>
        internal VkImage Image;

        /// <summary>The device memory backing this image when it is its own, as a render target's is.</summary>
        internal VkDeviceMemory Memory;

        /// <summary>The range of a block backing this image, for a texture.</summary>
        internal MemorySlice? Slice;

        /// <summary>The current image layout, updated after each layout transition.</summary>
        internal VkImageLayout Layout;

        /// <inheritdoc />
        public ImageDesc Description { get; }

        /// <summary>Creates a new Vulkan image wrapper.</summary>
        /// <param name="device">The owning graphics device.</param>
        /// <param name="image">The Vulkan image handle.</param>
        /// <param name="memory">The backing device memory.</param>
        /// <param name="desc">The image creation descriptor.</param>
        public VulkanImage(GraphicsDevice device, VkImage image, VkDeviceMemory memory, ImageDesc desc)
        {
            _device = device;
            Image = image;
            Memory = memory;
            Description = desc;
            Layout = VkImageLayout.Undefined;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Image.Handle != 0)
            {
                _device._deviceApi.vkDestroyImage(Image);
                Image = default;
            }
            if (Memory.Handle != 0)
            {
                _device._deviceApi.vkFreeMemory(Memory);
                Memory = default;
            }
            if (Slice is { } slice)
            {
                _device.FreeMemory(slice);
                Slice = null;
            }
        }
    }

    /// <summary>Wraps a Vulkan image view for a specific <see cref="IImage"/>.</summary>
    /// <seealso cref="IImageView"/>
    private sealed class VulkanImageView : IImageView
    {
        private readonly GraphicsDevice _device;

        /// <inheritdoc />
        public IImage Image { get; }

        /// <summary>The underlying Vulkan image view handle.</summary>
        internal VkImageView View;

        /// <summary>Creates a new Vulkan image view wrapper.</summary>
        /// <param name="device">The owning graphics device.</param>
        /// <param name="image">The image this view references.</param>
        /// <param name="view">The Vulkan image view handle.</param>
        public VulkanImageView(GraphicsDevice device, IImage image, VkImageView view)
        {
            _device = device;
            Image = image;
            View = view;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (View.Handle != 0)
            {
                _device._deviceApi.vkDestroyImageView(View);
                View = default;
            }
        }
    }

    /// <summary>Wraps a Vulkan sampler with its creation descriptor.</summary>
    /// <seealso cref="ISampler"/>
    private sealed class VulkanSampler : ISampler
    {
        private readonly GraphicsDevice _device;

        /// <inheritdoc />
        public SamplerDesc Description { get; }

        /// <summary>The underlying Vulkan sampler handle.</summary>
        internal VkSampler Sampler;

        /// <summary>Creates a new Vulkan sampler wrapper.</summary>
        /// <param name="device">The owning graphics device.</param>
        /// <param name="desc">The sampler creation descriptor.</param>
        /// <param name="sampler">The Vulkan sampler handle.</param>
        public VulkanSampler(GraphicsDevice device, SamplerDesc desc, VkSampler sampler)
        {
            _device = device;
            Description = desc;
            Sampler = sampler;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (Sampler.Handle != 0)
            {
                _device._deviceApi.vkDestroySampler(Sampler);
                Sampler = default;
            }
        }
    }

    /// <summary>Creates a device-local GPU image (texture or render target) backed by Vulkan memory.</summary>
    /// <param name="desc">Image creation descriptor (extent, format, usage).</param>
    /// <returns>A new <see cref="IImage"/> handle.</returns>
    public IImage CreateImage(ImageDesc desc)
    {
        VkImageCreateInfo imageInfo = new()
        {
            // A sampled color image can be viewed as sRGB too (CreateImageView with a format), so
            // the model pass decodes a base color texture that the 2D passes read as its bytes.
            flags = desc.Usage.HasFlag(ImageUsage.Sampled) && desc.Format is ImageFormat.R8G8B8A8_UNorm or ImageFormat.B8G8R8A8_UNorm
                ? VkImageCreateFlags.MutableFormat
                : 0,
            imageType = VkImageType.Image2D,
            format = ToVkFormat(desc.Format),
            extent = new VkExtent3D(desc.Extent.Width, desc.Extent.Height, 1),
            mipLevels = Math.Max(1, desc.MipLevels),
            arrayLayers = 1,
            samples = VkSampleCountFlags.Count1,
            tiling = VkImageTiling.Optimal,
            usage = ToVkImageUsage(desc.Usage),
            sharingMode = VkSharingMode.Exclusive,
            initialLayout = VkImageLayout.Undefined
        };

        _deviceApi.vkCreateImage(&imageInfo, null, out VkImage image).CheckResult();
        _deviceApi.vkGetImageMemoryRequirements(image, out VkMemoryRequirements req);

        var slice = AllocateMemory(req, VkMemoryPropertyFlags.DeviceLocal, image: true);
        _deviceApi.vkBindImageMemory(image, slice.Memory, slice.Offset).CheckResult();

        return new VulkanImage(this, image, default, desc) { Slice = slice };
    }

    /// <summary>Creates a typed <c>VkImageView</c> for the given image, selecting aspect flags based on the image format.</summary>
    /// <param name="image">The image to create a view for (must originate from this device).</param>
    /// <returns>A new <see cref="IImageView"/> handle.</returns>
    public IImageView CreateImageView(IImage image) => CreateImageView(image, image.Description.Format);

    /// <summary>
    /// Creates a view of <paramref name="image"/> in <paramref name="format"/>, which for a sampled
    /// UNORM color image may be its sRGB counterpart, so sampling decodes it to linear.
    /// </summary>
    public IImageView CreateImageView(IImage image, ImageFormat format) => CreateImageView(image, format, 0, 0);

    /// <summary>
    /// Creates a view of the first level of <paramref name="image"/> alone, as a compute shader
    /// writes a mipmapped texture through, since Vulkan stores through a view of one level.
    /// </summary>
    public IImageView CreateFirstLevelView(IImage image) => CreateImageView(image, image.Description.Format, 0, 1);

    // A view of levelCount levels from firstLevel, or of all of them for a count of 0.
    private IImageView CreateImageView(IImage image, ImageFormat format, uint firstLevel, uint levelCount)
    {
        if (image is not VulkanImage vkImage)
            throw new ArgumentException("Image was not created by this device.", nameof(image));

        VkImageAspectFlags aspect = vkImage.Description.Format switch
        {
            ImageFormat.D24_UNorm_S8_UInt => VkImageAspectFlags.Depth | VkImageAspectFlags.Stencil,
            ImageFormat.D32_Float          => VkImageAspectFlags.Depth,
            _                             => VkImageAspectFlags.Color
        };

        // An sRGB view of an image a compute shader writes is for sampling only, since an sRGB
        // format cannot be written as storage, which the view would otherwise inherit.
        var samplingOnly = new VkImageViewUsageCreateInfo { usage = VkImageUsageFlags.Sampled };
        var restrict = vkImage.Description.Usage.HasFlag(ImageUsage.Storage) && format != vkImage.Description.Format;
        VkImageViewCreateInfo viewInfo = new()
        {
            pNext = restrict ? &samplingOnly : null,
            image = vkImage.Image,
            viewType = VkImageViewType.Image2D,
            format = ToVkFormat(format),
            components = VkComponentMapping.Rgba,
            subresourceRange = new VkImageSubresourceRange(aspect, firstLevel, levelCount == 0 ? LevelsOf(vkImage) - firstLevel : levelCount, 0, 1)
        };

        _deviceApi.vkCreateImageView(&viewInfo, null, out VkImageView view).CheckResult();
        return new VulkanImageView(this, image, view);
    }

    private float _maxAnisotropy;

    /// <summary>Creates a Vulkan texture sampler with the specified filtering and addressing modes.</summary>
    /// <param name="desc">Sampler creation descriptor.</param>
    /// <returns>A new <see cref="ISampler"/> handle.</returns>
    public ISampler CreateSampler(SamplerDesc desc)
    {
        // The device's own limit, asked once. The feature is enabled when the device is made.
        if (_maxAnisotropy == 0)
        {
            _instanceApi.vkGetPhysicalDeviceProperties(_physicalDevice, out var properties);
            _maxAnisotropy = Math.Max(1, properties.limits.maxSamplerAnisotropy);
        }
        var anisotropy = Math.Clamp(desc.MaxAnisotropy, 1, _maxAnisotropy);

        VkSamplerCreateInfo info = new()
        {
            magFilter = desc.MagFilter == SamplerFilter.Linear ? VkFilter.Linear : VkFilter.Nearest,
            minFilter = desc.MinFilter == SamplerFilter.Linear ? VkFilter.Linear : VkFilter.Nearest,
            addressModeU = ToVkAddressMode(desc.AddressU),
            addressModeV = ToVkAddressMode(desc.AddressV),
            addressModeW = ToVkAddressMode(desc.AddressW),
            anisotropyEnable = anisotropy > 1,
            maxAnisotropy = anisotropy,
            borderColor = VkBorderColor.IntOpaqueBlack,
            unnormalizedCoordinates = false,
            compareEnable = false,
            compareOp = VkCompareOp.Always,
            // Blends between mip levels when the texture is smoothed and picks the nearest level
            // when it is not, and lets an image with mip levels use all of them. An image without
            // them has one level, which the bound leaves alone.
            mipmapMode = desc.MinFilter == SamplerFilter.Linear ? VkSamplerMipmapMode.Linear : VkSamplerMipmapMode.Nearest,
            minLod = 0,
            maxLod = Vulkan.VK_LOD_CLAMP_NONE,
        };

        _deviceApi.vkCreateSampler(&info, null, out VkSampler sampler).CheckResult();
        return new VulkanSampler(this, desc, sampler);
    }

    /// <summary>Maps an engine <see cref="SamplerAddressMode"/> to the Vulkan equivalent.</summary>
    private static VkSamplerAddressMode ToVkAddressMode(SamplerAddressMode mode) => mode switch
    {
        SamplerAddressMode.ClampToEdge => VkSamplerAddressMode.ClampToEdge,
        SamplerAddressMode.MirrorRepeat => VkSamplerAddressMode.MirroredRepeat,
        SamplerAddressMode.Repeat => VkSamplerAddressMode.Repeat,
        _ => VkSamplerAddressMode.ClampToEdge
    };

    /// <summary>Maps an engine <see cref="ImageFormat"/> to the Vulkan <c>VkFormat</c> equivalent.</summary>
    private static VkFormat ToVkFormat(ImageFormat format) => format switch
    {
        ImageFormat.R8G8B8A8_UNorm => VkFormat.R8G8B8A8Unorm,
        ImageFormat.B8G8R8A8_UNorm => VkFormat.B8G8R8A8Unorm,
        ImageFormat.D24_UNorm_S8_UInt => VkFormat.D24UnormS8Uint,
        ImageFormat.D32_Float => VkFormat.D32Sfloat,
        ImageFormat.R8G8B8A8_Srgb => VkFormat.R8G8B8A8Srgb,
        ImageFormat.B8G8R8A8_Srgb => VkFormat.B8G8R8A8Srgb,
        _ => VkFormat.Undefined
    };

    /// <summary>Converts engine <see cref="ImageUsage"/> flags to Vulkan <c>VkImageUsageFlags</c>.</summary>
    private static VkImageUsageFlags ToVkImageUsage(ImageUsage usage)
    {
        VkImageUsageFlags flags = 0;
        if (usage.HasFlag(ImageUsage.ColorAttachment)) flags |= VkImageUsageFlags.ColorAttachment;
        if (usage.HasFlag(ImageUsage.DepthStencilAttachment)) flags |= VkImageUsageFlags.DepthStencilAttachment;
        if (usage.HasFlag(ImageUsage.Storage)) flags |= VkImageUsageFlags.Storage;
        if (usage.HasFlag(ImageUsage.Sampled)) flags |= VkImageUsageFlags.Sampled;
        if (usage.HasFlag(ImageUsage.TransferSrc)) flags |= VkImageUsageFlags.TransferSrc;
        if (usage.HasFlag(ImageUsage.TransferDst)) flags |= VkImageUsageFlags.TransferDst;
        return flags;
    }

    /// <summary>Transitions an image layout via a pipeline barrier using default stage flags.</summary>
    /// <param name="image">The image to transition.</param>
    /// <param name="oldLayout">The current layout.</param>
    /// <param name="newLayout">The target layout.</param>
    /// <param name="aspect">Image aspect flags (color, depth, etc.).</param>
    internal void TransitionImageLayout(IImage image, VkImageLayout oldLayout, VkImageLayout newLayout, VkImageAspectFlags aspect)
    {
        if (image is not VulkanImage vkImage)
            throw new ArgumentException("Image was not created by this device.", nameof(image));

        var cmd = BeginSingleTimeCommands();

        VkImageMemoryBarrier barrier = new()
        {
            oldLayout = oldLayout,
            newLayout = newLayout,
            srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            image = vkImage.Image,
            subresourceRange = new VkImageSubresourceRange(aspect, 0, 1, 0, 1)
        };

        VkPipelineStageFlags srcStage = VkPipelineStageFlags.TopOfPipe;
        VkPipelineStageFlags dstStage = VkPipelineStageFlags.BottomOfPipe;

        _deviceApi.vkCmdPipelineBarrier(cmd, srcStage, dstStage, 0, 0, null, 0, null, 1, &barrier);
        EndSingleTimeCommands(cmd);
        vkImage.Layout = newLayout;
    }

    /// <summary>Transitions an image layout via a pipeline barrier with explicit stage flags.</summary>
    /// <param name="image">The image to transition.</param>
    /// <param name="oldLayout">The current layout.</param>
    /// <param name="newLayout">The target layout.</param>
    /// <param name="aspect">Image aspect flags (color, depth, etc.).</param>
    /// <param name="srcStage">Source pipeline stage for the barrier.</param>
    /// <param name="dstStage">Destination pipeline stage for the barrier.</param>
    internal void TransitionImageLayout(IImage image,
        VkImageLayout oldLayout,
        VkImageLayout newLayout,
        VkImageAspectFlags aspect,
        VkPipelineStageFlags srcStage,
        VkPipelineStageFlags dstStage)
    {
        if (image is not VulkanImage vkImage)
            throw new ArgumentException("Image was not created by this device.", nameof(image));

        var cmd = BeginSingleTimeCommands();

        VkImageMemoryBarrier barrier = new()
        {
            oldLayout = oldLayout,
            newLayout = newLayout,
            srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            image = vkImage.Image,
            subresourceRange = new VkImageSubresourceRange(aspect, 0, 1, 0, 1)
        };

        _deviceApi.vkCmdPipelineBarrier(cmd, srcStage, dstStage, 0, 0, null, 0, null, 1, &barrier);
        EndSingleTimeCommands(cmd);
        vkImage.Layout = newLayout;
    }

    /// <summary>Uploads raw pixel data to a 2D image via a staging buffer, transitioning layouts automatically.</summary>
    /// <param name="image">The destination image (must originate from this device).</param>
    /// <param name="data">Raw pixel data to upload.</param>
    /// <param name="width">Texture width in pixels.</param>
    /// <param name="height">Texture height in pixels.</param>
    /// <param name="bytesPerPixel">Bytes per pixel (e.g., 4 for RGBA8).</param>
    public void UploadTexture2D(IImage image, ReadOnlySpan<byte> data, uint width, uint height, int bytesPerPixel) =>
        UploadRegion(image, data, 0, 0, width, height, bytesPerPixel);

    /// <summary>
    /// Copies four-byte pixels into a rectangle of an image already holding pixels, the rest kept,
    /// and makes its smaller levels again, after every frame submitted before has finished reading it.
    /// </summary>
    internal void UploadTextureRegion(IImage image, ReadOnlySpan<byte> data, int x, int y, uint width, uint height) =>
        UploadRegion(image, data, x, y, width, height, 4);

    private void UploadRegion(IImage image, ReadOnlySpan<byte> data, int x, int y, uint width, uint height, int bytesPerPixel)
    {
        if (image is not VulkanImage vkImage)
            throw new ArgumentException("Image was not created by this device.", nameof(image));
        if (data.Length == 0) return;

        ulong expectedSize = (ulong)width * height * (ulong)bytesPerPixel;
        if ((ulong)data.Length < expectedSize)
            throw new ArgumentException("Provided data is smaller than the expected image size.", nameof(data));

        // Create staging buffer
        var stagingDesc = new BufferDesc(expectedSize, BufferUsage.TransferSrc, CpuAccessMode.Write);
        var staging = (VulkanBuffer)CreateBuffer(stagingDesc);
        try
        {
            var stagingSpan = Map(staging);
            data.Slice(0, (int)expectedSize).CopyTo(stagingSpan);
            Unmap(staging);

            var cmd = BeginSingleTimeCommands();

            // Transition image to transfer dst, every mip level, which the chain below fills
            VkImageMemoryBarrier barrierToDst = new()
            {
                oldLayout = vkImage.Layout,
                newLayout = VkImageLayout.TransferDstOptimal,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                image = vkImage.Image,
                subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, LevelsOf(vkImage), 0, 1)
            };

            // Every earlier command on the queue first, since a rectangle written into an image
            // the frames in flight sample would otherwise race their reads of it.
            VkPipelineStageFlags srcStage = VkPipelineStageFlags.AllCommands;
            VkPipelineStageFlags dstStage = VkPipelineStageFlags.Transfer;

            _deviceApi.vkCmdPipelineBarrier(cmd, srcStage, dstStage, 0, 0, null, 0, null, 1, &barrierToDst);

            // Copy buffer to image
            VkBufferImageCopy region = new()
            {
                bufferOffset = 0,
                bufferRowLength = 0,
                bufferImageHeight = 0,
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageOffset = new VkOffset3D(x, y, 0),
                imageExtent = new VkExtent3D(width, height, 1)
            };

            _deviceApi.vkCmdCopyBufferToImage(cmd, staging.Buffer, vkImage.Image, VkImageLayout.TransferDstOptimal, 1, &region);

            // Fill the smaller levels from the first, which leaves every level ready to sample
            RecordMipChain(cmd, vkImage);

            _deviceApi.vkEndCommandBuffer(cmd).CheckResult();

            VkSubmitInfo submitInfo = new()
            {
                commandBufferCount = 1,
                pCommandBuffers = &cmd
            };

            VkFence fence;
            VkFenceCreateInfo fenceInfo = new();
            _deviceApi.vkCreateFence(&fenceInfo, null, out fence).CheckResult();

            _deviceApi.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, fence).CheckResult();
            _deviceApi.vkWaitForFences(1, &fence, true, ulong.MaxValue).CheckResult();

            _deviceApi.vkDestroyFence(fence);
            _deviceApi.vkFreeCommandBuffers(_commandPool, 1, &cmd);

            vkImage.Layout = VkImageLayout.ShaderReadOnlyOptimal;
        }
        finally
        {
            staging.Dispose();
        }
    }

    /// <summary>Uploads a span of typed pixel data to a 2D image using sizeof(<typeparamref name="TPixel"/>) as bytes-per-pixel.</summary>
    /// <typeparam name="TPixel">Unmanaged pixel type (e.g. <c>uint</c> for RGBA8).</typeparam>
    /// <param name="image">The destination image.</param>
    /// <param name="pixels">Pixel data to upload.</param>
    /// <param name="width">Texture width in pixels.</param>
    /// <param name="height">Texture height in pixels.</param>
    internal void UploadTexture2D<TPixel>(IImage image, ReadOnlySpan<TPixel> pixels, uint width, uint height)
        where TPixel : unmanaged
    {
        int bpp = sizeof(TPixel);
        ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(pixels);
        ulong expectedSize = (ulong)width * height * (ulong)bpp;
        if ((ulong)bytes.Length < expectedSize)
            throw new ArgumentException("Pixel span is smaller than expected image size.", nameof(pixels));

        UploadTexture2D(image, bytes, width, height, bpp);
    }

    /// <inheritdoc />
    public void UploadTexture2DDeferred(ICommandBuffer commandBuffer, IImage image, ReadOnlySpan<byte> data, uint width, uint height, int bytesPerPixel)
    {
        if (image is not VulkanImage vkImage)
            throw new ArgumentException("Image was not created by this device.", nameof(image));
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        if (data.Length == 0) return;

        ulong expectedSize = (ulong)width * height * (ulong)bytesPerPixel;
        if ((ulong)data.Length < expectedSize)
            throw new ArgumentException("Provided data is smaller than the expected image size.", nameof(data));

        // Create staging buffer (will be kept alive until frame fence signals)
        var stagingDesc = new BufferDesc(expectedSize, BufferUsage.TransferSrc, CpuAccessMode.Write);
        var staging = (VulkanBuffer)CreateBuffer(stagingDesc);

        var stagingSpan = Map(staging);
        data.Slice(0, (int)expectedSize).CopyTo(stagingSpan);
        Unmap(staging);

        var cmd = vkCmd.Handle;

        // Transition image to transfer dst
        VkImageMemoryBarrier barrierToDst = new()
        {
            oldLayout = vkImage.Layout,
            newLayout = VkImageLayout.TransferDstOptimal,
            srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            image = vkImage.Image,
            subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, 1, 0, 1)
        };

        _deviceApi.vkCmdPipelineBarrier(cmd, VkPipelineStageFlags.TopOfPipe, VkPipelineStageFlags.Transfer,
            0, 0, null, 0, null, 1, &barrierToDst);

        // Copy buffer to image
        VkBufferImageCopy region = new()
        {
            bufferOffset = 0,
            bufferRowLength = 0,
            bufferImageHeight = 0,
            imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            imageOffset = new VkOffset3D(0, 0, 0),
            imageExtent = new VkExtent3D(width, height, 1)
        };

        _deviceApi.vkCmdCopyBufferToImage(cmd, staging.Buffer, vkImage.Image, VkImageLayout.TransferDstOptimal, 1, &region);

        // Transition image to shader read-only
        VkImageMemoryBarrier barrierToShaderRead = new()
        {
            oldLayout = VkImageLayout.TransferDstOptimal,
            newLayout = VkImageLayout.ShaderReadOnlyOptimal,
            srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            image = vkImage.Image,
            subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, 1, 0, 1)
        };

        _deviceApi.vkCmdPipelineBarrier(cmd, VkPipelineStageFlags.Transfer, VkPipelineStageFlags.FragmentShader,
            0, 0, null, 0, null, 1, &barrierToShaderRead);

        vkImage.Layout = VkImageLayout.ShaderReadOnlyOptimal;

        // Track staging buffer for deferred disposal after frame fence signals
        _deferredStagingBuffers[_currentFrame] ??= new List<IBuffer>();
        _deferredStagingBuffers[_currentFrame]!.Add(staging);
    }

    /// <summary>
    /// Copies the first level of <paramref name="source"/> into a new image with mip levels, of
    /// the same size and format, and fills its smaller levels from it.
    /// </summary>
    /// <remarks>
    /// How a texture gains mip levels after its pixels were uploaded and let go of on the CPU. The
    /// source must have been made with <see cref="ImageUsage.TransferSrc"/> and be ready to sample,
    /// and is left that way.
    /// </remarks>
    public void CopyWithMipmaps(IImage source, IImage destination)
    {
        if (source is not VulkanImage src || destination is not VulkanImage dst)
            throw new ArgumentException("Images were not created by this device.");

        var cmd = BeginSingleTimeCommands();
        Barrier(cmd, src, 0, 1, src.Layout, VkImageLayout.TransferSrcOptimal, VkAccessFlags.ShaderRead, VkAccessFlags.TransferRead,
            VkPipelineStageFlags.FragmentShader, VkPipelineStageFlags.Transfer);
        Barrier(cmd, dst, 0, LevelsOf(dst), VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal, 0, VkAccessFlags.TransferWrite,
            VkPipelineStageFlags.TopOfPipe, VkPipelineStageFlags.Transfer);

        var extent = src.Description.Extent;
        VkImageCopy region = new()
        {
            srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            extent = new VkExtent3D(extent.Width, extent.Height, 1),
        };
        _deviceApi.vkCmdCopyImage(cmd, src.Image, VkImageLayout.TransferSrcOptimal, dst.Image, VkImageLayout.TransferDstOptimal, 1, &region);

        Barrier(cmd, src, 0, 1, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal, VkAccessFlags.TransferRead, VkAccessFlags.ShaderRead,
            VkPipelineStageFlags.Transfer, VkPipelineStageFlags.FragmentShader);
        src.Layout = VkImageLayout.ShaderReadOnlyOptimal;
        RecordMipChain(cmd, dst);
        EndSingleTimeCommands(cmd);
    }

    private static uint LevelsOf(VulkanImage image) => Math.Max(1, image.Description.MipLevels);

    /// <summary>
    /// Records the blits that fill every mip level from the one above it, halving the size each
    /// time with linear filtering, and leaves every level ready to sample.
    /// </summary>
    /// <remarks>
    /// Expects every level in transfer-destination layout with the first one written. An image
    /// with one level is only moved to the sampling layout. R8G8B8A8 is required by Vulkan to
    /// support blits with linear filtering, so the formats textures use need no check.
    /// </remarks>
    private void RecordMipChain(VkCommandBuffer cmd, VulkanImage image)
    {
        var levels = LevelsOf(image);
        int width = (int)image.Description.Extent.Width, height = (int)image.Description.Extent.Height;
        for (uint level = 1; level < levels; level++)
        {
            Barrier(cmd, image, level - 1, 1, VkImageLayout.TransferDstOptimal, VkImageLayout.TransferSrcOptimal,
                VkAccessFlags.TransferWrite, VkAccessFlags.TransferRead, VkPipelineStageFlags.Transfer, VkPipelineStageFlags.Transfer);

            int nextWidth = Math.Max(1, width / 2), nextHeight = Math.Max(1, height / 2);
            VkImageBlit blit = new()
            {
                srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, level - 1, 0, 1),
                dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, level, 0, 1),
            };
            blit.srcOffsets[1] = new VkOffset3D(width, height, 1);
            blit.dstOffsets[1] = new VkOffset3D(nextWidth, nextHeight, 1);
            _deviceApi.vkCmdBlitImage(cmd, image.Image, VkImageLayout.TransferSrcOptimal, image.Image, VkImageLayout.TransferDstOptimal, 1, &blit, VkFilter.Linear);

            Barrier(cmd, image, level - 1, 1, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkAccessFlags.TransferRead, VkAccessFlags.ShaderRead, VkPipelineStageFlags.Transfer, VkPipelineStageFlags.FragmentShader);
            (width, height) = (nextWidth, nextHeight);
        }

        Barrier(cmd, image, levels - 1, 1, VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal,
            VkAccessFlags.TransferWrite, VkAccessFlags.ShaderRead, VkPipelineStageFlags.Transfer, VkPipelineStageFlags.FragmentShader);
        image.Layout = VkImageLayout.ShaderReadOnlyOptimal;
    }

    private void Barrier(VkCommandBuffer cmd, VulkanImage image, uint firstLevel, uint levelCount, VkImageLayout from, VkImageLayout to,
        VkAccessFlags srcAccess, VkAccessFlags dstAccess, VkPipelineStageFlags srcStage, VkPipelineStageFlags dstStage)
    {
        VkImageMemoryBarrier barrier = new()
        {
            oldLayout = from,
            newLayout = to,
            srcAccessMask = srcAccess,
            dstAccessMask = dstAccess,
            srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            image = image.Image,
            subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, firstLevel, levelCount, 0, 1),
        };
        _deviceApi.vkCmdPipelineBarrier(cmd, srcStage, dstStage, 0, 0, null, 0, null, 1, &barrier);
    }

    IImage IGraphicsDevice.CreateImage(ImageDesc desc) => CreateImage(desc);
    IImageView IGraphicsDevice.CreateImageView(IImage image) => CreateImageView(image);
    ISampler IGraphicsDevice.CreateSampler(SamplerDesc desc) => CreateSampler(desc);
}
