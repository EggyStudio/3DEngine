using Vortice.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Creates the <c>VkSurfaceKHR</c> from the platform surface source.</summary>
    private partial void CreateSurface()
    {
        Logger.Debug("Requesting surface handle from platform surface source...");
        var handle = _surfaceSource!.CreateSurfaceHandle((nint)_instance.Handle);
        _surface = new VkSurfaceKHR((ulong)handle);
        Logger.Debug($"VkSurfaceKHR created (handle=0x{_surface.Handle:X}).");
    }

    /// <summary>Destroys the <c>VkSurfaceKHR</c> if it was created.</summary>
    private partial void DestroySurface()
    {
        if (_surface.Handle != 0)
        {
            Logger.Debug("Destroying VkSurfaceKHR...");
            _instanceApi.vkDestroySurfaceKHR(_surface);
            _surface = default;
        }
    }
}
