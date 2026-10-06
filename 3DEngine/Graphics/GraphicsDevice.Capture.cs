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
        CopyPresented(cmd, imageIndex, buffer);
        return (buffer, callback, width, height);
    }

    // Copies the frame's swapchain image into a buffer as large, between the end of drawing and
    // the present, and leaves the image in the layout it was found in.
    private void CopyPresented(VkCommandBuffer cmd, uint imageIndex, VulkanBuffer buffer)
    {
        var image = _swapchainImages[imageIndex];

        PipelineBarrier(cmd, ImageBarrier(image, ColorLevels(0, 1), _finalLayout, VkImageLayout.TransferSrcOptimal,
            VkPipelineStageFlags2.ColorAttachmentOutput | VkPipelineStageFlags2.Transfer, VkAccessFlags2.ColorAttachmentWrite | VkAccessFlags2.TransferRead,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead));

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
    }

    // The window's frames, copied as each is presented from the first time a program asks for the
    // screen, so a call between frames reads the last one. Not kept before that, since every frame
    // would pay for a copy few programs read.
    private VulkanBuffer? _keptScreen;
    private VkExtent2D _keptScreenExtent;
    private bool _keepScreen;
    private bool _screenKept;

    /// <summary>
    /// Has every frame from the next one on copied as it is presented, for
    /// <see cref="ReadKeptScreen"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">The surface does not allow its images to be copied from.</exception>
    public void KeepScreen()
    {
        if (!_swapchainCopyable)
            throw new NotSupportedException("This surface does not allow its images to be copied, so the screen cannot be read.");
        _keepScreen = true;
    }

    /// <summary>
    /// The last frame presented since <see cref="KeepScreen"/>, as four bytes a pixel (red, green,
    /// blue, alpha), rows from the top, or null before one has been.
    /// </summary>
    /// <remarks>The device is idled first, so the frame's copy has finished, which a call costs the frames in flight.</remarks>
    internal byte[]? ReadKeptScreen(out int width, out int height)
    {
        (width, height) = ((int)_keptScreenExtent.width, (int)_keptScreenExtent.height);
        if (!_screenKept || _keptScreen is not { } buffer) return null;
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        var pixels = Map(buffer).ToArray();
        Unmap(buffer);
        ToRgba(pixels);
        return pixels;
    }

    // Records the copy of this frame into the kept buffer, once the program has asked for the
    // screen, making the buffer again when the window has changed size.
    private void RecordKeptScreen(VkCommandBuffer cmd, uint imageIndex)
    {
        if (!_keepScreen || imageIndex >= _swapchainImages.Length) return;
        if (_keptScreen is null || _keptScreenExtent.width != _swapchainExtent.width || _keptScreenExtent.height != _swapchainExtent.height)
        {
            if (_keptScreen is { } old)
            {
                // A frame still in flight may be copying into it, and a resize is rare.
                _deviceApi.vkDeviceWaitIdle().CheckResult();
                old.Dispose();
            }
            _keptScreenExtent = _swapchainExtent;
            _keptScreen = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)(_swapchainExtent.width * _swapchainExtent.height * 4), BufferUsage.TransferDst, CpuAccessMode.Read));
        }
        CopyPresented(cmd, imageIndex, _keptScreen);
        _screenKept = true;
    }

    private void DisposeKeptScreen()
    {
        _keptScreen?.Dispose();
        _keptScreen = null;
        (_keepScreen, _screenKept) = (false, false);
    }

    // The swapchain's pixels in RGBA order, opaque.
    private void ToRgba(byte[] pixels)
    {
        var bgra = _swapchainFormat is VkFormat.B8G8R8A8Unorm or VkFormat.B8G8R8A8Srgb;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if (bgra) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
            // The swapchain is composited opaque, so whatever alpha it holds is not what was seen.
            pixels[i + 3] = 255;
        }
    }

    // Waits for the frame that carried the copy, reads the pixels into RGBA order and hands them on.
    private void FinishCapture((VulkanBuffer Buffer, Action<byte[], int, int> Callback, int Width, int Height) capture, VkFence fence)
    {
        _deviceApi.vkWaitForFences(fence, true, ulong.MaxValue).CheckResult();
        try
        {
            var pixels = Map(capture.Buffer).ToArray();
            Unmap(capture.Buffer);
            ToRgba(pixels);

            capture.Callback(pixels, capture.Width, capture.Height);
        }
        finally
        {
            capture.Buffer.Dispose();
        }
    }

    /// <summary>
    /// The first level of a color image a pass samples, as four bytes a pixel (red, green, blue,
    /// alpha), rows from the top, copied now and waited for, after every frame in flight has
    /// finished with it.
    /// </summary>
    /// <remarks>
    /// The device is idled first, so a call costs the frames in flight, which a program reading
    /// a render texture back to save it can take. An image of half floats or floats, as a
    /// G-buffer's, is read at its own size and given as four bytes a pixel, each channel from 0 to
    /// 1 as a byte, as every image here is.
    /// </remarks>
    internal byte[] ReadPixels(IImage image)
    {
        var vkImage = (VulkanImage)image;
        var extent = vkImage.Description.Extent;
        var bytesPerPixel = vkImage.Description.Format switch
        {
            ImageFormat.R16G16B16A16_Float => 8,
            ImageFormat.R32G32B32A32_Float => 16,
            _ => 4,
        };
        FlushUploads();
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)(extent.Width * extent.Height * bytesPerPixel), BufferUsage.TransferDst, CpuAccessMode.Read));
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
            if (bytesPerPixel != 4) return ToBytes(pixels, bytesPerPixel);
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

    // Channels of half floats or floats as bytes, each clamped to 0 to 1.
    private static byte[] ToBytes(ReadOnlySpan<byte> wide, int bytesPerPixel)
    {
        var channels = wide.Length / (bytesPerPixel / 4);
        var bytes = new byte[channels];
        for (int c = 0; c < channels; c++)
        {
            float value = bytesPerPixel == 8
                ? (float)System.Buffers.Binary.BinaryPrimitives.ReadHalfLittleEndian(wide[(c * 2)..])
                : System.Buffers.Binary.BinaryPrimitives.ReadSingleLittleEndian(wide[(c * 4)..]);
            bytes[c] = (byte)MathF.Round(Math.Clamp(float.IsNaN(value) ? 0 : value, 0, 1) * 255);
        }
        return bytes;
    }
}
