using System.Linq;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Required Vulkan device extensions (currently just <c>VK_KHR_swapchain</c>).</summary>
    private static readonly string[] DeviceExtensions =
    {
        Utf8(VK_KHR_SWAPCHAIN_EXTENSION_NAME)
    };

    /// <summary>Creates the Vulkan logical device and retrieves the graphics and present queues.</summary>
    private partial void CreateLogicalDevice()
    {
        Logger.Debug("Setting up device queue create infos...");
        float priority = 1.0f;
        var families = UniqueQueueFamilies().ToArray();
        Logger.Debug($"Unique queue families: [{string.Join(", ", families)}]");

        VkDeviceQueueCreateInfo* queueInfos = stackalloc VkDeviceQueueCreateInfo[families.Length];
        for (int i = 0; i < families.Length; i++)
        {
            queueInfos[i] = new VkDeviceQueueCreateInfo
            {
                queueFamilyIndex = families[i],
                queueCount = 1,
                pQueuePriorities = &priority
            };
        }

        Logger.Debug($"Enabling device extensions: {string.Join(", ", DeviceExtensions)}");
        using var deviceExts = new VkStringArray(DeviceExtensions);

        VkPhysicalDeviceFeatures features = new()
        {
            samplerAnisotropy = true
        };

        VkDeviceCreateInfo createInfo = new()
        {
            queueCreateInfoCount = (uint)families.Length,
            pQueueCreateInfos = queueInfos,
            enabledExtensionCount = deviceExts.Length,
            ppEnabledExtensionNames = deviceExts,
            pEnabledFeatures = &features
        };

        if (_validationEnabled)
        {
            using var validation = new VkStringArray(ValidationLayers);
            createInfo.enabledLayerCount = validation.Length;
            createInfo.ppEnabledLayerNames = validation;
        }

        Logger.Debug("Calling vkCreateDevice...");
        _instanceApi.vkCreateDevice(_physicalDevice, &createInfo, null, out _device).CheckResult();
        Logger.Debug($"VkDevice created (handle=0x{_device.Handle:X}).");

        _deviceApi = GetApi(_instance, _device);

        Logger.Debug("Retrieving graphics and present device queues...");
        _deviceApi.vkGetDeviceQueue(_graphicsQueueFamily, 0, out _graphicsQueue);
        _deviceApi.vkGetDeviceQueue(_presentQueueFamily, 0, out _presentQueue);
        Logger.Debug($"Queues retrieved - graphics=family {_graphicsQueueFamily}, present=family {_presentQueueFamily}");
    }

    /// <summary>Destroys the Vulkan logical device.</summary>
    private partial void DestroyLogicalDevice()
    {
        if (_device.Handle != 0)
        {
            Logger.Debug("Destroying VkDevice...");
            _deviceApi.vkDestroyDevice();
            _device = default;
            Logger.Debug("VkDevice destroyed.");
        }
    }

    /// <summary>Yields the deduplicated graphics and present queue family indices.</summary>
    private IEnumerable<uint> UniqueQueueFamilies()
    {
        if (_graphicsQueueFamily == _presentQueueFamily)
        {
            yield return _graphicsQueueFamily;
        }
        else
        {
            yield return _graphicsQueueFamily;
            yield return _presentQueueFamily;
        }
    }
}
