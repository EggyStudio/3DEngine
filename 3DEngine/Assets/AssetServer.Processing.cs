namespace Engine;

public sealed partial class AssetServer
{
    /// <summary>
    /// Drains completed background loads into <see cref="Assets{T}"/> and fires
    /// <see cref="AssetEvent{T}"/>. Called once per frame by the <see cref="AssetPlugin"/>
    /// system in <see cref="Stage.PreUpdate"/>.
    /// </summary>
    /// <param name="world">The world containing asset and event resources.</param>
    internal void ProcessCompleted(World world)
    {
        int processed = 0;
        while (_completedLoads.TryDequeue(out var completed))
        {
            processed++;
            if (!completed.Success)
            {
                _states[completed.Id] = LoadState.Failed;
                Logger.Error($"Asset load failed: {completed.Path}. {completed.Error}");
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

    private void StoreAndNotify(World world, CompletedLoad completed) => OpsFor(completed.AssetType).Store(world, completed);

    private void CheckDependencyCompletion(World world)
    {
        foreach (var kv in _dependencies)
        {
            if (GetLoadState(kv.Key) != LoadState.Loaded) continue;
            if (!IsLoadedWithDependencies(kv.Key)) continue;

            // Every dependency has loaded, so LoadedWithDependencies is sent, once.
            if (!_idToPath.TryGetValue(kv.Key, out var path)) continue;
            if (!_pathToId.TryGetValue(path.ToString(), out var info)) continue;

            OpsFor(info.AssetType).SendLoadedWithDependencies(world, kv.Key, path);
        }
    }

    /// <summary>
    /// Clears all asset events. Called once per frame at <see cref="Stage.Last"/>
    /// by the <see cref="AssetPlugin"/>.
    /// </summary>
    /// <param name="world">The world containing event resources.</param>
    internal void ClearEvents(World world)
    {
        // Each type's once, from the list of the types known, since the values of the map of paths
        // are copied each time they are asked for and name a type once for each of its assets.
        foreach (var ops in Volatile.Read(ref _allOps))
            ops.ClearEvents(world);
    }

    // What the frame does with an asset of one type, made where the type is known, in Load and
    // LoadSync, so no generic method is made by reflection for it, which a native build cannot
    // (N 2.5). A dependency loaded alongside is an object's.
    private static readonly AssetTypeOps ObjectOps = new AssetTypeOps<object>();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Type, AssetTypeOps> _typeOps = new() { [typeof(object)] = ObjectOps };

    // The same operations as a list, replaced whole as a type comes, which a frame reads whole.
    private AssetTypeOps[] _allOps = [ObjectOps];
    private readonly Lock _opsGate = new();

    private void Know<T>()
    {
        if (_typeOps.ContainsKey(typeof(T))) return;
        lock (_opsGate)
            if (_typeOps.TryAdd(typeof(T), new AssetTypeOps<T>()))
                _allOps = [.. _allOps, _typeOps[typeof(T)]];
    }

    private AssetTypeOps OpsFor(Type type) => _typeOps[type];

    private abstract class AssetTypeOps
    {
        // Puts a loaded asset into Assets<T> and sends Added, or Modified for a reload.
        public abstract void Store(World world, CompletedLoad completed);

        public abstract void SendLoadedWithDependencies(World world, AssetId id, AssetPath path);

        public abstract void ClearEvents(World world);
    }

    private sealed class AssetTypeOps<T> : AssetTypeOps
    {
        public override void Store(World world, CompletedLoad completed)
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

        public override void SendLoadedWithDependencies(World world, AssetId id, AssetPath path)
        {
            var handle = new Handle<T>(id, path, strong: true);
            Events.Get<AssetEvent<T>>(world).Send(AssetEvent<T>.LoadedWithDependencies(handle));
        }

        public override void ClearEvents(World world)
        {
            if (world.TryGetResource<Events<AssetEvent<T>>>(out var events))
                events.Clear();
        }
    }
}
