namespace Engine;

/// <summary>
/// The scene's distance field an app starts with: how many cascades it has around the camera,
/// none by default, how wide a cell of the finest is, and how many cascades are built again a
/// frame at most.
/// </summary>
/// <remarks>
/// <see cref="Engine3D.SetSceneField"/> changes it while the app runs. Each cascade is 64 cells a
/// side and twice as coarse and as wide as the one before, so four cascades of 0.25 units reach 128
/// units across and take 4 MB of the GPU's memory.
/// </remarks>
/// <param name="Cascades">How many cascades, from 1 to 8, or 0 for no field.</param>
/// <param name="CellSize">The width of a cell of the finest cascade, in world units.</param>
/// <param name="UpdateBudget">How many cascades are built again in one frame at most, where the camera has moved past them or a mesh came or went.</param>
public sealed record SceneFieldConfig(int Cascades = 0, float CellSize = SceneFieldConfig.DefaultCellSize, int UpdateBudget = SceneFieldConfig.DefaultUpdateBudget)
{
    /// <summary>The width of a cell of the finest cascade unless one is given, in world units.</summary>
    public const float DefaultCellSize = 0.25f;

    /// <summary>How many cascades are built again a frame unless a number is given.</summary>
    public const int DefaultUpdateBudget = 1;
}
