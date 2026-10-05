namespace Engine;

/// <summary>
/// <see cref="IAssetLoader{T}"/> that loads any Assimp-supported model file
/// (FBX, OBJ, COLLADA, ...) into a <see cref="SceneAsset"/>. Delegates the actual
/// parsing to <see cref="AssimpModelReader"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Extensions"/> is populated at construction time from
/// <see cref="Assimp.AssimpContext.GetSupportedImportFormats"/> with the USD family
/// (<c>.usd / .usda / .usdc / .usdz</c>) excluded, because Assimp's USD importer reads
/// geometry without materials.
/// </para>
/// </remarks>
/// <seealso cref="AssimpModelPlugin"/>
/// <seealso cref="AssimpModelReader"/>
internal sealed class AssimpModelLoader : IAssetLoader<SceneAsset>
{
    private static readonly ILogger Logger = Log.Category("Engine.Models.Assimp");

    private static readonly HashSet<string> ExcludedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Assimp's USD importer reads geometry only and is experimental upstream, so a .usd
        // file is refused rather than loaded without its materials.
        ".usd", ".usda", ".usdc", ".usdz",
    };

    private readonly AssimpModelReader _reader;

    /// <summary>Creates a loader bound to the given reader.</summary>
    public AssimpModelLoader(AssimpModelReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        Extensions = DiscoverExtensions();
    }

    /// <inheritdoc />
    public string[] Extensions { get; }

    /// <inheritdoc />
    public async Task<AssetLoadResult<SceneAsset>> LoadAsync(AssetLoadContext context, CancellationToken ct)
    {
        try
        {
            var scene = await _reader.ReadAsync(context, SceneImportSettings.Default, ct);
            // A file of the right name and the wrong bytes can read as a scene with nothing in it,
            // as Assimp reads random bytes named .obj, which is refused rather than placed as nothing.
            static bool Holds(SceneNode node) => node.Components.Count > 0 || node.Children.Any(Holds);
            if (!scene.Roots.Any(Holds))
                return AssetLoadResult<SceneAsset>.Fail($"'{context.Path}' holds no meshes, lights or cameras");
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
            return AssetLoadResult<SceneAsset>.Fail($"Assimp model load failed for '{context.Path}': {ex.Message}");
        }
    }

    private static string[] DiscoverExtensions()
    {
        try
        {
            using var ctx = new Assimp.AssimpContext();
            var formats = ctx.GetSupportedImportFormats(); // e.g. ".fbx", ".obj", "*.dae"
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in formats)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                // Assimp returns either ".fbx" or "*.fbx" depending on platform; normalize.
                var ext = raw.TrimStart('*');
                if (!ext.StartsWith('.')) ext = "." + ext;
                if (ExcludedExtensions.Contains(ext)) continue;
                set.Add(ext);
            }
            return set.ToArray();
        }
        catch (Exception ex)
        {
            Logger.Warn($"AssimpModelLoader: GetSupportedImportFormats failed ({ex.Message}); falling back to a built-in baseline list.");
            return new[]
            {
                ".fbx", ".obj", ".dae", ".3ds", ".blend", ".ply", ".stl", ".x",
                ".md2", ".md3", ".md5mesh", ".ase", ".lwo", ".lws", ".ifc", ".ms3d",
                ".cob", ".scn", ".bvh", ".csm", ".xml", ".irrmesh", ".irr", ".ter",
                ".hmp", ".mesh", ".mesh.xml", ".raw", ".off", ".ac", ".ac3d", ".smd",
                ".vta", ".mdl", ".mdc", ".q3o", ".q3s", ".nff", ".pk3", ".ndo",
            };
        }
    }
}