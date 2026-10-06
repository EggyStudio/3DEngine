namespace Engine;

public sealed partial class EcsWorld
{
    /// <summary>Names <paramref name="entity"/>, replacing a name it had.</summary>
    public void SetName(int entity, string name)
    {
        if (Has<Name>(entity)) Update(entity, new Name(name));
        else Add(entity, new Name(name));
    }

    /// <summary>The name of <paramref name="entity"/>, or <c>null</c>.</summary>
    internal string? NameOf(int entity) => TryGet<Name>(entity, out var name) ? name.Value : null;

    /// <summary>The first entity with the name <paramref name="name"/>, or 0.</summary>
    /// <remarks>A walk over every named entity, so it is for finding something once, not every frame.</remarks>
    internal int FindByName(string name)
    {
        foreach (var (entity, value) in Query<Name>())
            if (value.Value == name) return entity;
        return 0;
    }

    /// <summary>Makes <paramref name="parent"/> the parent of <paramref name="child"/>, or removes the link when it is 0.</summary>
    /// <exception cref="InvalidOperationException">The link would make an entity its own ancestor.</exception>
    internal void SetParent(int child, int parent)
    {
        if (parent == 0)
        {
            Remove<Parent>(child);
            return;
        }

        for (var ancestor = parent; ancestor != 0; ancestor = ParentOf(ancestor))
            if (ancestor == child) throw new InvalidOperationException($"Entity {parent} is {child} or one of its children, so it cannot be its parent.");

        var link = new Parent(Handle(parent));
        if (Has<Parent>(child)) Update(child, link);
        else Add(child, link);
    }

    /// <summary>The parent of <paramref name="child"/>, or 0 when it has none or its parent is gone.</summary>
    public int ParentOf(int child) =>
        TryGet<Parent>(child, out var parent) && TryResolve(parent.Value, out var id) ? id : 0;

    /// <summary>The entities whose parent is <paramref name="parent"/>.</summary>
    /// <remarks>A walk over every entity with a parent, so it is for tools and for despawning, not every frame.</remarks>
    internal IReadOnlyList<int> ChildrenOf(int parent)
    {
        var children = new List<int>();
        foreach (var (entity, link) in Query<Parent>())
            if (TryResolve(link.Value, out var id) && id == parent) children.Add(entity);
        return children;
    }

    /// <summary>Despawns <paramref name="entity"/> and every entity below it.</summary>
    public void DespawnRecursive(int entity)
    {
        foreach (var child in ChildrenOf(entity)) DespawnRecursive(child);
        Despawn(entity);
    }
}
