using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    private Action<byte[], int, int>? _shownRequest;
    private bool _shownCopyable;

    // What the window's pass draws into where the frames are drawn at the picture's size of its
    // own, each of the window's images through a view, and with the window's samples a color image
    // every one shares and a depth image at the window's size.
    private VkImageView[] _shownViews = [];
    private VulkanFramebuffer[] _shownFramebuffers = [];
    private VkImage _shownColor, _shownDepth;
    private VkDeviceMemory _shownColorMemory, _shownDepthMemory;
    private VkImageView _shownColorView, _shownDepthView;

    /// <inheritdoc />
    public IFramebuffer? GetShownFramebuffer(uint imageIndex) =>
        _scaled && imageIndex < _shownFramebuffers.Length ? _shownFramebuffers[imageIndex] : null;

    /// <inheritdoc />
    public Extent2D ShownExtent => _scaled
        ? new Extent2D(_shownExtent.width, _shownExtent.height)
        : new Extent2D(_swapchainExtent.width, _swapchainExtent.height);

    /// <summary>
    /// Asks for the next frame as the window shows it, the picture brought onto the window with the
    /// interface over it where the frames are drawn at a size of their own, and as
    /// <see cref="RequestCapture"/> gives it otherwise.
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

    // Records the copy of the frame as the window shows it, once its pass has ended, if one was
    // asked for.
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

    /// <inheritdoc />
    /// <remarks>
    /// The frame image is read at each window pixel's own place as <c>scaled.slang</c> works it
    /// out, the nearest picture pixel, the sharp blocks, or a blend of the four round the place,
    /// which it takes wherever the picture is brought down along either side.
    /// </remarks>
    public void DrawPicture(ICommandBuffer commandBuffer, uint imageIndex)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        if (!_scaled || imageIndex >= _swapchainImages.Length || !PictureReady()) return;
        var cmd = vkCmd.Handle;
        var (frameWidth, frameHeight) = ((int)_swapchainExtent.width, (int)_swapchainExtent.height);
        var (x, y, width, height) = PictureSize.Placed(_surfaceSource!.Bars, (frameWidth, frameHeight),
            ((int)_shownExtent.width, (int)_shownExtent.height));
        var down = width < frameWidth || height < frameHeight;
        var mode = down ? 0u : _surfaceSource.Filter switch { PictureFilter.Nearest => 1u, PictureFilter.Sharp => 2u, _ => 0u };

        var viewport = new VkViewport(x, y, width, height, 0f, 1f);
        _deviceApi.vkCmdSetViewport(cmd, 0, 1, &viewport);
        var scissor = new VkRect2D(x, y, (uint)width, (uint)height);
        _deviceApi.vkCmdSetScissor(cmd, 0, 1, &scissor);
        _deviceApi.vkCmdBindPipeline(cmd, VkPipelineBindPoint.Graphics, _sharpPipeline);
        var set = _sharpSets[imageIndex];
        _deviceApi.vkCmdBindDescriptorSets(cmd, VkPipelineBindPoint.Graphics, _sharpLayout, 0, 1, &set, 0, null);
        var push = stackalloc uint[5];
        var floats = (float*)push;
        (floats[0], floats[1], floats[2], floats[3]) = (x, y, (float)width / frameWidth, (float)height / frameHeight);
        push[4] = mode;
        _deviceApi.vkCmdPushConstants(cmd, _sharpLayout, VkShaderStageFlags.Fragment, 0, PicturePush, push);
        _deviceApi.vkCmdDraw(cmd, 3, 1, 0, 0);

        viewport = new VkViewport(0, 0, _shownExtent.width, _shownExtent.height, 0f, 1f);
        _deviceApi.vkCmdSetViewport(cmd, 0, 1, &viewport);
        scissor = new VkRect2D(0, 0, _shownExtent.width, _shownExtent.height);
        _deviceApi.vkCmdSetScissor(cmd, 0, 1, &scissor);
    }

    // The window's images' views, and the window's pass's color and depth at its size and samples,
    // made with the frame images where they are drawn at a size of their own.
    private void CreateShownResources()
    {
        if (!_scaled) return;
        var (width, height) = (_shownExtent.width, _shownExtent.height);
        _shownViews = new VkImageView[_shownImages.Length];
        for (int i = 0; i < _shownImages.Length; i++)
            _shownViews[i] = TargetView(_shownImages[i], _swapchainFormat, VkImageAspectFlags.Color);
        bool msaa = _samples != VkSampleCountFlags.Count1;
        if (msaa)
        {
            (_shownColor, _shownColorMemory) = TargetImage(_swapchainFormat, width, height,
                VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransientAttachment, samples: _samples);
            _shownColorView = TargetView(_shownColor, _swapchainFormat, VkImageAspectFlags.Color);
        }
        (_shownDepth, _shownDepthMemory) = TargetImage(VkFormat.D32Sfloat, width, height, VkImageUsageFlags.DepthStencilAttachment, samples: _samples);
        _shownDepthView = TargetView(_shownDepth, VkFormat.D32Sfloat, VkImageAspectFlags.Depth);
        var depth = new Attachment(_shownDepth, _shownDepthView, VkImageAspectFlags.Depth, 0, VkImageLayout.DepthStencilAttachmentOptimal, Store: false);
        _shownFramebuffers = new VulkanFramebuffer[_shownImages.Length];
        for (int i = 0; i < _shownImages.Length; i++)
        {
            var shown = new Attachment(_shownImages[i], _shownViews[i], VkImageAspectFlags.Color, 0, VkImageLayout.PresentSrcKHR);
            _shownFramebuffers[i] = msaa
                ? new VulkanFramebuffer(
                    new Attachment(_shownColor, _shownColorView, VkImageAspectFlags.Color, 0, VkImageLayout.ColorAttachmentOptimal, Store: false),
                    shown, depth, default)
                : new VulkanFramebuffer(shown, default, depth, default);
        }
    }

    private void DestroyShownResources()
    {
        foreach (var view in _shownViews)
            if (view.Handle != 0) _deviceApi.vkDestroyImageView(view);
        _shownViews = [];
        _shownFramebuffers = [];
        foreach (var view in new[] { _shownColorView, _shownDepthView })
            if (view.Handle != 0) _deviceApi.vkDestroyImageView(view);
        foreach (var image in new[] { _shownColor, _shownDepth })
            if (image.Handle != 0)
            {
                DeviceObjects.Gone(DeviceObjects.Kind.Image);
                _deviceApi.vkDestroyImage(image);
            }
        foreach (var memory in new[] { _shownColorMemory, _shownDepthMemory })
            if (memory.Handle != 0)
            {
                DeviceObjects.Gone(DeviceObjects.Kind.Memory);
                _deviceApi.vkFreeMemory(memory);
            }
        (_shownColor, _shownDepth) = (default, default);
        (_shownColorMemory, _shownDepthMemory) = (default, default);
        (_shownColorView, _shownDepthView) = (default, default);
    }
}
