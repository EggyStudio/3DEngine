namespace Engine;

public sealed partial class AssetServer
{
    /// <summary>
    /// Drains completed background loads into <see cref="Assets{T}"/> and fires
    /// <see cref="AssetEvent{T}"/>. Called once per frame by the <see cref="AssetPlugin"/>
    /// system in <see cref="Stage.PreUpdate"/>.
    /// </summary>
    /// <param name="world">The world containing asset and event resources.</param>
    public void ProcessCompleted(World world)
    {
        int processed = 0;
        while (_completedLoads.TryDequeue(out var completed))
        {
            processed++;
            if (!completed.Success)
            {
                _states[completed.Id] = LoadState.Failed;
                Logger.Error($"Asset load failed: {completed.Path} - {completed.Error}");
                continue;
            }

            _states[completed.Id] = LoadState.Loaded;

            // Store dependencies
            if (completed.Dependencies is { Count: > 0 })
            {
                var depIds = new HashSet<AssetId>();
                foreach (var dep in completed.Dependencies)
                {
                    if (_pathToId.TryGetValue(dep.ToString(), out var depInfo))
                        depIds.Add(depInfo.Id);
                }
                _dependencies[completed.Id] = depIds;
            }

            // Store in typed Assets<T> and fire events
            StoreAndNotify(world, completed);
        }

        // Check for newly-satisfied dependency trees
        if (processed > 0)
            CheckDependencyCompletion(world);
    }

    private void StoreAndNotify(World world, CompletedLoad completed)
    {
        // Use reflection to call the generic store method for the correct asset type
        var method = typeof(AssetServer)
            .GetMethod(nameof(StoreTyped), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .MakeGenericMethod(completed.AssetType);
        method.Invoke(null, [world, completed]);
    }

    private static void StoreTyped<T>(World world, CompletedLoad completed)
    {
        // Ensure Assets<T> resource exists
        var assets = world.GetOrInsertResource(() => new Assets<T>());
        var handle = new Handle<T>(completed.Id, completed.Path, strong: true);

        bool existed = assets.Contains(completed.Id);
        assets.Set(completed.Id, (T)completed.Asset!);

        // Fire event
        var events = Events.Get<AssetEvent<T>>(world);
        events.Send(existed ? AssetEvent<T>.Modified(handle) : AssetEvent<T>.Added(handle));
    }

    private void CheckDependencyCompletion(World world)
    {
        foreach (var kv in _dependencies)
        {
            if (GetLoadState(kv.Key) != LoadState.Loaded) continue;
            if (!IsLoadedWithDependencies(kv.Key)) continue;

            // All deps satisfied - fire LoadedWithDependencies if not already done
            if (!_idToPath.TryGetValue(kv.Key, out var path)) continue;
            if (!_pathToId.TryGetValue(path.ToString(), out var info)) continue;

            // We fire the event via reflection for the correct type
            var method = typeof(AssetServer)
                .GetMethod(nameof(FireDepsLoaded), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(info.AssetType);
            method.Invoke(null, [world, kv.Key, path]);
        }
    }

    private static void FireDepsLoaded<T>(World world, AssetId id, AssetPath path)
    {
        var handle = new Handle<T>(id, path, strong: true);
        Events.Get<AssetEvent<T>>(world).Send(AssetEvent<T>.LoadedWithDependencies(handle));
    }

    /// <summary>
    /// Clears all asset events. Called once per frame at <see cref="Stage.Last"/>
    /// by the <see cref="AssetPlugin"/>.
    /// </summary>
    /// <param name="world">The world containing event resources.</param>
    public void ClearEvents(World world)
    {
        // Clear events for all known asset types
        foreach (var kv in _pathToId.Values)
        {
            var method = typeof(AssetServer)
                .GetMethod(nameof(ClearEventsTyped), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(kv.AssetType);
            method.Invoke(null, [world]);
        }
    }

    private static void ClearEventsTyped<T>(World world)
    {
        if (world.TryGetResource<Events<AssetEvent<T>>>(out var events))
            events.Clear();
    }
}
