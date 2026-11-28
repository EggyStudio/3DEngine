namespace Engine;


/// <summary>Information about the selected graphics adapter (GPU).</summary>
/// <param name="Name">Human-readable adapter name.</param>
/// <param name="VendorId">PCI vendor identifier.</param>
/// <param name="DeviceId">PCI device identifier.</param>
/// <param name="DeviceType">Device classification (discrete, integrated, etc.).</param>
/// <param name="Driver">The driver's version as its vendor numbers it, or empty where unknown.</param>
/// <param name="Api">The Vulkan version the device offers, as <c>1.3.280</c>, or empty where unknown.</param>
internal readonly record struct GraphicsAdapterInfo(string Name, uint VendorId, uint DeviceId, GraphicsDeviceType DeviceType, string Driver = "", string Api = "")
{
    /// <summary>
    /// A driver version as its vendor numbers it, NVIDIA's four parts in ten, eight, eight and six
    /// bits and Intel's two on Windows in fourteen each, where every other vendor packs it as Vulkan
    /// packs its own version.
    /// </summary>
    public static string DriverOf(uint vendor, uint version) => vendor switch
    {
        0x10DE => $"{version >> 22}.{(version >> 14) & 0xFF}.{(version >> 6) & 0xFF}.{version & 0x3F}",
        0x8086 when OperatingSystem.IsWindows() => $"{version >> 14}.{version & 0x3FFF}",
        _ => $"{version >> 22}.{(version >> 12) & 0x3FF}.{version & 0xFFF}",
    };

    /// <summary>Placeholder info for uninitialized or unknown adapters.</summary>
    public static readonly GraphicsAdapterInfo Unknown = new("Unknown", 0, 0, GraphicsDeviceType.Unknown);

    /// <summary>
    /// Whether the adapter is lavapipe before Mesa 26.3, which numbers its driver as Mesa is
    /// numbered and ends the process in the split cascades' kernel.
    /// </summary>
    /// <remarks>
    /// Mesa 25.2.8's lavapipe compiles that kernel to a shader that reads a buffer's descriptor
    /// through a null address, in the store's trace and in the frames of a slab 8 wide under the
    /// split layout, where 26.3.1's runs both, so the split's stages are not made on such a device.
    /// </remarks>
    public bool FaultsSplitTrace
    {
        get
        {
            if (DeviceType != GraphicsDeviceType.Cpu || !Name.StartsWith("llvmpipe", StringComparison.Ordinal)) return false;
            var parts = Driver.Split('.');
            return parts.Length >= 2 && int.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, out var major)
                && int.TryParse(parts[1], System.Globalization.CultureInfo.InvariantCulture, out var minor) && major * 100 + minor < 2603;
        }
    }
}
