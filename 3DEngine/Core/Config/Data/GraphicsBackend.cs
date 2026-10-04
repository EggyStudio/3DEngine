namespace Engine;

/// <summary>Graphics backend selector for the application window.</summary>
public enum GraphicsBackend
{
    /// <summary>The SDL software renderer, simple and portable, with no GPU needed.</summary>
    Sdl = 0,
    /// <summary>Vulkan GPU-accelerated rendering.</summary>
    Vulkan = 1,
}
