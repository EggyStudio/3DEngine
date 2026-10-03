namespace Engine;

public sealed partial class AssetServer
{
    private async Task WorkerLoop(int workerId, CancellationToken ct)
    {
        Logger.Debug($"Asset worker #{workerId} started.");
        try
        {
            await foreach (var request in _loadQueue.Reader.ReadAllAsync(ct))
            {
                try
                {
                    var result = await ExecuteLoad(request, ct);
                    _completedLoads.Enqueue(result);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _completedLoads.Enqueue(new CompletedLoad
                    {
                        Id = request.Id,
                        Path = request.Path,
                        AssetType = request.AssetType,
                        Success = false,
                        Error = ex.Message,
                    });
                }
            }
        }
        catch (OperationCanceledException) { /* expected on shutdown */ }
        Logger.Debug($"Asset worker #{workerId} stopped.");
    }

    private async Task<CompletedLoad> ExecuteLoad(LoadRequest request, CancellationToken ct)
    {
        string ext = request.Path.Extension;
        if (!_loaders.TryGetValue(ext, out var loader))
        {
            return new CompletedLoad
            {
                Id = request.Id,
                Path = request.Path,
                AssetType = request.AssetType,
                Success = false,
                Error = $"No loader for extension '{ext}'",
            };
        }

        // Find stream from sources
        Stream? stream = null;
        foreach (var (_, reader) in _sources)
        {
            if (!reader.Exists(request.Path)) continue;
            stream = await reader.ReadAsync(request.Path, ct);
            break;
        }

        if (stream is null)
        {
            return new CompletedLoad
            {
                Id = request.Id,
                Path = request.Path,
                AssetType = request.AssetType,
                Success = false,
                Error = $"Asset not found in any source: {request.Path}",
            };
        }

        // Track dependencies loaded by this asset
        var dependencies = new List<AssetPath>();
        using var ctx = new AssetLoadContext(stream, request.Path, depPath =>
        {
            dependencies.Add(depPath);
            // Ensure the dependency is queued for loading
            string depKey = depPath.ToString();
            if (_pathToId.TryGetValue(depKey, out var dep))
                return dep.Id;
            var depId = AssetId.Next();
            _pathToId[depKey] = (depId, typeof(object));
            _states[depId] = LoadState.Loading;
            _idToPath[depId] = depPath;
            _loadQueue.Writer.TryWrite(new LoadRequest(depId, depPath, typeof(object)));
            return depId;
        });

        var result = await loader.LoadUntypedAsync(ctx, ct);

        if (!result.Success)
        {
            return new CompletedLoad
            {
                Id = request.Id,
                Path = request.Path,
                AssetType = request.AssetType,
                Success = false,
                Error = result.Error,
            };
        }

        Logger.Debug($"Asset loaded: {request.Path} → {request.Id} ({request.AssetType.Name})");

        return new CompletedLoad
        {
            Id = request.Id,
            Path = request.Path,
            AssetType = request.AssetType,
            Asset = result.Asset,
            SubAssets = result.SubAssets,
            Dependencies = dependencies.Count > 0 ? dependencies : null,
            Success = true,
            IsReload = request.IsReload,
        };
    }
}
