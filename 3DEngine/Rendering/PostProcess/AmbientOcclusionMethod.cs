namespace Engine;

/// <summary>How the window's ambient occlusion is worked out from its depth, which <c>SetAmbientOcclusionMethod</c> chooses.</summary>
public enum AmbientOcclusionMethod
{
    /// <summary>
    /// By the horizons the depth gives along two slices a pixel, turned each frame, the default.
    /// </summary>
    Horizons,

    /// <summary>
    /// By rays over each pixel's hemisphere traced against the points around it taken as circles,
    /// Isheden's circular occluders (CORTAO), at 8 or 16 points a pixel.
    /// </summary>
    /// <remarks>
    /// A render texture's own occlusion is still worked out by <see cref="Horizons"/>, and so is the
    /// window's where its camera's projection is not a perspective without a turn or a skew of its
    /// own.
    /// </remarks>
    CircularOccluders,
}
