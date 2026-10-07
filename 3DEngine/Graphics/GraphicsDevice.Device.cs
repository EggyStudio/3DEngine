using System.Linq;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Engine;

internal sealed unsafe partial class GraphicsDevice
{
    /// <summary>Whether a compute shader can write a texture, which needs images read and written with no format named.</summary>
    public bool CanWriteImages { get; private set; }

    /// <summary>Whether a pipeline can draw triangles as points at their corners, which needs the device's fillModeNonSolid.</summary>
    public bool CanDrawPoints { get; private set; }

    /// <summary>
    /// Whether lines are drawn by the diamond rule OpenGL draws raylib's lines by, which needs a line
    /// rasterization extension with its bresenhamLines.
    /// </summary>
    public bool CanDrawBresenhamLines { get; private set; }

    /// <summary>Whether a sampler can mirror a texture once and clamp past it, Vulkan 1.2's feature for it being on, which raylib's <c>TEXTURE_WRAP_MIRROR_CLAMP</c> needs.</summary>
    public bool CanMirrorClamp { get; private set; }

    /// <summary>
    /// Whether a shader traces rays through acceleration structures of the scene's triangles
    /// (<c>VK_KHR_ray_query</c>), which the device turns on where the driver has it.
    /// </summary>
    public bool CanQueryRays { get; private set; }

    /// <summary>
    /// Whether a pipeline can blend and mask each color attachment of its own, which needs the
    /// device's independentBlend, so a target of several images keeps those past a shader's outputs.
    /// </summary>
    public bool CanBlendEachAttachment { get; private set; }

    /// <summary>Required Vulkan device extensions (currently just <c>VK_KHR_swapchain</c>).</summary>
    private static readonly string[] DeviceExtensions =
    {
        Utf8(VK_KHR_SWAPCHAIN_EXTENSION_NAME)
    };

    private bool HasDeviceExtension(string name)
    {
        _instanceApi.vkEnumerateDeviceExtensionProperties(_physicalDevice, out uint count).CheckResult();
        if (count == 0) return false;
        var properties = new VkExtensionProperties[(int)count];
        _instanceApi.vkEnumerateDeviceExtensionProperties(_physicalDevice, properties).CheckResult();
        for (int i = 0; i < (int)count; i++)
            fixed (byte* namePtr = properties[i].extensionName)
                if (System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)namePtr) == name) return true;
        return false;
    }

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

        // Drawing offscreen presents nothing, so it needs no swapchain extension. A device of the
        // portability subset, as MoltenVK is, must have the subset enabled to be used at all.
        var extensionNames = (_offscreen ? [] : DeviceExtensions).ToList();
        var subset = Utf8(VK_KHR_PORTABILITY_SUBSET_EXTENSION_NAME);
        if (HasDeviceExtension(subset)) extensionNames.Add(subset);
        // A model drawn as points writes no point size, which maintenance5 makes one pixel, as
        // rlgl's points are, where the driver has it. Without it the size is left to the driver.
        var maintenance5 = Utf8(VK_KHR_MAINTENANCE_5_EXTENSION_NAME);
        var hasMaintenance5 = HasDeviceExtension(maintenance5);
        if (hasMaintenance5) extensionNames.Add(maintenance5);
        // A line takes the pixels OpenGL's would, raylib's being drawn by the rule its diamonds
        // give, where the driver has the extension, which every desktop driver in use and lavapipe
        // do. Without it a line is the driver's own.
        var lineRasterization = new[] { Utf8(VK_KHR_LINE_RASTERIZATION_EXTENSION_NAME), Utf8(VK_EXT_LINE_RASTERIZATION_EXTENSION_NAME) }
            .FirstOrDefault(HasDeviceExtension);

        // A compute shader's RWTexture2D carries no format of its own, as Slang writes it, which
        // these let it read and write, on every desktop driver that has them.
        _instanceApi.vkGetPhysicalDeviceFeatures(_physicalDevice, out var supported);
        CanWriteImages = supported.shaderStorageImageReadWithoutFormat && supported.shaderStorageImageWriteWithoutFormat;
        // Triangles drawn as points, as rlgl's point mode draws a model, where the driver has it.
        CanDrawPoints = supported.fillModeNonSolid;
        // Each image of a target of several masked apart, which every desktop driver and lavapipe have.
        CanBlendEachAttachment = supported.independentBlend;
        VkPhysicalDeviceFeatures features = new()
        {
            samplerAnisotropy = true,
            shaderStorageImageReadWithoutFormat = CanWriteImages,
            shaderStorageImageWriteWithoutFormat = CanWriteImages,
            fillModeNonSolid = CanDrawPoints,
            independentBlend = CanBlendEachAttachment,
        };

        // A shader reading SV_InstanceID counts from the draw's first instance, which Slang reads
        // through the draw parameters, so the capability it declares needs this on.
        // Passes begin with dynamic rendering, and barriers are synchronization2's, both core in
        // Vulkan 1.3, which every desktop driver in use and lavapipe have.
        var lines = new VkPhysicalDeviceLineRasterizationFeatures();
        // The GPU's own ray tracing from a shader, where the driver has the three extensions it
        // takes, which a reflection the scene's distance field misses is traced with at the top
        // quality. Every desktop driver with ray tracing and lavapipe since Mesa 25 have them.
        var rayExtensions = new[]
        {
            Utf8(VK_KHR_ACCELERATION_STRUCTURE_EXTENSION_NAME), Utf8(VK_KHR_RAY_QUERY_EXTENSION_NAME),
            Utf8(VK_KHR_DEFERRED_HOST_OPERATIONS_EXTENSION_NAME),
        };
        var rayQuery = new VkPhysicalDeviceRayQueryFeaturesKHR { pNext = lineRasterization is null ? null : &lines };
        var structures = new VkPhysicalDeviceAccelerationStructureFeaturesKHR { pNext = &rayQuery };
        var hasRayExtensions = rayExtensions.All(HasDeviceExtension);
        var vulkan13 = new VkPhysicalDeviceVulkan13Features
        {
            pNext = hasRayExtensions ? &structures : lineRasterization is null ? null : &lines,
        };
        var vulkan12 = new VkPhysicalDeviceVulkan12Features { pNext = &vulkan13 };
        var vulkan11 = new VkPhysicalDeviceVulkan11Features { pNext = &vulkan12 };
        var supported2 = new VkPhysicalDeviceFeatures2 { pNext = &vulkan11 };
        _instanceApi.vkGetPhysicalDeviceFeatures2(_physicalDevice, &supported2);
        if (!vulkan13.dynamicRendering || !vulkan13.synchronization2)
            throw new InvalidOperationException("The GPU's driver lacks Vulkan 1.3's dynamic rendering or synchronization2, which the engine draws with.");
        CanDrawBresenhamLines = lineRasterization is not null && lines.bresenhamLines;
        if (CanDrawBresenhamLines) extensionNames.Add(lineRasterization!);
        // Left off on a device that draws on its CPU: lavapipe of Mesa 25.2 crashes in the model
        // pass's fragment stage at its first ray query, any-hit alone included, where the same
        // structures, which it builds without a word from the validation layer, trace on a GPU.
        _instanceApi.vkGetPhysicalDeviceProperties(_physicalDevice, out var device);
        var onCpu = device.deviceType == VkPhysicalDeviceType.Cpu;
        CanQueryRays = hasRayExtensions && structures.accelerationStructure && rayQuery.rayQuery && vulkan12.bufferDeviceAddress && !onCpu;
        if (CanQueryRays) extensionNames.AddRange(rayExtensions);
        var enabledLines = new VkPhysicalDeviceLineRasterizationFeatures { bresenhamLines = true };
        var enabledMaintenance5 = new VkPhysicalDeviceMaintenance5Features { maintenance5 = true, pNext = CanDrawBresenhamLines ? &enabledLines : null };
        var enabledRayQuery = new VkPhysicalDeviceRayQueryFeaturesKHR
        {
            rayQuery = true,
            pNext = hasMaintenance5 ? &enabledMaintenance5 : CanDrawBresenhamLines ? &enabledLines : null,
        };
        var enabledStructures = new VkPhysicalDeviceAccelerationStructureFeaturesKHR { accelerationStructure = true, pNext = &enabledRayQuery };
        var enabled13 = new VkPhysicalDeviceVulkan13Features
        {
            pNext = CanQueryRays ? &enabledStructures : hasMaintenance5 ? &enabledMaintenance5 : CanDrawBresenhamLines ? &enabledLines : null,
            dynamicRendering = true,
            synchronization2 = true,
        };
        CanMirrorClamp = vulkan12.samplerMirrorClampToEdge;
        var enabled12 = new VkPhysicalDeviceVulkan12Features
        {
            pNext = &enabled13,
            samplerMirrorClampToEdge = CanMirrorClamp,
            bufferDeviceAddress = CanQueryRays,
        };
        var enabled11 = new VkPhysicalDeviceVulkan11Features { pNext = &enabled12, shaderDrawParameters = vulkan11.shaderDrawParameters };

        Logger.Debug($"Enabling device extensions: {string.Join(", ", extensionNames)}");
        Logger.Info(CanQueryRays ? "Ray queries: on"
            : hasRayExtensions && onCpu ? "Ray queries: off, on a device that draws on its CPU, whose ray queries crash in the model pass"
            : "Ray queries: off, the driver lacks them");
        using var deviceExts = new VkStringArray(extensionNames);
        VkDeviceCreateInfo createInfo = new()
        {
            pNext = &enabled11,
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
        var (samplers, images) = StageLimits;
        Logger.Info($"A shader stage may read {samplers} samplers and {images} images.");

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
