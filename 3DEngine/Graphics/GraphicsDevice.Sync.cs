using Vortice.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Creates semaphores and pre-signaled fences for each frame-in-flight.</summary>
    private partial void CreateSyncObjects()
    {
        Logger.Debug($"Creating synchronization objects for {MaxFramesInFlight} frames-in-flight...");
        _imageAvailableSemaphores = new VkSemaphore[MaxFramesInFlight];
        _inFlightFences = new VkFence[MaxFramesInFlight];

        VkFenceCreateInfo fenceInfo = new() { flags = VkFenceCreateFlags.Signaled };

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            _deviceApi.vkCreateSemaphore(out _imageAvailableSemaphores[i]).CheckResult();
            _deviceApi.vkCreateFence(&fenceInfo, null, out _inFlightFences[i]).CheckResult();
        }
        Logger.Debug($"Sync objects created: {MaxFramesInFlight} acquire semaphores + {MaxFramesInFlight} fences (pre-signaled).");
    }

    /// <summary>Destroys all synchronization fences and semaphores.</summary>
    private partial void DestroySyncObjects()
    {
        Logger.Debug("Destroying sync objects (fences and semaphores)...");
        foreach (var fence in _inFlightFences)
            if (fence.Handle != 0) _deviceApi.vkDestroyFence(fence);
        foreach (var sem in _imageAvailableSemaphores)
            if (sem.Handle != 0) _deviceApi.vkDestroySemaphore(sem);
        _inFlightFences = Array.Empty<VkFence>();
        _imageAvailableSemaphores = Array.Empty<VkSemaphore>();
        Logger.Debug("Sync objects destroyed.");
    }

    // A semaphore for each swapchain image, which the frame drawn into it signals and its present
    // waits on. One for each frame in flight could be signalled again while the presentation
    // engine still waited on it from another image's present, which the Vulkan specification
    // forbids, since nothing tells the program when a present has finished with its semaphore.
    private void CreatePresentSemaphores()
    {
        _renderFinishedSemaphores = new VkSemaphore[_swapchainImages.Length];
        for (int i = 0; i < _renderFinishedSemaphores.Length; i++)
            _deviceApi.vkCreateSemaphore(out _renderFinishedSemaphores[i]).CheckResult();
    }

    private void DestroyPresentSemaphores()
    {
        foreach (var sem in _renderFinishedSemaphores)
            if (sem.Handle != 0) _deviceApi.vkDestroySemaphore(sem);
        _renderFinishedSemaphores = Array.Empty<VkSemaphore>();
    }
}
