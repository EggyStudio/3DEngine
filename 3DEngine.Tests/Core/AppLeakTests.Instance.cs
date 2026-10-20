using System.Runtime.InteropServices;
using System.Text;
using Vortice.Vulkan;

namespace Engine.Tests.Core;

public sealed partial class AppLeakTests
{
    // A Vulkan instance made through the loader the engine's devices use, as the engine's instance
    // is made but with no surface, no debug messages and no layer, as the apps beside it are made.
    // MoltenVK on macOS is a portability driver, which the loader gives an instance only to a caller
    // that asks for portability drivers, by the extension and the flag, where the loader offers
    // them. An instance that asked for nothing was answered ErrorIncompatibleDriver there.
    private sealed unsafe class HeldInstance : IDisposable
    {
        private readonly VkInstanceApi _api;

        public HeldInstance()
        {
            Vulkan.vkInitialize().CheckResult();
            var portability = Encoding.UTF8.GetString(Vulkan.VK_KHR_PORTABILITY_ENUMERATION_EXTENSION_NAME).TrimEnd('\0');
            var portable = Offered(portability);
            using var extensions = new VkStringArray(portable ? [portability] : new List<string>());
            VkApplicationInfo application = new() { apiVersion = VkVersion.Version_1_3 };
            VkInstanceCreateInfo info = new()
            {
                pApplicationInfo = &application,
                enabledExtensionCount = extensions.Length,
                ppEnabledExtensionNames = extensions,
                flags = portable ? VkInstanceCreateFlags.EnumeratePortabilityKHR : VkInstanceCreateFlags.None,
            };
            Vulkan.vkCreateInstance(&info, null, out var instance).CheckResult();
            _api = new VkInstanceApi(instance);
        }

        public void Dispose() => _api.vkDestroyInstance();

        private static bool Offered(string name)
        {
            if (Vulkan.vkEnumerateInstanceExtensionProperties(out uint count) != VkResult.Success || count == 0) return false;
            var properties = new VkExtensionProperties[(int)count];
            if (Vulkan.vkEnumerateInstanceExtensionProperties(properties) != VkResult.Success) return false;
            for (int i = 0; i < (int)count; i++)
                fixed (byte* offered = properties[i].extensionName)
                    if (Marshal.PtrToStringUTF8((nint)offered) == name) return true;
            return false;
        }
    }
}
