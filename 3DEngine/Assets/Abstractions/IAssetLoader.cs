namespace Engine;

/// <summary>
/// Defines how to load an asset of type <typeparamref name="T"/> from raw bytes.
/// Implement this interface to add support for a new asset format.
/// </summary>
/// <typeparam name="T">The type of asset this loader produces.</typeparam>
/// <remarks>
/// <para>
/// Loaders are registered with the <see cref="AssetServer"/> and matched by file extension.
/// The <see cref="LoadAsync"/> method is called on a background thread; it receives an
/// <see cref="AssetLoadContext"/> containing the byte stream, path, and methods for loading
/// sub-assets / dependencies.
/// </para>
/// <para>
/// A single file may produce multiple sub-assets (e.g. a GLTF file containing meshes, materials,
/// textures). Use <see cref="AssetLoadResult{T}"/> to return the primary asset plus labeled sub-assets.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class TextureLoader : IAssetLoader&lt;Texture&gt;
/// {
///     public string[] Extensions => [".png", ".jpg", ".bmp"];
///
///     public async Task&lt;AssetLoadResult&lt;Texture&gt;&gt; LoadAsync(AssetLoadContext ctx, CancellationToken ct)
///     {
///         var bytes = await ctx.ReadAllBytesAsync(ct);
///         var texture = Texture.FromBytes(bytes);
///         return AssetLoadResult.Ok(texture);
///     }
/// }
/// </code>
/// </example>
/// <seealso cref="AssetServer"/>
/// <seealso cref="AssetLoadContext"/>
/// <seealso cref="AssetLoadResult{T}"/>
public interface IAssetLoader<T>
{
    /// <summary>
    /// File extensions this loader handles, including the leading dot.
    /// E.g. <c>[".png", ".jpg"]</c>, <c>[".glsl"]</c>, <c>[".gltf", ".glb"]</c>.
    /// </summary>
    string[] Extensions { get; }

    /// <summary>
    /// Loads an asset from the provided context. Called on a background thread.
    /// </summary>
    /// <param name="context">
    /// Provides the byte stream, asset path, and methods for loading sub-assets or dependencies.
    /// </param>
    /// <param name="ct">Cancellation token honoured for cooperative cancellation.</param>
    /// <returns>The load result containing the asset data (or error).</returns>
    Task<AssetLoadResult<T>> LoadAsync(AssetLoadContext context, CancellationToken ct);
}

/// <summary>
/// Type-erased loader interface used internally by the <see cref="AssetServer"/>
/// to store loaders in a single dictionary.
/// </summary>
internal interface IAssetLoaderUntyped
{
    /// <summary>The asset type this loader produces.</summary>
    Type AssetType { get; }

    /// <summary>File extensions this loader handles.</summary>
    string[] Extensions { get; }

    /// <summary>Loads an asset and returns the result as a boxed object.</summary>
    Task<AssetLoadResultUntyped> LoadUntypedAsync(AssetLoadContext context, CancellationToken ct);
}

/// <summary>
/// Adapter wrapping <see cref="IAssetLoader{T}"/> to implement <see cref="IAssetLoaderUntyped"/>.
/// </summary>
internal sealed class AssetLoaderAdapter<T> : IAssetLoaderUntyped
{
    private readonly IAssetLoader<T> _inner;

    public AssetLoaderAdapter(IAssetLoader<T> inner) => _inner = inner;

    public Type AssetType => typeof(T);
    public string[] Extensions => _inner.Extensions;

    public async Task<AssetLoadResultUntyped> LoadUntypedAsync(AssetLoadContext context, CancellationToken ct)
    {
        var result = await _inner.LoadAsync(context, ct);
        if (!result.Success)
            return AssetLoadResultUntyped.Fail(result.Error!);

        return new AssetLoadResultUntyped
        {
            Asset = result.Asset!,
            SubAssets = result.SubAssets,
            Dependencies = result.Dependencies,
            Success = true,
        };
    }
}
