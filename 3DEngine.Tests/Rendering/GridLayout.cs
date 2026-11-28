using RadianceCascades;
using static Engine.E3D;

namespace Engine.Tests.Rendering;

/// <summary>
/// Lays global illumination's probes out on the grid about the eye for a test the split cascades
/// of the default layout do not pass, each way of setting it naming why.
/// </summary>
internal static class GridLayout
{
    /// <summary>
    /// For a test of a surface that lets a share of the light through, at which the split's rays
    /// end as at a solid one, carrying none of the light past it.
    /// </summary>
    public static void ForASurfaceLettingLightThrough() => Set();

    /// <summary>
    /// For a test of a wall or a floor thinner than a cell of the field below High, where the
    /// split's rays march the field and begin their march a cell along their way off the surface
    /// they leave, past a wall standing nearer.
    /// </summary>
    public static void ForAWallThinnerThanACell() => Set();

    /// <summary>
    /// For a test of the light a change of the scene takes away, where the split's rays shade what
    /// they meet with the grid's bounce, which takes frames to settle after the change.
    /// </summary>
    public static void ForTheGridBouncesLag() => Set();

    /// <summary>
    /// For a test of the grid's own probes and cascades, or of a bound measured under the grid.
    /// </summary>
    public static void ForTheGridsOwnReading() => Set();

    /// <summary>For a test the split fails by a cause not yet read.</summary>
    public static void ForAnUnreadCause() => Set();

    private static void Set() =>
        GetApp().World.GetOrInsertResource(static () => new GlobalIlluminationSettings()).Layout = ProbeLayout.Dense;
}
