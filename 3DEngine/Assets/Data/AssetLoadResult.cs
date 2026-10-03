namespace Engine;

/// <summary>
/// Result of an <see cref="IAssetLoader{T}.LoadAsync"/> call, containing the loaded asset,
/// optional labeled sub-assets, and dependency information.
/// </summary>
/// <typeparam name="T">The primary asset type.</typeparam>
/// <seealso cref="IAssetLoader{T}"/>
/// <seealso cref="AssetLoadContext"/>
public sealed class AssetLoadResult<T>
{
    /// <summary>The primary loaded asset. <c>default</c> when <see cref="Success"/> is <c>false</c>.</summary>
    public T? Asset { get; init; }

    /// <summary>Whether the load succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Error message when <see cref="Success"/> is <c>false</c>.</summary>
    public string? Error { get; init; }

    /// <summary>
    /// Optional labeled sub-assets produced from the same file (e.g. GLTF meshes, materials).
    /// Keys are labels (e.g. <c>"Mesh0"</c>, <c>"Material0"</c>); values are the sub-asset objects.
    /// </summary>
    public Dictionary<string, object>? SubAssets { get; init; }

    /// <summary>
    /// Asset paths that this asset depends on. The <see cref="AssetServer"/> ensures these
    /// are loaded before firing <see cref="AssetEventKind.LoadedWithDependencies"/>.
    /// </summary>
    public List<AssetPath>? Dependencies { get; init; }

    /// <summary>Creates a successful result with the given asset.</summary>
    /// <param name="asset">The loaded asset.</param>
    /// <returns>A successful result.</returns>
    public static AssetLoadResult<T> Ok(T asset) => new()
    {
        Asset = asset,
        Success = true,
    };

    /// <summary>Creates a successful result with the given asset and sub-assets.</summary>
    /// <param name="asset">The primary loaded asset.</param>
    /// <param name="subAssets">Labeled sub-assets.</param>
    /// <returns>A successful result.</returns>
    public static AssetLoadResult<T> Ok(T asset, Dictionary<string, object> subAssets) => new()
    {
        Asset = asset,
        SubAssets = subAssets,
        Success = true,
    };

    /// <summary>Creates a failed result with an error message.</summary>
    /// <param name="error">The error message.</param>
    /// <returns>A failed result.</returns>
    public static AssetLoadResult<T> Fail(string error) => new()
    {
        Success = false,
        Error = error,
    };
}

/// <summary>
/// Type-erased load result used internally by the <see cref="AssetServer"/>.
/// </summary>
internal sealed class AssetLoadResultUntyped
{
    /// <summary>The loaded asset as a boxed object.</summary>
    public object? Asset { get; init; }

    /// <summary>Whether the load succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Error message on failure.</summary>
    public string? Error { get; init; }

    /// <summary>Labeled sub-assets.</summary>
    public Dictionary<string, object>? SubAssets { get; init; }

    /// <summary>Dependencies of this asset.</summary>
    public List<AssetPath>? Dependencies { get; init; }

    /// <summary>Creates a failed untyped result.</summary>
    public static AssetLoadResultUntyped Fail(string error) => new()
    {
        Success = false,
        Error = error,
    };
}
