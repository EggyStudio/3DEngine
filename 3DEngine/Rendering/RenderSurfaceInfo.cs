namespace Engine;

/// <summary>Describes the size of the primary presentation surface (e.g., swapchain/backbuffer).</summary>
/// <seealso cref="Renderer"/>
public sealed class RenderSurfaceInfo
{
    /// <summary>Current surface width in pixels (≥ 1).</summary>
    public int Width { get; set; }

    /// <summary>Current surface height in pixels (≥ 1).</summary>
    public int Height { get; set; }

    /// <summary>Monotonically increasing revision counter, bumped on each resize.</summary>
    public ulong Revision { get; private set; }

    /// <summary>Updates the surface dimensions. Returns <c>true</c> if the size changed.</summary>
    /// <param name="width">New width in pixels (clamped to ≥ 1).</param>
    /// <param name="height">New height in pixels (clamped to ≥ 1).</param>
    /// <returns><c>true</c> if the dimensions changed; <c>false</c> if they were already the same.</returns>
    public bool Apply(int width, int height)
    {
        var clampedWidth = Math.Max(1, width);
        var clampedHeight = Math.Max(1, height);
        if (Width == clampedWidth && Height == clampedHeight)
            return false;
        Width = clampedWidth;
        Height = clampedHeight;
        Revision++;
        return true;
    }
}
