using System.Runtime.InteropServices;
using System.Text;
using Vortice.Vulkan;
using static Vortice.Vulkan.Vulkan;

namespace Engine;

public sealed unsafe partial class GraphicsDevice
{
    /// <summary>Khronos validation layer names requested when validation is enabled.</summary>
    private static readonly string[] ValidationLayers =
    {
        Utf8(VK_LAYER_KHRONOS_VALIDATION_EXTENSION_NAME)
    };

    /// <summary>Creates the Vulkan instance, configures validation layers and the debug messenger.</summary>
    /// <param name="appName">Application name embedded in the <c>VkApplicationInfo</c>.</param>
    private partial void CreateInstance(string appName)
    {
        Logger.Debug("Loading Vulkan library via vkInitialize()...");
        // Load the Vulkan library - must be called before any other Vulkan API.
        vkInitialize().CheckResult();
        Logger.Debug("Vulkan library loaded successfully.");

        Logger.Debug("Checking for validation layer support...");
        _validationEnabled = ShouldEnableValidation() && AreValidationLayersAvailable();
        Logger.Info($"Validation layers: {(_validationEnabled ? "ENABLED" : "DISABLED")}");

        VkUtf8ReadOnlyString appNameUtf8 = Encoding.UTF8.GetBytes(appName);
        VkUtf8ReadOnlyString engineNameUtf8 = "3DEngine"u8;

        VkApplicationInfo appInfo = new()
        {
            pApplicationName = appNameUtf8,
            applicationVersion = new VkVersion(1, 0, 0),
            pEngineName = engineNameUtf8,
            engineVersion = new VkVersion(1, 0, 0),
            apiVersion = VkVersion.Version_1_2
        };

        Logger.Debug("Querying required instance extensions from surface source...");
        var requiredExtensions = _surfaceSource!
            .GetRequiredInstanceExtensions()
            .ToList();
        var debugUtils = Utf8(VK_EXT_DEBUG_UTILS_EXTENSION_NAME);
        if (_validationEnabled && !requiredExtensions.Contains(debugUtils))
            requiredExtensions.Add(debugUtils);

        foreach (var ext in requiredExtensions)
            Logger.Debug($"  Required extension: {ext}");

        using var extensions = new VkStringArray(requiredExtensions);
        VkInstanceCreateInfo createInfo = new()
        {
            pApplicationInfo = &appInfo,
            enabledExtensionCount = extensions.Length,
            ppEnabledExtensionNames = extensions
        };

        VkDebugUtilsMessengerCreateInfoEXT debugCreateInfo = default;
        if (_validationEnabled)
        {
            Logger.Debug("Setting up validation layers and debug messenger for VkInstance...");
            using var validation = new VkStringArray(ValidationLayers);
            createInfo.enabledLayerCount = validation.Length;
            createInfo.ppEnabledLayerNames = validation;
            PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
            createInfo.pNext = &debugCreateInfo;
            vkCreateInstance(&createInfo, null, out _instance).CheckResult();
        }
        else
        {
            Logger.Debug("Creating VkInstance without validation layers...");
            vkCreateInstance(&createInfo, null, out _instance).CheckResult();
        }

        Logger.Debug($"VkInstance created (handle=0x{_instance.Handle:X}).");
        _instanceApi = GetApi(_instance);

        if (_validationEnabled)
        {
            Logger.Debug("Attaching Vulkan debug utils messenger for validation callbacks...");
            _instanceApi.vkCreateDebugUtilsMessengerEXT(&debugCreateInfo, null, out _debugMessenger).CheckResult();
            Logger.Debug("Debug messenger attached successfully.");
        }
    }

    /// <summary>Destroys the debug messenger and Vulkan instance.</summary>
    private partial void DestroyInstance()
    {
        if (_validationEnabled && _debugMessenger.Handle != 0)
        {
            Logger.Debug("Destroying Vulkan debug utils messenger...");
            _instanceApi.vkDestroyDebugUtilsMessengerEXT(_debugMessenger, null);
        }
        if (_instance.Handle != 0)
        {
            Logger.Debug("Destroying VkInstance...");
            _instanceApi.vkDestroyInstance();
            _instance = default;
            Logger.Debug("VkInstance destroyed.");
        }
    }

    /// <summary>Returns <see langword="true"/> when Vulkan validation should be enabled (always in DEBUG; via env var in RELEASE).</summary>
    private static bool ShouldEnableValidation()
    {
#if DEBUG
        return true;
#else
        return Environment.GetEnvironmentVariable("ENGINE_VULKAN_VALIDATION") == "1";
#endif
    }

    /// <summary>
    /// Enumerates available Vulkan instance layers and checks that all requested
    /// validation layers are present. Returns false if any layer is missing,
    /// preventing a segfault from requesting a non-existent layer.
    /// </summary>
    private static bool AreValidationLayersAvailable()
    {
        try
        {
            var result = vkEnumerateInstanceLayerProperties(out uint layerCount);
            if (result != VkResult.Success || layerCount == 0) return false;

            var availableLayers = new VkLayerProperties[(int)layerCount];
            result = vkEnumerateInstanceLayerProperties(availableLayers);
            if (result != VkResult.Success) return false;

            var available = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < (int)layerCount; i++)
            {
                fixed (byte* namePtr = availableLayers[i].layerName)
                {
                    var name = Marshal.PtrToStringUTF8((nint)namePtr);
                    if (name is not null) available.Add(name);
                }
            }

            foreach (var required in ValidationLayers)
            {
                if (!available.Contains(required))
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Populates the debug messenger create info with severity/type filters and the native callback.</summary>
    /// <param name="createInfo">The create info struct to populate (overwritten in-place).</param>
    private static void PopulateDebugMessengerCreateInfo(ref VkDebugUtilsMessengerCreateInfoEXT createInfo)
    {
        createInfo = new VkDebugUtilsMessengerCreateInfoEXT
        {
            messageSeverity = VkDebugUtilsMessageSeverityFlagsEXT.Verbose |
                              VkDebugUtilsMessageSeverityFlagsEXT.Warning |
                              VkDebugUtilsMessageSeverityFlagsEXT.Error,
            messageType = VkDebugUtilsMessageTypeFlagsEXT.General |
                          VkDebugUtilsMessageTypeFlagsEXT.Validation |
                          VkDebugUtilsMessageTypeFlagsEXT.Performance,
            pfnUserCallback = &DebugCallback
        };
    }

    /// <summary>Native callback invoked by the Vulkan validation layer; routes messages to <see cref="Log"/>.</summary>
    [UnmanagedCallersOnly]
    private static uint DebugCallback(
        VkDebugUtilsMessageSeverityFlagsEXT severity,
        VkDebugUtilsMessageTypeFlagsEXT type,
        VkDebugUtilsMessengerCallbackDataEXT* data,
        void* userData)
    {
        var message = Marshal.PtrToStringUTF8((nint)data->pMessage) ?? string.Empty;
        var logger = Log.Category("Vulkan.Validation");
        var formatted = $"[{type}] {message}";
        if (severity.HasFlag(VkDebugUtilsMessageSeverityFlagsEXT.Error))
            logger.Error(formatted);
        else if (severity.HasFlag(VkDebugUtilsMessageSeverityFlagsEXT.Warning))
            logger.Warn(formatted);
        else
            logger.Debug(formatted);
        return 0;
    }
}
