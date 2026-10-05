using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
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

        PipelineBarrier(cmd, ImageBarrier(image, ColorLevels(0, 1), _finalLayout, VkImageLayout.TransferSrcOptimal,
            VkPipelineStageFlags2.ColorAttachmentOutput, VkAccessFlags2.ColorAttachmentWrite, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead));

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

        PipelineBarrier(cmd, ImageBarrier(image, ColorLevels(0, 1), VkImageLayout.TransferSrcOptimal, _finalLayout,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead, VkPipelineStageFlags2.None, VkAccessFlags2.None));

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
    /// a pixel (red, green, blue, alpha), or four half floats for a half-float target, rows from
    /// the top, handed to <paramref name="done"/> on the thread that submits the frame.
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
            var bytes = image.Description.Format == ImageFormat.R16G16B16A16_Float ? 8 : 4;
            var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)(extent.Width * extent.Height * bytes), BufferUsage.TransferDst, CpuAccessMode.Read));
            PipelineBarrier(cmd, ImageBarrier(image.Image, ColorLevels(0, 1), VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferSrcOptimal,
                VkPipelineStageFlags2.ColorAttachmentOutput | VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ColorAttachmentWrite,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead));
            VkBufferImageCopy region = new()
            {
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageExtent = new VkExtent3D(extent.Width, extent.Height, 1),
            };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, image.Image, VkImageLayout.TransferSrcOptimal, buffer.Buffer, 1, &region);
            PipelineBarrier(cmd, ImageBarrier(image.Image, ColorLevels(0, 1), VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead, VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ShaderRead));
            recorded.Add((buffer, image, done));
        }
        _readbacks.Clear();
        return recorded;
    }

    // The copies each frame slot carried, handed on once the slot's fence has been waited for at
    // the start of its next frame. Waited for as the frame was submitted, they held the CPU until
    // the GPU had drawn the whole frame, 8 to 24 ms a frame while a level's probes were captured.
    private readonly List<(VulkanBuffer Buffer, VulkanImage Image, Action<byte[]> Done)>?[] _pendingReadbacks = new List<(VulkanBuffer, VulkanImage, Action<byte[]>)>?[MaxFramesInFlight];

    private void QueueReadbacks(List<(VulkanBuffer Buffer, VulkanImage Image, Action<byte[]> Done)> readbacks)
    {
        if (_pendingReadbacks[_currentFrame] is { } earlier) earlier.AddRange(readbacks);
        else _pendingReadbacks[_currentFrame] = readbacks;
    }

    // Hands on the copies of a slot whose fence has signalled, or drops every slot's when the
    // device is going away.
    private void FinishReadbacks(int slot, bool drop = false)
    {
        if (_pendingReadbacks[slot] is not { } readbacks) return;
        _pendingReadbacks[slot] = null;
        if (drop)
        {
            foreach (var (buffer, _, _) in readbacks) buffer.Dispose();
            return;
        }
        FinishReadbacks(readbacks);
    }

    // Hands each image's pixels on in RGBA order, from a frame the GPU has finished.
    private void FinishReadbacks(List<(VulkanBuffer Buffer, VulkanImage Image, Action<byte[]> Done)> readbacks)
    {
        var bgra = _swapchainFormat is VkFormat.B8G8R8A8Unorm or VkFormat.B8G8R8A8Srgb;
        foreach (var (buffer, image, done) in readbacks)
        {
            try
            {
                var pixels = Map(buffer).ToArray();
                if (bgra && image.Description.Format != ImageFormat.R16G16B16A16_Float)
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

    /// <summary>
    /// The first level of a color image a pass samples, as four bytes a pixel (red, green, blue,
    /// alpha), rows from the top, copied now and waited for, after every frame in flight has
    /// finished with it.
    /// </summary>
    /// <remarks>
    /// The device is idled first, so a call costs the frames in flight, which a program reading
    /// a render texture back to save it can take.
    /// </remarks>
    internal byte[] ReadPixels(IImage image)
    {
        var vkImage = (VulkanImage)image;
        var extent = vkImage.Description.Extent;
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)(extent.Width * extent.Height * 4), BufferUsage.TransferDst, CpuAccessMode.Read));
        try
        {
            var cmd = BeginSingleTimeCommands();
            Barrier(cmd, vkImage, 0, 1, VkImageLayout.ShaderReadOnlyOptimal, VkImageLayout.TransferSrcOptimal,
                VkAccessFlags2.ShaderRead, VkAccessFlags2.TransferRead, VkPipelineStageFlags2.AllCommands, VkPipelineStageFlags2.Transfer);
            VkBufferImageCopy region = new()
            {
                imageSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
                imageExtent = new VkExtent3D(extent.Width, extent.Height, 1),
            };
            _deviceApi.vkCmdCopyImageToBuffer(cmd, vkImage.Image, VkImageLayout.TransferSrcOptimal, buffer.Buffer, 1, &region);
            Barrier(cmd, vkImage, 0, 1, VkImageLayout.TransferSrcOptimal, VkImageLayout.ShaderReadOnlyOptimal,
                VkAccessFlags2.TransferRead, VkAccessFlags2.ShaderRead, VkPipelineStageFlags2.Transfer, VkPipelineStageFlags2.AllCommands);
            EndSingleTimeCommands(cmd);

            var pixels = Map(buffer).ToArray();
            // A render target's color is the window's format, which may hold blue first.
            if (vkImage.Description.Format == ImageFormat.B8G8R8A8_UNorm && _swapchainFormat is VkFormat.B8G8R8A8Unorm or VkFormat.B8G8R8A8Srgb)
                for (int i = 0; i < pixels.Length; i += 4)
                    (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            return pixels;
        }
        finally
        {
            buffer.Dispose();
        }
    }
}
