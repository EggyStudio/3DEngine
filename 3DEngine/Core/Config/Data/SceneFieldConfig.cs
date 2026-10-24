namespace Engine;

/// <summary>
/// The scene's distance field an app starts with, which says how many cascades it has around the
/// camera, none by default, how wide a cell of the finest is, and how many cascades and triangles
/// are built again a frame at most.
/// </summary>
/// <remarks>
/// <see cref="E3D.SetSceneField"/> changes it while the app runs. Each cascade is 64 cells a
/// side and twice as coarse and as wide as the one before, so four cascades of 0.25 units reach 128
/// units across and take 4 MB of the GPU's memory.
/// </remarks>
/// <param name="Cascades">How many cascades, from 1 to 8, or 0 for no field.</param>
/// <param name="CellSize">The width of a cell of the finest cascade, in world units.</param>
/// <param name="UpdateBudget">How many cascades are built again in one frame at most, where the camera has moved past them or a mesh came or went.</param>
/// <param name="TriangleBudget">How many triangles the cascades' builds splat in one frame at most, a mesh at least, a build of more going on over the frames after.</param>
public sealed record SceneFieldConfig(int Cascades = 0, float CellSize = SceneFieldConfig.DefaultCellSize, int UpdateBudget = SceneFieldConfig.DefaultUpdateBudget,
    int TriangleBudget = SceneFieldConfig.DefaultTriangleBudget)
{
    /// <summary>The width of a cell of the finest cascade unless one is given, in world units.</summary>
    public const float DefaultCellSize = 0.25f;

    /// <summary>How many cascades are built again a frame unless a number is given.</summary>
    public const int DefaultUpdateBudget = 1;

    /// <summary>How many triangles the builds splat a frame unless a number is given.</summary>
    /// <remarks>
    /// Read from the field's counts of the two scenes its readings are taken in. Ore splats 18,824
    /// triangles in a frame at its budget of two cascades and the Cornell box 180, and this is the
    /// least power of two above Ore's, so both build as they did, where 500 sections of 1,024
    /// triangles arriving in one frame, 512,000 at once, are built over sixteen frames or more.
    /// </remarks>
    public const int DefaultTriangleBudget = 32_768;
}
