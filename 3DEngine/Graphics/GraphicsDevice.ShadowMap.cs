using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// A depth image a light's view of the scene is drawn into, with a view and sampler to read it back
/// through.
/// </summary>
/// <remarks>
/// The pass has no color attachment. It clears the depth when it begins and leaves the image ready
/// to be sampled when it ends, and it waits for the shaders of earlier frames to finish reading it,
/// so one map serves every frame in flight.
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

    /// <summary>The depth-only pass that draws into the map, which the pipelines drawing here are made for.</summary>
    public IRenderPass RenderPass { get; }

    /// <summary>The depth image the pass draws into, its first layer when it has several.</summary>
    public IFramebuffer Framebuffer => Framebuffers[0];

    /// <summary>Each layer of the depth image, for a pass to draw into.</summary>
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

        var framebuffers = new IFramebuffer[layers];
        for (uint l = 0; l < layers; l++)
            framebuffers[l] = new VulkanFramebuffer(default, default,
                new Attachment(depth, layerViews[l], VkImageAspectFlags.Depth, l, VkImageLayout.ShaderReadOnlyOptimal), default);

        var depthImage = new VulkanImage(this, depth, depthMemory,
            new ImageDesc(new Extent2D(size, size), ImageFormat.D32_Float, ImageUsage.DepthStencilAttachment | ImageUsage.Sampled));
        var sampler = CreateSampler(new SamplerDesc(SamplerFilter.Nearest, SamplerFilter.Nearest,
            SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge, SamplerAddressMode.ClampToEdge));

        return new ShadowMap(
            new VulkanRenderPass(VkFormat.Undefined, VkFormat.D32Sfloat, VkSampleCountFlags.Count1),
            framebuffers,
            new VulkanImageView(this, depthImage, depthView),
            sampler,
            new Extent2D(size, size),
            () =>
            {
                sampler.Dispose();
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
        PipelineBarrier(cmd, ImageBarrier(image, new VkImageSubresourceRange(VkImageAspectFlags.Depth, 0, 1, 0, layers),
            VkImageLayout.Undefined, VkImageLayout.ShaderReadOnlyOptimal,
            VkPipelineStageFlags2.None, VkAccessFlags2.None, VkPipelineStageFlags2.FragmentShader, VkAccessFlags2.ShaderRead));
        EndSingleTimeCommands(cmd);
    }
}
