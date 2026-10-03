using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// Offscreen rendering support: render pass / framebuffer creation, single-use command buffers,
/// and image layout transition barriers for the Vulkan graphics device.
/// </summary>
public sealed unsafe partial class GraphicsDevice
{
    // -- Disposable wrappers for offscreen resources --

    /// <summary>Wraps a <c>VkRenderPass</c> created for offscreen rendering.</summary>
    private sealed class VulkanOffscreenRenderPass : IRenderPass, IDisposable
    {
        private readonly GraphicsDevice _device;
        internal VkRenderPass Handle;

        public VulkanOffscreenRenderPass(GraphicsDevice device, VkRenderPass handle)
        {
            _device = device;
            Handle = handle;
        }

        public void Dispose()
        {
            if (Handle.Handle != 0)
            {
                _device._deviceApi.vkDestroyRenderPass(Handle);
                Handle = default;
            }
        }
    }

    /// <summary>Wraps a <c>VkFramebuffer</c> created for offscreen rendering.</summary>
    private sealed class VulkanOffscreenFramebuffer : IFramebuffer, IDisposable
    {
        private readonly GraphicsDevice _device;
        internal VkFramebuffer Handle;

        public VulkanOffscreenFramebuffer(GraphicsDevice device, VkFramebuffer handle)
        {
            _device = device;
            Handle = handle;
        }

        public void Dispose()
        {
            if (Handle.Handle != 0)
            {
                _device._deviceApi.vkDestroyFramebuffer(Handle);
                Handle = default;
            }
        }
    }

    // -- IRenderPass / IFramebuffer creation --

    /// <inheritdoc />
    public IRenderPass CreateRenderPass(RenderPassDesc desc)
    {
        VkAttachmentDescription colorAttachment = new()
        {
            format = ToVkFormat(desc.ColorFormat),
            samples = VkSampleCountFlags.Count1,
            loadOp = desc.ClearOnLoad ? VkAttachmentLoadOp.Clear : VkAttachmentLoadOp.Load,
            storeOp = VkAttachmentStoreOp.Store,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            // When clearing, the contents are discarded so Undefined is safe and avoids
            // requiring the image to already be in ColorAttachmentOptimal layout.
            initialLayout = desc.ClearOnLoad ? VkImageLayout.Undefined : VkImageLayout.ColorAttachmentOptimal,
            finalLayout = VkImageLayout.ColorAttachmentOptimal
        };

        VkAttachmentReference colorRef = new()
        {
            attachment = 0,
            layout = VkImageLayout.ColorAttachmentOptimal
        };

        VkSubpassDescription subpass = new()
        {
            pipelineBindPoint = VkPipelineBindPoint.Graphics,
            colorAttachmentCount = 1,
            pColorAttachments = &colorRef
        };

        // Dependency to ensure previous fragment shader reads finish before we write
        VkSubpassDependency dependency = new()
        {
            srcSubpass = Vulkan.VK_SUBPASS_EXTERNAL,
            dstSubpass = 0,
            srcStageMask = VkPipelineStageFlags.FragmentShader,
            dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
            srcAccessMask = VkAccessFlags.ShaderRead,
            dstAccessMask = VkAccessFlags.ColorAttachmentWrite,
            dependencyFlags = VkDependencyFlags.ByRegion
        };

        VkRenderPassCreateInfo rpInfo = new()
        {
            attachmentCount = 1,
            pAttachments = &colorAttachment,
            subpassCount = 1,
            pSubpasses = &subpass,
            dependencyCount = 1,
            pDependencies = &dependency
        };

        _deviceApi.vkCreateRenderPass(&rpInfo, null, out VkRenderPass renderPass).CheckResult();
        return new VulkanOffscreenRenderPass(this, renderPass);
    }

    /// <inheritdoc />
    public IFramebuffer CreateFramebuffer(FramebufferDesc desc)
    {
        VkRenderPass rpHandle;
        if (desc.RenderPass is VulkanOffscreenRenderPass offscreenRp)
            rpHandle = offscreenRp.Handle;
        else if (desc.RenderPass is VulkanRenderPass swapRp)
            rpHandle = swapRp.Handle;
        else
            throw new ArgumentException("RenderPass must originate from this GraphicsDevice.", nameof(desc));

        if (desc.ColorAttachment is not VulkanImageView vkView)
            throw new ArgumentException("ColorAttachment must originate from this GraphicsDevice.", nameof(desc));

        VkImageView* attachments = stackalloc VkImageView[1];
        attachments[0] = vkView.View;

        VkFramebufferCreateInfo fbInfo = new()
        {
            renderPass = rpHandle,
            attachmentCount = 1,
            pAttachments = attachments,
            width = desc.Extent.Width,
            height = desc.Extent.Height,
            layers = 1
        };

        _deviceApi.vkCreateFramebuffer(&fbInfo, null, out VkFramebuffer framebuffer).CheckResult();
        return new VulkanOffscreenFramebuffer(this, framebuffer);
    }

    // -- Single-use command buffer (public wrappers) --

    /// <inheritdoc />
    public ICommandBuffer BeginCommands()
    {
        var cmd = BeginSingleTimeCommands();
        return new VulkanCommandBuffer(cmd);
    }

    /// <inheritdoc />
    public void SubmitAndWait(ICommandBuffer commandBuffer)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        EndSingleTimeCommands(vkCmd.Handle);
    }

    // -- In-command-buffer render pass begin/end --

    /// <inheritdoc />
    public void CmdBeginRenderPass(ICommandBuffer commandBuffer, IRenderPass renderPass, IFramebuffer framebuffer,
        Extent2D extent, ClearColor? clear = null)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));

        VkRenderPass rpHandle;
        if (renderPass is VulkanOffscreenRenderPass offscreenRp)
            rpHandle = offscreenRp.Handle;
        else if (renderPass is VulkanRenderPass swapRp)
            rpHandle = swapRp.Handle;
        else
            throw new ArgumentException("RenderPass must originate from this GraphicsDevice.", nameof(renderPass));

        VkFramebuffer fbHandle;
        if (framebuffer is VulkanOffscreenFramebuffer offscreenFb)
            fbHandle = offscreenFb.Handle;
        else if (framebuffer is VulkanFramebuffer swapFb)
            fbHandle = swapFb.Handle;
        else
            throw new ArgumentException("Framebuffer must originate from this GraphicsDevice.", nameof(framebuffer));

        var colorClear = clear.HasValue
            ? new VkClearValue(new VkClearColorValue(clear.Value.R, clear.Value.G, clear.Value.B, clear.Value.A))
            : new VkClearValue(new VkClearColorValue(0, 0, 0, 0));

        // Swapchain render passes include a depth attachment (2 attachments total).
        // Offscreen render passes have only 1 color attachment.
        bool hasDepth = renderPass is VulkanRenderPass;

        VkClearValue* clearValues = stackalloc VkClearValue[2];
        clearValues[0] = colorClear;
        clearValues[1] = new VkClearValue(new VkClearDepthStencilValue(1.0f, 0));

        VkRenderPassBeginInfo rpBegin = new()
        {
            renderPass = rpHandle,
            framebuffer = fbHandle,
            renderArea = new VkRect2D(new VkOffset2D(0, 0), new VkExtent2D(extent.Width, extent.Height)),
            clearValueCount = hasDepth ? 2u : 1u,
            pClearValues = clearValues
        };

        _deviceApi.vkCmdBeginRenderPass(vkCmd.Handle, &rpBegin, VkSubpassContents.Inline);
    }

    /// <inheritdoc />
    public void CmdEndRenderPass(ICommandBuffer commandBuffer)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        _deviceApi.vkCmdEndRenderPass(vkCmd.Handle);
    }

    // -- Pipeline barrier (image layout transition) --

    /// <inheritdoc />
    public void CmdPipelineBarrier(ICommandBuffer commandBuffer, IImage image, ImageLayout oldLayout, ImageLayout newLayout)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        if (image is not VulkanImage vkImage)
            throw new ArgumentException("Image was not created by this device.", nameof(image));

        var vkOld = ToVkImageLayout(oldLayout);
        var vkNew = ToVkImageLayout(newLayout);

        VkAccessFlags srcAccess = 0;
        VkAccessFlags dstAccess = 0;
        VkPipelineStageFlags srcStage = VkPipelineStageFlags.TopOfPipe;
        VkPipelineStageFlags dstStage = VkPipelineStageFlags.BottomOfPipe;

        // Source layout → access mask / stage
        switch (oldLayout)
        {
            case ImageLayout.Undefined:
                srcAccess = 0;
                srcStage = VkPipelineStageFlags.TopOfPipe;
                break;
            case ImageLayout.ColorAttachmentOptimal:
                srcAccess = VkAccessFlags.ColorAttachmentWrite;
                srcStage = VkPipelineStageFlags.ColorAttachmentOutput;
                break;
            case ImageLayout.ShaderReadOnlyOptimal:
                srcAccess = VkAccessFlags.ShaderRead;
                srcStage = VkPipelineStageFlags.FragmentShader;
                break;
            case ImageLayout.TransferDstOptimal:
                srcAccess = VkAccessFlags.TransferWrite;
                srcStage = VkPipelineStageFlags.Transfer;
                break;
        }

        // Destination layout → access mask / stage
        switch (newLayout)
        {
            case ImageLayout.ColorAttachmentOptimal:
                dstAccess = VkAccessFlags.ColorAttachmentRead | VkAccessFlags.ColorAttachmentWrite;
                dstStage = VkPipelineStageFlags.ColorAttachmentOutput;
                break;
            case ImageLayout.ShaderReadOnlyOptimal:
                dstAccess = VkAccessFlags.ShaderRead;
                dstStage = VkPipelineStageFlags.FragmentShader;
                break;
            case ImageLayout.TransferDstOptimal:
                dstAccess = VkAccessFlags.TransferWrite;
                dstStage = VkPipelineStageFlags.Transfer;
                break;
        }

        VkImageMemoryBarrier barrier = new()
        {
            oldLayout = vkOld,
            newLayout = vkNew,
            srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            image = vkImage.Image,
            subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, 1, 0, 1),
            srcAccessMask = srcAccess,
            dstAccessMask = dstAccess
        };

        _deviceApi.vkCmdPipelineBarrier(vkCmd.Handle, srcStage, dstStage,
            0, 0, null, 0, null, 1, &barrier);

        vkImage.Layout = vkNew;
    }

    /// <summary>Maps an engine <see cref="ImageLayout"/> to the Vulkan <c>VkImageLayout</c> equivalent.</summary>
    private static VkImageLayout ToVkImageLayout(ImageLayout layout) => layout switch
    {
        ImageLayout.Undefined => VkImageLayout.Undefined,
        ImageLayout.ColorAttachmentOptimal => VkImageLayout.ColorAttachmentOptimal,
        ImageLayout.ShaderReadOnlyOptimal => VkImageLayout.ShaderReadOnlyOptimal,
        ImageLayout.TransferDstOptimal => VkImageLayout.TransferDstOptimal,
        _ => VkImageLayout.Undefined
    };

    IRenderPass IGraphicsDevice.CreateRenderPass(RenderPassDesc desc) => CreateRenderPass(desc);
    IFramebuffer IGraphicsDevice.CreateFramebuffer(FramebufferDesc desc) => CreateFramebuffer(desc);
}
