using System.Numerics;

namespace Engine;

public static partial class Engine3D
{
    /// <summary>The camera's world to view transform, as raylib's <c>GetCameraMatrix</c> gives it.</summary>
    public static Matrix4x4 GetCameraMatrix(Camera3D camera) => camera.View;

    /// <summary>Where a world point appears in the window through a 3D camera, in pixels from the top left.</summary>
    public static Vector2 GetWorldToScreen(Vector3 position, Camera3D camera) =>
        GetWorldToScreenEx(position, camera, GetScreenWidth(), GetScreenHeight());

    /// <summary>Where a world point appears in a view <paramref name="width"/> by <paramref name="height"/> pixels through a 3D camera.</summary>
    /// <remarks>
    /// A point behind a perspective camera is projected through it, and lands mirrored on the far
    /// side of the view, as raylib's does. <see cref="IsPointInFrontOfCamera"/> tells the two apart.
    /// </remarks>
    public static Vector2 GetWorldToScreenEx(Vector3 position, Camera3D camera, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        var clip = Vector4.Transform(new Vector4(position, 1), camera.View * camera.ProjectionMatrix((float)width / height));
        var w = MathF.Abs(clip.W) < 1e-6f ? 1e-6f : clip.W;
        // Normalized device coordinates run down the screen as pixels do, since the projection is
        // flipped for Vulkan.
        return new Vector2((clip.X / w + 1) / 2 * width, (clip.Y / w + 1) / 2 * height);
    }

    /// <summary>Whether a world point is in front of a camera, so that <see cref="GetWorldToScreen"/> places it where it shows.</summary>
    public static bool IsPointInFrontOfCamera(Vector3 position, Camera3D camera) =>
        Vector3.Dot(position - camera.Position, camera.Target - camera.Position) > 0;
}
