using Vortice.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Indices of the graphics and present queue families for the selected physical device.</summary>
    private struct QueueFamilyIndices
    {
        /// <summary>Index of the graphics queue family, or <see cref="uint.MaxValue"/> if not found.</summary>
        public uint Graphics;

        /// <summary>Index of the present queue family, or <see cref="uint.MaxValue"/> if not found.</summary>
        public uint Present;

        /// <summary>Returns <see langword="true"/> when both graphics and present queue families have been found.</summary>
        public bool IsComplete => Graphics != uint.MaxValue && Present != uint.MaxValue;
    }

    /// <summary>Enumerates physical devices, scores their capabilities, and selects the best GPU.</summary>
    private partial void SelectPhysicalDevice()
    {
        Logger.Debug("Enumerating Vulkan physical devices...");
        _instanceApi.vkEnumeratePhysicalDevices(out uint deviceCount).CheckResult();
        if (deviceCount == 0) throw new InvalidOperationException("No Vulkan physical devices.");
        Logger.Debug($"Found {deviceCount} physical device(s).");

        Span<VkPhysicalDevice> devices = stackalloc VkPhysicalDevice[(int)deviceCount];
        _instanceApi.vkEnumeratePhysicalDevices(devices).CheckResult();

        VkPhysicalDevice? best = null;
        var bestScore = int.MinValue;

        foreach (var device in devices)
        {
            var indices = FindQueueFamilies(device);
            if (!indices.IsComplete) continue;

            _instanceApi.vkGetPhysicalDeviceProperties(device, out var props);
            _instanceApi.vkGetPhysicalDeviceFeatures(device, out var features);

            var deviceName = Utf8(new ReadOnlySpan<byte>(props.deviceName, MaxPhysicalDeviceNameSize));
            Logger.Debug($"  Evaluating GPU: {deviceName} (type={props.deviceType}, vendorId=0x{props.vendorID:X4}, deviceId=0x{props.deviceID:X4})");

            if (!features.geometryShader)
            {
                Logger.Debug($"    Rejected - no geometry shader support.");
                continue;
            }

            var score = props.deviceType switch
            {
                VkPhysicalDeviceType.DiscreteGpu => 1000,
                VkPhysicalDeviceType.IntegratedGpu => 500,
                _ => 100
            };

            score += (int)props.limits.maxImageDimension2D;
            Logger.Debug($"    Score: {score} (maxImageDim2D={props.limits.maxImageDimension2D}, graphicsQueue={indices.Graphics}, presentQueue={indices.Present})");

            if (score > bestScore)
            {
                bestScore = score;
                best = device;
                _graphicsQueueFamily = indices.Graphics;
                _presentQueueFamily = indices.Present;
                _adapterInfo = new GraphicsAdapterInfo(
                    Utf8(new ReadOnlySpan<byte>(props.deviceName, MaxPhysicalDeviceNameSize)),
                    props.vendorID,
                    props.deviceID,
                    ToDeviceType(props.deviceType));
            }
        }

        if (best is null)
        {
            Logger.Error("Failed to find a suitable Vulkan GPU - no device passed all requirements.");
            throw new InvalidOperationException("Failed to find a suitable GPU for Vulkan.");
        }

        _physicalDevice = best.Value;
        Logger.Info($"Selected GPU: {_adapterInfo.Name} (score={bestScore})");
    }

    /// <summary>Finds the graphics and present queue family indices for the given physical device.</summary>
    /// <param name="device">The physical device to query.</param>
    /// <returns>A <see cref="QueueFamilyIndices"/> indicating the discovered families.</returns>
    private QueueFamilyIndices FindQueueFamilies(VkPhysicalDevice device)
    {
        var result = new QueueFamilyIndices { Graphics = uint.MaxValue, Present = uint.MaxValue };

        _instanceApi.vkGetPhysicalDeviceQueueFamilyProperties(device, out uint count);
        Span<VkQueueFamilyProperties> props = stackalloc VkQueueFamilyProperties[(int)count];
        _instanceApi.vkGetPhysicalDeviceQueueFamilyProperties(device, props);

        for (uint i = 0; i < count; i++)
        {
            if ((props[(int)i].queueFlags & VkQueueFlags.Graphics) != 0)
                result.Graphics = i;

            _instanceApi.vkGetPhysicalDeviceSurfaceSupportKHR(device, i, _surface, out VkBool32 supports);
            if (supports)
                result.Present = i;

            if (result.IsComplete) break;
        }

        return result;
    }

    /// <summary>Maps a <see cref="VkPhysicalDeviceType"/> to the engine's <see cref="GraphicsDeviceType"/>.</summary>
    private static GraphicsDeviceType ToDeviceType(VkPhysicalDeviceType type) => type switch
    {
        VkPhysicalDeviceType.DiscreteGpu => GraphicsDeviceType.DiscreteGpu,
        VkPhysicalDeviceType.IntegratedGpu => GraphicsDeviceType.IntegratedGpu,
        VkPhysicalDeviceType.VirtualGpu => GraphicsDeviceType.VirtualGpu,
        VkPhysicalDeviceType.Cpu => GraphicsDeviceType.Cpu,
        VkPhysicalDeviceType.Other => GraphicsDeviceType.Software,
        _ => GraphicsDeviceType.Unknown
    };
}
