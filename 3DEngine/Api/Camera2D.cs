using System.Numerics;

namespace Engine;

/// <summary>
/// A 2D camera, as raylib's, which draws the world point <see cref="Target"/> at the screen point
/// <see cref="Offset"/>, turned by <see cref="Rotation"/> degrees and scaled by <see cref="Zoom"/>
/// around it.
/// </summary>
/// <param name="Offset">Where on the screen the target appears, as the screen's middle for a camera that follows.</param>
/// <param name="Target">The world point the camera looks at.</param>
/// <param name="Rotation">How far the view is turned, in degrees.</param>
/// <param name="Zoom">How much the world is scaled, 1 for a world unit to a pixel.</param>
public record struct Camera2D(Vector2 Offset, Vector2 Target, float Rotation = 0, float Zoom = 1)
{
    /// <summary>World to screen pixels, which moves the target to the origin, turns, scales, and moves it to the offset.</summary>
    public readonly Matrix4x4 Matrix =>
        Matrix4x4.CreateTranslation(-Target.X, -Target.Y, 0)
        * Matrix4x4.CreateRotationZ(float.DegreesToRadians(Rotation))
        * Matrix4x4.CreateScale(Zoom, Zoom, 1)
        * Matrix4x4.CreateTranslation(Offset.X, Offset.Y, 0);
}
