namespace Engine;

/// <summary>Two-dimensional extent in pixels (width × height).</summary>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
internal readonly record struct Extent2D(uint Width, uint Height);
