namespace Engine;

/// <summary>
/// Default <see cref="IAssetReader"/> that reads assets from a base directory on disk.
/// Supports watching via <see cref="FileAssetWatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// The base directory defaults to <c>{AppContext.BaseDirectory}/source</c> but can be configured
/// via the constructor. All asset paths are resolved relative to this base directory.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var reader = new FileAssetReader("/path/to/my/assets");
/// if (reader.Exists(new AssetPath("textures/ground.png")))
/// {
///     using var stream = await reader.ReadAsync(new AssetPath("textures/ground.png"));
///     // process bytes
/// }
/// </code>
/// </example>
/// <seealso cref="IAssetReader"/>
/// <seealso cref="FileAssetWatcher"/>
/// <seealso cref="AssetServer"/>
public sealed class FileAssetReader : IAssetReader
{
    private static readonly ILogger Logger = Log.Category("Engine.Assets.FileReader");

    /// <summary>The root directory all asset paths are resolved relative to.</summary>
    public string BaseDirectory { get; }

    /// <summary>
    /// Creates a new <see cref="FileAssetReader"/> rooted at the specified directory.
    /// </summary>
    /// <param name="baseDirectory">
    /// The root directory for asset resolution. If <c>null</c> or empty, defaults to
    /// <c>{AppContext.BaseDirectory}/source</c>.
    /// </param>
    public FileAssetReader(string? baseDirectory = null)
    {
        BaseDirectory = string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.Combine(AppContext.BaseDirectory, "source")
            : Path.GetFullPath(baseDirectory);

        Logger.Debug($"FileAssetReader initialized: {BaseDirectory}");
    }

    /// <summary>Resolves an <see cref="AssetPath"/> to an absolute filesystem path.</summary>
    /// <param name="path">The asset path to resolve.</param>
    /// <returns>The absolute path on disk.</returns>
    public string ResolvePath(AssetPath path) => Path.Combine(BaseDirectory, path.Path.Replace('/', Path.DirectorySeparatorChar));

    /// <inheritdoc />
    public bool Exists(AssetPath path) => File.Exists(ResolvePath(path));

    /// <inheritdoc />
    public Task<Stream> ReadAsync(AssetPath path, CancellationToken ct = default)
    {
        string fullPath = ResolvePath(path);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Asset file not found: {fullPath}", fullPath);

        Logger.Debug($"Reading asset from disk: {fullPath}");
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public IAssetWatcher? CreateWatcher()
    {
        if (!Directory.Exists(BaseDirectory))
        {
            Logger.Warn($"Cannot create watcher - directory does not exist: {BaseDirectory}");
            return null;
        }

        return new FileAssetWatcher(BaseDirectory);
    }
}
