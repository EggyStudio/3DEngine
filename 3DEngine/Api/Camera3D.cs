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
    /// W, A, S and D move, Q and E move down and up, Shift moves faster, and dragging with the
    /// right mouse button turns. The wheel moves forward and back.
    /// </summary>
    Free,

    /// <summary>The camera circles its target, and the wheel moves it closer or further.</summary>
    Orbital,

    /// <summary>
    /// The mouse turns the camera without a button, and W, A, S and D walk along the ground, the
    /// plane across <see cref="Camera3D.Up"/>. Meant with <see cref="Engine3D.DisableCursor"/>.
    /// </summary>
    FirstPerson,

    /// <summary>
    /// The camera looks at its target from behind. The mouse turns it around the target without a
    /// button, W, A, S and D walk both along the ground, and the wheel moves closer or further.
    /// </summary>
    ThirdPerson,
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
