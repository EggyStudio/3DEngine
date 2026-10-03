namespace Engine;

/// <summary>
/// <see cref="IAssetWatcher"/> implementation backed by a <see cref="FileWatcher"/>.
/// Translates filesystem change events into <see cref="AssetPath"/>s for the <see cref="AssetServer"/>.
/// </summary>
/// <remarks>
/// Created internally by <see cref="FileAssetReader.CreateWatcher"/>.
/// Watches all files recursively in the asset base directory.
/// </remarks>
/// <seealso cref="IAssetWatcher"/>
/// <seealso cref="FileAssetReader"/>
/// <seealso cref="FileWatcher"/>
internal sealed class FileAssetWatcher : IAssetWatcher
{
    private static readonly ILogger Logger = Log.Category("Engine.Assets.FileWatcher");

    private readonly FileWatcher _watcher;

    /// <inheritdoc />
    public event Action<AssetPath[]>? AssetsChanged;

    /// <summary>Creates a new asset watcher for the specified directory.</summary>
    /// <param name="baseDirectory">The root asset directory to watch.</param>
    public FileAssetWatcher(string baseDirectory)
    {
        _watcher = new FileWatcher(baseDirectory)
            .WithRecursive(true)
            .WithDebounce(TimeSpan.FromMilliseconds(300));

        _watcher.Changed += OnFilesChanged;
    }

    /// <inheritdoc />
    public void Start()
    {
        Logger.Info("FileAssetWatcher starting...");
        _watcher.Start();
    }

    /// <inheritdoc />
    public void Stop()
    {
        Logger.Info("FileAssetWatcher stopping...");
        _watcher.Stop();
    }

    /// <inheritdoc />
    public void Dispose() => _watcher.Dispose();

    private void OnFilesChanged(FileChangedEvent[] events)
    {
        // Filter out directories, convert to AssetPaths
        var assetPaths = new List<AssetPath>();
        foreach (var e in events)
        {
            // Skip directories
            if (Directory.Exists(e.FilePath)) continue;

            assetPaths.Add(new AssetPath(e.RelativePath));
        }

        if (assetPaths.Count > 0)
        {
            Logger.Debug($"Asset changes detected: {assetPaths.Count} file(s)");
            AssetsChanged?.Invoke(assetPaths.ToArray());
        }
    }
}
