namespace Engine;

/// <summary>How an image lays out a cube's six faces, for <see cref="Engine3D.LoadTextureCubemap"/>, as raylib's <c>CubemapLayout</c>.</summary>
/// <remarks>The faces are +X, -X, +Y, -Y, +Z and -Z, in that order along a line.</remarks>
public enum CubemapLayout
{
    /// <summary>Found from the image's shape: a line six faces long, or a cross four faces by three or three by four.</summary>
    AutoDetect,
    /// <summary>The six faces one under the next.</summary>
    LineVertical,
    /// <summary>The six faces side by side.</summary>
    LineHorizontal,
    /// <summary>A cross three faces wide and four high, +Y at its top, -X, +Z and +X across, then -Y, then -Z.</summary>
    CrossThreeByFour,
    /// <summary>A cross four faces wide and three high, +Y at its top, -X, +Z, +X and -Z across, then -Y.</summary>
    CrossFourByThree,
}
