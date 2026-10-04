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

    internal ShadowMap(IRenderPass renderPass, IReadOnlyList<IFramebuffer> framebuffers, IImageView depthView, ISampler sampler, Extent2D extent, Action dispose)
    {
        RenderPass = renderPass;
        Framebuffers = framebuffers;
        DepthView = depthView;
        Sampler = sampler;
        Extent = extent;
        _dispose = dispose;
    }

    /// <summary>The depth-only render pass that draws into the map.</summary>
    public IRenderPass RenderPass { get; }

    /// <summary>The framebuffer of the depth image, its first layer's when it has several.</summary>
    public IFramebuffer Framebuffer => Framebuffers[0];

    /// <summary>A framebuffer for each layer of the depth image.</summary>
    public IReadOnlyList<IFramebuffer> Framebuffers { get; }

    /// <summary>The depth image's view, for sampling the stored depths, of every layer as an array when it has several.</summary>
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
    /// <remarks>
    /// With more than one of <paramref name="layers"/> the image is an array, drawn a layer at a
    /// time through <see cref="ShadowMap.Framebuffers"/> and sampled as one array, and every layer
    /// is ready to sample from the start, so one never drawn is still valid to bind.
    /// </remarks>
    public ShadowMap CreateShadowMap(uint size, uint layers = 1)
    {
        if (!IsInitialized) throw new InvalidOperationException("Graphics device not initialized");
        size = Math.Max(1, size);
        layers = Math.Max(1, layers);

        var (depth, depthMemory) = TargetImage(VkFormat.D32Sfloat, size, size,
            VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled, layers: layers);
        var depthView = TargetView(depth, VkFormat.D32Sfloat, VkImageAspectFlags.Depth, layers: layers);
        var layerViews = new VkImageView[layers];
        for (uint l = 0; l < layers; l++)
            layerViews[l] = layers == 1 ? depthView : TargetView(depth, VkFormat.D32Sfloat, VkImageAspectFlags.Depth, firstLayer: l);
        if (layers > 1) ToShaderRead(depth, layers);

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

        var framebuffers = new VkFramebuffer[layers];
        for (int l = 0; l < layers; l++)
        {
            var attachment0 = layerViews[l];
            var framebufferInfo = new VkFramebufferCreateInfo
            {
                renderPass = renderPass,
                attachmentCount = 1,
                pAttachments = &attachment0,
                width = size,
                height = size,
                layers = 1,
            };
            _deviceApi.vkCreateFramebuffer(&framebufferInfo, null, out framebuffers[l]).CheckResult();
        }

        var depthImage = new VulkanImage(this, depth, depthMemory,
            new ImageDesc(new Extent2D(size, size), ImageFormat.D32_Float, ImageUsage.DepthStencilAttachment | ImageUsage.Sampled));
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        return new ShadowMap(
            new VulkanRenderPass(renderPass, depthOnly: true),
            [.. framebuffers.Select(f => (IFramebuffer)new VulkanFramebuffer(f))],
            new VulkanImageView(this, depthImage, depthView),
            sampler,
            new Extent2D(size, size),
            () =>
            {
                sampler.Dispose();
                foreach (var framebuffer in framebuffers) _deviceApi.vkDestroyFramebuffer(framebuffer);
                _deviceApi.vkDestroyRenderPass(renderPass);
                _deviceApi.vkDestroyImageView(depthView);
                if (layers > 1)
                    foreach (var view in layerViews) _deviceApi.vkDestroyImageView(view);
                depthImage.Dispose();
            });
    }

    // Moves every layer of a new depth array to the layout it is sampled in, so a layer no pass has
    // drawn yet is still valid to bind.
    private void ToShaderRead(VkImage image, uint layers)
    {
        var cmd = BeginSingleTimeCommands();
        var barrier = new VkImageMemoryBarrier
        {
            oldLayout = VkImageLayout.Undefined,
            newLayout = VkImageLayout.ShaderReadOnlyOptimal,
            srcQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            dstQueueFamilyIndex = Vulkan.VK_QUEUE_FAMILY_IGNORED,
            image = image,
            subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Depth, 0, 1, 0, layers),
            dstAccessMask = VkAccessFlags.ShaderRead,
        };
        _deviceApi.vkCmdPipelineBarrier(cmd, VkPipelineStageFlags.TopOfPipe, VkPipelineStageFlags.FragmentShader, 0, 0, null, 0, null, 1, &barrier);
        EndSingleTimeCommands(cmd);
    }
}
