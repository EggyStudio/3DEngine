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

        Logger.Debug("Querying swapchain support (capabilities, formats, present modes)...");
        var support = QuerySwapchainSupport(_physicalDevice);
        var surfaceFormat = ChooseSwapchainFormat(support.Formats);
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
            imageUsage = VkImageUsageFlags.ColorAttachment,
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
        if (_renderPass.Handle != 0)
            _deviceApi.vkDestroyRenderPass(_renderPass);
        if (_loadRenderPass.Handle != 0)
            _deviceApi.vkDestroyRenderPass(_loadRenderPass);
        if (_swapchain.Handle != 0)
            _deviceApi.vkDestroySwapchainKHR(_swapchain);
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
    private static VkPresentModeKHR ChoosePresentMode(VkPresentModeKHR[] modes)
    {
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

    /// <summary>Creates the depth buffer image, memory, and image view (<c>D32_SFLOAT</c>).</summary>
    private void CreateDepthResources()
    {
        // For now always use a 32-bit float depth buffer.
        VkFormat depthFormat = VkFormat.D32Sfloat;

        VkImageCreateInfo imageInfo = new()
        {
            imageType = VkImageType.Image2D,
            format = depthFormat,
            extent = new VkExtent3D(_swapchainExtent.width, _swapchainExtent.height, 1),
            mipLevels = 1,
            arrayLayers = 1,
            samples = VkSampleCountFlags.Count1,
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

    /// <summary>Creates the render pass with color and depth attachments.</summary>
    private void CreateRenderPass()
    {
        var colorAttachment = new VkAttachmentDescription
        {
            format = _swapchainFormat,
            samples = VkSampleCountFlags.Count1,
            loadOp = VkAttachmentLoadOp.Clear,
            storeOp = VkAttachmentStoreOp.Store,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = VkImageLayout.Undefined,
            finalLayout = VkImageLayout.PresentSrcKHR
        };

        var depthAttachment = new VkAttachmentDescription
        {
            format = VkFormat.D32Sfloat,
            samples = VkSampleCountFlags.Count1,
            loadOp = VkAttachmentLoadOp.Clear,
            storeOp = VkAttachmentStoreOp.DontCare,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = VkImageLayout.Undefined,
            finalLayout = VkImageLayout.DepthStencilAttachmentOptimal
        };

        VkAttachmentDescription* attachments = stackalloc VkAttachmentDescription[2];
        attachments[0] = colorAttachment;
        attachments[1] = depthAttachment;

        var colorAttachmentRef = new VkAttachmentReference { attachment = 0, layout = VkImageLayout.ColorAttachmentOptimal };
        var depthAttachmentRef = new VkAttachmentReference { attachment = 1, layout = VkImageLayout.DepthStencilAttachmentOptimal };

        var subpass = new VkSubpassDescription
        {
            pipelineBindPoint = VkPipelineBindPoint.Graphics,
            colorAttachmentCount = 1,
            pColorAttachments = &colorAttachmentRef,
            pDepthStencilAttachment = &depthAttachmentRef
        };

        var dependency = new VkSubpassDependency
        {
            srcSubpass = uint.MaxValue,
            dstSubpass = 0,
            srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput | VkPipelineStageFlags.EarlyFragmentTests,
            dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput | VkPipelineStageFlags.EarlyFragmentTests,
            srcAccessMask = 0,
            dstAccessMask = VkAccessFlags.ColorAttachmentWrite | VkAccessFlags.DepthStencilAttachmentWrite
        };

        VkRenderPassCreateInfo renderPassInfo = new()
        {
            attachmentCount = 2,
            pAttachments = attachments,
            subpassCount = 1,
            pSubpasses = &subpass,
            dependencyCount = 1,
            pDependencies = &dependency
        };

        _deviceApi.vkCreateRenderPass(&renderPassInfo, null, out _renderPass).CheckResult();
    }

    /// <summary>Creates a second render pass with <c>loadOp = Load</c> for subsequent passes that preserve existing content.</summary>
    private void CreateLoadRenderPass()
    {
        var colorAttachment = new VkAttachmentDescription
        {
            format = _swapchainFormat,
            samples = VkSampleCountFlags.Count1,
            loadOp = VkAttachmentLoadOp.Load,
            storeOp = VkAttachmentStoreOp.Store,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = VkImageLayout.PresentSrcKHR,
            finalLayout = VkImageLayout.PresentSrcKHR
        };

        var depthAttachment = new VkAttachmentDescription
        {
            format = VkFormat.D32Sfloat,
            samples = VkSampleCountFlags.Count1,
            loadOp = VkAttachmentLoadOp.Load,
            storeOp = VkAttachmentStoreOp.DontCare,
            stencilLoadOp = VkAttachmentLoadOp.DontCare,
            stencilStoreOp = VkAttachmentStoreOp.DontCare,
            initialLayout = VkImageLayout.DepthStencilAttachmentOptimal,
            finalLayout = VkImageLayout.DepthStencilAttachmentOptimal
        };

        VkAttachmentDescription* attachments = stackalloc VkAttachmentDescription[2];
        attachments[0] = colorAttachment;
        attachments[1] = depthAttachment;

        var colorAttachmentRef = new VkAttachmentReference { attachment = 0, layout = VkImageLayout.ColorAttachmentOptimal };
        var depthAttachmentRef = new VkAttachmentReference { attachment = 1, layout = VkImageLayout.DepthStencilAttachmentOptimal };

        var subpass = new VkSubpassDescription
        {
            pipelineBindPoint = VkPipelineBindPoint.Graphics,
            colorAttachmentCount = 1,
            pColorAttachments = &colorAttachmentRef,
            pDepthStencilAttachment = &depthAttachmentRef
        };

        var dependency = new VkSubpassDependency
        {
            srcSubpass = uint.MaxValue,
            dstSubpass = 0,
            srcStageMask = VkPipelineStageFlags.ColorAttachmentOutput | VkPipelineStageFlags.EarlyFragmentTests,
            dstStageMask = VkPipelineStageFlags.ColorAttachmentOutput | VkPipelineStageFlags.EarlyFragmentTests,
            srcAccessMask = VkAccessFlags.ColorAttachmentWrite,
            dstAccessMask = VkAccessFlags.ColorAttachmentRead | VkAccessFlags.ColorAttachmentWrite | VkAccessFlags.DepthStencilAttachmentWrite
        };

        VkRenderPassCreateInfo renderPassInfo = new()
        {
            attachmentCount = 2,
            pAttachments = attachments,
            subpassCount = 1,
            pSubpasses = &subpass,
            dependencyCount = 1,
            pDependencies = &dependency
        };

        _deviceApi.vkCreateRenderPass(&renderPassInfo, null, out _loadRenderPass).CheckResult();
    }

    /// <summary>Creates one framebuffer per swapchain image, attaching color and depth views.</summary>
    private void CreateFramebuffers()
    {
        _framebuffers = new VkFramebuffer[_swapchainImageViews.Length];
        VkImageView* attachments = stackalloc VkImageView[2];
        for (int i = 0; i < _swapchainImageViews.Length; i++)
        {
            attachments[0] = _swapchainImageViews[i];
            attachments[1] = _depthImageView;

            VkFramebufferCreateInfo framebufferInfo = new()
            {
                renderPass = _renderPass,
                attachmentCount = 2,
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
