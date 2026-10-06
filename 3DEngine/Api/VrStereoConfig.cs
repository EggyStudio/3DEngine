using System.Numerics;

namespace Engine;

/// <summary>
/// What drawing for two eyes takes from a <see cref="VrDeviceInfo"/>, each eye's projection and the
/// offset from the camera to it, and the lens parameters a distortion shader reads, raylib's
/// <c>VrStereoConfig</c>.
/// </summary>
/// <remarks>
/// The projections are the engine's own, depth from 0 to 1 and down the screen, as
/// <see cref="Camera3D.ProjectionMatrix"/> makes one, each for its half of the target. The lens
/// parameters are fractions of the whole target, as raylib's distortion shader reads them.
/// </remarks>
public struct VrStereoConfig
{
    /// <summary>The left eye's projection, then the right's.</summary>
    public Matrix4x4[] Projection;

    /// <summary>The offset from the camera to the left eye, then to the right.</summary>
    public Matrix4x4[] ViewOffset;

    /// <summary>Where the left lens's center falls on the target.</summary>
    public Vector2 LeftLensCenter;

    /// <summary>Where the right lens's center falls on the target.</summary>
    public Vector2 RightLensCenter;

    /// <summary>The middle of the left half of the target.</summary>
    public Vector2 LeftScreenCenter;

    /// <summary>The middle of the right half of the target.</summary>
    public Vector2 RightScreenCenter;

    /// <summary>The scale from the lens's space back to the target's.</summary>
    public Vector2 Scale;

    /// <summary>The scale from the target's space into the lens's.</summary>
    public Vector2 ScaleIn;
}
