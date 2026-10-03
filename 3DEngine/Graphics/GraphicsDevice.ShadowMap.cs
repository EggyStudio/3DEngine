using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// A depth image a light's view of the scene is drawn into, with the render pass to draw it with
/// and a view and sampler to read it back through.
/// </summary>
/// <remarks>
/// The pass has no color attachment. It clears the depth when it begins and leaves the image ready
/// to be sampled when it ends, and it waits for the fragment shaders of earlier frames to finish
/// reading it, so one map serves every frame in flight.
/// </remarks>
public sealed class ShadowMap : IDisposable
{
    private readonly Action _dispose;

    internal ShadowMap(IRenderPass renderPass, IFramebuffer framebuffer, IImageView depthView, ISampler sampler, Extent2D extent, Action dispose)
    {
        RenderPass = renderPass;
        Framebuffer = framebuffer;
        DepthView = depthView;
        Sampler = sampler;
        Extent = extent;
        _dispose = dispose;
    }

    /// <summary>The depth-only render pass that draws into the map.</summary>
    public IRenderPass RenderPass { get; }

    /// <summary>The framebuffer of the depth image.</summary>
    public IFramebuffer Framebuffer { get; }

    /// <summary>The depth image's view, for sampling the stored depths.</summary>
    public IImageView DepthView { get; }

    /// <summary>A nearest sampler that clamps to the edge, since depths are compared and not blended.</summary>
    public ISampler Sampler { get; }

    /// <summary>The map's size in texels.</summary>
    public Extent2D Extent { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Creates a square shadow map of <paramref name="size"/> texels on a side.</summary>
    /// <exception cref="InvalidOperationException">The device has not been initialized.</exception>
    public ShadowMap CreateShadowMap(uint size)
    {
        if (!IsInitialized) throw new InvalidOperationException("Graphics device not initialized");
        size = Math.Max(1, size);

        var (depth, depthMemory) = TargetImage(VkFormat.D32Sfloat, size, size,
            VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled);
        var depthView = TargetView(depth, VkFormat.D32Sfloat, VkImageAspectFlags.Depth);

        var attachment = new VkAttachmentDescription
        {
            format = VkFormat.D32Sfloat,
            samples = VkSampleCountFlags.Count1,
            loadOp = VkAttachmentLoadOp.Clear,
            storeOp = VkAttachmentStoreOp.Store,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = VkImageLayout.Undefined,
            finalLayout = VkImageLayout.ShaderReadOnlyOptimal,
        };
        var depthRef = new VkAttachmentReference { attachment = 0, layout = VkImageLayout.DepthStencilAttachmentOptimal };
        var subpass = new VkSubpassDescription
        {
            pipelineBindPoint = VkPipelineBindPoint.Graphics,
            pDepthStencilAttachment = &depthRef,
        };

        // Before the pass, every earlier read of the map by a fragment shader has finished, the
        // last frame's included, since they share the queue. After it, the depths are visible to
        // the fragment shaders of the passes that read them later in the frame.
        var dependencies = stackalloc VkSubpassDependency[2];
        dependencies[0] = new VkSubpassDependency
        {
            srcSubpass = Vulkan.VK_SUBPASS_EXTERNAL,
            dstSubpass = 0,
            srcStageMask = VkPipelineStageFlags.FragmentShader,
            dstStageMask = VkPipelineStageFlags.EarlyFragmentTests | VkPipelineStageFlags.LateFragmentTests,
            srcAccessMask = VkAccessFlags.ShaderRead,
            dstAccessMask = VkAccessFlags.DepthStencilAttachmentRead | VkAccessFlags.DepthStencilAttachmentWrite,
        };
        dependencies[1] = new VkSubpassDependency
        {
            srcSubpass = 0,
            dstSubpass = Vulkan.VK_SUBPASS_EXTERNAL,
            srcStageMask = VkPipelineStageFlags.LateFragmentTests,
            dstStageMask = VkPipelineStageFlags.FragmentShader,
            srcAccessMask = VkAccessFlags.DepthStencilAttachmentWrite,
            dstAccessMask = VkAccessFlags.ShaderRead,
        };

        var passInfo = new VkRenderPassCreateInfo
        {
            attachmentCount = 1,
            pAttachments = &attachment,
            subpassCount = 1,
            pSubpasses = &subpass,
            dependencyCount = 2,
            pDependencies = dependencies,
        };
        _deviceApi.vkCreateRenderPass(&passInfo, null, out VkRenderPass renderPass).CheckResult();

        var attachments = stackalloc VkImageView[1];
        attachments[0] = depthView;
        var framebufferInfo = new VkFramebufferCreateInfo
        {
            renderPass = renderPass,
            attachmentCount = 1,
            pAttachments = attachments,
            width = size,
            height = size,
            layers = 1,
        };
        _deviceApi.vkCreateFramebuffer(&framebufferInfo, null, out VkFramebuffer framebuffer).CheckResult();

        var depthImage = new VulkanImage(this, depth, depthMemory,
            new ImageDesc(new Extent2D(size, size), ImageFormat.D32_Float, ImageUsage.DepthStencilAttachment | ImageUsage.Sampled));
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        return new ShadowMap(
            new VulkanRenderPass(renderPass, depthOnly: true),
            new VulkanFramebuffer(framebuffer),
            new VulkanImageView(this, depthImage, depthView),
            sampler,
            new Extent2D(size, size),
            () =>
            {
                sampler.Dispose();
                _deviceApi.vkDestroyFramebuffer(framebuffer);
                _deviceApi.vkDestroyRenderPass(renderPass);
                _deviceApi.vkDestroyImageView(depthView);
                depthImage.Dispose();
            });
    }
}
