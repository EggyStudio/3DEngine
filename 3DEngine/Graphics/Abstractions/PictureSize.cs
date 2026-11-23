namespace Engine;

/// <summary>The size a <see cref="Picture"/> is drawn at for a window and where it stands on
/// it.</summary>
internal static class PictureSize
{
    /// <summary>The least share of the window's size the scene is drawn at.</summary>
    internal const float LowScale = 0.5f;

    /// <summary>The most share of the window's size the scene is drawn at.</summary>
    internal const float HighScale = 2f;

    /// <summary>
    /// The size in pixels the scene is drawn at for a window of <paramref name="window"/> pixels,
    /// the picture's own size where it holds one and the window's times its scale otherwise, then
    /// the largest size of the picture's aspect inside that, each side at least a pixel.
    /// </summary>
    internal static (int Width, int Height) Of(Picture picture, (int Width, int Height) window)
    {
        var scale = Scale(picture);
        var (width, height) = picture.Size ?? ((int)MathF.Round(window.Width * scale), (int)MathF.Round(window.Height * scale));
        (width, height) = (Math.Max(1, width), Math.Max(1, height));
        if (picture.Aspect is { Width: > 0, Height: > 0 } aspect)
        {
            if ((long)width * aspect.Height > (long)height * aspect.Width)
                width = Math.Max(1, (int)Math.Round((double)height * aspect.Width / aspect.Height));
            else
                height = Math.Max(1, (int)Math.Round((double)width * aspect.Height / aspect.Width));
        }
        return (width, height);
    }

    /// <summary>
    /// The picture's scale held between <see cref="LowScale"/> and <see cref="HighScale"/>, 1 where
    /// it is no number.
    /// </summary>
    internal static float Scale(Picture picture) =>
        float.IsFinite(picture.Scale) ? Math.Clamp(picture.Scale, LowScale, HighScale) : 1f;

    /// <summary>
    /// Whether the picture keeps its shape on the window with black round it, which it does where
    /// it fits and an aspect or a size of its own may give it a shape other than the window's.
    /// </summary>
    internal static bool Bars(Picture picture) =>
        picture.Fit == PictureFit.Fit && (picture.Aspect is not null || picture.Size is not null);

    /// <summary>
    /// The rectangle of the window's pixels a picture of <paramref name="picture"/> pixels is drawn
    /// into, as large as fits with its shape kept where <paramref name="bars"/>, and otherwise the
    /// whole window.
    /// </summary>
    internal static (int X, int Y, int Width, int Height) Placed(bool bars, (int Width, int Height) picture, (int Width, int Height) window) =>
        bars ? Placement.Of(picture.Width, picture.Height, window.Width, window.Height) : (0, 0, Math.Max(1, window.Width), Math.Max(1, window.Height));
}

/// <summary>Where a picture of its own size stands on the window it is brought onto.</summary>
internal static class Placement
{
    /// <summary>
    /// The rectangle of the window's pixels a picture of <paramref name="frameWidth"/> by <paramref
    /// name="frameHeight"/> pixels is drawn into, as large as fits with its shape kept and
    /// centered, the same for every filter.
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
