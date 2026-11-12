namespace Engine;

/// <summary>
/// The scene's distance field an app starts with, which says how many cascades it has around the
/// camera, none by default, how wide a cell of the finest is, and how many cascades and triangles
/// are built again a frame at most.
/// </summary>
/// <remarks>
/// <c>E3D.SetSceneField</c> changes it while the app runs. Each cascade is 64 cells a side and
/// twice as coarse and as wide as the one before, so four cascades of 0.25 units reach 128 units
/// across.
/// </remarks>
/// <param name="Cascades">How many cascades, from 1 to 8, or 0 for no field.</param>
/// <param name="CellSize">The width of a cell of the finest cascade, in world units.</param>
/// <param name="UpdateBudget">How many cascades are built again in one frame at most, where the camera has moved past them or a mesh came or went.</param>
/// <param name="TriangleBudget">How many triangles the cascades' builds splat in one frame at most, a mesh at least, a build of more going on over the frames after.</param>
/// <param name="FarCascade">
/// How far ahead of the camera, in world units, a far cascade past the others reaches, which their
/// count leaves out, held to the camera's far plane, so <see cref="float.PositiveInfinity"/>
/// reaches that plane, or 0 for none.
/// </param>
public sealed record SceneFieldConfig(int Cascades = 0, float CellSize = SceneFieldConfig.DefaultCellSize, int UpdateBudget = SceneFieldConfig.DefaultUpdateBudget,
    int TriangleBudget = SceneFieldConfig.DefaultTriangleBudget, float FarCascade = 0)
{
    /// <summary>The width of a cell of the finest cascade unless one is given, in world units.</summary>
    public const float DefaultCellSize = 0.25f;

    /// <summary>How many cascades are built again a frame unless a number is given.</summary>
    public const int DefaultUpdateBudget = 1;

    /// <summary>How many triangles the builds splat a frame unless a number is given.</summary>
    /// <remarks>
    /// The least power of two above the triangles a field of two cascades splats in a frame where
    /// a scene built of many small meshes changes some of them as it plays, so those changes build in
    /// the frame they arrive, while 500 meshes of 1,024 triangles arriving in one frame, 512,000 at
    /// once, are built over sixteen frames or more.
    /// </remarks>
    public const int DefaultTriangleBudget = 32_768;
}
