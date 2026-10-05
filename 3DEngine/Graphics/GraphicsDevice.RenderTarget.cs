using Vortice.Vulkan;

namespace Engine;

/// <summary>
/// An image the renderer draws into instead of the window, with the depth buffer to draw with, and
/// views to sample the color and the depth through.
/// </summary>
/// <remarks>
/// <para>
/// The color image has the swapchain's format and the depth image the swapchain's depth format, at
/// the window's samples, so its pass is the window's and every pipeline built for one draws into
/// the other. The pass clears both images when it begins and leaves the color and the depth ready
/// to be sampled when it ends.
/// </para>
/// <para>
/// A multisampled depth image cannot be sampled as one texture, so the pass resolves it into a
/// single-sampled one by taking each pixel's first sample, as the color is resolved.
/// </para>
/// </remarks>
internal sealed class RenderTarget : IDisposable
{
    private readonly Action _dispose;

    internal RenderTarget(IRenderPass renderPass, IFramebuffer framebuffer, IImageView colorView, IImageView srgbColorView, IImageView? depthView,
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

    /// <summary>The pass that draws into the target, which the pipelines drawing here are made for.</summary>
    public IRenderPass RenderPass { get; }

    /// <summary>The color and depth images the pass draws into.</summary>
    public IFramebuffer Framebuffer { get; }

    /// <summary>The color image's view, for sampling the result.</summary>
    public IImageView ColorView { get; }

    /// <summary>The color image viewed as sRGB, which a pass lighting in linear space samples it through, or the color view for a target of another format.</summary>
    public IImageView SrgbColorView { get; }

    /// <summary>The depth image, single-sampled, for sampling the distances drawn, 0 at the near plane and 1 at the far one, or null for a target made with none.</summary>
    public IImageView? DepthView { get; }

    /// <summary>The target's size in pixels.</summary>
    public Extent2D Extent { get; }

    /// <inheritdoc />
    public void Dispose() => _dispose();
}

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>Creates a target of <paramref name="width"/> by <paramref name="height"/> pixels in the window's format, with depth, at the window's samples.</summary>
    /// <exception cref="InvalidOperationException">The device has not been initialized.</exception>
    public RenderTarget CreateRenderTarget(uint width, uint height) => CreateRenderTarget(width, height, ImageFormat.Undefined);

    /// <summary>
    /// Creates a target of <paramref name="width"/> by <paramref name="height"/> pixels in
    /// <paramref name="format"/>, or the window's for <see cref="ImageFormat.Undefined"/>, with a
    /// depth image unless <paramref name="depth"/> is false, at the window's samples unless
    /// <paramref name="multisampled"/> is false.
    /// </summary>
    /// <exception cref="InvalidOperationException">The device has not been initialized.</exception>
    /// <remarks>
    /// A half-float target holds light past 1, which the HDR frame draws its scene into, and one
    /// with no depth and one sample is a level of the bloom chain. Only a target in the window's
    /// format has an sRGB view and is a storage image.
    /// </remarks>
    public RenderTarget CreateRenderTarget(uint width, uint height, ImageFormat format, bool depth = true, bool multisampled = true)
    {
        if (!IsInitialized) throw new InvalidOperationException("Graphics device not initialized");
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        bool window = format == ImageFormat.Undefined;
        var vkFormat = window ? _swapchainFormat : ToVkFormat(format);
        var samples = multisampled ? _samples : VkSampleCountFlags.Count1;

        // Storage too where the device can store to the format, so a compute shader writes the
        // target as it writes a texture.
        var storage = window && TargetsAreStorage();
        var (color, colorMemory) = TargetImage(vkFormat, width, height,
            VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.Sampled | VkImageUsageFlags.TransferSrc | (storage ? VkImageUsageFlags.Storage : 0),
            window ? VkImageCreateFlags.MutableFormat : 0);
        var colorView = TargetView(color, vkFormat, VkImageAspectFlags.Color);
        var srgbView = window ? TargetView(color, SrgbOf(vkFormat), VkImageAspectFlags.Color, samplingOnly: storage) : colorView;
        // Drawn at the window's samples, into multisampled color and depth images resolved into
        // the ones sampled, so the window's pipelines draw here too. With one sample, the depth
        // drawn into is the one sampled.
        bool msaa = samples != VkSampleCountFlags.Count1;
        var (depthImage, depthMemory) = depth
            ? TargetImage(VkFormat.D32Sfloat, width, height, VkImageUsageFlags.DepthStencilAttachment | (msaa ? 0 : VkImageUsageFlags.Sampled), samples: samples)
            : default;
        var depthView = depth ? TargetView(depthImage, VkFormat.D32Sfloat, VkImageAspectFlags.Depth) : default;
        var (msaaColor, msaaMemory) = msaa
            ? TargetImage(vkFormat, width, height, VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransientAttachment, samples: samples)
            : default;
        var msaaView = msaa ? TargetView(msaaColor, vkFormat, VkImageAspectFlags.Color) : default;
        var (resolvedDepth, resolvedDepthMemory) = msaa && depth
            ? TargetImage(VkFormat.D32Sfloat, width, height, VkImageUsageFlags.DepthStencilAttachment | VkImageUsageFlags.Sampled)
            : default;
        var resolvedDepthView = msaa && depth ? TargetView(resolvedDepth, VkFormat.D32Sfloat, VkImageAspectFlags.Depth) : default;

        // The depth is stored with multisampling too, since NVIDIA's driver writes nothing to the
        // resolve of a depth that is not stored.
        var sampled = VkImageLayout.ShaderReadOnlyOptimal;
        var framebuffer = msaa
            ? new VulkanFramebuffer(
                new Attachment(msaaColor, msaaView, VkImageAspectFlags.Color, 0, VkImageLayout.ColorAttachmentOptimal, Store: false),
                new Attachment(color, colorView, VkImageAspectFlags.Color, 0, sampled),
                new Attachment(depthImage, depthView, VkImageAspectFlags.Depth, 0, VkImageLayout.DepthStencilAttachmentOptimal),
                new Attachment(resolvedDepth, resolvedDepthView, VkImageAspectFlags.Depth, 0, sampled))
            : new VulkanFramebuffer(
                new Attachment(color, colorView, VkImageAspectFlags.Color, 0, sampled), default,
                new Attachment(depthImage, depthView, VkImageAspectFlags.Depth, 0, sampled), default);

        var colorImage = new VulkanImage(this, color, colorMemory,
            new ImageDesc(new Extent2D(width, height), window ? ImageFormat.B8G8R8A8_UNorm : format,
                ImageUsage.ColorAttachment | ImageUsage.Sampled | (storage ? ImageUsage.Storage : 0)));
        // Ready to sample, and to write with a dispatch, before its first pass, which leaves it so too.
        TransitionImageLayout(colorImage, VkImageLayout.Undefined, VkImageLayout.ShaderReadOnlyOptimal, VkImageAspectFlags.Color);
        // The image the depth is sampled from, which owns its memory, and the one drawn into when
        // that is another.
        var sampledDepth = depth
            ? new VulkanImage(this, msaa ? resolvedDepth : depthImage, msaa ? resolvedDepthMemory : depthMemory,
                new ImageDesc(new Extent2D(width, height), ImageFormat.D32_Float, ImageUsage.DepthStencilAttachment | ImageUsage.Sampled))
            : null;

        return new RenderTarget(
            new VulkanRenderPass(vkFormat, depth ? VkFormat.D32Sfloat : VkFormat.Undefined, samples),
            framebuffer,
            new VulkanImageView(this, colorImage, colorView),
            new VulkanImageView(this, colorImage, srgbView),
            sampledDepth is null ? null : new VulkanImageView(this, sampledDepth, msaa ? resolvedDepthView : depthView),
            new Extent2D(width, height),
            () =>
            {
                if (window) _deviceApi.vkDestroyImageView(srgbView);
                if (depth) _deviceApi.vkDestroyImageView(depthView);
                if (msaa)
                {
                    _deviceApi.vkDestroyImageView(msaaView);
                    _deviceApi.vkDestroyImage(msaaColor);
                    _deviceApi.vkFreeMemory(msaaMemory);
                }
                if (msaa && depth)
                {
                    _deviceApi.vkDestroyImageView(resolvedDepthView);
                    _deviceApi.vkDestroyImage(depthImage);
                    _deviceApi.vkFreeMemory(depthMemory);
                }
                _deviceApi.vkDestroyImageView(colorView);
                colorImage.Dispose();
                sampledDepth?.Dispose();
            });
    }

    // The sRGB format of the same class, for a view that decodes a UNORM image when sampled.
    private static VkFormat SrgbOf(VkFormat format) => format switch
    {
        VkFormat.B8G8R8A8Unorm => VkFormat.B8G8R8A8Srgb,
        VkFormat.R8G8B8A8Unorm => VkFormat.R8G8B8A8Srgb,
        _ => format,
    };

    // Whether the window's format can be a storage image on this device, which most desktop GPUs
    // allow for eight-bit BGRA and some do not, asked once.
    private bool? _targetsAreStorage;

    private bool TargetsAreStorage()
    {
        if (_targetsAreStorage is { } known) return known;
        _instanceApi.vkGetPhysicalDeviceFormatProperties(_physicalDevice, _swapchainFormat, out var properties);
        return (_targetsAreStorage = CanWriteImages && (properties.optimalTilingFeatures & VkFormatFeatureFlags.StorageImage) != 0).Value;
    }

    private (VkImage Image, VkDeviceMemory Memory) TargetImage(VkFormat format, uint width, uint height, VkImageUsageFlags usage,
        VkImageCreateFlags flags = 0, VkSampleCountFlags samples = VkSampleCountFlags.Count1, uint layers = 1)
    {
        var info = new VkImageCreateInfo
        {
            flags = flags,
            imageType = VkImageType.Image2D,
            format = format,
            extent = new VkExtent3D(width, height, 1),
            mipLevels = 1,
            arrayLayers = layers,
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

    // A view of one layer from firstLayer, or with layers past one, of that many as an array.
    // With samplingOnly, the view is for sampling alone, as the sRGB view of a color image that is
    // a storage image too must be, since an sRGB format cannot be stored to.
    private VkImageView TargetView(VkImage image, VkFormat format, VkImageAspectFlags aspect, uint firstLayer = 0, uint layers = 1,
        bool samplingOnly = false)
    {
        var usage = new VkImageViewUsageCreateInfo { usage = VkImageUsageFlags.Sampled };
        var info = new VkImageViewCreateInfo
        {
            pNext = samplingOnly ? &usage : null,
            image = image,
            viewType = layers > 1 ? VkImageViewType.Image2DArray : VkImageViewType.Image2D,
            format = format,
            components = VkComponentMapping.Rgba,
            subresourceRange = new VkImageSubresourceRange(aspect, 0, 1, firstLayer, layers),
        };
        _deviceApi.vkCreateImageView(&info, null, out VkImageView view).CheckResult();
        return view;
    }
}
