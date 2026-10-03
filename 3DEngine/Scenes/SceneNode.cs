using System.Numerics;

namespace Engine;

/// <summary>
/// A single node in a <see cref="Scene"/> hierarchy. Carries a local-space <see cref="Transform"/>,
/// optional payload references (mesh, material, light, camera, ...) and a list of child nodes.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SceneNode"/> is intentionally a <b>mutable POCO</b> so backend readers can populate
/// it incrementally during traversal. Once the owning <see cref="Scene"/> is published to the
/// rest of the engine (via <see cref="SceneAsset"/>), the node tree is treated as immutable -
/// callers must not mutate it concurrently with consumers.
/// </para>
/// <para>
/// <b>Transform space:</b> <see cref="LocalTransform"/> is in the parent's local space.
/// World-space transforms are computed by spawn systems while walking the tree (multiplying
/// down the chain), so a single Y-up/Z-up axis swap on the root is enough to normalize the
/// whole hierarchy.
/// </para>
/// <para>
/// <b>Payload bag:</b> the engine doesn't yet have a fixed taxonomy of "components on a scene
/// node" (separate concern from ECS components - <see cref="SceneNode"/> is the *interchange*
/// representation, not the runtime ECS layout). Backends attach typed payloads via
/// <see cref="Components"/>; spawn systems pattern-match on type and translate to ECS.
/// Concrete payload types live alongside their backend (e.g. <c>SceneMeshPayload</c>,
/// <c>SceneCameraPayload</c>) so this module stays format-agnostic.
/// </para>
/// </remarks>
public sealed class SceneNode
{
    /// <summary>Display name (often the leaf segment of the source path, e.g. <c>"World"</c> from <c>"/Hello/World"</c>).</summary>
    public string Name { get; init; } = "Node";

    /// <summary>
    /// Stable source path for diagnostics, hot-reload diffing, and round-tripping
    /// (e.g. <c>"/Hello/World"</c> for USD prims).
    /// </summary>
    public string SourcePath { get; init; } = "/";

    /// <summary>Local-space transform relative to the parent node.</summary>
    public Transform LocalTransform { get; set; } = new() { Rotation = Quaternion.Identity, Scale = Vector3.One };

    /// <summary>Whether this node is enabled (visibility / activation hint for the spawn step).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Authoring-time visibility classification (USD <c>UsdGeomImageable.purpose</c>).
    /// Spawn systems filter against <see cref="SceneImportSettings.IncludePurposes"/>
    /// (or their own <see cref="ScenePurposeMask"/>) before materializing the node.
    /// </summary>
    public ScenePurpose Purpose { get; set; } = ScenePurpose.Default;

    /// <summary>Backend-attached payloads (mesh, material, light, camera, custom...). Spawn systems pattern-match on type.</summary>
    public List<object> Components { get; } = new();

    /// <summary>Child nodes (owned). Parent is implicit via traversal order.</summary>
    public List<SceneNode> Children { get; } = new();

    /// <summary>Convenience: get the first attached component of type <typeparamref name="T"/>, or <c>default</c>.</summary>
    public T? GetComponent<T>() where T : class
    {
        foreach (var c in Components)
            if (c is T t) return t;
        return null;
    }
}