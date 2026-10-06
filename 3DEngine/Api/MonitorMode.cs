using SDL3;

namespace Engine;

/// <summary>A size and refresh rate a monitor can be driven at in fullscreen.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="RefreshRate">The refresh rate in hertz, rounded, or 0 when it is not known.</param>
public readonly record struct MonitorMode(int Width, int Height, int RefreshRate);
