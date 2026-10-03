namespace Engine;

public sealed partial class AssetServer
{
    /// <summary>
    /// Adds an asset source with an optional label. Sources are probed in registration order.
    /// </summary>
    /// <param name="reader">The asset reader to add.</param>
    /// <param name="label">An optional human-readable label for diagnostics.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public AssetServer AddSource(IAssetReader reader, string? label = null)
    {
        label ??= reader.GetType().Name;
        _sources.Add((label, reader));
        Logger.Info($"Asset source added: '{label}' ({reader.GetType().Name})");
        return this;
    }

    /// <summary>
    /// Registers a typed asset loader. The loader handles all file extensions declared
    /// by <see cref="IAssetLoader{T}.Extensions"/>.
    /// </summary>
    /// <typeparam name="T">The asset type the loader produces.</typeparam>
    /// <param name="loader">The loader implementation.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public AssetServer RegisterLoader<T>(IAssetLoader<T> loader)
    {
        var adapter = new AssetLoaderAdapter<T>(loader);
        foreach (string ext in loader.Extensions)
        {
            string normalized = ext.StartsWith('.') ? ext : $".{ext}";
            _loaders[normalized] = adapter;
            Logger.Debug($"Loader registered: {normalized} → {loader.GetType().Name} → {typeof(T).Name}");
        }
        return this;
    }
}
