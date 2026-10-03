namespace Engine;

public sealed partial class AssetServer
{
    /// <summary>Enables file watching for hot-reload on all filesystem sources.</summary>
    public void EnableWatching()
    {
        if (_watchEnabled) return;
        _watchEnabled = true;

        foreach (var (label, reader) in _sources)
        {
            var watcher = reader.CreateWatcher();
            if (watcher is null) continue;

            watcher.AssetsChanged += OnAssetsChanged;
            watcher.Start();
            _watchers.Add(watcher);
            Logger.Info($"Hot-reload watcher started for source: '{label}'");
        }
    }

    /// <summary>Disables file watching.</summary>
    public void DisableWatching()
    {
        _watchEnabled = false;
        foreach (var w in _watchers)
            w.Dispose();
        _watchers.Clear();
    }

    private void OnAssetsChanged(AssetPath[] changedPaths)
    {
        foreach (var path in changedPaths)
        {
            string key = path.ToString();
            if (!_pathToId.TryGetValue(key, out var info)) continue;

            // Re-enqueue load for hot-reload
            _states[info.Id] = LoadState.Loading;
            _loadQueue.Writer.TryWrite(new LoadRequest(info.Id, path, info.AssetType, true));
            Logger.Info($"Hot-reload triggered: {path}");
        }
    }
}
