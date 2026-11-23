namespace Engine;

/// <summary>
/// How frames drawn at a size of their own are scaled onto a window that covers its screen, as in
/// borderless fullscreen at <c>window.width</c> by <c>window.height</c> (<c>window.scaling</c>).
/// </summary>
internal enum WindowScaling
{
    /// <summary>
    /// Filtered, soft at any ratio, as large as fits with its shape kept and black round it.
    /// </summary>
    Linear,

    /// <summary>
    /// Each pixel of the screen the nearest of the frame's, sharp, even at a whole ratio and uneven
    /// otherwise, as large as fits with its shape kept and black round it.
    /// </summary>
    Nearest,

    /// <summary>
    /// The largest whole multiple of the frame that fits, each of its pixels a square of the same
    /// size, centered with black round it, and as <see cref="Linear"/> where the frame is larger
    /// than the screen.
    /// </summary>
    Integer,
}

/// <summary>Where a frame of its own size stands on the window it is scaled onto.</summary>
internal static class Placement
{
    /// <summary>
    /// The rectangle of the window's pixels a frame of <paramref name="frameWidth"/> by
    /// <paramref name="frameHeight"/> pixels is drawn into, centered, and whether it is filtered.
    /// </summary>
    internal static (int X, int Y, int Width, int Height, bool Filtered) Of(int frameWidth, int frameHeight,
        int windowWidth, int windowHeight, WindowScaling scaling)
    {
        frameWidth = Math.Max(1, frameWidth);
        frameHeight = Math.Max(1, frameHeight);
        var fit = Math.Min((double)windowWidth / frameWidth, (double)windowHeight / frameHeight);
        var whole = scaling == WindowScaling.Integer && fit >= 1;
        if (whole) fit = Math.Floor(fit);
        var width = Math.Clamp((int)Math.Round(frameWidth * fit), 1, Math.Max(1, windowWidth));
        var height = Math.Clamp((int)Math.Round(frameHeight * fit), 1, Math.Max(1, windowHeight));
        return ((windowWidth - width) / 2, (windowHeight - height) / 2, width, height,
            scaling == WindowScaling.Linear || (scaling == WindowScaling.Integer && !whole));
    }
}
