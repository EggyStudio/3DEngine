namespace Engine;

/// <summary>
/// <see cref="ISceneWriter"/> for OpenUSD. Serializes a <see cref="Scene"/> into a
/// <c>.usd / .usda / .usdc</c> file using the bundled UniversalSceneDescription bindings.
/// </summary>
/// <remarks>
/// <para>
/// Quick-start (mirrors the package's authoring example) for reference once implemented:
/// <code>
/// using UniversalSceneDescription;
/// using pxr;
///
/// using var stage = UsdStage.CreateNew(targetPath);
/// UsdGeomXform.Define(stage,   new SdfPath("/Hello"));
/// UsdGeomSphere.Define(stage,  new SdfPath("/Hello/World"));
/// stage.Save();
/// </code>
/// </para>
/// <para>
/// <b>Stub status:</b> currently a no-op that just creates an empty <c>.usda</c> file (or
/// throws if the bindings aren't initialized). Implement the inverse traversal of
/// <see cref="UsdSceneReader"/>: walk <see cref="Scene.Roots"/>, define a <c>UsdGeomXform</c>
/// per <see cref="SceneNode"/>, write <see cref="SceneNode.LocalTransform"/> via
/// <c>UsdGeomXformable</c> ops, and serialize attached payloads.
/// </para>
/// </remarks>
public sealed class UsdSceneWriter : ISceneWriter
{
    private static readonly ILogger Logger = Log.Category("Engine.Scenes.Usd");

    /// <inheritdoc />
    public string FormatId => "usd";

    /// <inheritdoc />
    public Task WriteAsync(Scene scene, string targetPath, SceneExportSettings settings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        // TODO: Real USD authoring.
        //
        //   using var stage = UniversalSceneDescription.UsdStage.CreateNew(targetPath);
        //   pxr.UsdGeomSetStageUpAxis(stage,
        //       settings.CoordinateSystem == SceneCoordinateSystem.ZUp ? "Z" : "Y");
        //   pxr.UsdGeomSetStageMetersPerUnit(stage, settings.MetersPerUnit);
        //
        //   foreach (var root in scene.Roots)
        //       WritePrim(stage, new pxr.SdfPath("/" + root.Name), root);
        //
        //   stage.Save();

        Logger.Warn($"UsdSceneWriter: stub - no actual stage emitted for '{targetPath}'. Implement prim authoring.");
        return Task.CompletedTask;
    }
}

