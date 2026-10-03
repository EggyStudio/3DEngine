namespace Engine;

/// <summary>
/// Abstraction over the source of raw asset bytes. Implementations read from the filesystem,
/// embedded resources, archives, network, etc.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="AssetServer"/> probes registered readers in priority order when loading an asset.
/// The default <see cref="FileAssetReader"/> reads from a base directory on disk.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class ZipAssetReader : IAssetReader
/// {
///     public bool Exists(AssetPath path) => _archive.EntryExists(path.Path);
///     public Task&lt;Stream&gt; ReadAsync(AssetPath path, CancellationToken ct)
///         => Task.FromResult&lt;Stream&gt;(_archive.Open(path.Path));
///     public IAssetWatcher? CreateWatcher() => null;
/// }
/// </code>
/// </example>
/// <seealso cref="FileAssetReader"/>
/// <seealso cref="EmbeddedAssetReader"/>
/// <seealso cref="AssetServer"/>
public interface IAssetReader
{
    /// <summary>Returns <c>true</c> if the asset exists in this source.</summary>
    /// <param name="path">The asset path to check.</param>
    bool Exists(AssetPath path);

    /// <summary>
    /// Opens the asset for reading. Returns a seekable stream when possible.
    /// The caller is responsible for disposing the stream.
    /// </summary>
    /// <param name="path">The asset path to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A stream containing the raw asset bytes.</returns>
    /// <exception cref="FileNotFoundException">The asset does not exist in this source.</exception>
    Task<Stream> ReadAsync(AssetPath path, CancellationToken ct = default);

    /// <summary>
    /// Creates a watcher for this source that can detect asset changes, or <c>null</c> if
    /// the source does not support watching (e.g. embedded resources, network).
    /// </summary>
    /// <returns>An <see cref="IAssetWatcher"/> or <c>null</c>.</returns>
    IAssetWatcher? CreateWatcher() => null;
}

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
