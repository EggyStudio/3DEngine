namespace Engine;

/// <summary>
/// Asset wrapper around a <see cref="Scene"/>, suitable for storage in <c>Assets&lt;SceneAsset&gt;</c>
/// and reference via <c>Handle&lt;SceneAsset&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// Produced by an <see cref="ISceneReader"/>, as <see cref="AssimpModelReader"/>, and stored by
/// the <see cref="AssetServer"/>. Carries the immutable scene snapshot and where it came from,
/// which hot reload and writing back to the source format read.
/// </para>
/// <para>
/// <b>A snapshot:</b> <see cref="SceneAsset"/> is a flattened, immutable snapshot, so a
/// background loader can hand it to the main thread safely.
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