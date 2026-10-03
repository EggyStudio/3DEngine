namespace Engine;

/// <summary>GPU device type classification, matching Vulkan physical device types.</summary>
public enum GraphicsDeviceType
{
    /// <summary>Device type cannot be determined.</summary>
    Unknown,
    /// <summary>Integrated GPU sharing system memory with the CPU.</summary>
    IntegratedGpu,
    /// <summary>Discrete GPU with dedicated video memory.</summary>
    DiscreteGpu,
    /// <summary>Virtual GPU in a virtualization environment.</summary>
    VirtualGpu,
    /// <summary>CPU-only software rasterizer.</summary>
    Cpu,
    /// <summary>Software-based rendering backend (e.g., SDL software renderer).</summary>
    Software
}
