using SDL3;

namespace Engine;

/// <summary>
/// <see cref="ISurfaceSource"/> implementation backed by an SDL3 Vulkan window.
/// Provides Vulkan instance extensions, surface creation, and drawable size queries.
/// </summary>
/// <seealso cref="SdlWindow"/>
/// <seealso cref="ISurfaceSource"/>
internal sealed class SdlSurfaceSource : ISurfaceSource
{
    private readonly SdlWindow _sdl;

    /// <summary>Creates a surface source bound to the given SDL window.</summary>
    /// <param name="sdl">The SDL window wrapper to obtain Vulkan handles from.</param>
    public SdlSurfaceSource(SdlWindow sdl)
    {
        _sdl = sdl;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">SDL failed to enumerate Vulkan extensions.</exception>
    public IReadOnlyList<string> GetRequiredInstanceExtensions()
    {
        var names = SDL.VulkanGetInstanceExtensions(out var count);
        if (names is null || count == 0)
            throw new InvalidOperationException($"SDL.VulkanGetInstanceExtensions failed: {SDL.GetError()}");
        return names;
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">SDL Vulkan surface creation failed.</exception>
    public nint CreateSurfaceHandle(nint instanceHandle)
    {
        if (!SDL.VulkanCreateSurface(_sdl.Window, instanceHandle, IntPtr.Zero, out var surface))
            throw new InvalidOperationException($"SDL.VulkanCreateSurface failed: {SDL.GetError()}");
        return surface;
    }
    
    /// <inheritdoc />
    public (uint Width, uint Height) GetDrawableSize()
    {
        // Use pixel dimensions (not logical points) for the Vulkan swapchain
        if (SDL.GetWindowSizeInPixels(_sdl.Window, out int pxW, out int pxH) && pxW > 0 && pxH > 0)
        {
            return ((uint)pxW, (uint)pxH);
        }
        uint w = (uint)Math.Max(1, _sdl.Width);
        uint h = (uint)Math.Max(1, _sdl.Height);
        return (w, h);
    }
}