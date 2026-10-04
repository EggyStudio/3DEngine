using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// An image the renderer draws into instead of the window, with the depth buffer and render pass
/// to draw with, and views to sample the color and the depth through.
/// </summary>
/// <remarks>
/// <para>
/// The color image has the swapchain's format and the depth image the swapchain's depth format, so
/// the render pass is compatible with the window's and every pipeline built for one draws into the
/// other. The pass clears both attachments when it begins and leaves the color and the depth ready
/// to be sampled when it ends.
/// </para>
/// <para>
/// A multisampled depth image cannot be sampled as one texture, so the pass resolves it into a
/// single-sampled one by taking each pixel's first sample, as the color is resolved. The resolve
/// is part of the subpass rather than an attachment the pipelines see, and Vulkan leaves resolve
/// attachments out of compatibility for a pass of one subpass, so the window's pipelines still
/// draw here.
/// </para>
/// </remarks>
public sealed class RenderTarget : IDisposable
{
    private readonly Action _dispose;

    internal RenderTarget(IRenderPass renderPass, IFramebuffer framebuffer, IImageView colorView, IImageView srgbColorView, IImageView depthView,
        Extent2D extent, Action dispose)
    {
        RenderPass = renderPass;
        Framebuffer = framebuffer;
        ColorView = colorView;
        SrgbColorView = srgbColorView;
        DepthView = depthView;
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

    /// <summary>The depth image, single-sampled, for sampling the distances drawn, 0 at the near plane and 1 at the far one.</summary>
    public IImageView DepthView { get; }

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
        // Drawn at the window's samples, into multisampled color and depth images resolved into
        // the ones sampled, so the window's pipelines draw here too. With one sample, the depth
        // drawn into is the one sampled.
        bool msaa = _samples != VkSampleCountFlags.Count1;
        var (depth, depthMemory) = TargetImage(VkFormat.D32Sfloat, width, height,
            VkImageUsageFlags.DepthStencilAttachment | (msaa ? 0 : VkImageUsageFlags.Sampled), samples: _samples);
        var depthView = TargetView(depth, VkFormat.D32Sfloat, VkImageAspectFlags.Depth);
        var (msaaColor, msaaMemory) = msaa
            ? TargetImage(_swapchainFormat, width, height, VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransientAttachment, samples: _samples)
            : default;
        var msaaView = msaa ? TargetView(msaaColor, _swapchainFormat, VkImageAspectFlags.Color) : default;
        var (resolvedDepth, resolvedDepthMemory) = msaa
            ? TargetImage(VkFormat.D32Sfloat, width, height, VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled)
            : default;
        var resolvedDepthView = msaa ? TargetView(resolvedDepth, VkFormat.D32Sfloat, VkImageAspectFlags.Depth) : default;

        var renderPass = CreateTargetPass(msaa);

        var views = stackalloc VkImageView[4];
        views[0] = msaa ? msaaView : colorView;
        views[1] = depthView;
        views[2] = colorView;
        views[3] = resolvedDepthView;
        var framebufferInfo = new VkFramebufferCreateInfo
        {
            renderPass = renderPass,
            attachmentCount = msaa ? 4u : 2u,
            pAttachments = views,
            width = width,
            height = height,
            layers = 1,
        };
        _deviceApi.vkCreateFramebuffer(&framebufferInfo, null, out VkFramebuffer framebuffer).CheckResult();

        var colorImage = new VulkanImage(this, color, colorMemory,
            new ImageDesc(new Extent2D(width, height), ImageFormat.B8G8R8A8_UNorm, ImageUsage.ColorAttachment | ImageUsage.Sampled));
        // The image the depth is sampled from, which owns its memory, and the one drawn into when
        // that is another.
        var sampledDepth = msaa ? resolvedDepth : depth;
        var depthImage = new VulkanImage(this, sampledDepth, msaa ? resolvedDepthMemory : depthMemory,
            new ImageDesc(new Extent2D(width, height), ImageFormat.D32_Float, ImageUsage.DepthStencilAttachment | ImageUsage.Sampled));

        return new RenderTarget(
            new VulkanRenderPass(renderPass, samples: _samples),
            new VulkanFramebuffer(framebuffer),
            new VulkanImageView(this, colorImage, colorView),
            new VulkanImageView(this, colorImage, srgbView),
            new VulkanImageView(this, depthImage, msaa ? resolvedDepthView : depthView),
            new Extent2D(width, height),
            () =>
            {
                _deviceApi.vkDestroyImageView(srgbView);
                _deviceApi.vkDestroyFramebuffer(framebuffer);
                _deviceApi.vkDestroyRenderPass(renderPass);
                _deviceApi.vkDestroyImageView(depthView);
                if (msaa)
                {
                    _deviceApi.vkDestroyImageView(msaaView);
                    _deviceApi.vkDestroyImage(msaaColor);
                    _deviceApi.vkFreeMemory(msaaMemory);
                    _deviceApi.vkDestroyImageView(resolvedDepthView);
                    _deviceApi.vkDestroyImage(depth);
                    _deviceApi.vkFreeMemory(depthMemory);
                }
                _deviceApi.vkDestroyImageView(colorView);
                colorImage.Dispose();
                depthImage.Dispose();
            });
    }

    /// <summary>
    /// The window's color and depth pass, cleared and left to be sampled, with the depth stored, and
    /// with multisampling the depth resolved beside the color.
    /// </summary>
    /// <remarks>
    /// Made with <c>vkCreateRenderPass2</c>, since a depth resolve exists only there, and with the
    /// attachments, the subpass and the dependencies of <see cref="CreateColorDepthPass"/> otherwise,
    /// which compatibility asks for.
    /// </remarks>
    private VkRenderPass CreateTargetPass(bool msaa)
    {
        var format = _swapchainFormat;
        var attachments = stackalloc VkAttachmentDescription2[4];
        attachments[0] = new VkAttachmentDescription2
        {
            format = format,
            samples = _samples,
            loadOp = VkAttachmentLoadOp.Clear,
            storeOp = VkAttachmentStoreOp.Store,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = VkImageLayout.Undefined,
            finalLayout = msaa ? VkImageLayout.ColorAttachmentOptimal : VkImageLayout.ShaderReadOnlyOptimal,
        };
        attachments[1] = new VkAttachmentDescription2
        {
            format = VkFormat.D32Sfloat,
            samples = _samples,
            loadOp = VkAttachmentLoadOp.Clear,
            // Stored with multisampling too, since NVIDIA's driver writes nothing to the depth
            // resolve of an attachment whose depth is not stored.
            storeOp = VkAttachmentStoreOp.Store,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = VkImageLayout.Undefined,
            finalLayout = msaa ? VkImageLayout.DepthStencilAttachmentOptimal : VkImageLayout.ShaderReadOnlyOptimal,
        };
        attachments[2] = new VkAttachmentDescription2
        {
            format = format,
            samples = VkSampleCountFlags.Count1,
            loadOp = VkAttachmentLoadOp.DontCare,
            storeOp = VkAttachmentStoreOp.Store,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = VkImageLayout.Undefined,
            finalLayout = VkImageLayout.ShaderReadOnlyOptimal,
        };
        attachments[3] = attachments[2] with { format = VkFormat.D32Sfloat };

        var colorRef = new VkAttachmentReference2 { attachment = 0, layout = VkImageLayout.ColorAttachmentOptimal, aspectMask = VkImageAspectFlags.Color };
        var depthRef = new VkAttachmentReference2 { attachment = 1, layout = VkImageLayout.DepthStencilAttachmentOptimal, aspectMask = VkImageAspectFlags.Depth };
        var resolveRef = new VkAttachmentReference2 { attachment = 2, layout = VkImageLayout.ColorAttachmentOptimal, aspectMask = VkImageAspectFlags.Color };
        var depthResolveRef = new VkAttachmentReference2 { attachment = 3, layout = VkImageLayout.DepthStencilAttachmentOptimal, aspectMask = VkImageAspectFlags.Depth };
        // The first sample is the one resolve every device supports for depth.
        var depthResolve = new VkSubpassDescriptionDepthStencilResolve
        {
            depthResolveMode = VkResolveModeFlags.SampleZero,
            stencilResolveMode = VkResolveModeFlags.None,
            pDepthStencilResolveAttachment = &depthResolveRef,
        };
        var subpass = new VkSubpassDescription2
        {
            pNext = msaa ? &depthResolve : null,
            pipelineBindPoint = VkPipelineBindPoint.Graphics,
            colorAttachmentCount = 1,
            pColorAttachments = &colorRef,
            pResolveAttachments = msaa ? &resolveRef : null,
            pDepthStencilAttachment = &depthRef,
        };

        var original = stackalloc VkSubpassDependency[2];
        ColorDepthDependencies(original);
        var dependencies = stackalloc VkSubpassDependency2[2];
        for (int i = 0; i < 2; i++)
        {
            dependencies[i] = new VkSubpassDependency2
            {
                srcSubpass = original[i].srcSubpass,
                dstSubpass = original[i].dstSubpass,
                srcStageMask = original[i].srcStageMask,
                dstStageMask = original[i].dstStageMask,
                srcAccessMask = original[i].srcAccessMask,
                dstAccessMask = original[i].dstAccessMask,
                dependencyFlags = original[i].dependencyFlags,
            };
        }

        var info = new VkRenderPassCreateInfo2
        {
            attachmentCount = msaa ? 4u : 2u,
            pAttachments = attachments,
            subpassCount = 1,
            pSubpasses = &subpass,
            dependencyCount = 2,
            pDependencies = dependencies,
        };
        _deviceApi.vkCreateRenderPass2(&info, null, out VkRenderPass pass).CheckResult();
        return pass;
    }

    // The sRGB format of the same class, for a view that decodes a UNORM image when sampled.
    private static VkFormat SrgbOf(VkFormat format) => format switch
    {
        VkFormat.B8G8R8A8Unorm => VkFormat.B8G8R8A8Srgb,
        VkFormat.R8G8B8A8Unorm => VkFormat.R8G8B8A8Srgb,
        _ => format,
    };

    private (VkImage Image, VkDeviceMemory Memory) TargetImage(VkFormat format, uint width, uint height, VkImageUsageFlags usage,
        VkImageCreateFlags flags = 0, VkSampleCountFlags samples = VkSampleCountFlags.Count1)
    {
        var info = new VkImageCreateInfo
        {
            flags = flags,
            imageType = VkImageType.Image2D,
            format = format,
            extent = new VkExtent3D(width, height, 1),
            mipLevels = 1,
            arrayLayers = 1,
            samples = samples,
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
