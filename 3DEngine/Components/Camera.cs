namespace Engine;

/// <summary>Simple perspective camera component with projection parameters.</summary>
/// <seealso cref="ExtractedView"/>
/// <seealso cref="Transform"/>
public struct Camera
{
    /// <summary>Vertical field of view in radians.</summary>
    public float FovY;

    /// <summary>Near clip plane distance.</summary>
    public float Near;

    /// <summary>Far clip plane distance.</summary>
    public float Far;

    /// <summary>Optional name of a render texture target; if <c>null</c>/empty, renders to the primary surface.</summary>
    public string? TargetName;

    /// <summary>Creates a new camera with the specified projection parameters.</summary>
    /// <param name="fovY">Vertical field of view in degrees (default 60°). Stored internally as radians.</param>
    /// <param name="near">Near clip plane distance.</param>
    /// <param name="far">Far clip plane distance.</param>
    /// <param name="targetName">Optional render texture target name.</param>
    public Camera(float fovY = 60f, float near = 0.1f, float far = 1000f,
        string? targetName = null)
    {
        FovY = Single.DegreesToRadians(fovY);
        Near = near;
        Far = far;
        TargetName = targetName;
    }
}
