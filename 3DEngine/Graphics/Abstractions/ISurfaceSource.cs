namespace Engine;

/// <summary>
/// Platform-specific surface provider for Vulkan instance and surface creation.
/// Implemented by window backends (e.g., SDL) to bridge the platform window into the Vulkan graphics pipeline.
/// </summary>
/// <seealso cref="IGraphicsDevice"/>
internal interface ISurfaceSource
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

    /// <summary>
    /// Whether there is no window at all, so the device draws into images of its own and
    /// presents nothing. False for every window.
    /// </summary>
    bool IsOffscreen => false;
}

/// <summary>
/// A surface with no window behind it, for rendering with no display. The device draws into
/// images of its own of this size, which a capture reads as it would a window's.
/// </summary>
/// <param name="width">The width drawn at.</param>
/// <param name="height">The height drawn at.</param>
internal sealed class OffscreenSurface(uint width, uint height) : ISurfaceSource
{
    /// <summary>
    /// The size of the images drawn into, which <c>window.size</c> changes as a window's is
    /// resized, and which is zero across while <c>window.minimize</c> has it hidden, when no frame
    /// is drawn.
    /// </summary>
    public (uint Width, uint Height) Size { get; set; } = (width, height);

    /// <inheritdoc />
    public IReadOnlyList<string> GetRequiredInstanceExtensions() => [];

    /// <inheritdoc />
    public nint CreateSurfaceHandle(nint instanceHandle) =>
        throw new NotSupportedException("An offscreen surface has no window to make a VkSurfaceKHR from.");

    /// <inheritdoc />
    public (uint Width, uint Height) GetDrawableSize() => (Math.Max(1, Size.Width), Math.Max(1, Size.Height));

    /// <inheritdoc />
    public bool IsOffscreen => true;
}
