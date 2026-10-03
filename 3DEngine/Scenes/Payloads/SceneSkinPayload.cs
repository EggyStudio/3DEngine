namespace Engine;

/// <summary>
/// Per-vertex skinning data attached alongside a <see cref="SceneMeshPayload"/> when the
/// source mesh is bound to a <see cref="SceneSkeletonPayload"/>. Indices into
/// <see cref="JointIndices"/> match the layout of <see cref="SceneMeshPayload.Positions"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Influences:</b> the engine standardises on 4 influences per vertex (the de-facto
/// glTF 2.0 / FBX baseline). Importers that encounter more drop the lowest-weight
/// influences and renormalise; importers with fewer pad the unused slots with weight 0.
/// </para>
/// <para>
/// <b>Indices:</b> <see cref="JointIndices"/> stores 4 byte-sized joint ids per vertex
/// packed into a <see cref="ushort"/>x4 (matching common GPU vertex-attribute layouts).
/// <see cref="JointWeights"/> stores the matching normalised weights (sum ≈ 1.0).
/// </para>
/// </remarks>
/// <seealso cref="SceneSkeletonPayload"/>
/// <seealso cref="SceneMeshPayload"/>
public sealed class SceneSkinPayload
{
    /// <summary>
    /// Source path of the bound <see cref="SceneSkeletonPayload"/>, matched against
    /// the skeleton node's <see cref="SceneNode.SourcePath"/> at spawn time.
    /// </summary>
    public required string SkeletonPath { get; init; }

    /// <summary>
    /// Joint indices per vertex (4 per vertex, flattened). Length is
    /// <c>4 * SceneMeshPayload.Positions.Length</c>.
    /// </summary>
    public required ushort[] JointIndices { get; init; }

    /// <summary>
    /// Joint weights per vertex (4 per vertex, flattened, normalised). Length matches
    /// <see cref="JointIndices"/>.
    /// </summary>
    public required float[] JointWeights { get; init; }
}