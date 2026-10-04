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

public static partial class Engine3D
{
    /// <summary>Draws the following 2D calls through <paramref name="camera"/>, in world units, until <see cref="EndMode2D"/>.</summary>
    public static void BeginMode2D(Camera2D camera) => DrawList.SetTransform(camera.Matrix * ScreenTransform(), depthTest: false);

    /// <summary>Returns to drawing in screen pixels.</summary>
    public static void EndMode2D() => DrawList.SetTransform(ScreenTransform(), depthTest: false);

    /// <summary>The camera's world to screen transform.</summary>
    public static Matrix4x4 GetCameraMatrix2D(Camera2D camera) => camera.Matrix;

    /// <summary>Where a world point appears on the screen through a 2D camera.</summary>
    public static Vector2 GetWorldToScreen2D(Vector2 position, Camera2D camera) => Vector2.Transform(position, camera.Matrix);

    /// <summary>The world point under a screen point through a 2D camera, as the mouse pointer's.</summary>
    public static Vector2 GetScreenToWorld2D(Vector2 position, Camera2D camera) =>
        Matrix4x4.Invert(camera.Matrix, out var inverse) ? Vector2.Transform(position, inverse) : position;
}
