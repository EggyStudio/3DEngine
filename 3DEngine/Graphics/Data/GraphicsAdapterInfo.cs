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
}
