using System.Numerics;

namespace Engine;

/// <summary>
/// Backend-agnostic point-instancer payload mirroring <c>UsdGeomPointInstancer</c>:
/// a set of prototypes (referenced by sub-tree path) plus per-instance prototype indices
/// and pre-composed local-to-parent matrices. Carried lazily on a <see cref="SceneNode"/>
/// so consumers can decide whether to materialize one ECS entity per instance, hand the
/// data off to a future GPU instancing path, or skip entirely.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why pre-compose the matrices:</b> USD authors instances as separate
/// <c>positions</c> / <c>orientations</c> / <c>scales</c> primvars. Recomposing them on
/// every spawn / re-render is wasteful, and the spawn system needs the matrix form anyway
/// (basis change happens at the parent's transform). The reader composes once.
/// </para>
/// <para>
/// <b>v1 behavior:</b> the spawn system logs and skips this payload (no entities spawned).
/// Expanding into either per-instance ECS entities or a GPU instance buffer is a follow-up
/// ticket; carrying the data losslessly here keeps that work decoupled from the reader.
/// </para>
/// </remarks>
public sealed class SceneInstancingPayload
{
    /// <summary>Display name (typically the instancer prim's leaf name).</summary>
    public string Name { get; init; } = "PointInstancer";

    /// <summary>
    /// Source prim path of the prototypes container (the instancer's
    /// <c>prototypes</c> relationship target). Prototype sub-prims live underneath
    /// this path; <see cref="ProtoIndices"/> entries index into the ordered list of
    /// prototype targets at read time.
    /// </summary>
    public required string PrototypeRoot { get; init; }

    /// <summary>
    /// One entry per instance, indexing into the instancer's prototype list. Length
    /// matches <see cref="InstanceTransforms"/> and (when present) <see cref="InstanceIds"/>.
    /// </summary>
    public required int[] ProtoIndices { get; init; }

    /// <summary>
    /// Local-space transform per instance (relative to the instancer node's parent).
    /// Pre-composed by the reader from the source <c>positions</c>, <c>orientations</c>
    /// and <c>scales</c> primvars in TRS order (matches the USD evaluation rule).
    /// </summary>
    public required Matrix4x4[] InstanceTransforms { get; init; }

    /// <summary>
    /// Optional stable per-instance identifier (<c>UsdGeomPointInstancer.ids</c>).
    /// When <c>null</c> the implicit array index is the identifier.
    /// </summary>
    public int[]? InstanceIds { get; init; }
}