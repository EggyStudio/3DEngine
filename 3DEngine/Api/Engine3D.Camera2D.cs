using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    /// <summary>Draws the following 2D calls through <paramref name="camera"/>, in world units, until <see cref="EndMode2D"/>.</summary>
    public static void BeginMode2D(Camera2D camera)
    {
        SetRlCamera(camera.Matrix, ScreenTransform(), depthTest: false);
        ResetRlglUnlessPushed();
    }

    /// <summary>Returns to drawing in screen pixels.</summary>
    public static void EndMode2D()
    {
        SetRlCamera(Matrix4x4.Identity, ScreenTransform(), depthTest: false);
        ResetRlglUnlessPushed();
    }

    /// <summary>The camera's world to screen transform.</summary>
    public static Matrix4x4 GetCameraMatrix2D(Camera2D camera) => camera.Matrix;

    /// <summary>Where a world point appears on the screen through a 2D camera.</summary>
    public static Vector2 GetWorldToScreen2D(Vector2 position, Camera2D camera) => Vector2.Transform(position, camera.Matrix);

    /// <summary>The world point under a screen point through a 2D camera, as the mouse pointer's.</summary>
    public static Vector2 GetScreenToWorld2D(Vector2 position, Camera2D camera) =>
        Matrix4x4.Invert(camera.Matrix, out var inverse) ? Vector2.Transform(position, inverse) : position;
}
