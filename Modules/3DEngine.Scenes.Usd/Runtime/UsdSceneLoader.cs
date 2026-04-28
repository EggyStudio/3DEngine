namespace Engine;

/// <summary>
/// <see cref="IAssetLoader{T}"/> that loads USD files (<c>.usd / .usda / .usdc</c>) into a
/// <see cref="SceneAsset"/>. Delegates the actual stage parsing to <see cref="UsdSceneReader"/>
/// so command-line / editor consumers can reuse the same conversion logic.
/// </summary>
/// <remarks>
/// <para>
/// Registered with the <see cref="AssetServer"/> by <see cref="UsdScenesPlugin"/>. Once active
/// you can do:
/// <code>
/// Handle&lt;SceneAsset&gt; sceneHandle = server.Load&lt;SceneAsset&gt;("scenes/sponza.usda");
/// </code>
/// and the resulting <see cref="SceneAsset"/> will be published to <c>Assets&lt;SceneAsset&gt;</c>
/// after the background worker completes (with an <c>AssetEvent&lt;SceneAsset&gt;.Added</c>).
/// </para>
/// <para>
/// <b>Stub status:</b> this class wires the plumbing end-to-end but the USD-to-<see cref="Scene"/>
/// translation in <see cref="UsdSceneReader.ReadAsync"/> is still a TODO - it currently produces
/// an empty <see cref="Scene"/> with the source path set. Implement the prim traversal
/// (<c>UsdPrim</c> → <see cref="SceneNode"/>, <c>UsdGeomXformable</c> → <see cref="Transform"/>,
/// <c>UsdGeomMesh</c> → mesh + material payloads) in that file.
/// </para>
/// </remarks>
/// <seealso cref="UsdScenesPlugin"/>
/// <seealso cref="UsdSceneReader"/>
/// <seealso cref="IAssetLoader{T}"/>
public sealed class UsdSceneLoader : IAssetLoader<SceneAsset>
{
    private readonly UsdSceneReader _reader = new();

    /// <inheritdoc />
    public string[] Extensions => [".usd", ".usda", ".usdc"];

    /// <inheritdoc />
    public async Task<AssetLoadResult<SceneAsset>> LoadAsync(AssetLoadContext context, CancellationToken ct)
    {
        try
        {
            var scene = await _reader.ReadAsync(context, SceneImportSettings.Default, ct);
            var asset = new SceneAsset
            {
                Scene = scene,
                SourcePath = context.Path.ToString(),
                SourceFormat = _reader.FormatId,
            };
            return AssetLoadResult<SceneAsset>.Ok(asset);
        }
        catch (Exception ex)
        {
            return AssetLoadResult<SceneAsset>.Fail($"USD scene load failed for '{context.Path}': {ex.Message}");
        }
    }
}

