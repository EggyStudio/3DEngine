namespace Engine;

public sealed partial class AssetServer
{
    /// <summary>
    /// Begins loading an asset from the given path. Returns a handle immediately;
    /// the actual load happens on a background thread. Deduplicates by path.
    /// </summary>
    /// <typeparam name="T">The expected asset type.</typeparam>
    /// <param name="path">
    /// Relative asset path, optionally with a label (e.g. <c>"models/tree.gltf#Mesh0"</c>).
    /// </param>
    /// <returns>A strong <see cref="Handle{T}"/> that will resolve once loading completes.</returns>
    /// <exception cref="InvalidOperationException">No loader registered for the file extension.</exception>
    public Handle<T> Load<T>(string path)
    {
        var assetPath = AssetPath.Parse(path);
        return Load<T>(assetPath);
    }

    /// <summary>
    /// Begins loading an asset from the given <see cref="AssetPath"/>.
    /// </summary>
    /// <typeparam name="T">The expected asset type.</typeparam>
    /// <param name="path">The asset path.</param>
    /// <returns>A strong <see cref="Handle{T}"/>.</returns>
    public Handle<T> Load<T>(AssetPath path)
    {
        string key = path.ToString();

        // Deduplication: return existing handle if already requested
        if (_pathToId.TryGetValue(key, out var existing))
        {
            var handle = new Handle<T>(existing.Id, path, strong: true);
            HandleRefCounts.Increment(existing.Id);
            Logger.Debug($"Load (deduplicated): {path} → {existing.Id}");
            return handle;
        }

        // Validate loader exists
        string ext = path.Extension;
        if (!_loaders.ContainsKey(ext))
            throw new InvalidOperationException($"No asset loader registered for extension '{ext}'. Path: {path}");

        // Allocate ID and register
        var id = AssetId.Next();
        _pathToId[key] = (id, typeof(T));
        _states[id] = LoadState.Loading;
        _idToPath[id] = path;
        HandleRefCounts.Increment(id);

        var handleResult = new Handle<T>(id, path, strong: true);

        // Enqueue for background loading
        _loadQueue.Writer.TryWrite(new LoadRequest(id, path, typeof(T)));
        Logger.Debug($"Load enqueued: {path} → {id}");

        return handleResult;
    }

    /// <summary>
    /// Synchronously loads an asset, blocking the calling thread until the load completes.
    /// Use sparingly - prefer async <see cref="Load{T}(string)"/> in most cases.
    /// </summary>
    /// <typeparam name="T">The expected asset type.</typeparam>
    /// <param name="path">Relative asset path.</param>
    /// <returns>The loaded asset.</returns>
    /// <exception cref="InvalidOperationException">Load failed or no loader registered.</exception>
    public T LoadSync<T>(string path)
    {
        var assetPath = AssetPath.Parse(path);
        string ext = assetPath.Extension;

        if (!_loaders.TryGetValue(ext, out var loader))
            throw new InvalidOperationException($"No asset loader registered for extension '{ext}'. Path: {path}");

        // Find the stream
        Stream? stream = null;
        foreach (var (_, reader) in _sources)
        {
            if (!reader.Exists(assetPath)) continue;
            stream = reader.ReadAsync(assetPath, CancellationToken.None).GetAwaiter().GetResult();
            break;
        }

        if (stream is null)
            throw new FileNotFoundException($"Asset not found in any source: {path}");

        using var ctx = new AssetLoadContext(stream, assetPath, depPath =>
        {
            // Synchronous dependency tracking: just allocate an ID
            string depKey = depPath.ToString();
            if (_pathToId.TryGetValue(depKey, out var dep))
                return dep.Id;
            var depId = AssetId.Next();
            _pathToId[depKey] = (depId, typeof(object));
            _states[depId] = LoadState.NotLoaded;
            _idToPath[depId] = depPath;
            return depId;
        });

        var result = loader.LoadUntypedAsync(ctx, CancellationToken.None).GetAwaiter().GetResult();
        if (!result.Success || result.Asset is null)
            throw new InvalidOperationException($"Failed to load asset '{path}': {result.Error ?? "Unknown error"}");

        if (result.Asset is not T typedAsset)
            throw new InvalidCastException($"Asset '{path}' loaded as {result.Asset.GetType().Name} but expected {typeof(T).Name}");

        // Store in tracking
        var id = AssetId.Next();
        string keyStr = assetPath.ToString();
        _pathToId[keyStr] = (id, typeof(T));
        _states[id] = LoadState.Loaded;
        _idToPath[id] = assetPath;

        Logger.Debug($"LoadSync completed: {path} → {id}");
        return typedAsset;
    }

    /// <summary>Gets the current load state of an asset.</summary>
    /// <param name="id">The asset ID to query.</param>
    /// <returns>The current <see cref="LoadState"/>.</returns>
    public LoadState GetLoadState(AssetId id) =>
        _states.GetValueOrDefault(id, LoadState.NotLoaded);

    /// <summary>Gets the current load state of a handle.</summary>
    /// <typeparam name="T">The asset type.</typeparam>
    /// <param name="handle">The handle to query.</param>
    /// <returns>The current <see cref="LoadState"/>.</returns>
    public LoadState GetLoadState<T>(Handle<T> handle) => 
        GetLoadState(handle.Id);

    /// <summary>Returns <c>true</c> when the asset and all its dependencies are loaded.</summary>
    /// <param name="id">The asset ID.</param>
    public bool IsLoadedWithDependencies(AssetId id)
    {
        if (GetLoadState(id) != LoadState.Loaded) return false;
        if (!_dependencies.TryGetValue(id, out var deps)) return true;
        foreach (var dep in deps)
        {
            if (GetLoadState(dep) != LoadState.Loaded) 
                return false;
        }
        return true;
    }
}
