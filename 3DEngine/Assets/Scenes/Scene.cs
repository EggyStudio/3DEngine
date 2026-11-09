using System.Numerics;

namespace Engine;

/// <summary>
/// Backend-agnostic, in-memory representation of a scene. Produced by an <see cref="ISceneReader"/>
/// (as <see cref="AssimpModelReader"/>) and consumed by spawn systems that translate it into
/// ECS entities via <c>EcsCommands</c>.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Scene"/> is an <b>immutable snapshot</b> by convention. Once a reader returns one,
/// the spawning side may iterate it freely from any thread. A changed source is read again into
/// a new snapshot rather than changing a <see cref="Scene"/> in place, which keeps the
/// cross-thread contract simple and
/// matches how the <see cref="AssetServer"/> publishes results from background loaders.
/// </para>
/// <para>
/// <b>Coordinate / unit policy:</b> readers <i>preserve</i> the source basis and units rather
/// than per-vertex normalization. <see cref="SourceCoordinateSystem"/> and
/// <see cref="SourceMetersPerUnit"/> are therefore <b>load-bearing</b>, more than diagnostic,
/// since downstream spawn systems (<c>SceneSpawnSystem</c>) apply a single root-level basis-change
/// matrix (axis swap + uniform scale) derived from these fields. Two reasons to do it this way:
/// <list type="bullet">
///   <item><description>
///     The reader stays symmetric with the writer, so a round trip
///     <c>read → write</c> is byte-stable, since vertex data was never rotated or rescaled.
///   </description></item>
///   <item><description>
///     Per-vertex axis swaps lose precision on large stages and would have to be undone
///     by the writer; a single matrix at the spawn root avoids both costs.
///   </description></item>
/// </list>
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

    /// <summary>Images the source file carries inside itself, as a <c>.glb</c> does, which its materials name as <c>*0</c>, <c>*1</c> and so on.</summary>
    public List<SceneEmbeddedTexture> EmbeddedTextures { get; } = new();

    /// <summary>
    /// The embedded image a material's texture path names, or null when the path names a file.
    /// </summary>
    /// <remarks>
    /// A path of <c>*</c> and an index names one by position, as glTF and FBX imports
    /// write it. Some formats name an embedded image by its original file name instead, so a path
    /// whose file name matches one is answered too, as Assimp's own <c>GetEmbeddedTexture</c> does.
    /// </remarks>
    public SceneEmbeddedTexture? FindEmbeddedTexture(string path)
    {
        if (path.StartsWith('*'))
            return int.TryParse(path.AsSpan(1), out var index) && index >= 0 && index < EmbeddedTextures.Count
                ? EmbeddedTextures[index]
                : null;

        var name = System.IO.Path.GetFileName(path.Replace('\\', '/'));
        return EmbeddedTextures.FirstOrDefault(t =>
            t.FileName is { Length: > 0 } file && string.Equals(System.IO.Path.GetFileName(file.Replace('\\', '/')), name, StringComparison.OrdinalIgnoreCase));
    }

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
