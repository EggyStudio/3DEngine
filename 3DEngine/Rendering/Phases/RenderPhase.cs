namespace Engine;

/// <summary>
/// Generic collection of <see cref="IPhaseItem"/> instances that are sorted before rendering.
/// </summary>
/// <typeparam name="T">The phase item type (must implement <see cref="IPhaseItem"/>).</typeparam>
/// <seealso cref="IPhaseItem"/>
/// <seealso cref="Opaque3dPhase"/>
/// <seealso cref="Transparent3dPhase"/>
public sealed class RenderPhase<T> where T : struct, IPhaseItem
{
    /// <summary>The unsorted list of items queued this frame.</summary>
    public List<T> Items { get; } = new();

    /// <summary>Adds an item to the phase.</summary>
    /// <param name="item">The phase item to enqueue.</param>
    public void Add(T item) => Items.Add(item);

    /// <summary>Sorts items using the default ascending <see cref="IPhaseItem.SortKey"/> order.
    /// Opaque phases should sort ascending (front-to-back); transparent phases should sort descending.</summary>
    /// <param name="descending">When <c>true</c>, sorts in descending key order (back-to-front).</param>
    public void Sort(bool descending = false)
    {
        if (descending)
            Items.Sort((a, b) => b.SortKey.CompareTo(a.SortKey));
        else
            Items.Sort((a, b) => a.SortKey.CompareTo(b.SortKey));
    }

    /// <summary>Clears all items for the next frame.</summary>
    public void Clear() => Items.Clear();
}
