namespace Engine;

// What a component's store tells the index of its entities as a component is added or replaced,
// and removed, with the entity despawned or not.
internal interface IKeyIndexOf<T>
{
    void Put(int entity, in T component);

    void Drop(int entity);
}

/// <summary>
/// The entities of a world by the key their <typeparamref name="T"/> names, kept as the component is
/// added, replaced and removed, and as its entity is despawned, from <see cref="EcsWorld.Index{T,TKey}"/>.
/// </summary>
/// <remarks>
/// <para>
/// A game that finds an entity by where it is, as the voxel game finds the fire burning in a cell,
/// walked every one of them into a set of its own each frame it asked. A lookup here is a
/// dictionary's, and allocates nothing.
/// </para>
/// <para>
/// A key names one entity, and a second given the same key takes its place, the first found by it
/// no more. The key is read as the component is added or replaced through the world, by
/// <see cref="EcsWorld.Add{T}(int, T)"/>, <see cref="EcsWorld.Update{T}(int, T)"/> or a command, so a key changed in
/// place through a reference is not seen until the component is replaced, and the entity is found
/// by its old key no more meanwhile, since a lookup holds the key it finds against the component's.
/// </para>
/// </remarks>
public sealed class KeyIndex<T, TKey> : IKeyIndexOf<T> where T : IKeyed<TKey> where TKey : notnull
{
    private readonly EcsWorld.ComponentStore<T> _store;
    private readonly Dictionary<TKey, int> _byKey = [];
    private readonly Dictionary<int, TKey> _keyOf = [];

    internal KeyIndex(EcsWorld.ComponentStore<T> store)
    {
        _store = store;
        foreach (var (entity, component) in store.Enumerate()) Put(entity, component);
    }

    /// <summary>How many keys name an entity.</summary>
    public int Count => _byKey.Count;

    /// <summary>The entity whose component names <paramref name="key"/>, where one does.</summary>
    public bool TryGet(TKey key, out int entity)
    {
        if (_byKey.TryGetValue(key, out entity) && _store.Has(entity) && EqualityComparer<TKey>.Default.Equals(_store.GetRef(entity).Key, key))
            return true;
        entity = -1;
        return false;
    }

    /// <summary>Whether an entity's component names <paramref name="key"/>.</summary>
    public bool Contains(TKey key) => TryGet(key, out _);

    void IKeyIndexOf<T>.Put(int entity, in T component) => Put(entity, component);

    void IKeyIndexOf<T>.Drop(int entity)
    {
        if (_keyOf.Remove(entity, out var key) && _byKey.TryGetValue(key, out var holder) && holder == entity) _byKey.Remove(key);
    }

    private void Put(int entity, in T component)
    {
        var key = component.Key;
        if (_keyOf.TryGetValue(entity, out var old) && !EqualityComparer<TKey>.Default.Equals(old, key)
            && _byKey.TryGetValue(old, out var holder) && holder == entity)
            _byKey.Remove(old);
        _keyOf[entity] = key;
        _byKey[key] = entity;
    }
}
