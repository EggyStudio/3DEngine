namespace Engine;

/// <summary>
/// How the picture is brought up onto a window larger than it. A picture larger than the window is
/// brought down filtered linearly whatever the filter.
/// </summary>
public enum PictureFilter
{
    /// <summary>Filtered, soft at any ratio.</summary>
    Linear,

    /// <summary>
    /// Each pixel of the window the nearest of the picture's, sharp, even at a whole ratio and
    /// uneven otherwise.
    /// </summary>
    Nearest,

    /// <summary>
    /// Each pixel of the window inside a pixel of the picture from it whole and one across an edge
    /// a blend of the two, so every pixel of the picture is a block of one width that holds still
    /// at any ratio, equal to <see cref="Nearest"/> at a whole ratio.
    /// </summary>
    Sharp,
}
