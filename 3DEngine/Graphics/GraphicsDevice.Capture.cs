using Vortice.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    private Action<byte[], int, int>? _captureRequest;
    private bool _swapchainCopyable;

    /// <summary>
    /// Asks for the next presented frame's pixels. <paramref name="onCaptured"/> receives them as
    /// four bytes per pixel (red, green, blue, alpha), rows from the top, with the width and
    /// height, on the thread that submits the frame.
    /// </summary>
    /// <remarks>
    /// The frame is copied out of the swapchain image after it is drawn and before it is
    /// presented, and the submit waits for that copy, so a capture costs the frame a stall. A second
    /// request before the first is served replaces it.
    /// </remarks>
    /// <exception cref="NotSupportedException">The surface does not allow its images to be copied from.</exception>
    public void RequestCapture(Action<byte[], int, int> onCaptured)
    {
        if (!_swapchainCopyable)
            throw new NotSupportedException("This surface does not allow its images to be copied, so frames cannot be captured.");
        _captureRequest = onCaptured;
    }

    // Records the copy of the presented image into a host-visible buffer, if a capture was asked
    // for. Called while the frame's command buffer is still open, after the render pass has ended
    // and left the image in its final layout, PresentSrcKHR or, offscreen, TransferSrcOptimal.
    private (VulkanBuffer Buffer, Action<byte[], int, int> Callback, int Width, int Height)? RecordCapture(VkCommandBuffer cmd, uint imageIndex)
    {
        if (_captureRequest is not { } callback || imageIndex >= _swapchainImages.Length) return null;
        _captureRequest = null;

        int width = (int)_swapchainExtent.width, height = (int)_swapchainExtent.height;
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)(width * height * 4), BufferUsage.TransferDst, CpuAccessMode.Read));
        var image = _swapchainImages[imageIndex];

        VkImageMemoryBarrier toCopy = new()
        {
            oldLayout = _finalLayout,
            newLayout = VkImageLayout.TransferSrcOptimal,
            srcAccessMask = VkAccessFlags.ColorAttachmentWrite,
            dstAccessMask = VkAccessFlags.TransferRead,
            srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            image = image,
            subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, 1, 0, 1),
        };
        _deviceApi.vkCmdPipelineBarrier(cmd, VkPipelineStageFlags.ColorAttachmentOutput, VkPipelineStageFlags.Transfer, 0, 0, null, 0, null, 1, &toCopy);

        VkBufferImageCopy region = new()
        {
            bufferOffset = 0,
            bufferRowLength = 0,
            bufferImageHeight = 0,
            imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            imageOffset = new VkOffset3D(0, 0, 0),
            imageExtent = new VkExtent3D(_swapchainExtent.width, _swapchainExtent.height, 1),
        };
        _deviceApi.vkCmdCopyImageToBuffer(cmd, image, VkImageLayout.TransferSrcOptimal, buffer.Buffer, 1, &region);

        VkImageMemoryBarrier toPresent = toCopy with
        {
            oldLayout = VkImageLayout.TransferSrcOptimal,
            newLayout = _finalLayout,
            srcAccessMask = VkAccessFlags.TransferRead,
            dstAccessMask = 0,
        };
        _deviceApi.vkCmdPipelineBarrier(cmd, VkPipelineStageFlags.Transfer, VkPipelineStageFlags.BottomOfPipe, 0, 0, null, 0, null, 1, &toPresent);

        return (buffer, callback, width, height);
    }

    // Waits for the frame that carried the copy, reads the pixels into RGBA order and hands them on.
    private void FinishCapture((VulkanBuffer Buffer, Action<byte[], int, int> Callback, int Width, int Height) capture, VkFence fence)
    {
        _deviceApi.vkWaitForFences(fence, true, ulong.MaxValue).CheckResult();
        try
        {
            var pixels = Map(capture.Buffer).ToArray();
            Unmap(capture.Buffer);

            var bgra = _swapchainFormat is VkFormat.B8G8R8A8Unorm or VkFormat.B8G8R8A8Srgb;
            for (int i = 0; i < pixels.Length; i += 4)
            {
                if (bgra) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
                // The swapchain is composited opaque, so whatever alpha it holds is not what was seen.
                pixels[i + 3] = 255;
            }

            capture.Callback(pixels, capture.Width, capture.Height);
        }
        finally
        {
            capture.Buffer.Dispose();
        }
    }

    // Render target images asked to be read back at the end of this frame, each with what is told
    // the pixels.
    private readonly List<(VulkanImage Image, Action<byte[]> Done)> _readbacks = [];

    /// <summary>
    /// Asks for a render target's color image as the frame being recorded leaves it, as four bytes
    /// a pixel (red, green, blue, alpha), rows from the top, handed to <paramref name="done"/> on
    /// the thread that submits the frame.
    /// </summary>
    /// <remarks>
    /// The submit waits for the copy, as a capture's does, so a readback costs the frame a stall,
    /// which a reflection probe's capture, made once, can take.
    /// </remarks>
    internal void RequestReadback(IImage image, Action<byte[]> done) => _readbacks.Add(((VulkanImage)image, done));

    // Records the copy of each image asked for into a buffer of its own. Called while the frame's
    // command buffer is still open, after every pass has ended and left the images ready to sample.
    private List<(VulkanBuffer Buffer, VulkanImage Image, Action<byte[]> Done)>? RecordReadbacks(VkCommandBuffer cmd)
    {
        if (_readbacks.Count == 0) return null;
        var recorded = new List<(VulkanBuffer, VulkanImage, Action<byte[]>)>(_readbacks.Count);
        foreach (var (image, done) in _readbacks)
        {
            var extent = image.Description.Extent;
            var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)(extent.Width * extent.Height * 4), BufferUsage.TransferDst, CpuAccessMode.Read));
            VkImageMemoryBarrier toCopy = new()
            {
                oldLayout = VkImageLayout.ShaderReadOnlyOptimal,
                newLayout = VkImageLayout.TransferSrcOptimal,
                srcAccessMask = VkAccessFlags.ColorAttachmentWrite,
                dstAccessMask = VkAccessFlags.TransferRead,
                srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
                image = image.Image,
                subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, 1, 0, 1),
            };
            _deviceApi.vkCmdPipelineBarrier(cmd, VkPipelineStageFlags.ColorAttachmentOutput | VkPipelineStageFlags.FragmentShader,
                VkPipelineStageFlags.Transfer, 0, 0, null, 0, null, 1, &toCopy);
            VkBufferImageCopy region = new()
            {
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageExtent = new VkExtent3D(extent.Width, extent.Height, 1),
            };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, image.Image, VkImageLayout.TransferSrcOptimal, buffer.Buffer, 1, &region);
            VkImageMemoryBarrier back = toCopy with
            {
                oldLayout = VkImageLayout.TransferSrcOptimal,
                newLayout = VkImageLayout.ShaderReadOnlyOptimal,
                srcAccessMask = VkAccessFlags.TransferRead,
                dstAccessMask = VkAccessFlags.ShaderRead,
            };
            _deviceApi.vkCmdPipelineBarrier(cmd, VkPipelineStageFlags.Transfer, VkPipelineStageFlags.FragmentShader, 0, 0, null, 0, null, 1, &back);
            recorded.Add((buffer, image, done));
        }
        _readbacks.Clear();
        return recorded;
    }

    // Waits for the frame that carried the copies and hands each image's pixels on in RGBA order.
    private void FinishReadbacks(List<(VulkanBuffer Buffer, VulkanImage Image, Action<byte[]> Done)> readbacks, VkFence fence)
    {
        _deviceApi.vkWaitForFences(fence, true, ulong.MaxValue).CheckResult();
        var bgra = _swapchainFormat is VkFormat.B8G8R8A8Unorm or VkFormat.B8G8R8A8Srgb;
        foreach (var (buffer, _, done) in readbacks)
        {
            try
            {
                var pixels = Map(buffer).ToArray();
                if (bgra)
                    for (int i = 0; i < pixels.Length; i += 4)
                        (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
                done(pixels);
            }
            finally
            {
                buffer.Dispose();
            }
        }
    }
}
