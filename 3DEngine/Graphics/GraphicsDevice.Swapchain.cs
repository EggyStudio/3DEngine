using System.Linq;
using Vortice.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Creates the swapchain, image views, depth buffer, render pass, framebuffers, and command pool.</summary>
    private partial void CreateSwapchainResources()
    {
        Logger.Debug("Querying drawable size from surface source...");
        var drawable = _surfaceSource!.GetDrawableSize();
        if (drawable.Width == 0 || drawable.Height == 0)
            drawable = (1, 1);
        Logger.Debug($"Drawable size: {drawable.Width}x{drawable.Height}");

        if (_offscreen)
        {
            CreateOffscreenImages((uint)drawable.Width, (uint)drawable.Height);
            CreateFrameResources();
            return;
        }

        Logger.Debug("Querying swapchain support (capabilities, formats, present modes)...");
        var support = QuerySwapchainSupport(_physicalDevice);
        var surfaceFormat = ChooseSwapchainFormat(support.Formats);
        _swapchainCopyable = (support.Capabilities.supportedUsageFlags & VkImageUsageFlags.TransferSrc) != 0;
        var presentMode = ChoosePresentMode(support.PresentModes);
        var extent = ChooseSwapExtent(support.Capabilities, (uint)drawable.Width, (uint)drawable.Height);
        Logger.Debug($"Chosen surface format: {surfaceFormat.format}, color space: {surfaceFormat.colorSpace}");
        Logger.Debug($"Chosen present mode: {presentMode}");
        Logger.Debug($"Chosen swap extent: {extent.width}x{extent.height}");

        _swapchainFormat = surfaceFormat.format;
        _swapchainExtent = extent;

        uint imageCount = support.Capabilities.minImageCount + 1;
        if (support.Capabilities.maxImageCount > 0 && imageCount > support.Capabilities.maxImageCount)
            imageCount = support.Capabilities.maxImageCount;

        VkSwapchainCreateInfoKHR createInfo = new()
        {
            surface = _surface,
            minImageCount = imageCount,
            imageFormat = surfaceFormat.format,
            imageColorSpace = surfaceFormat.colorSpace,
            imageExtent = extent,
            imageArrayLayers = 1,
            // Copyable where the surface allows it, so a frame can be captured (RequestCapture).
            imageUsage = VkImageUsageFlags.ColorAttachment
                         | (_swapchainCopyable ? VkImageUsageFlags.TransferSrc : 0),
            preTransform = support.Capabilities.currentTransform,
            compositeAlpha = VkCompositeAlphaFlagsKHR.Opaque,
            presentMode = presentMode,
            clipped = true,
            oldSwapchain = _swapchain
        };

        if (_graphicsQueueFamily != _presentQueueFamily)
        {
            var families = stackalloc uint[2];
            families[0] = _graphicsQueueFamily;
            families[1] = _presentQueueFamily;
            createInfo.imageSharingMode = VkSharingMode.Concurrent;
            createInfo.queueFamilyIndexCount = 2;
            createInfo.pQueueFamilyIndices = families;
        }
        else
        {
            createInfo.imageSharingMode = VkSharingMode.Exclusive;
        }

        Logger.Debug($"Creating VkSwapchainKHR with {imageCount} images, sharing mode={createInfo.imageSharingMode}...");
        _deviceApi.vkCreateSwapchainKHR(&createInfo, null, out _swapchain).CheckResult();
        Logger.Debug($"VkSwapchainKHR created (handle=0x{_swapchain.Handle:X}).");

        _deviceApi.vkGetSwapchainImagesKHR(_swapchain, out uint count).CheckResult();
        Span<VkImage> images = stackalloc VkImage[(int)count];
        _deviceApi.vkGetSwapchainImagesKHR(_swapchain, images).CheckResult();
        _swapchainImages = images.ToArray();
        Logger.Debug($"Retrieved {_swapchainImages.Length} swapchain images.");

        CreateFrameResources();
    }

    // What every frame image needs, from a swapchain or not: views, depth, the passes, the
    // framebuffers and the command buffers.
    private void CreateFrameResources()
    {
        Logger.Debug("Creating image views for swapchain images...");
        CreateImageViews();
        Logger.Debug("Creating depth buffer resources (D32_Sfloat)...");
        CreateDepthResources();
        Logger.Debug("Creating render pass (color + depth attachments)...");
        CreateRenderPass();
        Logger.Debug("Creating load render pass (loadOp=Load variant)...");
        CreateLoadRenderPass();
        Logger.Debug("Creating framebuffers (one per swapchain image)...");
        CreateFramebuffers();
        Logger.Debug("Creating command pool and allocating command buffers...");
        CreateCommandPoolAndBuffers();
        Logger.Debug("Swapchain resource creation complete.");
    }

    /// <summary>Destroys all swapchain-related resources including framebuffers, image views, depth buffer, render pass, and command pool.</summary>
    private partial void DestroySwapchainResources()
    {
        Logger.Debug($"Destroying swapchain resources - {_framebuffers.Length} framebuffers, {_swapchainImageViews.Length} image views...");
        foreach (var fb in _framebuffers)
            if (fb.Handle != 0) _deviceApi.vkDestroyFramebuffer(fb);
        foreach (var iv in _swapchainImageViews)
            if (iv.Handle != 0) _deviceApi.vkDestroyImageView(iv);
        if (_depthImageView.Handle != 0)
            _deviceApi.vkDestroyImageView(_depthImageView);
        if (_depthImage.Handle != 0)
            _deviceApi.vkDestroyImage(_depthImage);
        if (_depthImageMemory.Handle != 0)
            _deviceApi.vkFreeMemory(_depthImageMemory);
        if (_msaaColorView.Handle != 0)
            _deviceApi.vkDestroyImageView(_msaaColorView);
        if (_msaaColorImage.Handle != 0)
            _deviceApi.vkDestroyImage(_msaaColorImage);
        if (_msaaColorMemory.Handle != 0)
            _deviceApi.vkFreeMemory(_msaaColorMemory);
        if (_renderPass.Handle != 0)
            _deviceApi.vkDestroyRenderPass(_renderPass);
        if (_loadRenderPass.Handle != 0)
            _deviceApi.vkDestroyRenderPass(_loadRenderPass);
        if (_swapchain.Handle != 0)
            _deviceApi.vkDestroySwapchainKHR(_swapchain);
        if (_offscreen)
        {
            foreach (var image in _swapchainImages)
                if (image.Handle != 0) _deviceApi.vkDestroyImage(image);
            foreach (var memory in _offscreenMemory)
                if (memory.Handle != 0) _deviceApi.vkFreeMemory(memory);
            _offscreenMemory = [];
        }
        if (_commandPool.Handle != 0)
            _deviceApi.vkDestroyCommandPool(_commandPool);

        _framebuffers = Array.Empty<VkFramebuffer>();
        _swapchainImageViews = Array.Empty<VkImageView>();
        _swapchainImages = Array.Empty<VkImage>();
        _commandBuffers = Array.Empty<VkCommandBuffer>();
        _renderPass = default;
        _loadRenderPass = default;
        _swapchain = default;
        _commandPool = default;
        _depthImage = default;
        _depthImageMemory = default;
        _depthImageView = default;
        _msaaColorImage = default;
        _msaaColorMemory = default;
        _msaaColorView = default;
    }

    /// <summary>The samples a frame is drawn with: <see cref="RequestedSamples"/> rounded down to what the device can multisample color and depth at.</summary>
    private VkSampleCountFlags ChooseSamples(int requested)
    {
        _instanceApi.vkGetPhysicalDeviceProperties(_physicalDevice, out var props);
        var supported = props.limits.framebufferColorSampleCounts & props.limits.framebufferDepthSampleCounts;
        foreach (var count in new[] { 8, 4, 2 })
            if (requested >= count && (supported & (VkSampleCountFlags)count) != 0)
                return (VkSampleCountFlags)count;
        return VkSampleCountFlags.Count1;
    }

    /// <summary>
    /// Makes the frame images of a run with no window: one per frame in flight, in the format a
    /// swapchain usually offers, drawable and copyable, so the passes and captures treat them as
    /// a swapchain's.
    /// </summary>
    private void CreateOffscreenImages(uint width, uint height)
    {
        _swapchainFormat = VkFormat.B8G8R8A8Unorm;
        _swapchainExtent = new VkExtent2D(width, height);
        _swapchainCopyable = true;
        _swapchainImages = new VkImage[MaxFramesInFlight];
        _offscreenMemory = new VkDeviceMemory[MaxFramesInFlight];
        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            VkImageCreateInfo info = new()
            {
                imageType = VkImageType.Image2D,
                format = _swapchainFormat,
                extent = new VkExtent3D(width, height, 1),
                mipLevels = 1,
                arrayLayers = 1,
                samples = VkSampleCountFlags.Count1,
                tiling = VkImageTiling.Optimal,
                usage = VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransferSrc,
                sharingMode = VkSharingMode.Exclusive,
                initialLayout = VkImageLayout.Undefined,
            };
            _deviceApi.vkCreateImage(&info, null, out _swapchainImages[i]).CheckResult();
            _deviceApi.vkGetImageMemoryRequirements(_swapchainImages[i], out VkMemoryRequirements req);
            VkMemoryAllocateInfo alloc = new()
            {
                allocationSize = req.size,
                memoryTypeIndex = FindMemoryType(req.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal),
            };
            _deviceApi.vkAllocateMemory(&alloc, null, out _offscreenMemory[i]).CheckResult();
            _deviceApi.vkBindImageMemory(_swapchainImages[i], _offscreenMemory[i], 0).CheckResult();
        }
        Logger.Info($"Offscreen frames: {MaxFramesInFlight} images of {width}x{height}, no window.");
    }

    /// <summary>Queries surface capabilities, supported formats, and present modes for swapchain creation.</summary>
    /// <param name="device">The physical device to query.</param>
    private (VkSurfaceCapabilitiesKHR Capabilities, VkSurfaceFormatKHR[] Formats, VkPresentModeKHR[] PresentModes) QuerySwapchainSupport(VkPhysicalDevice device)
    {
        _instanceApi.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(device, _surface, out VkSurfaceCapabilitiesKHR capabilities).CheckResult();
        _instanceApi.vkGetPhysicalDeviceSurfaceFormatsKHR(device, _surface, out uint formatCount).CheckResult();
        Span<VkSurfaceFormatKHR> formats = stackalloc VkSurfaceFormatKHR[(int)formatCount];
        _instanceApi.vkGetPhysicalDeviceSurfaceFormatsKHR(device, _surface, formats).CheckResult();
        _instanceApi.vkGetPhysicalDeviceSurfacePresentModesKHR(device, _surface, out uint modeCount).CheckResult();
        Span<VkPresentModeKHR> modes = stackalloc VkPresentModeKHR[(int)modeCount];
        _instanceApi.vkGetPhysicalDeviceSurfacePresentModesKHR(device, _surface, modes).CheckResult();
        return (capabilities, formats.ToArray(), modes.ToArray());
    }

    /// <summary>Selects the preferred swapchain surface format, favoring <c>B8G8R8A8_UNORM</c>.</summary>
    private static VkSurfaceFormatKHR ChooseSwapchainFormat(VkSurfaceFormatKHR[] formats)
    {
        if (formats.Length == 1 && formats[0].format == VkFormat.Undefined)
            return formats[0] with { format = VkFormat.B8G8R8A8Unorm };

        foreach (var format in formats)
        {
            if (format.format == VkFormat.B8G8R8A8Unorm)
                return format;
        }

        return formats[0];
    }

    /// <summary>Selects the preferred present mode: Mailbox &gt; Immediate &gt; FIFO.</summary>
    private VkPresentModeKHR ChoosePresentMode(VkPresentModeKHR[] modes)
    {
        // FIFO waits for the display's refresh, and every device has it.
        if (Vsync) return VkPresentModeKHR.Fifo;
        if (modes.Contains(VkPresentModeKHR.Mailbox))
            return VkPresentModeKHR.Mailbox;
        if (modes.Contains(VkPresentModeKHR.Immediate))
            return VkPresentModeKHR.Immediate;
        return VkPresentModeKHR.Fifo;
    }

    /// <summary>Determines the swapchain extent, clamping to surface capabilities.</summary>
    private static VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR caps, uint width, uint height)
    {
        if (caps.currentExtent.width != uint.MaxValue)
            return caps.currentExtent;

        return new VkExtent2D
        {
            width = Math.Clamp(width, caps.minImageExtent.width, caps.maxImageExtent.width),
            height = Math.Clamp(height, caps.minImageExtent.height, caps.maxImageExtent.height)
        };
    }

    /// <summary>
    /// Creates the depth buffer (<c>D32_SFLOAT</c>), and with multisampling the color image the
    /// passes draw into before it is resolved into the frame image, both at the frame's samples.
    /// </summary>
    private void CreateDepthResources()
    {
        _samples = ChooseSamples(RequestedSamples);
        if (_samples != VkSampleCountFlags.Count1)
        {
            (_msaaColorImage, _msaaColorMemory) = TargetImage(_swapchainFormat, _swapchainExtent.width, _swapchainExtent.height,
                VkImageUsageFlags.ColorAttachment | VkImageUsageFlags.TransientAttachment, samples: _samples);
            _msaaColorView = TargetView(_msaaColorImage, _swapchainFormat, VkImageAspectFlags.Color);
        }

        // For now always use a 32-bit float depth buffer.
        VkFormat depthFormat = VkFormat.D32Sfloat;

        VkImageCreateInfo imageInfo = new()
        {
            imageType = VkImageType.Image2D,
            format = depthFormat,
            extent = new VkExtent3D(_swapchainExtent.width, _swapchainExtent.height, 1),
            mipLevels = 1,
            arrayLayers = 1,
            samples = _samples,
            tiling = VkImageTiling.Optimal,
            usage = VkImageUsageFlags.DepthStencilAttachment,
            sharingMode = VkSharingMode.Exclusive,
            initialLayout = VkImageLayout.Undefined
        };

        _deviceApi.vkCreateImage(&imageInfo, null, out _depthImage).CheckResult();
        _deviceApi.vkGetImageMemoryRequirements(_depthImage, out VkMemoryRequirements req);

        VkMemoryAllocateInfo allocInfo = new()
        {
            allocationSize = req.size,
            memoryTypeIndex = FindMemoryType(req.memoryTypeBits, VkMemoryPropertyFlags.DeviceLocal)
        };

        _deviceApi.vkAllocateMemory(&allocInfo, null, out _depthImageMemory).CheckResult();
        _deviceApi.vkBindImageMemory(_depthImage, _depthImageMemory, 0).CheckResult();

        VkImageViewCreateInfo viewInfo = new()
        {
            image = _depthImage,
            viewType = VkImageViewType.Image2D,
            format = depthFormat,
            subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Depth, 0, 1, 0, 1)
        };

        _deviceApi.vkCreateImageView(&viewInfo, null, out _depthImageView).CheckResult();
    }

    /// <summary>Creates a <c>VkImageView</c> for each swapchain image.</summary>
    private void CreateImageViews()
    {
        _swapchainImageViews = new VkImageView[_swapchainImages.Length];
        for (int i = 0; i < _swapchainImages.Length; i++)
        {
            VkImageViewCreateInfo viewInfo = new()
            {
                image = _swapchainImages[i],
                viewType = VkImageViewType.Image2D,
                format = _swapchainFormat,
                components = VkComponentMapping.Rgba,
                subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, 1, 0, 1)
            };
            _deviceApi.vkCreateImageView(&viewInfo, null, out _swapchainImageViews[i]).CheckResult();
        }
    }

    /// <summary>Creates the window's render pass, which clears its attachments.</summary>
    private void CreateRenderPass() =>
        _renderPass = CreateColorDepthPass(_swapchainFormat, _samples, load: false, VkImageLayout.Undefined, _finalLayout);

    /// <summary>
    /// Creates the color and depth pass every window and render target pass is, so a pipeline made
    /// for one draws in the others. With more than one sample the color and depth are multisampled
    /// images and the subpass resolves the color into a third attachment, the image that is shown
    /// or sampled, which is laid out from <paramref name="initialLayout"/> to <paramref name="finalLayout"/>.
    /// </summary>
    internal VkRenderPass CreateColorDepthPass(VkFormat format, VkSampleCountFlags samples, bool load, VkImageLayout initialLayout, VkImageLayout finalLayout)
    {
        bool msaa = samples != VkSampleCountFlags.Count1;
        var attachments = stackalloc VkAttachmentDescription[3];
        attachments[0] = new VkAttachmentDescription
        {
            format = format,
            samples = samples,
            loadOp = load ? VkAttachmentLoadOp.Load : VkAttachmentLoadOp.Clear,
            storeOp = VkAttachmentStoreOp.Store,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = msaa ? (load ? VkImageLayout.ColorAttachmentOptimal : VkImageLayout.Undefined) : initialLayout,
            finalLayout = msaa ? VkImageLayout.ColorAttachmentOptimal : finalLayout,
        };
        attachments[1] = new VkAttachmentDescription
        {
            format = VkFormat.D32Sfloat,
            samples = samples,
            loadOp = load ? VkAttachmentLoadOp.Load : VkAttachmentLoadOp.Clear,
            storeOp = VkAttachmentStoreOp.DontCare,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = load ? VkImageLayout.DepthStencilAttachmentOptimal : VkImageLayout.Undefined,
            finalLayout = VkImageLayout.DepthStencilAttachmentOptimal,
        };
        attachments[2] = new VkAttachmentDescription
        {
            format = format,
            samples = VkSampleCountFlags.Count1,
            loadOp = VkAttachmentLoadOp.DontCare,
            storeOp = VkAttachmentStoreOp.Store,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = initialLayout,
            finalLayout = finalLayout,
        };

        var colorRef = new VkAttachmentReference { attachment = 0, layout = VkImageLayout.ColorAttachmentOptimal };
        var depthRef = new VkAttachmentReference { attachment = 1, layout = VkImageLayout.DepthStencilAttachmentOptimal };
        var resolveRef = new VkAttachmentReference { attachment = 2, layout = VkImageLayout.ColorAttachmentOptimal };
        var subpass = new VkSubpassDescription
        {
            pipelineBindPoint = VkPipelineBindPoint.Graphics,
            colorAttachmentCount = 1,
            pColorAttachments = &colorRef,
            pResolveAttachments = msaa ? &resolveRef : null,
            pDepthStencilAttachment = &depthRef,
        };

        var dependencies = stackalloc VkSubpassDependency[2];
        ColorDepthDependencies(dependencies);

        var info = new VkRenderPassCreateInfo
        {
            attachmentCount = msaa ? 3u : 2u,
            pAttachments = attachments,
            subpassCount = 1,
            pSubpasses = &subpass,
            dependencyCount = 2,
            pDependencies = dependencies,
        };
        _deviceApi.vkCreateRenderPass(&info, null, out VkRenderPass pass).CheckResult();
        return pass;
    }

    /// <summary>
    /// Writes the two subpass dependencies every color and depth pass has, which are the window's,
    /// its load pass's and a render target's.
    /// </summary>
    /// <remarks>
    /// One pair for all three, because a pipeline made for one render pass is used inside the others,
    /// and Vulkan counts render passes compatible only when their dependencies are identical. Before
    /// the pass, earlier attachment writes and fragment shader reads of the images have finished, which
    /// covers the swapchain image's acquire and the last frame's sampling of a render target. After it,
    /// the color writes are visible to the fragment shaders of later passes that sample a target.
    /// </remarks>
    internal static void ColorDepthDependencies(VkSubpassDependency* dependencies)
    {
        dependencies[0] = new VkSubpassDependency
        {
            srcSubpass = Vulkan.VK_SUBPASS_EXTERNAL,
            dstSubpass = 0,
            srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput | VkPipelineStageFlags.EarlyFragmentTests |
                           VkPipelineStageFlags.LateFragmentTests | VkPipelineStageFlags.FragmentShader,
            dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput | VkPipelineStageFlags.EarlyFragmentTests,
            srcAccessMask = VkAccessFlags.ColorAttachmentWrite | VkAccessFlags.DepthStencilAttachmentWrite,
            dstAccessMask = VkAccessFlags.ColorAttachmentRead | VkAccessFlags.ColorAttachmentWrite |
                            VkAccessFlags.DepthStencilAttachmentRead | VkAccessFlags.DepthStencilAttachmentWrite,
        };
        dependencies[1] = new VkSubpassDependency
        {
            srcSubpass = 0,
            dstSubpass = Vulkan.VK_SUBPASS_EXTERNAL,
            srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput,
            dstStageMask = VkPipelineStageFlags.FragmentShader,
            srcAccessMask = VkAccessFlags.ColorAttachmentWrite,
            dstAccessMask = VkAccessFlags.ShaderRead,
        };
    }

    /// <summary>Creates a second render pass with <c>loadOp = Load</c> for subsequent passes that preserve existing content.</summary>
    /// <remarks>The color starts where the window's pass left it, which offscreen is TransferSrcOptimal.</remarks>
    private void CreateLoadRenderPass() =>
        _loadRenderPass = CreateColorDepthPass(_swapchainFormat, _samples, load: true, _finalLayout, _finalLayout);

    /// <summary>Creates one framebuffer per swapchain image, attaching color and depth views.</summary>
    private void CreateFramebuffers()
    {
        _framebuffers = new VkFramebuffer[_swapchainImageViews.Length];
        bool msaa = _samples != VkSampleCountFlags.Count1;
        VkImageView* attachments = stackalloc VkImageView[3];
        for (int i = 0; i < _swapchainImageViews.Length; i++)
        {
            // Multisampled, the frame image is the resolve target after the shared color and depth.
            attachments[0] = msaa ? _msaaColorView : _swapchainImageViews[i];
            attachments[1] = _depthImageView;
            attachments[2] = _swapchainImageViews[i];

            VkFramebufferCreateInfo framebufferInfo = new()
            {
                renderPass = _renderPass,
                attachmentCount = msaa ? 3u : 2u,
                pAttachments = attachments,
                width = _swapchainExtent.width,
                height = _swapchainExtent.height,
                layers = 1
            };

            _deviceApi.vkCreateFramebuffer(&framebufferInfo, null, out _framebuffers[i]).CheckResult();
        }
    }

    /// <summary>Creates the command pool and allocates one primary command buffer per frame-in-flight.</summary>
    private void CreateCommandPoolAndBuffers()
    {
        VkCommandPoolCreateInfo poolInfo = new()
        {
            flags = VkCommandPoolCreateFlags.ResetCommandBuffer,
            queueFamilyIndex = _graphicsQueueFamily
        };

        _deviceApi.vkCreateCommandPool(&poolInfo, null, out _commandPool).CheckResult();

        _commandBuffers = new VkCommandBuffer[MaxFramesInFlight];
        VkCommandBufferAllocateInfo allocInfo = new()
        {
            commandPool = _commandPool,
            level = VkCommandBufferLevel.Primary,
            commandBufferCount = (uint)_commandBuffers.Length
        };

        fixed (VkCommandBuffer* buffers = _commandBuffers)
        {
            _deviceApi.vkAllocateCommandBuffers(&allocInfo, buffers).CheckResult();
        }
    }
}
