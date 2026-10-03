using System.Numerics;

namespace Engine;

/// <summary>GPU uniform buffer layout for camera data (view + projection matrices).</summary>
/// <seealso cref="ExtractedView"/>
public struct CameraUniform
{
    /// <summary>The view (world-to-eye) matrix.</summary>
    public Matrix4x4 View;

    /// <summary>The projection (eye-to-clip) matrix.</summary>
    public Matrix4x4 Projection;
}
