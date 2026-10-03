using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Engine;

/// <summary>
/// Per-type asset storage, stored as a <see cref="World"/> resource.
/// One <see cref="Assets{T}"/> instance exists for each asset type that has been loaded.
/// </summary>
/// <typeparam name="T">The asset type stored in this collection.</typeparam>
/// <remarks>
/// <para>
/// Thread-safe for concurrent reads. Mutations (Add, Remove) are performed by the
/// <see cref="AssetServer"/> on the main schedule thread during <see cref="Stage.PreUpdate"/>.
/// </para>
/// <para>
/// When an asset is replaced (hot-reload), the same <see cref="AssetId"/> is reused with the
/// new data, so existing handles remain valid.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // In a system:
/// var assets = world.Resource&lt;Assets&lt;Texture&gt;&gt;();
/// if (assets.TryGet(myHandle, out var texture))
///     BindTexture(texture);
///
/// // Iterate all loaded textures:
/// foreach (var (id, texture) in assets)
///     Console.WriteLine($"Texture {id}: {texture}");
/// </code>
/// </example>
/// <seealso cref="Handle{T}"/>
/// <seealso cref="AssetServer"/>
/// <seealso cref="AssetEvent{T}"/>
public sealed class Assets<T> : IDisposable
{
    private readonly ConcurrentDictionary<AssetId, T> _storage = new();

    /// <summary>Number of loaded assets of this type.</summary>
    public int Count => _storage.Count;

    /// <summary>Returns <c>true</c> if the asset referenced by <paramref name="handle"/> is available.</summary>
    /// <param name="handle">The handle to check.</param>
    public bool Contains(Handle<T> handle) => _storage.ContainsKey(handle.Id);

    /// <summary>Returns <c>true</c> if the asset with the given <paramref name="id"/> is available.</summary>
    /// <param name="id">The asset ID to check.</param>
    public bool Contains(AssetId id) => _storage.ContainsKey(id);

    /// <summary>
    /// Gets the loaded asset referenced by <paramref name="handle"/>.
    /// </summary>
    /// <param name="handle">The handle referencing the asset.</param>
    /// <returns>The loaded asset.</returns>
    /// <exception cref="KeyNotFoundException">The asset is not loaded or has been removed.</exception>
    public T Get(Handle<T> handle)
    {
        if (_storage.TryGetValue(handle.Id, out var asset))
            return asset;
        throw new KeyNotFoundException($"Asset not found: {handle}");
    }

    /// <summary>Gets the loaded asset by <paramref name="id"/>.</summary>
    /// <param name="id">The asset identifier.</param>
    /// <returns>The loaded asset.</returns>
    /// <exception cref="KeyNotFoundException">The asset is not loaded or has been removed.</exception>
    public T Get(AssetId id)
    {
        if (_storage.TryGetValue(id, out var asset))
            return asset;
        throw new KeyNotFoundException($"Asset not found: {id}");
    }

    /// <summary>Attempts to retrieve the asset referenced by <paramref name="handle"/>.</summary>
    /// <param name="handle">The handle referencing the asset.</param>
    /// <param name="asset">The loaded asset, or <c>default</c> if not found.</param>
    /// <returns><c>true</c> if the asset was found; otherwise <c>false</c>.</returns>
    public bool TryGet(Handle<T> handle, [MaybeNullWhen(false)] out T asset)
        => _storage.TryGetValue(handle.Id, out asset);

    /// <summary>Attempts to retrieve the asset by <paramref name="id"/>.</summary>
    /// <param name="id">The asset identifier.</param>
    /// <param name="asset">The loaded asset, or <c>default</c> if not found.</param>
    /// <returns><c>true</c> if the asset was found; otherwise <c>false</c>.</returns>
    public bool TryGet(AssetId id, [MaybeNullWhen(false)] out T asset)
        => _storage.TryGetValue(id, out asset);

    /// <summary>Adds or replaces an asset. Used internally by <see cref="AssetServer"/>.</summary>
    /// <param name="id">The asset identifier.</param>
    /// <param name="asset">The asset data to store.</param>
    internal void Set(AssetId id, T asset) => _storage[id] = asset;

    /// <summary>Removes an asset. Disposes it if it implements <see cref="IDisposable"/>.</summary>
    /// <param name="id">The asset identifier to remove.</param>
    /// <returns><c>true</c> if the asset was removed; <c>false</c> if it was not present.</returns>
    internal bool Remove(AssetId id)
    {
        if (!_storage.TryRemove(id, out var asset))
            return false;
        (asset as IDisposable)?.Dispose();
        HandleRefCounts.Remove(id);
        return true;
    }

    /// <summary>Returns all loaded asset IDs of this type.</summary>
    public ICollection<AssetId> Ids => _storage.Keys;

    /// <summary>Returns all loaded assets of this type.</summary>
    public ICollection<T> Values => _storage.Values;

    /// <summary>Enumerates all (id, asset) pairs.</summary>
    /// <returns>An enumerator over all loaded assets and their IDs.</returns>
    public IEnumerator<KeyValuePair<AssetId, T>> GetEnumerator() => _storage.GetEnumerator();

    /// <summary>Disposes all stored assets that implement <see cref="IDisposable"/>, then clears the collection.</summary>
    public void Dispose()
    {
        foreach (var kv in _storage)
        {
            (kv.Value as IDisposable)?.Dispose();
            HandleRefCounts.Remove(kv.Key);
        }

        _storage.Clear();
    }
}