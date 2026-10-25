using System.Runtime.InteropServices;

namespace Engine;

/// <summary>
/// The entities of a world under each key their <typeparamref name="T"/> names, many to a key, kept
/// as the component is added, replaced and removed, and as its entity is despawned, from
/// <see cref="EcsWorld.Groups{T,TKey}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="KeyIndex{T,TKey}"/> keeps one entity to a key, as a fire to a cell. A key that names
/// many, as the mobs standing in a voxel game's column, was walked over every mob to find a column's.
/// Here each key holds a list of its entities, an entity's place in it kept so it leaves the list in
/// constant time, and <see cref="Members"/> hands the list out as a span, which allocates nothing.
/// </para>
/// <para>
/// The key is read as the component is added or replaced through the world, by
/// <see cref="EcsWorld.Add{T}(int, T)"/>, <see cref="EcsWorld.Update{T}(int, T)"/> or a command, so a
/// key changed in place through a reference is not seen until the component is replaced.
/// </para>
/// </remarks>
public sealed class GroupIndex<T, TKey> : IKeyIndexOf<T> where T : IKeyed<TKey> where TKey : notnull
{
    private readonly Dictionary<TKey, List<int>> _members = [];
    private readonly Dictionary<int, (TKey Key, int At)> _placeOf = [];

    internal GroupIndex(EcsWorld.ComponentStore<T> store)
    {
        foreach (var (entity, component) in store.Enumerate()) Put(entity, component);
    }

    /// <summary>How many keys name an entity.</summary>
    public int Count => _members.Count;

    /// <summary>The entities whose component names <paramref name="key"/>, in no set order, none where no component does.</summary>
    /// <remarks>
    /// A span over the index's own list, which the next component added, replaced or removed under
    /// the key changes, so a loop that does that through the world copies the span first, where one
    /// that queues it through commands does not.
    /// </remarks>
    public ReadOnlySpan<int> Members(TKey key) =>
        _members.TryGetValue(key, out var members) ? CollectionsMarshal.AsSpan(members) : [];

    /// <summary>How many entities' components name <paramref name="key"/>.</summary>
    public int CountOf(TKey key) => _members.TryGetValue(key, out var members) ? members.Count : 0;

    void IKeyIndexOf<T>.Put(int entity, in T component) => Put(entity, component);

    void IKeyIndexOf<T>.Drop(int entity) => Drop(entity);

    private void Put(int entity, in T component)
    {
        var key = component.Key;
        if (_placeOf.TryGetValue(entity, out var place))
        {
            if (EqualityComparer<TKey>.Default.Equals(place.Key, key)) return;
            Drop(entity);
        }
        if (!_members.TryGetValue(key, out var members)) _members[key] = members = [];
        _placeOf[entity] = (key, members.Count);
        members.Add(entity);
    }

    // Takes the entity out of its key's list, the list's last entity moved into its place.
    private void Drop(int entity)
    {
        if (!_placeOf.Remove(entity, out var place)) return;
        var members = _members[place.Key];
        var last = members[^1];
        members[place.At] = last;
        members.RemoveAt(members.Count - 1);
        if (last != entity) _placeOf[last] = (place.Key, place.At);
        if (members.Count == 0) _members.Remove(place.Key);
    }
}
