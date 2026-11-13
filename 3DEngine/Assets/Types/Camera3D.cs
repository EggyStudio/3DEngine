using System.Numerics;

namespace Engine;

/// <summary>A camera the program keeps and passes to <c>E3D.BeginMode3D</c>.</summary>
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

    /// <summary>
    /// The world to view transform with the eye at nothing, the view's turn alone, which takes a
    /// point less <see cref="Position"/>, so a camera far from the world's origin is turned without
    /// a translation of the world's size.
    /// </summary>
    internal readonly Matrix4x4 TurnedView => Matrix4x4.CreateLookAt(Vector3.Zero, Target - Position, Up);

    /// <summary>The view to clip transform for a target of the given aspect ratio.</summary>
    /// <remarks>
    /// Y is flipped for Vulkan, whose clip space points down where <c>System.Numerics</c> assumes
    /// up, and depth runs from 0 to 1. It clips from 0.05 to 4000 units away, as raylib's
    /// <c>RL_CULL_DISTANCE_NEAR</c> and <c>RL_CULL_DISTANCE_FAR</c> do.
    /// </remarks>
    public readonly Matrix4x4 ProjectionMatrix(float aspect)
    {
        const float near = 0.05f, far = 4000f;
        var projection = Projection == CameraProjection.Perspective
            ? Matrix4x4.CreatePerspectiveFieldOfView(float.DegreesToRadians(FovY), aspect, near, far)
            : Matrix4x4.CreateOrthographic(FovY * aspect, FovY, near, far);
        projection.M22 = -projection.M22;
        return projection;
    }
}
