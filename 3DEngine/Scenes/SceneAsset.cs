namespace Engine;

/// <summary>
/// Asset wrapper around a <see cref="Scene"/>, suitable for storage in <c>Assets&lt;SceneAsset&gt;</c>
/// and reference via <c>Handle&lt;SceneAsset&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Produced by <see cref="ISceneReader"/> implementations (e.g. <c>UsdSceneReader</c>) and stored
/// by the <see cref="AssetServer"/>. Carries the immutable scene snapshot plus provenance metadata
/// useful for hot-reload, the editor inspector, and round-tripping back to the source format.
/// </para>
/// <para>
/// <b>Snapshot vs. live-stage:</b> for the runtime, <see cref="SceneAsset"/> is always a flattened,
/// immutable snapshot - safe to publish to the main thread from a background loader. The editor
/// may keep a parallel live <c>UsdStage</c> for non-destructive editing alongside the snapshot
/// (owned by <c>Engine.Scenes.Usd</c>); the asset stays the source of truth for runtime systems.
/// </para>
/// </remarks>
public sealed class SceneAsset
{
    /// <summary>The immutable, normalized scene tree.</summary>
    public required Scene Scene { get; init; }

    /// <summary>Source asset path the scene was loaded from (e.g. <c>"scenes/sponza.usda"</c>), or <c>null</c> for in-memory scenes.</summary>
    public string? SourcePath { get; init; }

    /// <summary>
    /// Identifier of the backend that produced this asset (e.g. <c>"usd"</c>, <c>"gltf"</c>).
    /// Used by <c>ISceneWriter</c> dispatch when round-tripping.
    /// </summary>
    public string SourceFormat { get; init; } = "unknown";
}