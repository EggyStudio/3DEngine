using SDL3;
using Vortice.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>
    /// A window of its own beside the main one, as one of ImGui's viewports, with its surface and
    /// swapchain, which a frame draws into after the main window's pass and presents with it.
    /// </summary>
    /// <remarks>
    /// Its image is acquired as the frame records it, waited on by the frame's submit and presented
    /// by the same call that presents the main window, so the windows show the same frame. A window
    /// resized, or whose swapchain went out of date, is given a new one before its next acquire.
    /// </remarks>
    internal sealed class WindowSurface
    {
        internal nint Window { get; init; }
        internal VkSurfaceKHR Surface { get; init; }
        internal VkSwapchainKHR Swapchain;
        internal VkImage[] Images = [];
        internal VkImageView[] Views = [];
        internal IFramebuffer[] Framebuffers = [];
        internal VkFormat Format;
        internal VkExtent2D Extent;
        // An image acquired for each frame in flight, and one drawn for each image, which the
        // frame's submit waits on and signals and the present waits on.
        internal VkSemaphore[] Acquired = [];
        internal VkSemaphore[] Drawn = [];
        internal uint Image;
        internal bool Stale;
        // Whether its images can be copied from, which a capture needs, and the capture asked for.
        internal bool Copyable;
        internal Action<byte[], int, int>? CaptureRequest;

        /// <summary>The pass its image is drawn in, one sample of its format with no depth.</summary>
        public IRenderPass Pass => new VulkanRenderPass(Format, VkFormat.Undefined, VkSampleCountFlags.Count1);

        /// <summary>The attachments of the image acquired this frame.</summary>
        public IFramebuffer Framebuffer => Framebuffers[Image];

        /// <summary>Its swapchain's size in pixels.</summary>
        public Extent2D Size => new(Extent.width, Extent.height);
    }

    // The windows whose images this frame acquired, which its submit and present take in.
    private readonly List<WindowSurface> _presenting = [];

    /// <summary>Whether windows beside the main one can be drawn into, which they cannot with no main window to present beside.</summary>
    internal bool HasWindowSurfaces => IsInitialized && !_offscreen;

    /// <summary>Makes a surface and a swapchain for the SDL window <paramref name="window"/>.</summary>
    /// <exception cref="InvalidOperationException">The device draws offscreen, or SDL cannot make the surface, or the device cannot present to it.</exception>
    internal WindowSurface CreateWindowSurface(nint window)
    {
        if (!HasWindowSurfaces) throw new InvalidOperationException("A device drawing offscreen presents to no window.");
        if (!SDL.VulkanCreateSurface(window, (nint)_instance.Handle, IntPtr.Zero, out var handle))
            throw new InvalidOperationException($"SDL.VulkanCreateSurface failed: {SDL.GetError()}");
        var surface = new WindowSurface { Window = window, Surface = new VkSurfaceKHR((ulong)handle) };
        _instanceApi.vkGetPhysicalDeviceSurfaceSupportKHR(_physicalDevice, _presentQueueFamily, surface.Surface, out VkBool32 supported).CheckResult();
        if (!supported)
        {
            _instanceApi.vkDestroySurfaceKHR(surface.Surface);
            throw new InvalidOperationException("The device's present queue cannot present to the window.");
        }
        surface.Acquired = [.. Enumerable.Range(0, MaxFramesInFlight).Select(_ => NewSemaphore())];
        BuildWindowSwapchain(surface);
        return surface;
    }

    /// <summary>Marks a window's swapchain to be made again at its window's size before its next image.</summary>
    internal static void ResizeWindowSurface(WindowSurface surface) => surface.Stale = true;

    /// <summary>
    /// Acquires the window's next image for this frame, its swapchain made again first where it is
    /// stale, and takes it into the frame's submit and present.
    /// </summary>
    /// <returns>False where no image could be had, as from a window of no size, which this frame leaves undrawn.</returns>
    internal bool AcquireWindowImage(WindowSurface surface)
    {
        if (!HasWindowSurfaces || _presenting.Contains(surface)) return false;
        if (surface.Stale)
        {
            _deviceApi.vkDeviceWaitIdle().CheckResult();
            BuildWindowSwapchain(surface);
        }
        if (surface.Extent.width == 0 || surface.Extent.height == 0 || surface.Swapchain.Handle == 0) return false;

        var result = _deviceApi.vkAcquireNextImageKHR(surface.Swapchain, ulong.MaxValue, surface.Acquired[_currentFrame], default, out var image);
        if (result == VkResult.ErrorOutOfDateKHR)
        {
            surface.Stale = true;
            return false;
        }
        if (result != VkResult.SuboptimalKHR) result.CheckResult();
        surface.Image = image;
        _presenting.Add(surface);
        return true;
    }

    /// <summary>
    /// Asks for the window's next presented frame's pixels, as <see cref="RequestCapture"/> does for
    /// the main window's, four bytes a pixel in red, green, blue and alpha, rows from the top.
    /// </summary>
    /// <exception cref="NotSupportedException">The window's surface does not allow its images to be copied from.</exception>
    internal static void RequestWindowCapture(WindowSurface surface, Action<byte[], int, int> onCaptured)
    {
        if (!surface.Copyable) throw new NotSupportedException("This window's surface does not allow its images to be copied, so its frames cannot be captured.");
        surface.CaptureRequest = onCaptured;
    }

    // Records the copy of each window's image drawn this frame whose capture was asked for, while
    // the frame's command buffer is still open and the image is in the layout it is presented in.
    // Null where none was, which is every frame but one, so a frame allocates nothing for them.
    private List<(VulkanBuffer Buffer, Action<byte[], int, int> Callback, int Width, int Height, VkFormat Format)>? RecordWindowCaptures(VkCommandBuffer cmd)
    {
        List<(VulkanBuffer, Action<byte[], int, int>, int, int, VkFormat)>? captures = null;
        foreach (var surface in _presenting)
        {
            if (surface.CaptureRequest is not { } callback) continue;
            captures ??= [];
            surface.CaptureRequest = null;
            var buffer = (VulkanBuffer)CreateBuffer(new BufferDesc((ulong)(surface.Extent.width * surface.Extent.height * 4), BufferUsage.TransferDst, CpuAccessMode.Read));
            CopyOut(cmd, surface.Images[surface.Image], surface.Extent, VkImageLayout.PresentSrcKHR, buffer);
            captures.Add((buffer, callback, (int)surface.Extent.width, (int)surface.Extent.height, surface.Format));
        }
        return captures;
    }

    // Waits for the frame that carried the copies and hands each on in RGBA order.
    private void FinishWindowCaptures(List<(VulkanBuffer Buffer, Action<byte[], int, int> Callback, int Width, int Height, VkFormat Format)>? captures, VkFence fence)
    {
        if (captures is null) return;
        _deviceApi.vkWaitForFences(fence, true, ulong.MaxValue).CheckResult();
        foreach (var capture in captures)
        {
            try
            {
                var pixels = Map(capture.Buffer).ToArray();
                Unmap(capture.Buffer);
                ToRgba(pixels, capture.Format);
                capture.Callback(pixels, capture.Width, capture.Height);
            }
            finally
            {
                capture.Buffer.Dispose();
            }
        }
    }

    /// <summary>Destroys a window's swapchain and surface, once nothing in flight draws into them.</summary>
    internal void DestroyWindowSurface(WindowSurface surface)
    {
        _deviceApi.vkDeviceWaitIdle().CheckResult();
        _presenting.Remove(surface);
        DestroyWindowImages(surface);
        if (surface.Swapchain.Handle != 0) _deviceApi.vkDestroySwapchainKHR(surface.Swapchain);
        surface.Swapchain = default;
        foreach (var semaphore in surface.Acquired.Concat(surface.Drawn)) _deviceApi.vkDestroySemaphore(semaphore);
        (surface.Acquired, surface.Drawn) = ([], []);
        _instanceApi.vkDestroySurfaceKHR(surface.Surface);
    }

    // The window's swapchain at its window's size in pixels, made over the one it had, with a view,
    // the attachments and a semaphore for each image.
    private void BuildWindowSwapchain(WindowSurface surface)
    {
        surface.Stale = false;
        _instanceApi.vkGetPhysicalDeviceSurfaceCapabilitiesKHR(_physicalDevice, surface.Surface, out VkSurfaceCapabilitiesKHR capabilities).CheckResult();
        _instanceApi.vkGetPhysicalDeviceSurfaceFormatsKHR(_physicalDevice, surface.Surface, out uint formatCount).CheckResult();
        Span<VkSurfaceFormatKHR> formats = stackalloc VkSurfaceFormatKHR[(int)formatCount];
        _instanceApi.vkGetPhysicalDeviceSurfaceFormatsKHR(_physicalDevice, surface.Surface, formats).CheckResult();
        var format = ChooseSwapchainFormat(formats.ToArray());
        _instanceApi.vkGetPhysicalDeviceSurfacePresentModesKHR(_physicalDevice, surface.Surface, out uint modeCount).CheckResult();
        Span<VkPresentModeKHR> modes = stackalloc VkPresentModeKHR[(int)modeCount];
        _instanceApi.vkGetPhysicalDeviceSurfacePresentModesKHR(_physicalDevice, surface.Surface, modes).CheckResult();
        SDL.GetWindowSizeInPixels(surface.Window, out var width, out var height);
        var extent = ChooseSwapExtent(capabilities, (uint)Math.Max(0, width), (uint)Math.Max(0, height));

        surface.Copyable = (capabilities.supportedUsageFlags & VkImageUsageFlags.TransferSrc) != 0;
        var old = surface.Swapchain;
        DestroyWindowImages(surface);
        surface.Format = format.format;
        surface.Extent = extent;
        if (extent.width == 0 || extent.height == 0)
        {
            // A window of no size, as one minimized, has no swapchain until it has a size again.
            if (old.Handle != 0) _deviceApi.vkDestroySwapchainKHR(old);
            surface.Swapchain = default;
            return;
        }

        uint imageCount = capabilities.minImageCount + 1;
        if (capabilities.maxImageCount > 0 && imageCount > capabilities.maxImageCount) imageCount = capabilities.maxImageCount;
        VkSwapchainCreateInfoKHR createInfo = new()
        {
            surface = surface.Surface,
            minImageCount = imageCount,
            imageFormat = format.format,
            imageColorSpace = format.colorSpace,
            imageExtent = extent,
            imageArrayLayers = 1,
            imageUsage = VkImageUsageFlags.ColorAttachment | (surface.Copyable ? VkImageUsageFlags.TransferSrc : 0),
            imageSharingMode = VkSharingMode.Exclusive,
            preTransform = capabilities.currentTransform,
            compositeAlpha = ChooseCompositeAlpha(capabilities.supportedCompositeAlpha),
            // As the main window presents, since both are presented by one call, and one waiting for
            // the display where the other does not would hold the frame to the display's refresh.
            presentMode = ChoosePresentMode(modes.ToArray()),
            clipped = true,
            oldSwapchain = old,
        };
        var families = stackalloc uint[2];
        if (_graphicsQueueFamily != _presentQueueFamily)
        {
            families[0] = _graphicsQueueFamily;
            families[1] = _presentQueueFamily;
            createInfo.imageSharingMode = VkSharingMode.Concurrent;
            createInfo.queueFamilyIndexCount = 2;
            createInfo.pQueueFamilyIndices = families;
        }
        _deviceApi.vkCreateSwapchainKHR(&createInfo, null, out surface.Swapchain).CheckResult();
        if (old.Handle != 0) _deviceApi.vkDestroySwapchainKHR(old);

        _deviceApi.vkGetSwapchainImagesKHR(surface.Swapchain, out uint count).CheckResult();
        Span<VkImage> images = stackalloc VkImage[(int)count];
        _deviceApi.vkGetSwapchainImagesKHR(surface.Swapchain, images).CheckResult();
        surface.Images = images.ToArray();
        surface.Views = new VkImageView[count];
        surface.Framebuffers = new IFramebuffer[count];
        surface.Drawn = new VkSemaphore[count];
        for (int i = 0; i < count; i++)
        {
            VkImageViewCreateInfo viewInfo = new()
            {
                image = surface.Images[i],
                viewType = VkImageViewType.Image2D,
                format = surface.Format,
                components = VkComponentMapping.Rgba,
                subresourceRange = new VkImageSubresourceRange(VkImageAspectFlags.Color, 0, 1, 0, 1),
            };
            _deviceApi.vkCreateImageView(&viewInfo, null, out surface.Views[i]).CheckResult();
            surface.Framebuffers[i] = new VulkanFramebuffer(
                new Attachment(surface.Images[i], surface.Views[i], VkImageAspectFlags.Color, 0, VkImageLayout.PresentSrcKHR), default, default, default);
            surface.Drawn[i] = NewSemaphore();
        }
    }

    private void DestroyWindowImages(WindowSurface surface)
    {
        foreach (var view in surface.Views) _deviceApi.vkDestroyImageView(view);
        foreach (var semaphore in surface.Drawn) _deviceApi.vkDestroySemaphore(semaphore);
        (surface.Images, surface.Views, surface.Framebuffers, surface.Drawn) = ([], [], [], []);
    }

    private VkSemaphore NewSemaphore()
    {
        VkSemaphoreCreateInfo info = new();
        _deviceApi.vkCreateSemaphore(&info, null, out VkSemaphore semaphore).CheckResult();
        return semaphore;
    }
}
