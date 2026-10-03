using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// An image the renderer draws into instead of the window, with the depth buffer and render pass
/// to draw with, and a view to sample the result through.
/// </summary>
/// <remarks>
/// The color image has the swapchain's format and the depth image the swapchain's depth format, so
/// the render pass is compatible with the window's and every pipeline built for one draws into the
/// other. The pass clears both attachments when it begins and leaves the color image ready to be
/// sampled when it ends.
/// </remarks>
public sealed class RenderTarget : IDisposable
{
    private readonly Action _dispose;

    internal RenderTarget(IRenderPass renderPass, IFramebuffer framebuffer, IImageView colorView, IImageView srgbColorView, Extent2D extent, Action dispose)
    {
        RenderPass = renderPass;
        Framebuffer = framebuffer;
        ColorView = colorView;
        SrgbColorView = srgbColorView;
        Extent = extent;
        _dispose = dispose;
    }

    /// <summary>The render pass that draws into the target.</summary>
    public IRenderPass RenderPass { get; }

    /// <summary>The framebuffer of the color and depth images.</summary>
    public IFramebuffer Framebuffer { get; }

    /// <summary>The color image's view, for sampling the result.</summary>
    public IImageView ColorView { get; }

    /// <summary>The color image viewed as sRGB, which a pass lighting in linear space samples it through.</summary>
    public IImageView SrgbColorView { get; }

    /// <summary>The target's size in pixels.</summary>
    public Extent2D Extent { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Creates a target of <paramref name="width"/> by <paramref name="height"/> pixels.</summary>
    /// <exception cref="InvalidOperationException">The device has not been initialized.</exception>
    public RenderTarget CreateRenderTarget(uint width, uint height)
    {
        if (!IsInitialized) throw new InvalidOperationException("Graphics device not initialized");
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        var (color, colorMemory) = TargetImage(_swapchainFormat, width, height,
            VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc, VkImageCreateFlags.MutableFormat);
        var colorView = TargetView(color, _swapchainFormat, VkImageAspectFlags.Color);
        var srgbView = TargetView(color, SrgbOf(_swapchainFormat), VkImageAspectFlags.Color);
        var (depth, depthMemory) = TargetImage(VkFormat.D32Sfloat, width, height, VkImageUsageFlags.DepthStencilAttachment);
        var depthView = TargetView(depth, VkFormat.D32Sfloat, VkImageAspectFlags.Depth);

        var attachments = stackalloc VkAttachmentDescription[2];
        attachments[0] = new VkAttachmentDescription
        {
            format = _swapchainFormat,
            samples = VkSampleCountFlags.Count1,
            loadOp = VkAttachmentLoadOp.Clear,
            storeOp = VkAttachmentStoreOp.Store,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = VkImageLayout.Undefined,
            finalLayout = VkImageLayout.ShaderReadOnlyOptimal,
        };
        attachments[1] = new VkAttachmentDescription
        {
            format = VkFormat.D32Sfloat,
            samples = VkSampleCountFlags.Count1,
            loadOp = VkAttachmentLoadOp.Clear,
            storeOp = VkAttachmentStoreOp.DontCare,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = VkImageLayout.Undefined,
            finalLayout = VkImageLayout.DepthStencilAttachmentOptimal,
        };

        var colorRef = new VkAttachmentReference { attachment = 0, layout = VkImageLayout.ColorAttachmentOptimal };
        var depthRef = new VkAttachmentReference { attachment = 1, layout = VkImageLayout.DepthStencilAttachmentOptimal };
        var subpass = new VkSubpassDescription
        {
            pipelineBindPoint = VkPipelineBindPoint.Graphics,
            colorAttachmentCount = 1,
            pColorAttachments = &colorRef,
            pDepthStencilAttachment = &depthRef,
        };

        // The window's pair, so the window's pipelines draw into the target (ColorDepthDependencies).
        var dependencies = stackalloc VkSubpassDependency[2];
        ColorDepthDependencies(dependencies);

        var passInfo = new VkRenderPassCreateInfo
        {
            attachmentCount = 2,
            pAttachments = attachments,
            subpassCount = 1,
            pSubpasses = &subpass,
            dependencyCount = 2,
            pDependencies = dependencies,
        };
        _deviceApi.vkCreateRenderPass(&passInfo, null, out VkRenderPass renderPass).CheckResult();

        var views = stackalloc VkImageView[2];
        views[0] = colorView;
        views[1] = depthView;
        var framebufferInfo = new VkFramebufferCreateInfo
        {
            renderPass = renderPass,
            attachmentCount = 2,
            pAttachments = views,
            width = width,
            height = height,
            layers = 1,
        };
        _deviceApi.vkCreateFramebuffer(&framebufferInfo, null, out VkFramebuffer framebuffer).CheckResult();

        var colorImage = new VulkanImage(this, color, colorMemory,
            new ImageDesc(new Extent2D(width, height), ImageFormat.B8G8R8A8_UNorm, ImageUsage.ColorAttachment | ImageUsage.Sampled));

        return new RenderTarget(
            new VulkanRenderPass(renderPass),
            new VulkanFramebuffer(framebuffer),
            new VulkanImageView(this, colorImage, colorView),
            new VulkanImageView(this, colorImage, srgbView),
            new Extent2D(width, height),
            () =>
            {
                _deviceApi.vkDestroyImageView(srgbView);
                _deviceApi.vkDestroyFramebuffer(framebuffer);
                _deviceApi.vkDestroyRenderPass(renderPass);
                _deviceApi.vkDestroyImageView(depthView);
                _deviceApi.vkDestroyImage(depth);
                _deviceApi.vkFreeMemory(depthMemory);
                _deviceApi.vkDestroyImageView(colorView);
                colorImage.Dispose();
            });
    }

    // The sRGB format of the same class, for a view that decodes a UNORM image when sampled.
    private static VkFormat SrgbOf(VkFormat format) => format switch
    {
        VkFormat.B8G8R8A8Unorm => VkFormat.B8G8R8A8Srgb,
        VkFormat.R8G8B8A8Unorm => VkFormat.R8G8B8A8Srgb,
        _ => format,
    };

    private (VkImage Image, VkDeviceMemory Memory) TargetImage(VkFormat format, uint width, uint height, VkImageUsageFlags usage,
        VkImageCreateFlags flags = 0)
    {
        var info = new VkImageCreateInfo
        {
            flags = flags,
            imageType = VkImageType.Image2D,
            format = format,
            extent = new VkExtent3D(width, height, 1),
            mipLevels = 1,
            arrayLayers = 1,
            samples = VkSampleCountFlags.Count1,
            tiling = VkImageTiling.Optimal,
            usage = usage,
            sharingMode = VkSharingMode.Exclusive,
            initialLayout = VkImageLayout.Undefined,
        };
        _deviceApi.vkCreateImage(&info, null, out VkImage image).CheckResult();
        _deviceApi.vkGetImageMemoryRequirements(image, out VkMemoryRequirements requirements);
        var allocation = new VkMemoryAllocateInfo
        {
            allocationSize = requirements.size,
            memoryTypeIndex = FindMemoryType(requirements.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal),
        };
        _deviceApi.vkAllocateMemory(&allocation, null, out VkDeviceMemory memory).CheckResult();
        _deviceApi.vkBindImageMemory(image, memory, 0).CheckResult();
        return (image, memory);
    }

    private VkImageView TargetView(VkImage image, VkFormat format, VkImageAspectFlags aspect)
    {
        var info = new VkImageViewCreateInfo
        {
            image = image,
            viewType = VkImageViewType.Image2D,
            format = format,
            components = VkComponentMapping.Rgba,
            subresourceRange = new VkImageSubresourceRange(aspect, 0, 1, 0, 1),
        };
        _deviceApi.vkCreateImageView(&info, null, out VkImageView view).CheckResult();
        return view;
    }
}
