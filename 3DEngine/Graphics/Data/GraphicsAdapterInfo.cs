namespace Engine;


/// <summary>Information about the selected graphics adapter (GPU).</summary>
/// <param name="Name">Human-readable adapter name.</param>
/// <param name="VendorId">PCI vendor identifier.</param>
/// <param name="DeviceId">PCI device identifier.</param>
/// <param name="DeviceType">Device classification (discrete, integrated, etc.).</param>
public readonly record struct GraphicsAdapterInfo(string Name, uint VendorId, uint DeviceId, GraphicsDeviceType DeviceType)
{
    /// <summary>Placeholder info for uninitialized or unknown adapters.</summary>
    public static readonly GraphicsAdapterInfo Unknown = new("Unknown", 0, 0, GraphicsDeviceType.Unknown);
}
