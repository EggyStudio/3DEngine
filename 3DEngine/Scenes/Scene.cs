using System.Numerics;

namespace Engine;

/// <summary>
/// Backend-agnostic, in-memory representation of a scene. Produced by an <see cref="ISceneReader"/>
/// (e.g. <c>UsdSceneReader</c>) and consumed by spawn systems that translate it into ECS entities
/// via <c>EcsCommands</c>.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Scene"/> is an <b>immutable snapshot</b> by convention: once a reader returns one,
/// the spawning side may iterate it freely from any thread. Mutating editor sessions should
/// re-author the source (e.g. live <c>UsdStage</c>) and re-emit a new snapshot, instead of
/// mutating <see cref="Scene"/> in place. This keeps the cross-thread contract simple and
/// matches how the <see cref="AssetServer"/> publishes results from background loaders.
/// </para>
/// <para>
/// <b>Coordinate / unit policy:</b> readers <i>preserve</i> the source basis and units rather
/// than per-vertex normalization. <see cref="SourceCoordinateSystem"/> and
/// <see cref="SourceMetersPerUnit"/> are therefore <b>load-bearing</b>, not just diagnostic:
/// downstream spawn systems (<c>SceneSpawnSystem</c>) apply a single root-level basis-change
/// matrix (axis swap + uniform scale) derived from these fields. Two reasons to do it this way:
/// <list type="bullet">
///   <item><description>
///     The reader stays symmetric with the writer - a round-trip
///     <c>read → write</c> is byte-stable, since vertex data was never rotated or rescaled.
///   </description></item>
///   <item><description>
///     Per-vertex axis swaps lose precision on large stages and would have to be undone
///     by the writer; a single matrix at the spawn root avoids both costs.
///   </description></item>
/// </list>
/// This matches Omniverse's convention of treating USD as the source of truth.
/// </para>
/// </remarks>
/// <seealso cref="SceneNode"/>
/// <seealso cref="SceneAsset"/>
/// <seealso cref="ISceneReader"/>
public sealed class Scene
{
    /// <summary>Logical name (often the source file stem). Diagnostic only.</summary>
    public string Name { get; init; } = "Scene";

    /// <summary>Top-level nodes. Each node owns its own children recursively via <see cref="SceneNode.Children"/>.</summary>
    public List<SceneNode> Roots { get; } = new();

    /// <summary>
    /// Coordinate system the scene was authored in. <b>Load-bearing</b>: spawn systems
    /// derive the root-level basis-change matrix from this value (cf. type-level remarks).
    /// </summary>
    public SceneCoordinateSystem SourceCoordinateSystem { get; init; } = SceneCoordinateSystem.YUp;

    /// <summary>
    /// Source <c>metersPerUnit</c> as authored on the stage (e.g. <c>0.01</c> for centimeters,
    /// <c>1.0</c> for meters). <b>Load-bearing</b>: spawn systems multiply this into the
    /// root-level scale so vertex data stays in source units while the world ends up in meters.
    /// </summary>
    public double SourceMetersPerUnit { get; init; } = 1.0;

    /// <summary>Depth-first enumeration of every node in the scene.</summary>
    public IEnumerable<SceneNode> Traverse()
    {
        foreach (var root in Roots)
        {
            foreach (var n in TraverseRecursive(root))
                yield return n;
        }
    }

    private static IEnumerable<SceneNode> TraverseRecursive(SceneNode node)
    {
        yield return node;
        foreach (var c in node.Children)
            foreach (var n in TraverseRecursive(c))
                yield return n;
    }
}

/// <summary>Coordinate system convention for a source scene (engine canonical is Y-up).</summary>
public enum SceneCoordinateSystem
{
    /// <summary>Right-handed, Y-up (engine canonical).</summary>
    YUp,
    /// <summary>Right-handed, Z-up (common for USD, Blender, Unreal).</summary>
    ZUp,
}