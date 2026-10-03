using System.Runtime.CompilerServices;

namespace Engine;

public sealed partial class EcsWorld
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
        // Indexed by EcsWorld.WorldId. Grows on demand; reads are lock-free via volatile length check.
        internal static ComponentStore<T>?[] Stores = Array.Empty<ComponentStore<T>?>();
        internal static readonly object SyncRoot = new();
    }

    /// <summary>Retrieves or creates the typed component store for <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The component type.</typeparam>
    /// <param name="create">When <c>true</c> (default), creates a new store if one does not exist; when <c>false</c>, returns <c>null</c>.</param>
    /// <returns>The <see cref="ComponentStore{T}"/> for the requested type, or <c>null</c> when <paramref name="create"/> is <c>false</c> and no store exists.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ComponentStore<T> GetStore<T>(bool create = true)
    {
        var id = WorldId;
        var arr = StoreCache<T>.Stores;
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
        // Check dictionary first (for stores created before the cache existed)
        if (_stores.TryGetValue(typeof(T), out var existing))
        {
            var typed = (ComponentStore<T>)existing;
            SetStoreCache(typed);
            return typed;
        }
        var created = new ComponentStore<T>();
        _stores[typeof(T)] = created;
        _storeList.Add(created);
        SetStoreCache(created);
        return created;
    }

    /// <summary>Writes a store reference into the static-generic cache array.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetStoreCache<T>(ComponentStore<T> store)
    {
        var id = WorldId;
        lock (StoreCache<T>.SyncRoot)
        {
            if (id >= StoreCache<T>.Stores.Length)
            {
                var newArr = new ComponentStore<T>?[Math.Max(id + 1, 4)];
                StoreCache<T>.Stores.CopyTo(newArr, 0);
                StoreCache<T>.Stores = newArr;
            }
            StoreCache<T>.Stores[id] = store;
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
