using System.Numerics;

namespace Engine;

/// <summary>
/// Backend-agnostic skeleton (joint hierarchy) payload attached to a <see cref="SceneNode"/>
/// via <see cref="SceneNode.Components"/>. Produced by skinned model importers
/// (glTF <c>skins</c>, FBX bones, COLLADA controllers, ...) and consumed at spawn time by
/// systems that translate skinned meshes to the renderer.
/// </summary>
/// <remarks>
/// <para>
/// Backend-agnostic, like the other <c>Scene*Payload</c> types in this folder. The
/// model-import backends (<c>Engine.Models.Assimp</c>, <c>Engine.Models.Gltf</c>)
/// populate it today; a future UsdSkel pass in <c>Engine.Scenes.Usd</c> can attach the
/// same payload type so consumers stay format-agnostic.
/// </para>
/// <para>
/// <b>Layout:</b> <see cref="JointNames"/>, <see cref="ParentIndices"/> and
/// <see cref="InverseBindMatrices"/> are parallel arrays indexed by joint id (the same
/// indices used by <see cref="SceneSkinPayload.JointIndices"/>). Roots have parent
/// <c>-1</c>. <see cref="LocalBindTransforms"/> is the joint's local pose at bind time
/// (relative to its parent); spawners that build a runtime skeleton can derive world-space
/// bind poses from it without inverting the bind matrices.
/// </para>
/// </remarks>
/// <seealso cref="SceneSkinPayload"/>
/// <seealso cref="SceneAnimationPayload"/>
public sealed class SceneSkeletonPayload
{
    /// <summary>Display name (typically the source skeleton / armature node name).</summary>
    public string Name { get; init; } = "Skeleton";

    /// <summary>Joint display names indexed by joint id. Required, never null.</summary>
    public required string[] JointNames { get; init; }

    /// <summary>
    /// Parent joint id for each joint, or <c>-1</c> for roots. Length matches
    /// <see cref="JointNames"/>. Joints are stored in topological order: a joint's parent
    /// always appears earlier in the array.
    /// </summary>
    public required int[] ParentIndices { get; init; }

    /// <summary>
    /// Inverse bind matrix per joint (transforms a vertex from mesh space into joint
    /// local space at bind time). Length matches <see cref="JointNames"/>. Equivalent to
    /// glTF <c>skin.inverseBindMatrices</c>; for Assimp this is <c>aiBone.OffsetMatrix</c>.
    /// </summary>
    public required Matrix4x4[] InverseBindMatrices { get; init; }

    /// <summary>
    /// Local-space bind pose for each joint relative to its parent. May be empty when
    /// the source format only authored inverse bind matrices and the importer chose not
    /// to derive locals.
    /// </summary>
    public Transform[] LocalBindTransforms { get; init; } = Array.Empty<Transform>();
}