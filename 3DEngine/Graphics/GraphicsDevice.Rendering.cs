using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// Passes through dynamic rendering. A pass is begun on the images it draws into, with no render
/// pass object or framebuffer made ahead, and moves those images into and out of their attachment
/// layouts by barriers of its own.
/// </summary>
internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>What a pass draws into, by format and samples, which is all a pipeline drawing in it is made for.</summary>
    /// <remarks>
    /// Compared by value, so two passes with the same formats share their pipelines and a cache
    /// keyed by the pass finds them whichever object it was given.
    /// </remarks>
    private sealed record VulkanRenderPass(VkFormat ColorFormat, VkFormat DepthFormat, VkSampleCountFlags Samples) : IRenderPass
    {
        /// <summary>Whether the pass has a depth attachment and no color one, as a shadow map's has.</summary>
        internal bool DepthOnly => ColorFormat == VkFormat.Undefined;
    }

    /// <summary>
    /// One image a pass draws or resolves into, through a view of one of its layers, with the
    /// layout it is left in when the pass ends and whether what was drawn is kept.
    /// </summary>
    private readonly record struct Attachment(VkImage Image, VkImageView View, VkImageAspectFlags Aspect, uint Layer,
        VkImageLayout Final, bool Store = true)
    {
        internal bool Exists => View.Handle != 0;

        internal VkImageLayout Drawn => Aspect == VkImageAspectFlags.Color
            ? VkImageLayout.ColorAttachmentOptimal
            : VkImageLayout.DepthStencilAttachmentOptimal;

        internal VkImageSubresourceRange Range => new(Aspect, 0, 1, Layer, 1);
    }

    /// <summary>
    /// The images of one pass. With multisampling the color and depth are the multisampled images
    /// and the resolves are the ones shown or sampled after.
    /// </summary>
    private sealed class VulkanFramebuffer(Attachment color, Attachment resolve, Attachment depth, Attachment depthResolve) : IFramebuffer
    {
        internal Attachment Color { get; } = color;
        internal Attachment Resolve { get; } = resolve;
        internal Attachment Depth { get; } = depth;
        internal Attachment DepthResolve { get; } = depthResolve;
    }

    // Every stage a pass's attachments are written or read at, before a pass and after it. A
    // resolve writes at the color output stage, the depth's included.
    private const VkPipelineStageFlags2 AttachmentStages = VkPipelineStageFlags2.ColorAttachmentOutput |
        VkPipelineStageFlags2.EarlyFragmentTests | VkPipelineStageFlags2.LateFragmentTests;

    private const VkAccessFlags2 AttachmentWrites = VkAccessFlags2.ColorAttachmentWrite | VkAccessFlags2.DepthStencilAttachmentWrite;

    /// <inheritdoc />
    /// <remarks>
    /// With <paramref name="clear"/> every attachment is cleared, the depth to 1, and without it each
    /// keeps what it holds, read from the layout the last pass left it in. Before drawing, the
    /// images wait on every earlier write to them and every earlier read by a shader or a copy, which
    /// covers the swapchain image's acquire and the last frame's sampling of a target.
    /// </remarks>
    public void CmdBeginRenderPass(ICommandBuffer commandBuffer, IRenderPass renderPass, IFramebuffer framebuffer,
        Extent2D extent, ClearColor? clear = null)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        if (framebuffer is not VulkanFramebuffer fb)
            throw new ArgumentException("Framebuffer must originate from this GraphicsDevice.", nameof(framebuffer));

        var toDrawn = stackalloc VkImageMemoryBarrier2[4];
        uint count = 0;
        foreach (var a in (ReadOnlySpan<Attachment>)[fb.Color, fb.Resolve, fb.Depth, fb.DepthResolve])
            if (a.Exists)
                toDrawn[count++] = ImageBarrier(a.Image, a.Range, clear is null ? a.Final : VkImageLayout.Undefined, a.Drawn,
                    AttachmentStages | VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader | VkPipelineStageFlags2.Transfer,
                    AttachmentWrites | VkAccessFlags2.ShaderWrite | VkAccessFlags2.TransferWrite,
                    AttachmentStages,
                    AttachmentWrites | VkAccessFlags2.ColorAttachmentRead | VkAccessFlags2.DepthStencilAttachmentRead);
        PipelineBarrier(vkCmd.Handle, new ReadOnlySpan<VkImageMemoryBarrier2>(toDrawn, (int)count));

        var load = clear is null ? VkAttachmentLoadOp.Load : VkAttachmentLoadOp.Clear;
        var c = clear ?? default;
        var color = new VkRenderingAttachmentInfo
        {
            imageView = fb.Color.View,
            imageLayout = VkImageLayout.ColorAttachmentOptimal,
            resolveMode = fb.Resolve.Exists ? VkResolveModeFlags.Average : VkResolveModeFlags.None,
            resolveImageView = fb.Resolve.View,
            resolveImageLayout = VkImageLayout.ColorAttachmentOptimal,
            loadOp = load,
            storeOp = fb.Color.Store ? VkAttachmentStoreOp.Store : VkAttachmentStoreOp.DontCare,
            clearValue = new VkClearValue(new VkClearColorValue(c.R, c.G, c.B, c.A)),
        };
        // The first sample is the one resolve every device supports for depth.
        var depth = new VkRenderingAttachmentInfo
        {
            imageView = fb.Depth.View,
            imageLayout = VkImageLayout.DepthStencilAttachmentOptimal,
            resolveMode = fb.DepthResolve.Exists ? VkResolveModeFlags.SampleZero : VkResolveModeFlags.None,
            resolveImageView = fb.DepthResolve.View,
            resolveImageLayout = VkImageLayout.DepthStencilAttachmentOptimal,
            loadOp = load,
            storeOp = fb.Depth.Store ? VkAttachmentStoreOp.Store : VkAttachmentStoreOp.DontCare,
            clearValue = new VkClearValue(new VkClearDepthStencilValue(1.0f, 0)),
        };
        var info = new VkRenderingInfo
        {
            renderArea = new VkRect2D(new VkOffset2D(0, 0), new VkExtent2D(extent.Width, extent.Height)),
            layerCount = 1,
            colorAttachmentCount = fb.Color.Exists ? 1u : 0u,
            pColorAttachments = fb.Color.Exists ? &color : null,
            pDepthAttachment = fb.Depth.Exists ? &depth : null,
        };
        _deviceApi.vkCmdBeginRendering(vkCmd.Handle, &info);
        _activeFramebuffer = fb;
    }

    // The framebuffer of the pass begun last, whose images the end of the pass moves on.
    private VulkanFramebuffer? _activeFramebuffer;

    /// <inheritdoc />
    /// <remarks>
    /// Each image moves to the layout it is left in, a target's and a shadow map's to be sampled by
    /// the fragment and compute shaders after, the window's to be shown or copied, once its writes
    /// and resolves have finished.
    /// </remarks>
    public void CmdEndRenderPass(ICommandBuffer commandBuffer)
    {
        if (commandBuffer is not VulkanCommandBuffer vkCmd)
            throw new ArgumentException("Command buffer was not created by this device.", nameof(commandBuffer));
        _deviceApi.vkCmdEndRendering(vkCmd.Handle);
        if (_activeFramebuffer is not { } fb) return;
        _activeFramebuffer = null;

        var toFinal = stackalloc VkImageMemoryBarrier2[4];
        uint count = 0;
        foreach (var a in (ReadOnlySpan<Attachment>)[fb.Color, fb.Resolve, fb.Depth, fb.DepthResolve])
            if (a.Exists && a.Final != a.Drawn)
            {
                var (stage, access) = a.Final switch
                {
                    VkImageLayout.ShaderReadOnlyOptimal => (VkPipelineStageFlags2.FragmentShader | VkPipelineStageFlags2.ComputeShader, VkAccessFlags2.ShaderRead),
                    VkImageLayout.TransferSrcOptimal => (VkPipelineStageFlags2.Transfer, VkAccessFlags2.TransferRead),
                    _ => (VkPipelineStageFlags2.None, VkAccessFlags2.None),
                };
                toFinal[count++] = ImageBarrier(a.Image, a.Range, a.Drawn, a.Final, AttachmentStages, AttachmentWrites, stage, access);
            }
        PipelineBarrier(vkCmd.Handle, new ReadOnlySpan<VkImageMemoryBarrier2>(toFinal, (int)count));
    }

    /// <summary>The window's pass, which every frame image is drawn in.</summary>
    private VulkanRenderPass WindowPass => new(_swapchainFormat, VkFormat.D32Sfloat, _samples);

    /// <summary>The attachments of each frame image, made with the images.</summary>
    private void CreateFramebuffers()
    {
        bool msaa = _samples != VkSampleCountFlags.Count1;
        var depth = new Attachment(_depthImage, _depthImageView, VkImageAspectFlags.Depth, 0, VkImageLayout.DepthStencilAttachmentOptimal, Store: false);
        _framebuffers = new VulkanFramebuffer[_swapchainImages.Length];
        for (int i = 0; i < _swapchainImages.Length; i++)
        {
            var image = new Attachment(_swapchainImages[i], _swapchainImageViews[i], VkImageAspectFlags.Color, 0, _finalLayout);
            // Multisampled, the frame image is the resolve of a color image every frame shares.
            _framebuffers[i] = msaa
                ? new VulkanFramebuffer(
                    new Attachment(_msaaColorImage, _msaaColorView, VkImageAspectFlags.Color, 0, VkImageLayout.ColorAttachmentOptimal, Store: false),
                    image, depth, default)
                : new VulkanFramebuffer(image, default, depth, default);
        }
    }
}
