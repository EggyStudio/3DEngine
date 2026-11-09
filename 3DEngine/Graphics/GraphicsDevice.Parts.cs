using Vortice.Vulkan;

namespace Engine;

internal sealed partial class GraphicsDevice
{
    // The parts of the device an area of the renderer keeps, its pipelines and its images, each
    // made the first time its area asks for it and let go with the device, after the frames in
    // flight have ended and before the descriptor resources and the memory blocks, so no area's
    // work outlives the device and the device names no area.
    private readonly Dictionary<Type, IDisposable> _parts = [];
    private readonly List<IDisposable> _partsMade = [];

    /// <summary>
    /// The part of type <typeparamref name="T"/> the device keeps for an area, made by
    /// <paramref name="make"/> the first time it is asked for and disposed with the device.
    /// </summary>
    internal T Part<T>(Func<GraphicsDevice, T> make) where T : class, IDisposable
    {
        if (_parts.TryGetValue(typeof(T), out var part)) return (T)part;
        var made = make(this);
        _parts[typeof(T)] = made;
        _partsMade.Add(made);
        return made;
    }

    /// <summary>The device's own Vulkan calls, through which a part records and makes its objects.</summary>
    internal VkDeviceApi Api => _deviceApi;

    /// <summary>
    /// The Vulkan set behind a descriptor set the device made, which a part writes its own
    /// bindings into, the set's class staying the device's with the pool it goes back to.
    /// </summary>
    internal static VkDescriptorSet SetHandle(IDescriptorSet set) => ((VulkanDescriptorSet)set).Handle;

    // Lets the parts go, the last made first, since a part may use one made before it.
    private void DisposeParts()
    {
        for (int i = _partsMade.Count - 1; i >= 0; i--) _partsMade[i].Dispose();
        _partsMade.Clear();
        _parts.Clear();
    }
}
