namespace Engine;

/// <summary>
/// ECS marker component attached to every entity spawned from a <see cref="SceneNode"/>
/// by <c>SceneSpawnSystem</c>. Records the source provenance so editor tools (selection,
/// hot-reload diff, "ping in stage") can map an entity back to its authoring prim.
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally a tiny <c>struct</c> so it costs nothing to query against: spawn
/// systems iterate <c>Query&lt;SceneInstance&gt;</c> to find every node-derived entity for
/// re-spawn / despawn passes, and the editor uses it to highlight selections in the
/// scene tree.
/// </para>
/// <para>
/// <see cref="SceneAssetId"/> identifies which loaded <see cref="SceneAsset"/> the entity
/// came from (so multiple stage instances stay disjoint), and <see cref="SourcePath"/>
/// mirrors <see cref="SceneNode.SourcePath"/> for stable cross-format addressing.
/// </para>
/// </remarks>
public struct SceneInstance
{
    /// <summary>
    /// Identifier of the source <see cref="SceneAsset"/> (typically its
    /// <see cref="AssetServer"/> handle id). Disambiguates entities when the same stage
    /// is instanced multiple times in the world.
    /// </summary>
    public ulong SceneAssetId;

    /// <summary>
    /// Stable source-prim path the entity was spawned from
    /// (e.g. <c>"/World/Hero/Body"</c>). Mirrors <see cref="SceneNode.SourcePath"/>.
    /// </summary>
    public string SourcePath;
}