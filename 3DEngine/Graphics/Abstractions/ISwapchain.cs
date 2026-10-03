namespace Engine;

/// <summary>Swapchain abstraction providing image acquisition, extent queries, and resize support.</summary>
/// <seealso cref="IGraphicsDevice"/>
public interface ISwapchain : IDisposable
{
    /// <summary>Current swapchain extent (width × height) in pixels.</summary>
    Extent2D Extent { get; }

    /// <summary>Number of images in the swapchain.</summary>
    uint ImageCount { get; }

    /// <summary>Acquires the next available swapchain image for rendering.</summary>
    /// <param name="imageIndex">When returning <see cref="AcquireResult.Success"/>, the index of the acquired image.</param>
    /// <returns>The result of the acquisition attempt.</returns>
    AcquireResult AcquireNextImage(out uint imageIndex);

    /// <summary>Resizes the swapchain to the specified extent.</summary>
    /// <param name="newExtent">The new target extent in pixels.</param>
    void Resize(Extent2D newExtent);
}
