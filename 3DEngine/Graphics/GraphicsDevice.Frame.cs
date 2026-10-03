using Vortice.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Acquires the next swapchain image and begins a command buffer. Render pass lifecycle is managed by graph nodes.</summary>
    /// <param name="clearColor">The clear color (stored for SwapchainTarget consumers).</param>
    /// <returns>A <see cref="VulkanFrameContext"/> encapsulating the in-flight frame state.</returns>
    private partial IFrameContext BeginFrameInternal(ClearColor clearColor)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Graphics device not initialized");

        _deviceApi.vkWaitForFences(_inFlightFences[_currentFrame], true, ulong.MaxValue).CheckResult();

        // After the fence signals, the GPU has finished reading staging buffers from this slot's
        // previous frame - safe to dispose them now.
        FlushDeferredStagingBuffers(_currentFrame);

        var result = _deviceApi.vkAcquireNextImageKHR(_swapchain, ulong.MaxValue,
            _imageAvailableSemaphores[_currentFrame], default, out uint imageIndex);

        if (result == VkResult.ErrorOutOfDateKHR)
        {
            Logger.Warn("Swapchain out-of-date during image acquisition - triggering resize and retry.");
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
        _deviceApi.vkEndCommandBuffer(ctx.CommandBufferHandle).CheckResult();

        var waitStage = VkPipelineStageFlags.ColorAttachmentOutput;
        VkSemaphore* waitSemaphores = stackalloc VkSemaphore[1];
        waitSemaphores[0] = _imageAvailableSemaphores[_currentFrame];
        VkCommandBuffer* commandBuffers = stackalloc VkCommandBuffer[1];
        commandBuffers[0] = ctx.CommandBufferHandle;
        VkSemaphore* signalSemaphores = stackalloc VkSemaphore[1];
        signalSemaphores[0] = _renderFinishedSemaphores[_currentFrame];

        VkSubmitInfo submitInfo = new()
        {
            waitSemaphoreCount = 1,
            pWaitSemaphores = waitSemaphores,
            pWaitDstStageMask = &waitStage,
            commandBufferCount = 1,
            pCommandBuffers = commandBuffers,
            signalSemaphoreCount = 1,
            pSignalSemaphores = signalSemaphores
        };

        _deviceApi.vkResetFences(_inFlightFences[_currentFrame]).CheckResult();
        _deviceApi.vkQueueSubmit(_graphicsQueue, 1, &submitInfo, _inFlightFences[_currentFrame]).CheckResult();

        VkSemaphore* presentWaitSemaphores = stackalloc VkSemaphore[1];
        presentWaitSemaphores[0] = _renderFinishedSemaphores[_currentFrame];
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

        var presentResult = _deviceApi.vkQueuePresentKHR(_presentQueue, &presentInfo);

        if (presentResult == VkResult.ErrorOutOfDateKHR)
        {
            // Swapchain is unusable - must rebuild now.
            Logger.Warn("Swapchain out-of-date during present - triggering resize.");
            OnResize();
        }
        else if (presentResult == VkResult.SuboptimalKHR)
        {
            // Swapchain still works but isn't ideal (e.g. mid-drag on Linux).
            // Let it ride - the next coalesced ResizeEvent or a future
            // ErrorOutOfDateKHR will trigger a rebuild at the right time.
            if (!_suboptimalLogged)
            {
                Logger.Debug("Swapchain suboptimal during present - deferring resize.");
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

    /// <summary>Thin wrapper around a native <c>VkCommandBuffer</c> handle.</summary>
    /// <seealso cref="ICommandBuffer"/>
    private sealed class VulkanCommandBuffer : ICommandBuffer
    {
        /// <summary>The underlying Vulkan command buffer handle.</summary>
        internal VkCommandBuffer Handle { get; }
        /// <summary>Creates a wrapper around the given Vulkan command buffer handle.</summary>
        public VulkanCommandBuffer(VkCommandBuffer handle) => Handle = handle;
    }

    /// <summary>Thin wrapper around a native <c>VkRenderPass</c> handle.</summary>
    /// <seealso cref="IRenderPass"/>
    private sealed class VulkanRenderPass : IRenderPass
    {
        /// <summary>The underlying Vulkan render pass handle.</summary>
        internal VkRenderPass Handle { get; }
        /// <summary>Creates a wrapper around the given Vulkan render pass handle.</summary>
        public VulkanRenderPass(VkRenderPass handle) => Handle = handle;
    }

    /// <summary>Thin wrapper around a native <c>VkFramebuffer</c> handle.</summary>
    /// <seealso cref="IFramebuffer"/>
    private sealed class VulkanFramebuffer : IFramebuffer
    {
        /// <summary>The underlying Vulkan framebuffer handle.</summary>
        internal VkFramebuffer Handle { get; }
        /// <summary>Creates a wrapper around the given Vulkan framebuffer handle.</summary>
        public VulkanFramebuffer(VkFramebuffer handle) => Handle = handle;
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
}
