namespace Engine;

/// <summary>Graphics backend selector for the application window.</summary>
public enum GraphicsBackend
{
    /// <summary>SDL software renderer - simple, portable, no GPU required.</summary>
    Sdl = 0,
    /// <summary>Vulkan GPU-accelerated rendering.</summary>
    Vulkan = 1,
}
