namespace Engine;

/// <summary>Simple perspective camera component with projection parameters.</summary>
/// <seealso cref="ExtractedView"/>
/// <seealso cref="Transform"/>
[SceneComponent]
public struct Camera
{
    /// <summary>Vertical field of view in radians.</summary>
    public float FovY;

    /// <summary>Near clip plane distance.</summary>
    public float Near;

    /// <summary>Far clip plane distance.</summary>
    public float Far;

    /// <summary>
    /// The render texture the camera draws the mesh entities into, or none for the window, which the
    /// first camera without one draws into. A scene file does not hold it, since a render texture is
    /// made by the running program.
    /// </summary>
    public RenderTexture2D Target;

    /// <summary>The color a camera's render texture is cleared to, unless the program clears it inside <c>BeginTextureMode</c>.</summary>
    public Color Background;

    /// <summary>
    /// Whether the mouse picks the mesh entities this camera draws into the window, triggering a
    /// <see cref="Pointer{TEvent}"/> on the one under it as it moves over, leaves, presses, clicks
    /// and drags.
    /// </summary>
    /// <remarks>
    /// Read for the camera that draws the window alone, the first without a <see cref="Target"/>.
    /// It costs a ray cast a frame at every <see cref="Mesh"/> entity, by the sphere around each
    /// one's positions and then the triangles of those the ray reaches, so it is off by default, and
    /// a world where no camera sets it casts nothing.
    /// </remarks>
    public bool Picking;

    /// <summary>A 60 degree field of view from 0.1 to 1000 units. A default <see cref="Camera"/> sees nothing.</summary>
    public static Camera Default => new(60f);

    /// <summary>Creates a new camera with the specified projection parameters.</summary>
    /// <param name="fovY">Vertical field of view in degrees (default 60°). Stored internally as radians.</param>
    /// <param name="near">Near clip plane distance.</param>
    /// <param name="far">Far clip plane distance.</param>
    /// <param name="target">The render texture it draws into, or none for the window.</param>
    public Camera(float fovY = 60f, float near = 0.1f, float far = 1000f, RenderTexture2D target = default)
    {
        FovY = Single.DegreesToRadians(fovY);
        Near = near;
        Far = far;
        Target = target;
    }
}
