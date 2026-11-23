using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    private Action<byte[], int, int>? _shownRequest;
    private bool _shownCopyable;

    /// <summary>
    /// Asks for the next frame as the window shows it, scaled onto the window where the frames are
    /// drawn at a size of their own, and as <see cref="RequestCapture"/> gives it otherwise.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The surface does not allow its images to be copied from.
    /// </exception>
    public void RequestShownCapture(Action<byte[], int, int> onCaptured)
    {
        if (!(_scaled ? _shownCopyable : _swapchainCopyable))
            throw new NotSupportedException("This surface does not allow its images to be copied, so frames cannot be captured.");
        _shownRequest = _shownRequest is null ? onCaptured : _shownRequest + onCaptured;
    }

    // Records the copy of the frame as the window shows it, once it is copied onto the window's
    // image, if one was asked for.
    private (VulkanBuffer Buffer, Action<byte[], int, int> Callback, int Width, int Height)? RecordShownCapture(VkCommandBuffer cmd, uint imageIndex)
    {
        if (_shownRequest is not { } callback || imageIndex >= _swapchainImages.Length) return null;
        _shownRequest = null;
        var (image, extent, layout) = _scaled
            ? (_shownImages[imageIndex], _shownExtent, VkImageLayout.PresentSrcKHR)
            : (_swapchainImages[imageIndex], _swapchainExtent, _finalLayout);
        var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)(extent.width * extent.height * 4), BufferUsage.TransferDst, CpuAccessMode.Read));
        CopyOut(cmd, image, extent, layout, buffer);
        return (buffer, callback, (int)extent.width, (int)extent.height);
    }

    // Records the copy of the frame image onto the window's image it is presented in, scaled as the
    // surface says with black round it, once the frame and its captures are done with the frame
    // image, which the render pass leaves ready to be copied from. The acquire's wait holds the
    // copy as it holds the drawing.
    private void RecordShown(VkCommandBuffer cmd, uint imageIndex)
    {
        if (!_scaled || imageIndex >= _shownImages.Length) return;
        var shown = _shownImages[imageIndex];
        var (x, y, width, height, filtered) = Placement.Of((int)_swapchainExtent.width, (int)_swapchainExtent.height,
            (int)_shownExtent.width, (int)_shownExtent.height, _surfaceSource!.Scaling);
        var whole = ColorLevels(0, 1);
        PipelineBarrier(cmd, ImageBarrier(shown, whole, VkImageLayout.Undefined, VkImageLayout.TransferDstOptimal,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.None, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite));
        if (width < _shownExtent.width || height < _shownExtent.height)
        {
            var black = new VkClearColorValue(0f, 0f, 0f, 1f);
            _deviceApi.vkCmdClearColorImage(cmd, shown, VkImageLayout.TransferDstOptimal, &black, 1, &whole);
            PipelineBarrier(cmd, ImageBarrier(shown, whole, VkImageLayout.TransferDstOptimal, VkImageLayout.TransferDstOptimal,
                VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite));
        }
        VkImageBlit blit = new()
        {
            srcSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
            dstSubresource = new VkImageSubresourceLayers(VkImageAspectFlags.Color, 0, 0, 1),
        };
        blit.srcOffsets[1] = new VkOffset3D((int)_swapchainExtent.width, (int)_swapchainExtent.height, 1);
        blit.dstOffsets[0] = new VkOffset3D(x, y, 0);
        blit.dstOffsets[1] = new VkOffset3D(x + width, y + height, 1);
        _deviceApi.vkCmdBlitImage(cmd, _swapchainImages[imageIndex], VkImageLayout.TransferSrcOptimal, shown, VkImageLayout.TransferDstOptimal,
            1, &blit, filtered ? VkFilter.Linear : VkFilter.Nearest);
        PipelineBarrier(cmd, ImageBarrier(shown, whole, VkImageLayout.TransferDstOptimal, VkImageLayout.PresentSrcKHR,
            VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferWrite, VkPipelineStageFlags2.None, VkAccessFlags2.None));
    }
}
