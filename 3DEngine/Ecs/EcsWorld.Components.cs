using System.Runtime.CompilerServices;

namespace Engine;

public sealed partial class EcsWorld : IDisposable
{
    /// <summary>Per-world ID counter for static-generic store cache indexing.</summary>
    private static int _nextWorldId;

    /// <summary>Unique ID for this world instance, used by the static-generic store cache.</summary>
    internal readonly int WorldId = Interlocked.Increment(ref _nextWorldId) - 1;

    /// <summary>
    /// Static-generic per-<typeparamref name="T"/> cache that maps world IDs to component stores.
    /// Avoids <c>Dictionary&lt;Type, IComponentStore&gt;</c> lookup on every hot-path call.
    /// </summary>
    private static class StoreCache<T>
    {
        // Indexed by EcsWorld.WorldId. Grown and written under SyncRoot by replacing the array
        // whole, and read without the lock through Volatile.Read, which sees either the old array
        // or the new one completely.
        internal static ComponentStore<T>?[] Stores = Array.Empty<ComponentStore<T>?>();
        internal static readonly object SyncRoot = new();

        // Drops one world's store, so the static array no longer keeps it reachable.
        internal static void Release(int worldId)
        {
            lock (SyncRoot)
                if (worldId < Stores.Length) Stores[worldId] = null;
        }
    }

    // One release per component type this world made a store for, run when it is disposed or
    // collected. The cache arrays are static and outlive every world, so without this each world
    // made in a process kept all its component data alive for good. Keyed by the type, so a store
    // forgotten while the world runs gives up its release too, which names the type.
    private readonly Dictionary<Type, Action<int>> _cacheReleases = [];
    private int _released;

    /// <summary>
    /// Drops this world's component stores, from the static cache its typed lookups go through as
    /// well as from the world itself. The world is not used afterward.
    /// </summary>
    /// <remarks>
    /// <see cref="World"/> disposes the <see cref="EcsWorld"/> it holds with the app. A world that is
    /// never disposed is released when it is collected, since the cache holds its stores and not
    /// the world.
    /// </remarks>
    public void Dispose()
    {
        ReleaseCaches();
        lock (_stores)
        {
            _stores.Clear();
            _storeList.Clear();
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the cache slots of a world that was dropped without being disposed.</summary>
    ~EcsWorld() => ReleaseCaches();

    private void ReleaseCaches()
    {
        if (Interlocked.Exchange(ref _released, 1) == 1) return;
        Action<int>[] releases;
        lock (_cacheReleases) releases = [.. _cacheReleases.Values];
        foreach (var release in releases) release(WorldId);
    }

    /// <summary>Retrieves or creates the typed component store for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="create">When <c>true</c> (default), creates a new store if one does not exist; when <c>false</c>, returns <c>null</c>.</param>
    /// <returns>The <see cref="ComponentStore{T}"/> for the requested type, or <c>null</c> when <paramref name="create"/> is <c>false</c> and no store exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ComponentStore<T> GetStore<T>(bool create = true)
    {
        var id = WorldId;
        var arr = Volatile.Read(ref StoreCache<T>.Stores);
        if ((uint)id < (uint)arr.Length)
        {
            var cached = arr[id];
            if (cached != null) return cached;
        }
        if (!create) return null!;
        return GetOrCreateStoreSlow<T>();
    }

    /// <summary>Slow path: creates and caches a new component store when the fast cache misses.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private ComponentStore<T> GetOrCreateStoreSlow<T>()
    {
        // Locked, because systems in a parallel batch reach here together the first time they
        // touch a type. Unlocked, two of them each made a store, one landed in the cache the
        // systems read and the other in the dictionary the console reads, and the dictionary
        // itself could be corrupted by the concurrent writes.
        // A disposed world has released its stores and registers no release for new ones, so a
        // store made now would stay in the static cache for good.
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _released) == 1, this);
        lock (_stores)
        {
            if (_stores.TryGetValue(typeof(T), out var existing))
            {
                var typed = (ComponentStore<T>)existing;
                SetStoreCache(typed);
                return typed;
            }
            var created = new ComponentStore<T>(_frame);
            _stores[typeof(T)] = created;
            _storeList.Add(created);
            lock (_cacheReleases) _cacheReleases[typeof(T)] = StoreCache<T>.Release;
            SetStoreCache(created);
            return created;
        }
    }

    /// <summary>Writes a store reference into the static-generic cache array.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetStoreCache<T>(ComponentStore<T> store)
    {
        var id = WorldId;
        lock (StoreCache<T>.SyncRoot)
        {
            var stores = StoreCache<T>.Stores;
            if (id >= stores.Length)
            {
                var grown = new ComponentStore<T>?[Math.Max(id + 1, 4)];
                stores.CopyTo(grown, 0);
                grown[id] = store;
                Volatile.Write(ref StoreCache<T>.Stores, grown);
            }
            else
            {
                Volatile.Write(ref stores[id], store);
            }
        }
    }

    /// <summary>
    /// Returns the <see cref="ComponentStore{T}"/> for public callers (generated code).
    /// Creates the store if it doesn't exist. Bypasses dictionary for maximum throughput.
    /// </summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <returns>The component store (never null).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ComponentStore<T> GetStorePublic<T>() => GetStore<T>(create: true);
}
