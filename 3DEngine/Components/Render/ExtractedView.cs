using System.Numerics;

namespace Engine;

/// <summary>
/// Per-entity render component for cameras extracted during the extract phase.
/// Contains the computed view/projection matrices and viewport dimensions.
/// </summary>
/// <seealso cref="CameraExtract"/>
/// <seealso cref="MainPassNode"/>
public struct ExtractedView
{
    /// <summary>View (world-to-eye) matrix.</summary>
    public Matrix4x4 View;

    /// <summary>Projection (eye-to-clip) matrix.</summary>
    public Matrix4x4 Projection;

    /// <summary>Viewport width in pixels.</summary>
    public int Width;

    /// <summary>Viewport height in pixels.</summary>
    public int Height;
}
