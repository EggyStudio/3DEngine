namespace Engine;

/// <summary>
/// Notification interface for asset source changes. Used by <see cref="AssetServer"/>
/// to trigger hot-reload when files change on disk.
/// </summary>
/// <seealso cref="IAssetReader"/>
/// <seealso cref="FileWatcher"/>
public interface IAssetWatcher : IDisposable
{
    /// <summary>
    /// Fired when one or more assets in the source have changed.
    /// The event provides the relative paths of changed assets.
    /// </summary>
    event Action<AssetPath[]> AssetsChanged;

    /// <summary>Starts watching for changes.</summary>
    void Start();

    /// <summary>Stops watching for changes.</summary>
    void Stop();
}
