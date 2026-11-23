namespace Engine;

/// <summary>
/// How the light the world's probes hold reaches the picture where the screen's probes stand, as
/// <c>gi.gather</c> names it.
/// </summary>
internal enum ScreenGather
{
    /// <summary>
    /// The screen's probes, one a tile tracing sixteen rays, kept over the frames and blended with
    /// their neighbors.
    /// </summary>
    Probes,

    /// <summary>
    /// The gather by pixel, one ray from each texel of a grid of the window's pixels at
    /// <see cref="GatherSize"/>, made whole each frame and blended within it, the screen's probes
    /// standing beside it for the glow lights alone.
    /// </summary>
    Pixels,
}

/// <summary>
/// The window's pixels along each side of a texel of the gather by pixel's grid, as
/// <c>gi.gather_size</c> names it.
/// </summary>
internal enum GatherSize
{
    /// <summary>A texel a pixel.</summary>
    Full = 1,

    /// <summary>A texel each two pixels a side.</summary>
    Half = 2,

    /// <summary>A texel each four pixels a side.</summary>
    Quarter = 4,

    /// <summary>
    /// A texel each eight pixels a side, the tile of the screen's probes at
    /// <see cref="GlobalIllumination.High"/> in a window up to 1080 pixels high.
    /// </summary>
    Eighth = 8,
}
