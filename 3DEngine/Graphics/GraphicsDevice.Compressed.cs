using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>Whether the device samples images of BC1, BC5 and BC7 blocks, which every desktop driver and lavapipe do.</summary>
    public bool CanSampleBlockCompressed { get; private set; }

    /// <summary>
    /// An image of blocks the GPU samples as they are, <paramref name="levels"/> its mip levels from
    /// the first, each copied in as it is, by the CPU where the device copies so (<see cref="CopiesOnHost"/>)
    /// or by the uploads recorded with it, ready to sample from the next submission on.
    /// </summary>
    /// <remarks>
    /// The image is made in a format a view of another of its class reads as well, so a color's
    /// blocks are sampled decoded from sRGB through one view and as they are through the other, as
    /// an image of four bytes a pixel is.
    /// </remarks>
    internal IImage CreateCompressedTexture(ImageFormat format, uint width, uint height, IReadOnlyList<ReadOnlyMemory<byte>> levels)
    {
        var image = (VulkanImage)CreateImage(new ImageDesc(new Extent2D(width, height), format,
            ImageUsage.Sampled | ImageUsage.TransferDst | ImageUsage.TransferSrc, (uint)levels.Count));
        // Every level copied in by the CPU where the device copies so, with no staging buffer and
        // no command, ready to sample from the next submission on.
        if (image.HostCopy)
        {
            var pins = levels.Select(level => level.Pin()).ToArray();
            try
            {
                var pointers = pins.Select(pin => (nint)pin.Pointer).ToArray();
                TransitionOnHost(image, (uint)levels.Count, VkImageLayout.ShaderReadOnlyOptimal);
                CopyOnHost(image, VkImageLayout.ShaderReadOnlyOptimal, pointers, width, height);
            }
            finally
            {
                foreach (var pin in pins) pin.Dispose();
            }
            return image;
        }
        // Each level from a multiple of 16 bytes, which a copy's offset into the buffer takes for a
        // block of 8 bytes or 16.
        var offsets = new ulong[levels.Count];
        ulong size = 0;
        for (int i = 0; i < levels.Count; i++)
        {
            offsets[i] = size;
            size = (size + (ulong)levels[i].Length + 15) / 16 * 16;
        }
        var staging = (VulkanBuffer)CreateBuffer(new BufferDesc(Math.Max(16, size), BufferUsage.TransferSrc, CpuAccessMode.Write));
        var bytes = Map(staging);
        for (int i = 0; i < levels.Count; i++)
            levels[i].Span.CopyTo(bytes[(int)offsets[i]..]);
        Unmap(staging);

        var cmd = UploadCommands();
        Barrier(cmd, image, 0, (uint)levels.Count, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal, VkAccessFlags2.None,
            VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.TopOfPipe, VkPipelineStageFlags2.Transfer);
        var regions = stackalloc VkBufferImageCopy[levels.Count];
        for (int i = 0; i < levels.Count; i++)
            regions[i] = new VkBufferImageCopy
            {
                bufferOffset = offsets[i],
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, (uint)i, 0, 1),
                imageExtent = new VkExtent3D(Math.Max(1, width >> i), Math.Max(1, height >> i), 1),
            };
        _deviceApi.vkCmdCopyBufferToImage(cmd, staging.Buffer, image.Image, VkImageLayout.TransferDstOptimal, (uint)levels.Count, regions);
        Barrier(cmd, image, 0, (uint)levels.Count, VkImageLayout.TransferDstOptimal, VkImageLayout.ShaderReadOnlyOptimal, VkAccessFlags2.TransferWrite,
            VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
        _batchStaging.Add(staging);
        image.Layout = VkImageLayout.ShaderReadOnlyOptimal;
        return image;
    }

    /// <summary>Whether an image holds blocks rather than pixels, which <see cref="DecodedPixels"/> reads.</summary>
    internal static bool HoldsBlocks(IImage image) => image.Description.Format is ImageFormat.Bc1_UNorm or ImageFormat.Bc1_Srgb
        or ImageFormat.Bc1Alpha_UNorm or ImageFormat.Bc1Alpha_Srgb or ImageFormat.Bc5_UNorm or ImageFormat.Bc7_UNorm or ImageFormat.Bc7_Srgb;

    /// <summary>
    /// A level of an image of blocks unpacked to four bytes a pixel by a blit, rows from the top, or
    /// none where the device blits from none of that format, which the texture store takes its
    /// average color and whether it is translucent from, as it does from an image's pixels.
    /// </summary>
    /// <remarks>
    /// The blit and the copy into a buffer the CPU reads go in one submission, which waits for the
    /// uploads before it.
    /// </remarks>
    internal byte[]? DecodedPixels(IImage compressed, uint level = 0)
    {
        var source = (VulkanImage)compressed;
        _instanceApi.vkGetPhysicalDeviceFormatProperties(_physicalDevice, ToVkFormat(source.Description.Format), out var properties);
        if ((properties.optimalTilingFeatures & VkFormatFeatureFlags.BlitSrc) == 0) return null;
        var whole = source.Description.Extent;
        var extent = new Extent2D(Math.Max(1, whole.Width >> (int)level), Math.Max(1, whole.Height >> (int)level));
        var target = (VulkanImage)CreateImage(new ImageDesc(extent, ImageFormat.R8G8B8A8_UNorm, ImageUsage.TransferDst | ImageUsage.TransferSrc));
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)(extent.Width * extent.Height * 4), BufferUsage.TransferDst, CpuAccessMode.Read));
        try
        {
            var cmd = BeginSingleTimeCommands();
            Barrier(cmd, source, level, 1, source.Layout, VkImageLayout.TransferSrcOptimal, VkAccessFlags2.ShaderRead, VkAccessFlags2.TransferRead,
                VkPipelineStageFlags2.AllCommands, VkPipelineStageFlags2.Transfer);
            Barrier(cmd, target, 0, 1, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal, VkAccessFlags2.None, VkAccessFlags2.TransferWrite,
                VkPipelineStageFlags2.TopOfPipe, VkPipelineStageFlags2.Transfer);
            var corner = new VkOffset3D((int)extent.Width, (int)extent.Height, 1);
            var blit = new VkImageBlit
            {
                srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, level, 0, 1),
                dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            };
            blit.srcOffsets[1] = corner;
            blit.dstOffsets[1] = corner;
            _deviceApi.vkCmdBlitImage(cmd, source.Image, VkImageLayout.TransferSrcOptimal, target.Image, VkImageLayout.TransferDstOptimal, 1, &blit, VkFilter.Nearest);
            Barrier(cmd, source, level, 1, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal, VkAccessFlags2.TransferRead,
                VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.FragmentShader);
            Barrier(cmd, target, 0, 1, VkImageLayout.TransferDstOptimal, VkImageLayout.TransferSrcOptimal, VkAccessFlags2.TransferWrite,
                VkAccessFlags2.TransferRead, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.Transfer);
            var region = new VkBufferImageCopy
            {
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageExtent = new VkExtent3D(extent.Width, extent.Height, 1),
            };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, target.Image, VkImageLayout.TransferSrcOptimal, buffer.Buffer, 1, &region);
            EndSingleTimeCommands(cmd);
            source.Layout = VkImageLayout.ShaderReadOnlyOptimal;
            return Map(buffer).ToArray();
        }
        finally
        {
            buffer.Dispose();
            target.Dispose();
        }
    }
}
