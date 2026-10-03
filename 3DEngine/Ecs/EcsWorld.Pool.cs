namespace Engine;

/// <summary>
/// Generational entity ID allocator with free-list reuse.
/// </summary>
/// <remarks>
/// Entity IDs start at 1 (0 is reserved as an invalid/null entity). When an entity is despawned,
/// its ID is pushed onto a free-list for reuse. Each allocation/deallocation bumps a per-ID
/// generation counter, allowing external code to detect stale references.
/// </remarks>
/// <seealso cref="EcsWorld"/>
internal sealed class EntityPool
{
    /// <summary>Generation assigned to newly allocated entity IDs.</summary>
    private const int FirstGeneration = 1;

    /// <summary>Next entity ID to allocate when the free-list is empty.</summary>
    private int _nextEntity = 1;

    /// <summary>Stack of despawned entity IDs available for reuse.</summary>
    private readonly Stack<int> _free = new();

    /// <summary>Per-ID generation counters; index is entity ID, value is generation (0 = never used).</summary>
    private int[] _generations = System.Array.Empty<int>();

    /// <summary>Allocates a new entity ID (or reuses a despawned one) and returns it.</summary>
    /// <returns>The allocated entity ID with its generation initialized to at least <c>1</c>.</returns>
    public int Spawn()
    {
        int id = _free.Count > 0 ? _free.Pop() : _nextEntity++;
        EnsureCapacity(id);
        if (_generations[id] == 0) _generations[id] = FirstGeneration;
        return id;
    }

    /// <summary>Gets the current generation for <paramref name="id"/>, or <c>0</c> if never allocated.</summary>
    /// <param name="id">The entity ID to query.</param>
    /// <returns>The generation counter, or <c>0</c> if the ID was never used.</returns>
    public int GetGeneration(int id)
    {
        if ((uint)id >= (uint)_generations.Length) return 0;
        return _generations[id];
    }

    /// <summary>
    /// Despawns <paramref name="id"/> by incrementing its generation and pushing it onto the free-list.
    /// The caller must have already removed all components for this entity.
    /// </summary>
    /// <param name="id">The entity ID to despawn.</param>
    public void Despawn(int id)
    {
        EnsureCapacity(id);
        int g = _generations[id];
        if (g == 0) g = FirstGeneration;
        g = g == int.MaxValue ? FirstGeneration : g + 1;
        _generations[id] = g;
        _free.Push(id);
    }

    /// <summary>The next entity ID that will be allocated (useful for capacity hints).</summary>
    public int NextEntityId => _nextEntity;

    /// <summary>Pre-grows the generation array to hold at least <paramref name="maxEntityId"/> IDs without resizing during bulk spawns.</summary>
    /// <param name="maxEntityId">The maximum entity ID expected.</param>
    public void ReserveCapacity(int maxEntityId)
    {
        EnsureCapacity(maxEntityId);
    }

    /// <summary>Ensures the generation array is large enough to index <paramref name="id"/>.</summary>
    /// <param name="id">The entity ID that must be indexable.</param>
    private void EnsureCapacity(int id)
    {
        if (id < _generations.Length) return;
        int newSize = _generations.Length == 0 ? System.Math.Max(128, id + 1) : _generations.Length;
        while (id >= newSize) newSize *= 2;
        System.Array.Resize(ref _generations, newSize);
    }
}
