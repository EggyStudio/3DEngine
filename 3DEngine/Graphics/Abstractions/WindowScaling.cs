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
    /// Each pixel of the screen inside a pixel of the frame from it whole and one across an edge a
    /// blend of the two, so every pixel of the frame is a block of one width that holds still at
    /// any ratio, equal to <see cref="Nearest"/> at a whole ratio, as large as fits with its shape
    /// kept and black round it, and as <see cref="Linear"/> where the frame is larger than the
    /// screen.
    /// </summary>
    Sharp,
}

/// <summary>Where a frame of its own size stands on the window it is scaled onto.</summary>
internal static class Placement
{
    /// <summary>
    /// The rectangle of the window's pixels a frame of <paramref name="frameWidth"/> by <paramref
    /// name="frameHeight"/> pixels is drawn into, as large as fits with its shape kept and
    /// centered, the same for every scaling.
    /// </summary>
    internal static (int X, int Y, int Width, int Height) Of(int frameWidth, int frameHeight, int windowWidth, int windowHeight)
    {
        frameWidth = Math.Max(1, frameWidth);
        frameHeight = Math.Max(1, frameHeight);
        var fit = Math.Min((double)windowWidth / frameWidth, (double)windowHeight / frameHeight);
        var width = Math.Clamp((int)Math.Round(frameWidth * fit), 1, Math.Max(1, windowWidth));
        var height = Math.Clamp((int)Math.Round(frameHeight * fit), 1, Math.Max(1, windowHeight));
        return ((windowWidth - width) / 2, (windowHeight - height) / 2, width, height);
    }
}
