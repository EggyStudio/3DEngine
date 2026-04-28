using System.Numerics;

namespace Engine;

/// <summary>
/// <see cref="ISceneReader"/> for OpenUSD (<c>.usd / .usda / .usdc</c>). Opens a
/// <c>UsdStage</c> via the bundled UniversalSceneDescription bindings, walks the prim
/// hierarchy, and emits a normalized engine <see cref="Scene"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Normalization performed during read:</b>
/// <list type="bullet">
///   <item><description>
///     <b>Coordinate system:</b> if the stage's <c>upAxis</c> is Z (USD default for many DCCs)
///     and the import target is Y-up, apply a single root-level basis change
///     (rotate by -90° around X, swap Y and Z). All descendant local transforms stay untouched.
///   </description></item>
///   <item><description>
///     <b>Units:</b> read <c>UsdGeomGetStageMetersPerUnit(stage)</c> and rescale root translation
///     / mesh vertex data by <c>sourceMetersPerUnit / targetMetersPerUnit</c>.
///   </description></item>
///   <item><description>
///     <b>Composition:</b> when <see cref="SceneImportSettings.FlattenComposition"/> is <c>true</c>
///     (the runtime default), references / payloads / variant selections are resolved by USD's
///     own composition engine before traversal - we just walk the resulting cached prim tree.
///   </description></item>
/// </list>
/// </para>
/// <para>
/// <b>Threading:</b> called on an <see cref="AssetServer"/> background worker. The native
/// <c>UsdStage</c> is opened, traversed, and disposed entirely on this thread; the returned
/// <see cref="Scene"/> contains only managed value types so it can cross threads safely.
/// </para>
/// <para>
/// <b>Stub status:</b> the conversion pipeline below is intentionally a placeholder. Real prim
/// traversal (<c>UsdPrim</c> → <see cref="SceneNode"/>, <c>UsdGeomXformable.GetLocalTransformation</c>
/// → <see cref="Transform"/>, <c>UsdGeomMesh</c> → mesh + material payloads) lands as the USD
/// integration matures (see roadmap "Scene graph and serialization (USD integration)").
/// </para>
/// </remarks>
public sealed class UsdSceneReader : ISceneReader
{
    private static readonly ILogger Logger = Log.Category("Engine.Scenes.Usd");

    /// <inheritdoc />
    public string[] Extensions => [".usd", ".usda", ".usdc"];

    /// <inheritdoc />
    public string FormatId => "usd";

    /// <inheritdoc />
    public Task<Scene> ReadAsync(AssetLoadContext context, SceneImportSettings settings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        // TODO: Real USD ingestion.
        //
        //   using var stage = UniversalSceneDescription.UsdStage.Open(context.Path.ToString());
        //   var sourceMpu  = pxr.UsdGeomGetStageMetersPerUnit(stage);
        //   var sourceUp   = pxr.UsdGeomGetStageUpAxis(stage);  // "Y" or "Z"
        //   var basisFix   = ComputeBasisChange(sourceUp, settings.TargetCoordinateSystem);
        //   var unitScale  = (float)(sourceMpu / settings.TargetMetersPerUnit);
        //   var scene      = new Scene
        //   {
        //       Name                    = Path.GetFileNameWithoutExtension(context.Path.ToString()),
        //       SourceCoordinateSystem  = sourceUp == "Z" ? SceneCoordinateSystem.ZUp : SceneCoordinateSystem.YUp,
        //       SourceMetersPerUnit     = sourceMpu,
        //   };
        //   foreach (var prim in stage.GetPseudoRoot().GetChildren())
        //       scene.Roots.Add(ConvertPrim(prim, basisFix, unitScale));
        //   return scene;
        //
        // For now, hand back an empty scene so the asset pipeline plumbing is exercisable
        // (Handle<SceneAsset>, AssetEvent<SceneAsset>, hot-reload, etc.).

        Logger.Warn($"UsdSceneReader: stub returning empty Scene for '{context.Path}'. Implement prim traversal.");

        var scene = new Scene
        {
            Name = context.Path.ToString(),
            SourceCoordinateSystem = SceneCoordinateSystem.YUp,
            SourceMetersPerUnit = 1.0,
        };
        return Task.FromResult(scene);
    }
}

