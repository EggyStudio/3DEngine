using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>Acquires the next swapchain image and begins a command buffer. Render pass lifecycle is managed by graph nodes.</summary>
    /// <param name="clearColor">The clear color (stored for SwapchainTarget consumers).</param>
    /// <returns>A <see cref="VulkanFrameContext"/> encapsulating the in-flight frame state.</returns>
    private partial IFrameContext BeginFrameInternal(ClearColor clearColor)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Graphics device not initialized");

        var mark = System.Diagnostics.Stopwatch.GetTimestamp();
        _deviceApi.vkWaitForFences(_inFlightFences[_currentFrame], true, ulong.MaxValue).CheckResult();
        FenceWaitMs = System.Diagnostics.Stopwatch.GetElapsedTime(mark).TotalMilliseconds;

        // After the fence signals, the GPU has finished reading staging buffers from this slot's
        // previous frame, so they are disposed now.
        FlushDeferredStagingBuffers(_currentFrame);
        RetireUploads();
        FinishReadbacks(_currentFrame);

        uint imageIndex;
        var result = VkResult.Success;
        mark = System.Diagnostics.Stopwatch.GetTimestamp();
        if (_offscreen)
            imageIndex = _offscreenNext++ % (uint)_swapchainImages.Length;
        else
            result = _deviceApi.vkAcquireNextImageKHR(_swapchain, ulong.MaxValue,
                _imageAvailableSemaphores[_currentFrame], default, out imageIndex);

        AcquireMs = System.Diagnostics.Stopwatch.GetElapsedTime(mark).TotalMilliseconds;
        NoteDisplayWait(AcquireMs);

        if (result == VkResult.ErrorOutOfDateKHR)
        {
            Logger.Warn("Swapchain out of date while acquiring an image, so it is resized and the acquire retried.");
            OnResize();
            return BeginFrameInternal(clearColor);
        }

        if (result != VkResult.SuboptimalKHR)
            result.CheckResult();

        _lastAcquiredImageIndex = imageIndex;
        var cmd = _commandBuffers[_currentFrame];

        VkCommandBufferBeginInfo beginInfo = new();
        _deviceApi.vkResetCommandBuffer(cmd, 0).CheckResult();
        _deviceApi.vkBeginCommandBuffer(cmd, &beginInfo).CheckResult();

        return new VulkanFrameContext(this, imageIndex, _currentFrame, MaxFramesInFlight, cmd, _swapchainExtent, _resizeVersion);
    }

    /// <summary>Ends the command buffer, submits to the graphics queue, and presents the frame.</summary>
    /// <param name="ctx">The frame context returned by <see cref="BeginFrameInternal"/>.</param>
    private partial void SubmitFrame(VulkanFrameContext ctx)
    {
        var readbacks = RecordReadbacks(ctx.CommandBufferHandle);
        var capture = RecordCapture(ctx.CommandBufferHandle, ctx.FrameIndex);
        _deviceApi.vkEndCommandBuffer(ctx.CommandBufferHandle).CheckResult();

        var waitStage = VkPipelineStageFlags.ColorAttachmentOutput;
        VkSemaphore* waitSemaphores = stackalloc VkSemaphore[1];
        waitSemaphores[0] = _imageAvailableSemaphores[_currentFrame];
        VkCommandBuffer* commandBuffers = stackalloc VkCommandBuffer[1];
        commandBuffers[0] = ctx.CommandBufferHandle;
        VkSemaphore* signalSemaphores = stackalloc VkSemaphore[1];
        signalSemaphores[0] = _renderFinishedSemaphores[ctx.FrameIndex];

        // Offscreen, no image is acquired to wait for and none is presented to signal.
        uint semaphores = _offscreen ? 0u : 1u;
        VkSubmitInfo submitInfo = new()
        {
            waitSemaphoreCount = semaphores,
            pWaitSemaphores = waitSemaphores,
            pWaitDstStageMask = &waitStage,
            commandBufferCount = 1,
            pCommandBuffers = commandBuffers,
            signalSemaphoreCount = semaphores,
            pSignalSemaphores = signalSemaphores
        };

        _deviceApi.vkResetFences(_inFlightFences[_currentFrame]).CheckResult();
        FlushUploads();
        _deviceApi.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, _inFlightFences[_currentFrame]).CheckResult();

        if (capture is { } taken)
            FinishCapture(taken, _inFlightFences[_currentFrame]);
        if (readbacks is not null)
            QueueReadbacks(readbacks);

        if (_offscreen)
        {
            _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
            return;
        }

        VkSemaphore* presentWaitSemaphores = stackalloc VkSemaphore[1];
        presentWaitSemaphores[0] = _renderFinishedSemaphores[ctx.FrameIndex];
        VkSwapchainKHR* swapchains = stackalloc VkSwapchainKHR[1];
        swapchains[0] = _swapchain;
        uint* imageIndices = stackalloc uint[1];
        imageIndices[0] = ctx.FrameIndex;

        VkPresentInfoKHR presentInfo = new()
        {
            waitSemaphoreCount = 1,
            pWaitSemaphores = presentWaitSemaphores,
            swapchainCount = 1,
            pSwapchains = swapchains,
            pImageIndices = imageIndices
        };

        var mark = System.Diagnostics.Stopwatch.GetTimestamp();
        var presentResult = _deviceApi.vkQueuePresentKHR(_presentQueue, &presentInfo);
        PresentMs = System.Diagnostics.Stopwatch.GetElapsedTime(mark).TotalMilliseconds;
        NoteDisplayWait(PresentMs);

        if (presentResult == VkResult.ErrorOutOfDateKHR)
        {
            // The swapchain cannot be used, so it is rebuilt now.
            Logger.Warn("Swapchain out of date while presenting, so it is resized.");
            OnResize();
        }
        else if (presentResult == VkResult.SuboptimalKHR)
        {
            // Swapchain still works but isn't ideal (e.g. mid-drag on Linux).
            // It is left as it is. The next coalesced ResizeEvent or a future
            // ErrorOutOfDateKHR will trigger a rebuild at the right time.
            if (!_suboptimalLogged)
            {
                Logger.Debug("Swapchain suboptimal while presenting, so its resize waits.");
                _suboptimalLogged = true;
            }
        }
        else
        {
            presentResult.CheckResult();
        }

        _currentFrame = (_currentFrame + 1) % MaxFramesInFlight;
    }

    /// <summary>Vulkan implementation of <see cref="IFrameContext"/> holding per-frame handles and the resize generation.</summary>
    /// <seealso cref="IFrameContext"/>
    private sealed class VulkanFrameContext : IFrameContext
    {
        private readonly GraphicsDevice _owner;
        private readonly ulong _bornResizeVersion;

        /// <inheritdoc />
        public uint FrameIndex { get; }
        /// <inheritdoc />
        public int InFlightIndex { get; }
        /// <inheritdoc />
        public int FramesInFlight { get; }
        /// <inheritdoc />
        public ICommandBuffer CommandBuffer { get; }
        /// <inheritdoc />
        public Extent2D Extent { get; }

        /// <summary>The raw Vulkan command buffer handle for direct API calls.</summary>
        internal VkCommandBuffer CommandBufferHandle { get; }

        /// <summary>Creates a new frame context capturing the current swapchain and synchronization state.</summary>
        /// <param name="owner">The owning graphics device.</param>
        /// <param name="frameIndex">Swapchain image index for this frame.</param>
        /// <param name="inFlightIndex">In-flight slot index (0 .. <c>MaxFramesInFlight-1</c>).</param>
        /// <param name="framesInFlight">Total number of frames allowed in flight.</param>
        /// <param name="cmd">The Vulkan command buffer for this frame.</param>
        /// <param name="extent">The current swapchain extent.</param>
        /// <param name="resizeVersion">Swapchain resize generation at context creation time.</param>
        public VulkanFrameContext(GraphicsDevice owner, uint frameIndex, int inFlightIndex, int framesInFlight,
            VkCommandBuffer cmd, VkExtent2D extent, ulong resizeVersion)
        {
            _owner = owner;
            _bornResizeVersion = resizeVersion;
            FrameIndex = frameIndex;
            InFlightIndex = inFlightIndex;
            FramesInFlight = framesInFlight;
            CommandBufferHandle = cmd;
            Extent = new Extent2D(extent.width, extent.height);
            CommandBuffer = new VulkanCommandBuffer(cmd);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_bornResizeVersion != _owner._resizeVersion)
            {
                // resources already destroyed with swapchain recreation
            }
        }
    }

    private bool _debugUtils;

    /// <summary>
    /// Names a buffer or image for a capture tool such as RenderDoc, which shows it by the name,
    /// where the instance has <c>VK_EXT_debug_utils</c>.
    /// </summary>
    internal void Name(object resource, string name)
    {
        if (!_debugUtils) return;
        var (type, handle) = resource switch
        {
            VulkanImage image => (VkObjectType.Image, image.Image.Handle),
            VulkanBuffer buffer => (VkObjectType.Buffer, buffer.Buffer.Handle),
            _ => (VkObjectType.Unknown, 0UL),
        };
        if (handle == 0) return;
        var bytes = System.Text.Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* text = bytes)
        {
            var info = new VkDebugUtilsObjectNameInfoEXT { objectType = type, objectHandle = handle, pObjectName = text };
            _instanceApi.vkSetDebugUtilsObjectNameEXT(_device, &info);
        }
    }

    /// <summary>
    /// Opens a labeled region of <paramref name="commands"/>, which a capture tool such as RenderDoc
    /// shows by its name, where the instance has <c>VK_EXT_debug_utils</c>.
    /// </summary>
    public void BeginDebugLabel(ICommandBuffer commands, string name)
    {
        if (!_debugUtils || commands is not VulkanCommandBuffer vk) return;
        var bytes = System.Text.Encoding.UTF8.GetBytes(name + "\0");
        fixed (byte* text = bytes)
        {
            var label = new VkDebugUtilsLabelEXT { pLabelName = text };
            _instanceApi.vkCmdBeginDebugUtilsLabelEXT(vk.Handle, &label);
        }
    }

    /// <summary>Closes the region <see cref="BeginDebugLabel"/> opened last.</summary>
    public void EndDebugLabel(ICommandBuffer commands)
    {
        if (_debugUtils && commands is VulkanCommandBuffer vk) _instanceApi.vkCmdEndDebugUtilsLabelEXT(vk.Handle);
    }

    /// <summary>Thin wrapper around a native <c>VkCommandBuffer</c> handle.</summary>
    /// <seealso cref="ICommandBuffer"/>
    private sealed class VulkanCommandBuffer : ICommandBuffer
    {
        /// <summary>The underlying Vulkan command buffer handle.</summary>
        internal VkCommandBuffer Handle { get; }
        /// <summary>Creates a wrapper around the given Vulkan command buffer handle.</summary>
        public VulkanCommandBuffer(VkCommandBuffer handle) => Handle = handle;
    }

    /// <summary>Adapter that exposes the device's swapchain state through the <see cref="ISwapchain"/> interface.</summary>
    /// <seealso cref="ISwapchain"/>
    private sealed class VulkanSwapchain : ISwapchain
    {
        private readonly GraphicsDevice _owner;
        /// <summary>Creates a swapchain adapter for the given graphics device.</summary>
        public VulkanSwapchain(GraphicsDevice owner) => _owner = owner;

        /// <inheritdoc />
        public Extent2D Extent => new(_owner._swapchainExtent.width, _owner._swapchainExtent.height);
        /// <inheritdoc />
        public uint ImageCount => (uint)_owner._swapchainImages.Length;

        /// <inheritdoc />
        public AcquireResult AcquireNextImage(out uint imageIndex)
        {
            imageIndex = _owner._lastAcquiredImageIndex;
            return AcquireResult.Success;
        }

        /// <inheritdoc />
        public void Resize(Extent2D newExtent) => _owner.OnResize();
        /// <inheritdoc />
        public void Dispose() { }
    }

    /// <summary>How long the last frame waited for its slot's fence, the GPU finishing the frame that used it before.</summary>
    public double FenceWaitMs { get; private set; }

    /// <summary>How long the last frame waited to be given a swapchain image to draw into.</summary>
    public double AcquireMs { get; private set; }

    /// <summary>How long the last frame's present call took, which a present mode that waits for the display holds.</summary>
    public double PresentMs { get; private set; }

    // When the display last held a frame back a quarter second or more, and how many times in a
    // row it has, within ten seconds of each other.
    private long _lastLongWait;
    private int _longWaits;
    private bool _longWaitsWarned;

    // A desktop that holds frames back for most of a second at a time shows here and nowhere in
    // the engine's own work, so it is said once in the log, with what avoids it.
    private void NoteDisplayWait(double milliseconds)
    {
        if (milliseconds < 250 || _longWaitsWarned) return;
        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        _longWaits = System.Diagnostics.Stopwatch.GetElapsedTime(_lastLongWait, now).TotalSeconds < 10 ? _longWaits + 1 : 1;
        _lastLongWait = now;
        if (_longWaits < 3) return;
        _longWaitsWarned = true;
        Logger.Warn($"The display has held frames back three times in ten seconds, the last for {milliseconds:0} ms, in acquiring or presenting an image, " +
                    "which the engine's work does not account for. Under Wayland with NVIDIA's driver, SDL_VIDEO_DRIVER=x11 presents through XWayland " +
                    "without it, and an offscreen run (--offscreen) times a program with no display at all (BUILDING.md, Timing a program).");
    }
}
