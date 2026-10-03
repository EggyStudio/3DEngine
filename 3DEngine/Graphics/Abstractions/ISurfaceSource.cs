namespace Engine;

/// <summary>
/// Platform-specific surface provider for Vulkan instance and surface creation.
/// Implemented by window backends (e.g., SDL) to bridge the platform window into the Vulkan graphics pipeline.
/// </summary>
/// <seealso cref="IGraphicsDevice"/>
public interface ISurfaceSource
{
    /// <summary>Returns the Vulkan instance extensions required by the platform surface (e.g., <c>VK_KHR_surface</c>, <c>VK_KHR_xlib_surface</c>).</summary>
    /// <returns>A read-only list of extension name strings.</returns>
    IReadOnlyList<string> GetRequiredInstanceExtensions();

    /// <summary>Creates a Vulkan surface handle (<c>VkSurfaceKHR</c>) from the platform window.</summary>
    /// <param name="instanceHandle">The <c>VkInstance</c> handle as a native integer.</param>
    /// <returns>The created <c>VkSurfaceKHR</c> handle as a native integer.</returns>
    nint CreateSurfaceHandle(nint instanceHandle);

    /// <summary>Returns the current drawable size of the surface in pixels.</summary>
    /// <returns>A tuple of (Width, Height) in pixels.</returns>
    (uint Width, uint Height) GetDrawableSize();
}
