using System.Numerics;

namespace Engine;

/// <summary>How a <see cref="Camera3D"/> projects the world.</summary>
public enum CameraProjection
{
    /// <summary>Things further away are drawn smaller. <see cref="Camera3D.FovY"/> is the vertical angle in degrees.</summary>
    Perspective,

    /// <summary>Things are drawn the same size at any distance. <see cref="Camera3D.FovY"/> is the visible height in world units.</summary>
    Orthographic,
}

/// <summary>How <see cref="Engine3D.UpdateCamera"/> moves a camera from input.</summary>
public enum CameraMode
{
    /// <summary>
    /// The mouse and the arrow keys turn, Q and E roll, W, A, S and D move the way it looks, Space
    /// rises and left Ctrl sinks, dragging with the middle button pans, and the wheel and the
    /// keypad's plus and minus move it nearer its target and farther.
    /// </summary>
    Free,

    /// <summary>The camera circles its target half a radian a second, and the wheel moves it nearer and farther.</summary>
    Orbital,

    /// <summary>
    /// The mouse and the arrow keys turn the camera, Q and E roll it, and W, A, S and D walk along
    /// the ground, the plane across <see cref="Camera3D.Up"/>. Meant with <see cref="Engine3D.DisableCursor"/>.
    /// </summary>
    FirstPerson,

    /// <summary>
    /// The camera turns about its target with the mouse and the arrow keys, W, A, S and D walk the
    /// target along the ground, and the wheel moves the camera nearer and farther.
    /// </summary>
    ThirdPerson,

    /// <summary>Nothing moves the camera, which the program moves by its own means, as raylib's custom mode.</summary>
    Custom,
}

/// <summary>A camera the program keeps and passes to <see cref="Engine3D.BeginMode3D"/>.</summary>
/// <remarks>
/// A camera is a value rather than an entity, so a program keeps as many as it likes and
/// switches between them by passing a different one. The fields are raylib's.
/// </remarks>
public struct Camera3D
{
    /// <summary>Where the camera is.</summary>
    public Vector3 Position;

    /// <summary>The point it looks at.</summary>
    public Vector3 Target;

    /// <summary>Which way is up for it, usually <see cref="Vector3.UnitY"/>.</summary>
    public Vector3 Up;

    /// <summary>The vertical field of view in degrees, or the visible height when orthographic.</summary>
    public float FovY;

    /// <summary>How the camera projects.</summary>
    public CameraProjection Projection;

    /// <summary>Creates a perspective camera.</summary>
    public Camera3D(Vector3 position, Vector3 target, Vector3 up, float fovY = 45f,
        CameraProjection projection = CameraProjection.Perspective)
    {
        Position = position;
        Target = target;
        Up = up;
        FovY = fovY;
        Projection = projection;
    }

    /// <summary>The world to view transform.</summary>
    public readonly Matrix4x4 View => Matrix4x4.CreateLookAt(Position, Target, Up);

    /// <summary>The view to clip transform for a target of the given aspect ratio.</summary>
    /// <remarks>
    /// Y is flipped for Vulkan, whose clip space points down where <c>System.Numerics</c> assumes
    /// up, and depth runs from 0 to 1.
    /// </remarks>
    public readonly Matrix4x4 ProjectionMatrix(float aspect)
    {
        const float near = 0.05f, far = 1000f;
        var projection = Projection == CameraProjection.Perspective
            ? Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(FovY), aspect, near, far)
            : Matrix4x4.CreateOrthographic(FovY * aspect, FovY, near, far);
        projection.M22 = -projection.M22;
        return projection;
    }
}

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
