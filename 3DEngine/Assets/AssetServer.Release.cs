namespace Engine;

public sealed partial class AssetServer
{
    /// <summary>
    /// Removes a loaded or failed asset and forgets its path, so a later <c>Load</c> of the path
    /// reads the file again, and sends <see cref="AssetEventKind.Removed"/> for it, which the
    /// renderer frees its GPU copy by.
    /// </summary>
    /// <returns>Whether the server knew the asset.</returns>
    /// <remarks>
    /// Handles to it that remain resolve to nothing, as one still loading does, so it is removed
    /// once nothing holds a count of it (<see cref="AssetRelease"/>). An asset still loading is
    /// left, since what its load brings back would be stored under an id nothing knows.
    /// </remarks>
    internal bool Unload<T>(World world, AssetId id)
    {
        if (!_idToPath.TryGetValue(id, out var path)) return false;
        if (GetLoadState(id) == LoadState.Loading) return false;

        if (world.TryGetResource<Assets<T>>(out var assets)) assets.Remove(id);
        HandleRefCounts.Remove(id);
        _states.TryRemove(id, out _);
        _idToPath.TryRemove(id, out _);
        _dependencies.TryRemove(id, out _);
        // The path is forgotten only while it still names this asset, as a path loaded again
        // since names the new one.
        var key = path.ToString();
        if (_pathToId.TryGetValue(key, out var entry) && entry.Id == id) _pathToId.TryRemove(key, out _);

        Events.Get<AssetEvent<T>>(world).Send(AssetEvent<T>.Removed(new Handle<T>(id, path, strong: false)));
        Logger.Debug($"Unloaded {path} ({id}), which nothing used.");
        return true;
    }
}
