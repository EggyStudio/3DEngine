using System.Collections.Concurrent;

namespace Engine;

/// <summary>
/// <see cref="IAssetReader"/> backed by an in-memory <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Designed for synthetic assets that don't live on disk - typically images embedded
/// inside compound files (glTF binary buffers, USDZ archives) that the importing reader
/// has already extracted into raw bytes and wants to surface to downstream loaders
/// (notably <c>TextureAssetLoader</c>) using the regular extension-based dispatch.
/// </summary>
/// <remarks>
/// <para>
/// <b>Lifecycle:</b> a single instance is registered as an <see cref="AssetServer"/>
/// source by <see cref="AssetPlugin"/>. Importers obtain it via
/// <c>world.Resource&lt;InMemoryAssetReader&gt;()</c> (or by calling the
/// <see cref="ProcessWideStore"/> static fast-path when no world reference is in scope -
/// e.g. inside an <see cref="ISceneReader"/> running on a worker thread). The store is
/// process-wide so concurrent imports share it; entries are keyed by <see cref="AssetPath"/>
/// canonical form.
/// </para>
/// <para>
/// <b>No watching:</b> <see cref="CreateWatcher"/> returns <c>null</c>; in-memory assets
/// don't change without an explicit <see cref="Set"/> call (which the AssetServer's
/// hot-reload pipeline can be notified of out-of-band if needed in the future).
/// </para>
/// </remarks>
/// <seealso cref="FileAssetReader"/>
/// <seealso cref="EmbeddedAssetReader"/>
public sealed class InMemoryAssetReader : IAssetReader
{
    private static readonly ILogger Logger = Log.Category("Engine.Assets");

    private readonly ConcurrentDictionary<string, byte[]> _store;

    /// <summary>
    /// Process-wide store shared by every <see cref="InMemoryAssetReader"/> instance and
    /// by the <see cref="ProcessWideStore"/> static helpers. Lives for the lifetime of
    /// the process; entries are normally added once during asset import and never
    /// removed (the asset cache itself drives lifetime via <see cref="Handle{T}"/>
    /// ref-counting).
    /// </summary>
    public static ConcurrentDictionary<string, byte[]> ProcessWideStore { get; } = new(StringComparer.Ordinal);

    /// <summary>Creates a reader bound to the process-wide store.</summary>
    public InMemoryAssetReader() : this(ProcessWideStore) { }

    /// <summary>Creates a reader bound to a custom store (test seam).</summary>
    public InMemoryAssetReader(ConcurrentDictionary<string, byte[]> store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>Number of entries currently in the backing store (diagnostic).</summary>
    public int Count => _store.Count;

    /// <summary>
    /// Adds (or replaces) the bytes served for <paramref name="path"/>. Thread-safe.
    /// </summary>
    public void Set(AssetPath path, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        _store[path.Path] = bytes;
        Logger.Debug($"InMemoryAssetReader: stored {bytes.Length} byte(s) at '{path}'.");
    }

    /// <summary>Convenience: <see cref="Set(AssetPath, byte[])"/> on the process-wide store.</summary>
    public static void Publish(AssetPath path, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ProcessWideStore[path.Path] = bytes;
    }

    /// <inheritdoc />
    public bool Exists(AssetPath path) => _store.ContainsKey(path.Path);

    /// <inheritdoc />
    public Task<Stream> ReadAsync(AssetPath path, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(path.Path, out var bytes))
            throw new FileNotFoundException($"InMemoryAssetReader: no entry for '{path}'.", path.Path);

        // MemoryStream over the byte[]; AssetServer disposes the stream so we use the
        // public ctor to allow reads + writes without exposing the underlying buffer.
        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    /// <inheritdoc />
    public IAssetWatcher? CreateWatcher() => null;
}

