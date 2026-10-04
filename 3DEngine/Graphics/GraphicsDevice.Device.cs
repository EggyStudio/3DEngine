using System.Linq;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Whether a compute shader can write a texture, which needs images read and written with no format named.</summary>
    public bool CanWriteImages { get; private set; }

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

        // Drawing offscreen presents nothing, so it needs no swapchain extension.
        var extensionNames = _offscreen ? [] : DeviceExtensions;
        Logger.Debug($"Enabling device extensions: {string.Join(", ", extensionNames)}");
        using var deviceExts = new VkStringArray(extensionNames);

        // A compute shader's RWTexture2D carries no format of its own, as Slang writes it, which
        // these let it read and write, on every desktop driver that has them.
        _instanceApi.vkGetPhysicalDeviceFeatures(_physicalDevice, out var supported);
        CanWriteImages = supported.shaderStorageImageReadWithoutFormat && supported.shaderStorageImageWriteWithoutFormat;
        VkPhysicalDeviceFeatures features = new()
        {
            samplerAnisotropy = true,
            shaderStorageImageReadWithoutFormat = CanWriteImages,
            shaderStorageImageWriteWithoutFormat = CanWriteImages,
        };

        VkDeviceCreateInfo createInfo = new()
        {
            queueCreateInfoCount = (uint)families.Length,
            pQueueCreateInfos = queueInfos,
            enabledExtensionCount = deviceExts.Length,
            ppEnabledExtensionNames = deviceExts,
            pEnabledFeatures = &features
        };

        // No device layers. They have been ignored since Vulkan 1.0.13, the instance's layers cover
        // the device, and the array once named here was freed before vkCreateDevice read it, which
        // crashed every run that had the validation layer installed.

        Logger.Debug("Calling vkCreateDevice...");
        _instanceApi.vkCreateDevice(_physicalDevice, &createInfo, null, out _device).CheckResult();
        Logger.Debug($"VkDevice created (handle=0x{_device.Handle:X}).");

        // Made directly for the same reason as the instance's table: GetApi's cache outlives the device.
        _deviceApi = new VkDeviceApi(_instanceApi, _device);

        Logger.Debug("Retrieving graphics and present device queues...");
        _deviceApi.vkGetDeviceQueue(_graphicsQueueFamily, 0, out _graphicsQueue);
        _deviceApi.vkGetDeviceQueue(_presentQueueFamily, 0, out _presentQueue);
        Logger.Debug($"Queues retrieved, graphics=family {_graphicsQueueFamily}, present=family {_presentQueueFamily}");
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
